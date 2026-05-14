using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public ICommand NewNoteCommand { get; }
        public ICommand SaveNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }

        public NotesViewModel()
        {
            _currentUserId = Manager.CurrentUser?.UserID ?? 0;
            NewNoteCommand = new RelayCommand(_ => CreateNewNote());
            SaveNoteCommand = new RelayCommand(_ => SaveNote(), _ => IsNoteSelected);
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => IsNoteSelected);
            LoadNotes();
        }

        private void LoadNotes()
        {
            using (var context = DataEntities.GetContext())
            {
                var query = context.Set<Note>()
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

        private void CreateNewNote()
        {
            var newNote = new Note
            {
                UserID = _currentUserId,
                Title = "Новая заметка",
                Content = "",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDeleted = false
            };
            using (var context = DataEntities.GetContext())
            {
                context.Set<Note>().Add(newNote);
                context.SaveChanges();
            }
            LoadNotes();
            // Выбираем только что созданную заметку
            SelectedNote = Notes.FirstOrDefault(n => n.NoteID == newNote.NoteID);
        }

        private void SaveNote()
        {
            if (SelectedNote == null) return;

            // Сохраняем ID текущей заметки, чтобы восстановить выбор после перезагрузки
            int selectedId = SelectedNote.NoteID;

            // Обновляем дату
            SelectedNote.UpdatedAt = DateTime.Now;

            using (var context = DataEntities.GetContext())
            {
                context.Entry(SelectedNote).State = System.Data.Entity.EntityState.Modified;
                context.SaveChanges();
            }

            // Перезагружаем список, чтобы отобразить актуальные данные (например, новую дату)
            LoadNotes();

            // Восстанавливаем выбранную заметку
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
                    var note = context.Set<Note>().Find(SelectedNote.NoteID);
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