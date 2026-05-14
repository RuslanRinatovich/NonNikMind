using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Text.RegularExpressions;
using MindKeeper.Entity;
using MindKeeper.Services;
using EntityModel = MindKeeper.Entity.Entity;

namespace MindKeeper.Pages
{
    public partial class NotesPage : System.Windows.Controls.Page
    {
        private NotesViewModel _viewModel;

        public NotesPage()
        {
            InitializeComponent();
            _viewModel = new NotesViewModel();
            DataContext = _viewModel;
        }
        public NotesPage(int? selectedNoteId = null) : this()
        {
            if (selectedNoteId.HasValue)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var node = _viewModel.FindNode(_viewModel.RootNotes, selectedNoteId.Value);
                    if (node != null)
                        _viewModel.SelectedNode = node;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // Добавьте этот метод в класс NotesPage
        public void NavigateToNote(int noteId)
        {
            var node = _viewModel.FindNode(_viewModel.RootNotes, noteId);
            if (node != null)
            {
                _viewModel.SelectedNode = node;
            }
        }

        private void TreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is NoteNode node)
                _viewModel.SelectedNode = node;
        }

        private void RelatedNote_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var textBlock = sender as System.Windows.Controls.TextBlock;
            var note = textBlock?.DataContext as Note;
            if (note != null && _viewModel != null)
            {
                _viewModel.SelectedNode = _viewModel.FindNode(_viewModel.RootNotes, note.NoteID);
            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }

    public class NoteNode : INotifyPropertyChanged
    {
        private bool _isExpanded = true;

        public Note Note { get; set; }
        public ObservableCollection<NoteNode> Children { get; set; } = new ObservableCollection<NoteNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class NotesViewModel : INotifyPropertyChanged
    {
        private readonly int _currentUserId;
        private ObservableCollection<NoteNode> _rootNotes;
        private NoteNode _selectedNode;
        private string _searchText;

        // Для тегов
        private ObservableCollection<Tag> _allTags;
        private ObservableCollection<Tag> _currentNoteTags;
        private Tag _selectedTagToAdd;
        private string _newTagName;

        // Для связей
        private ObservableCollection<Note> _relatedNotes;

        public ObservableCollection<NoteNode> RootNotes
        {
            get => _rootNotes;
            set { _rootNotes = value; OnPropertyChanged(); }
        }

        public NoteNode SelectedNode
        {
            get => _selectedNode; 

            set
            {
                _selectedNode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNoteSelected));
                OnPropertyChanged(nameof(SelectedNote));   // <-- добавить эту строку
                LoadCurrentNoteTags();
                LoadRelatedNotes();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Совместимость со старым кодом: SelectedNote возвращает Note выбранного узла
        public Note SelectedNote => SelectedNode?.Note;

        public bool IsNoteSelected => SelectedNode != null;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                LoadNotes();   // полное обновление дерева с учётом поиска
            }
        }

