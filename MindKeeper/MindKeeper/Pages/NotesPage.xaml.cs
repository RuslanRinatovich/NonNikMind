using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MindKeeper.Entity;

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
    }

    public class NotesViewModel : INotifyPropertyChanged
    {
        private readonly int _currentUserId;
        private ObservableCollection<Note> _notes;
        private Note _selectedNote;
        private string _searchText;

        // Для тегов
        private ObservableCollection<Tag> _allTags;
        private ObservableCollection<Tag> _currentNoteTags;
        private Tag _selectedTagToAdd;
        private string _newTagName;

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
                LoadCurrentNoteTags();  // загружаем теги для выбранной заметки
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

        // Теги: список всех тегов
        public ObservableCollection<Tag> AllTags
        {
            get => _allTags;
            set { _allTags = value; OnPropertyChanged(); }
        }

        // Теги текущей заметки
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

        // Команды
        public ICommand NewNoteCommand { get; }
        public ICommand SaveNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }
        public ICommand AddTagCommand { get; }
        public ICommand RemoveTagCommand { get; }

        public NotesViewModel()
        {
            _currentUserId = Manager.CurrentUser?.UserID ?? 0;

            NewNoteCommand = new RelayCommand(_ => CreateNewNote());
            SaveNoteCommand = new RelayCommand(_ => SaveNote(), _ => IsNoteSelected);
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => IsNoteSelected);
            AddTagCommand = new RelayCommand(_ => AddNewTag(), _ => !string.IsNullOrWhiteSpace(NewTagName));
            RemoveTagCommand = new RelayCommand(tag => RemoveTag((Tag)tag));

            LoadAllTags();
            LoadNotes();
        }

        // Загрузка всех заметок пользователя (с тегами)
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

        // Загрузка всех тегов из БД
        private void LoadAllTags()
        {
            using (var context = DataEntities.GetContext())
            {
                var tags = context.Tags.OrderBy(t => t.TagName).ToList();
                AllTags = new ObservableCollection<Tag>(tags);
            }
        }

        // Загрузка тегов для выбранной заметки
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

        // Создание новой заметки
        private void CreateNewNote()
        {
            string baseTitle = "Новая заметка";
            string title = baseTitle;
            int counter = 1;

            using (var context = DataEntities.GetContext())
            {
                while (true)
                {
                    // Проверка существования (включая удалённые заметки) – для выбора уникального имени
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
                        break; // успешно
                    }
                    catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
                    {
                        // Если вдруг нарушение уникальности (например, ограничение ещё не удалено), генерируем новое имя
                        var inner = ex.InnerException?.Message;
                        if (inner != null && inner.Contains("UQ_User_Title"))
                        {
                            title = $"{baseTitle} ({counter++})";
                            context.Entry(newNote).State = System.Data.Entity.EntityState.Detached;
                        }
                        else
                        {
                            throw; // другая ошибка
                        }
                    }
                }
                LoadNotes();
                SelectedNote = Notes.FirstOrDefault(n => n.Title == title);
            }
        }
        // Сохранение изменений текущей заметки
        private void SaveNote()
        {
            if (SelectedNote == null) return;
            int selectedId = SelectedNote.NoteID;

            // Проверка уникальности заголовка (исключая текущую заметку)
            using (var checkContext = DataEntities.GetContext())
            {
                if (checkContext.Notes.Any(n => n.UserID == _currentUserId && n.Title == SelectedNote.Title && n.NoteID != selectedId && (n.IsDeleted == false)))
                {
                    System.Windows.MessageBox.Show("Заметка с таким заголовком уже существует. Пожалуйста, выберите другой заголовок.",
                                                   "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }

            SelectedNote.UpdatedAt = DateTime.Now;
            using (var context = DataEntities.GetContext())
            {
                context.Entry(SelectedNote).State = System.Data.Entity.EntityState.Modified;
                context.SaveChanges();
            }
            LoadNotes();
            SelectedNote = Notes.FirstOrDefault(n => n.NoteID == selectedId);
        }
        // Мягкое удаление заметки
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

        // Добавление нового тега (если имя введено в ComboBox)
        private void AddNewTag()
        {
            if (string.IsNullOrWhiteSpace(NewTagName)) return;

            int currentNoteId = SelectedNote?.NoteID ?? 0;
            if (currentNoteId == 0) return;

            using (var context = DataEntities.GetContext())
            {
                // Найти или создать тег
                var tag = context.Tags.FirstOrDefault(t => t.TagName == NewTagName);
                if (tag == null)
                {
                    tag = new Tag { TagName = NewTagName };
                    context.Tags.Add(tag);
                    context.SaveChanges();
                }

                // Привязать к заметке
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

        // Добавление существующего тега (выбранного из ComboBox)
        private void AddExistingTag()
        {
            if (SelectedTagToAdd == null) return;
            int currentNoteId = SelectedNote?.NoteID ?? 0;
            if (currentNoteId == 0) return;

            using (var context = DataEntities.GetContext())
            {
                // Перезагружаем тег в текущем контексте (находим по ID)
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
                    // Находим тот же тег в текущем контексте
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

    // Реализация RelayCommand с поддержкой CanExecuteChanged
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