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
using EntityModel = MindKeeper.Entity.Entity; // Алиас для класса Entity

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

        private void RelatedNote_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var textBlock = sender as System.Windows.Controls.TextBlock;
            var note = textBlock?.DataContext as Note;
            if (note != null && _viewModel != null)
            {
                _viewModel.SelectedNote = _viewModel.Notes.FirstOrDefault(n => n.NoteID == note.NoteID);
            }
        }
    }

    public class NotesViewModel : INotifyPropertyChanged
    {
        private readonly int _currentUserId;
        private ObservableCollection<Note> _notes;
        private Note _selectedNote;
        private string _searchText;

        private ObservableCollection<Tag> _allTags;
        private ObservableCollection<Tag> _currentNoteTags;
        private Tag _selectedTagToAdd;
        private string _newTagName;

        private ObservableCollection<Note> _relatedNotes;

        



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
            // Обновляем список связанных заметок
            LoadRelatedNotes();
        }
        public ObservableCollection<Note> Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        public Note SelectedNote
        {
            get => _selectedNote;
            set
            {
                _selectedNote = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNoteSelected));
                LoadCurrentNoteTags();
                LoadRelatedNotes();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsNoteSelected => SelectedNote != null;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                LoadNotes();
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
        public ICommand SaveNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }
        public ICommand AddTagCommand { get; }
        public ICommand RemoveTagCommand { get; }
        public ICommand SummarizeCommand { get; }
        public ICommand RemoveLinkCommand { get; }
        public NotesViewModel()
        {
            _currentUserId = Manager.CurrentUser?.UserID ?? 0;

            NewNoteCommand = new RelayCommand(_ => CreateNewNote());
            SaveNoteCommand = new RelayCommand(_ => SaveNote(), _ => IsNoteSelected);
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => IsNoteSelected);
            AddTagCommand = new RelayCommand(_ => AddNewTag(), _ => !string.IsNullOrWhiteSpace(NewTagName));
            RemoveTagCommand = new RelayCommand(tag => RemoveTag((Tag)tag));
            SummarizeCommand = new RelayCommand(_ => CreateSummary(), _ => IsNoteSelected);
            RemoveLinkCommand = new RelayCommand(targetNote => RemoveLink((Note)targetNote), _ => IsNoteSelected);
            LoadAllTags();
            LoadNotes();
        }

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

                var list = query.OrderByDescending(n => n.UpdatedAt ?? DateTime.MinValue).ToList();
                Notes = new ObservableCollection<Note>(list);
            }
        }

        private void LoadAllTags()
        {
            using (var context = DataEntities.GetContext())
            {
                var tags = context.Tags.OrderBy(t => t.TagName).ToList();
                AllTags = new ObservableCollection<Tag>(tags);
            }
        }

        private void LoadCurrentNoteTags()
        {
            if (SelectedNote == null)
            {
                CurrentNoteTags = new ObservableCollection<Tag>();
                return;
            }

            using (var context = DataEntities.GetContext())
            {
                var note = context.Notes.Include("Tags")
                    .FirstOrDefault(n => n.NoteID == SelectedNote.NoteID);
                if (note?.Tags != null)
                    CurrentNoteTags = new ObservableCollection<Tag>(note.Tags);
                else
                    CurrentNoteTags = new ObservableCollection<Tag>();
            }
        }

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

        private void UpdateLinksForCurrentNote()
        {
            if (SelectedNote == null) return;

            var regex = new Regex(@"\[\[(.*?)\]\]");
            var matches = regex.Matches(SelectedNote.Content ?? "");

            var targetTitles = new System.Collections.Generic.HashSet<string>();
            foreach (Match match in matches)
            {
                string title = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    targetTitles.Add(title);
            }

            using (var context = DataEntities.GetContext())
            {
                var lowerTitles = targetTitles.Select(t => t.ToLower()).ToList();
                var targetNoteIds = context.Notes
                    .Where(n => n.UserID == _currentUserId && n.IsDeleted == false
                                && lowerTitles.Contains(n.Title.ToLower()))
                    .Select(n => n.NoteID)
                    .ToList();

                var existingLinkTargetIds = context.Links
                    .Where(l => l.SourceNoteID == SelectedNote.NoteID)
                    .Select(l => l.TargetNoteID)
                    .ToList();

                var toDelete = context.Links
                    .Where(l => l.SourceNoteID == SelectedNote.NoteID && !targetNoteIds.Contains(l.TargetNoteID));
                context.Links.RemoveRange(toDelete);

                foreach (int targetId in targetNoteIds)
                {
                    if (!existingLinkTargetIds.Contains(targetId))
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
        }

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
                    {
                        note.Tags.Add(tag);
                    }
                }
                context.SaveChanges();
            }
            LoadCurrentNoteTags();
            LoadAllTags();
        }

        private void SaveExtractedEntities()
        {
            if (SelectedNote == null) return;
            var (dates, emails, phones, urls) = AiService.ExtractEntities(SelectedNote.Content ?? "");

            using (var context = DataEntities.GetContext())
            {
                var oldEntities = context.Entities.Where(e => e.NoteID == SelectedNote.NoteID);
                context.Entities.RemoveRange(oldEntities);

                foreach (var date in dates)
                {
                    context.Entities.Add(new EntityModel { NoteID = SelectedNote.NoteID, EntityType = "date", EntityValue = date });
                }
                foreach (var email in emails)
                {
                    context.Entities.Add(new EntityModel { NoteID = SelectedNote.NoteID, EntityType = "email", EntityValue = email });
                }
                foreach (var phone in phones)
                {
                    context.Entities.Add(new EntityModel { NoteID = SelectedNote.NoteID, EntityType = "phone", EntityValue = phone });
                }
                foreach (var url in urls)
                {
                    context.Entities.Add(new EntityModel { NoteID = SelectedNote.NoteID, EntityType = "url", EntityValue = url });
                }
                context.SaveChanges();
            }
        }

        private void CreateNewNote()
        {
            string baseTitle = "Новая заметка";
            string title = baseTitle;
            int counter = 1;

            using (var context = DataEntities.GetContext())
            {
                while (true)
                {
                    bool exists = context.Notes.Any(n => n.UserID == _currentUserId && n.Title == title);
                    if (exists)
                    {
                        title = $"{baseTitle} ({counter++})";
                        continue;
                    }

                    var newNote = new Note
                    {
                        UserID = _currentUserId,
                        Title = title,
                        Content = "",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsDeleted = false
                    };
                    try
                    {
                        context.Notes.Add(newNote);
                        context.SaveChanges();
                        break;
                    }
                    catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
                    {
                        var inner = ex.InnerException?.Message;
                        if (inner != null && inner.Contains("UQ_User_Title"))
                        {
                            title = $"{baseTitle} ({counter++})";
                            context.Entry(newNote).State = System.Data.Entity.EntityState.Detached;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                LoadNotes();
                SelectedNote = Notes.FirstOrDefault(n => n.Title == title);
            }
        }

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
            using (var context = DataEntities.GetContext())
            {
                context.Entry(SelectedNote).State = EntityState.Modified;
                context.SaveChanges();
            }

            AutoTagFromKeywords();
            SaveExtractedEntities();
            UpdateLinksForCurrentNote();

            LoadNotes();
            SelectedNote = Notes.FirstOrDefault(n => n.NoteID == selectedId);
        }

        private void DeleteNote()
        {
            if (SelectedNote == null) return;
            var result = System.Windows.MessageBox.Show($"Удалить заметку \"{SelectedNote.Title}\"?",
                                                        "Подтверждение",
                                                        System.Windows.MessageBoxButton.YesNo,
                                                        System.Windows.MessageBoxImage.Question);
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
                SelectedNote = null;
            }
        }

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
                {
                    finalTitle = $"{newTitle} ({counter++})";
                }
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
                SelectedNote = Notes.FirstOrDefault(n => n.NoteID == newNote.NoteID);
            }
        }

        private void AddNewTag()
        {
            if (string.IsNullOrWhiteSpace(NewTagName)) return;
            int currentNoteId = SelectedNote?.NoteID ?? 0;
            if (currentNoteId == 0) return;

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
            SelectedNote = Notes.FirstOrDefault(n => n.NoteID == currentNoteId);
        }

        private void AddExistingTag()
        {
            if (SelectedTagToAdd == null) return;
            int currentNoteId = SelectedNote?.NoteID ?? 0;
            if (currentNoteId == 0) return;

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
            SelectedNote = Notes.FirstOrDefault(n => n.NoteID == currentNoteId);
            SelectedTagToAdd = null;
        }

        private void RemoveTag(Tag tag)
        {
            if (tag == null) return;
            int currentNoteId = SelectedNote?.NoteID ?? 0;
            if (currentNoteId == 0) return;

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
            SelectedNote = Notes.FirstOrDefault(n => n.NoteID == currentNoteId);
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