        public ObservableCollection<Tag> AllTags
        {
            get => _allTags;
            set { _allTags = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Tag> CurrentNoteTags
        {
            get => _currentNoteTags;
            set { _currentNoteTags = value; OnPropertyChanged(); }
        }

        public Tag SelectedTagToAdd
        {
            get => _selectedTagToAdd;
            set
            {
                _selectedTagToAdd = value;
                OnPropertyChanged();
                if (value != null)
                    AddExistingTag();
            }
        }

        public string NewTagName
        {
            get => _newTagName;
            set { _newTagName = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Note> RelatedNotes
        {
            get => _relatedNotes;
            set { _relatedNotes = value; OnPropertyChanged(); }
        }

        public ICommand NewNoteCommand { get; }
        public ICommand NewChildNoteCommand { get; }
        public ICommand SaveNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }
        public ICommand AddTagCommand { get; }
        public ICommand RemoveTagCommand { get; }
        public ICommand SummarizeCommand { get; }
        public ICommand RemoveLinkCommand { get; }

        public NotesViewModel()
        {
            _currentUserId = Manager.CurrentUser?.UserID ?? 0;

            NewNoteCommand = new RelayCommand(_ => CreateNewNote(false));
            NewChildNoteCommand = new RelayCommand(_ => CreateNewNote(true), _ => IsNoteSelected);
            SaveNoteCommand = new RelayCommand(_ => SaveNote(), _ => IsNoteSelected);
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => IsNoteSelected);
            AddTagCommand = new RelayCommand(_ => AddNewTag(), _ => !string.IsNullOrWhiteSpace(NewTagName));
            RemoveTagCommand = new RelayCommand(tag => RemoveTag((Tag)tag));
            SummarizeCommand = new RelayCommand(_ => CreateSummary(), _ => IsNoteSelected);
            RemoveLinkCommand = new RelayCommand(targetNote => RemoveLink((Note)targetNote), _ => IsNoteSelected);

            LoadAllTags();
            LoadNotes();
        }

       


        // Построение дерева из плоского списка заметок
        private void LoadNotes()
        {
            using (var context = DataEntities.GetContext())
            {
                var query = context.Notes.Include("Tags")
                    .Where(n => n.UserID == _currentUserId && n.IsDeleted == false);

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    query = query.Where(n => n.Title.Contains(SearchText) ||
                                             (n.Content != null && n.Content.Contains(SearchText)));
                }

                var allNotes = query.OrderBy(n => n.CreatedAt).ToList();

                var dict = allNotes.ToDictionary(n => n.NoteID, n => new NoteNode { Note = n });
                var roots = new ObservableCollection<NoteNode>();

                foreach (var note in allNotes)
                {
                    var node = dict[note.NoteID];
                    if (note.ParentNoteID == null || !dict.ContainsKey(note.ParentNoteID.Value))
                        roots.Add(node);
                    else
                        dict[note.ParentNoteID.Value].Children.Add(node);
                }
                RootNotes = roots;
            }
        }

        // Поиск узла в дереве по ID заметки (для восстановления выбора)
        public NoteNode FindNode(ObservableCollection<NoteNode> nodes, int noteId)
        {
            foreach (var node in nodes)
            {
                if (node.Note.NoteID == noteId)
                    return node;
                var found = FindNode(node.Children, noteId);
                if (found != null)
                    return found;
            }
            return null;
        }

        // Создание новой заметки (корневой или дочерней)
        private void CreateNewNote(bool isChild)
        {
            int? parentId = isChild ? SelectedNote?.NoteID : null;
            string baseTitle = "Новая заметка";
            string title = baseTitle;
            int counter = 1;

            using (var context = DataEntities.GetContext())
            {
                while (context.Notes.Any(n => n.UserID == _currentUserId && n.Title == title))
                    title = $"{baseTitle} ({counter++})";

                var newNote = new Note
                {
                    UserID = _currentUserId,
                    Title = title,
                    Content = "",
                    ParentNoteID = parentId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                };
                context.Notes.Add(newNote);
                context.SaveChanges();

                LoadNotes(); // перестраиваем дерево
                SelectedNode = FindNode(RootNotes, newNote.NoteID);
            }
        }


        // Сохранение изменений
        private void SaveNote()
        {
            if (SelectedNote == null) return;
            int selectedId = SelectedNote.NoteID;

            using (var checkContext = DataEntities.GetContext())
            {
                if (checkContext.Notes.Any(n => n.UserID == _currentUserId && n.Title == SelectedNote.Title && n.NoteID != selectedId && (n.IsDeleted == false)))
                {
                    System.Windows.MessageBox.Show("Заметка с таким заголовком уже существует.", "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }

            SelectedNote.UpdatedAt = DateTime.Now;

            // Сохраняем изменения в БД
            using (var context = DataEntities.GetContext())
            {
                context.Entry(SelectedNote).State = EntityState.Modified;
                context.SaveChanges();
            }

            // Обновляем связи (работает с тем же SelectedNote, но нужно заново прикрепить к контексту)
            UpdateLinksForCurrentNote();
            System.Windows.MessageBox.Show("UpdateLinksForCurrentNote вызван");
            // Авто-тегирование и сущности
            AutoTagFromKeywords();
            SaveExtractedEntities();

            // Перезагружаем дерево и восстанавливаем выбор
            LoadNotes();
            SelectedNode = FindNode(RootNotes, selectedId);
        }

        // Мягкое удаление
        private void DeleteNote()
        {
            if (SelectedNote == null) return;
            var result = System.Windows.MessageBox.Show($"Удалить заметку \"{SelectedNote.Title}\"?", "Подтверждение",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                using (var context = DataEntities.GetContext())
                {
                    var note = context.Notes.Find(SelectedNote.NoteID);
                    if (note != null)
                    {
                        note.IsDeleted = true;
                        context.SaveChanges();
                    }
                }
                LoadNotes();
                SelectedNode = null;
            }
        }

        // Авто-тегирование
        private void AutoTagFromKeywords()
        {
            if (SelectedNote == null) return;
            var keywords = AiService.ExtractKeywords(SelectedNote.Content ?? "");
            if (keywords.Count == 0) return;

            using (var context = DataEntities.GetContext())
            {
                var note = context.Notes.Include("Tags").FirstOrDefault(n => n.NoteID == SelectedNote.NoteID);
                if (note == null) return;

                foreach (var kw in keywords)
                {
                    var tag = context.Tags.FirstOrDefault(t => t.TagName == kw);
                    if (tag == null)
                    {
                        tag = new Tag { TagName = kw };
                        context.Tags.Add(tag);
                        context.SaveChanges();
                    }
                    if (!note.Tags.Any(t => t.TagID == tag.TagID))
                        note.Tags.Add(tag);
                }
                context.SaveChanges();
            }
            LoadCurrentNoteTags();
            LoadAllTags();
            LoadNotes();
            // После перезагрузки выбор восстанавливается внешним кодом (SaveNote уже это делает)
        }

        // Сохранение сущностей
        private void SaveExtractedEntities()
        {
            if (SelectedNote == null) return;
            var (dates, emails, phones, urls) = AiService.ExtractEntities(SelectedNote.Content ?? "");

            using (var context = DataEntities.GetContext())
            {
                var oldEntities = context.Entities.Where(e => e.NoteID == SelectedNote.NoteID);
                context.Entities.RemoveRange(oldEntities);

                foreach (var date in dates)
                    context.Entities.Add(new EntityModel { NoteID = SelectedNote.NoteID, EntityType = "date", EntityValue = date });
                foreach (var email in emails)
                    context.Entities.Add(new EntityModel { NoteID = SelectedNote.NoteID, EntityType = "email", EntityValue = email });
                foreach (var phone in phones)
                    context.Entities.Add(new EntityModel { NoteID = SelectedNote.NoteID, EntityType = "phone", EntityValue = phone });
                foreach (var url in urls)
                    context.Entities.Add(new EntityModel { NoteID = SelectedNote.NoteID, EntityType = "url", EntityValue = url });
                context.SaveChanges();
            }
        }

        // Обновление связей по [[...]]
        private void UpdateLinksForCurrentNote()
        {
            if (SelectedNote == null || string.IsNullOrWhiteSpace(SelectedNote.Content)) return;

            var regex = new Regex(@"\[\[(.*?)\]\]");
            var matches = regex.Matches(SelectedNote.Content);

            var targetTitles = new System.Collections.Generic.HashSet<string>();
            foreach (Match match in matches)
            {
                string title = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    targetTitles.Add(title);
            }

            if (targetTitles.Count == 0)
            {
                // Если ссылок нет, удаляем все существующие связи этой заметки
                using (var context = DataEntities.GetContext())
                {
                    var toDelete = context.Links.Where(l => l.SourceNoteID == SelectedNote.NoteID);
                    context.Links.RemoveRange(toDelete);
                    context.SaveChanges();
                }
                LoadRelatedNotes();
                return;
            }

            using (var context = DataEntities.GetContext())
            {
                var lowerTitles = targetTitles.Select(t => t.ToLower()).ToList();
                var targetNoteIds = context.Notes
                    .Where(n => n.UserID == _currentUserId && n.IsDeleted == false && lowerTitles.Contains(n.Title.ToLower()))
                    .Select(n => n.NoteID)
                    .ToList();

                // Получаем текущие связи заметки
                var existingLinks = context.Links.Where(l => l.SourceNoteID == SelectedNote.NoteID).ToList();

                // Удаляем связи, которых больше нет в тексте
                var toDelete = existingLinks.Where(l => !targetNoteIds.Contains(l.TargetNoteID)).ToList();
                context.Links.RemoveRange(toDelete);

                // Добавляем новые связи
                foreach (int targetId in targetNoteIds)
                {
                    if (!existingLinks.Any(l => l.TargetNoteID == targetId))
                    {
                        context.Links.Add(new Link
                        {
                            SourceNoteID = SelectedNote.NoteID,
                            TargetNoteID = targetId,
                            LinkType = "auto"
                        });
                    }
                }
                context.SaveChanges();
            }

            // Обновляем отображение связанных заметок
            LoadRelatedNotes();
        }
        // Удаление связи
        private void RemoveLink(Note targetNote)
        {
            if (SelectedNote == null || targetNote == null) return;
            using (var context = DataEntities.GetContext())
            {
                var link = context.Links.FirstOrDefault(l => l.SourceNoteID == SelectedNote.NoteID && l.TargetNoteID == targetNote.NoteID);
                if (link != null)
                {
                    context.Links.Remove(link);
                    context.SaveChanges();
                }
            }
            LoadRelatedNotes();
        }

        // Конспект
        private void CreateSummary()
        {
            if (SelectedNote == null) return;
            string summary = AiService.GenerateSimpleSummary(SelectedNote.Content ?? "");

            string newTitle = $"Конспект: {SelectedNote.Title}";
            int counter = 1;
            using (var context = DataEntities.GetContext())
            {
                string finalTitle = newTitle;
                while (context.Notes.Any(n => n.UserID == _currentUserId && n.Title == finalTitle))
                    finalTitle = $"{newTitle} ({counter++})";
                var newNote = new Note
                {
                    UserID = _currentUserId,
                    Title = finalTitle,
                    Content = summary,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                };
                context.Notes.Add(newNote);
                context.SaveChanges();
                LoadNotes();
                SelectedNode = FindNode(RootNotes, newNote.NoteID);
            }
        }

        // Загрузка всех тегов
        private void LoadAllTags()
        {
            using (var context = DataEntities.GetContext())
            {
                var tags = context.Tags.OrderBy(t => t.TagName).ToList();
                AllTags = new ObservableCollection<Tag>(tags);
            }
        }

        // Загрузка тегов текущей заметки
        private void LoadCurrentNoteTags()
        {
            if (SelectedNote == null)
            {
                CurrentNoteTags = new ObservableCollection<Tag>();
                return;
            }
            using (var context = DataEntities.GetContext())
            {
                var note = context.Notes.Include("Tags").FirstOrDefault(n => n.NoteID == SelectedNote.NoteID);
                CurrentNoteTags = note?.Tags != null ? new ObservableCollection<Tag>(note.Tags) : new ObservableCollection<Tag>();
            }
        }

        // Загрузка связанных заметок
        private void LoadRelatedNotes()
        {
            if (SelectedNote == null)
            {
                RelatedNotes = new ObservableCollection<Note>();
                return;
            }
            using (var context = DataEntities.GetContext())
            {
                var targetIds = context.Links
                    .Where(l => l.SourceNoteID == SelectedNote.NoteID)
                    .Select(l => l.TargetNoteID)
                    .ToList();

                var related = context.Notes
                    .Where(n => targetIds.Contains(n.NoteID) && n.IsDeleted == false)
                    .ToList();
                RelatedNotes = new ObservableCollection<Note>(related);
            }
        }

        // Добавление нового тега
        private void AddNewTag()
        {
            if (string.IsNullOrWhiteSpace(NewTagName) || SelectedNote == null) return;
            int currentNoteId = SelectedNote.NoteID;

            using (var context = DataEntities.GetContext())
            {
                var tag = context.Tags.FirstOrDefault(t => t.TagName == NewTagName);
                if (tag == null)
                {
                    tag = new Tag { TagName = NewTagName };
                    context.Tags.Add(tag);
                    context.SaveChanges();
                }
                var note = context.Notes.Include("Tags").FirstOrDefault(n => n.NoteID == currentNoteId);
                if (note != null && !note.Tags.Any(t => t.TagID == tag.TagID))
                {
                    note.Tags.Add(tag);
                    context.SaveChanges();
                }
            }
            NewTagName = "";
            LoadAllTags();
            LoadCurrentNoteTags();
            LoadNotes();
            SelectedNode = FindNode(RootNotes, currentNoteId);
        }

        // Добавление существующего тега
        private void AddExistingTag()
        {
            if (SelectedTagToAdd == null || SelectedNote == null) return;
            int currentNoteId = SelectedNote.NoteID;

            using (var context = DataEntities.GetContext())
            {
                var tag = context.Tags.Find(SelectedTagToAdd.TagID);
                if (tag == null) return;
                var note = context.Notes.Include("Tags").FirstOrDefault(n => n.NoteID == currentNoteId);
                if (note != null && !note.Tags.Any(t => t.TagID == tag.TagID))
                {
                    note.Tags.Add(tag);
                    context.SaveChanges();
                }
            }
            LoadCurrentNoteTags();
            LoadNotes();
            SelectedNode = FindNode(RootNotes, currentNoteId);
            SelectedTagToAdd = null;
        }

        // Удаление тега из заметки
        private void RemoveTag(Tag tag)
        {
            if (tag == null || SelectedNote == null) return;
            int currentNoteId = SelectedNote.NoteID;

            using (var context = DataEntities.GetContext())
            {
                var note = context.Notes.Include("Tags").FirstOrDefault(n => n.NoteID == currentNoteId);
                if (note != null)
                {
                    var tagToRemove = context.Tags.Find(tag.TagID);
                    if (tagToRemove != null && note.Tags.Contains(tagToRemove))
                    {
                        note.Tags.Remove(tagToRemove);
                        context.SaveChanges();
                    }
                }
            }
            LoadCurrentNoteTags();
            LoadNotes();
            SelectedNode = FindNode(RootNotes, currentNoteId);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}