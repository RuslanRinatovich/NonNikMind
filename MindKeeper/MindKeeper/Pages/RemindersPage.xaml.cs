using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using MindKeeper.Entity;

namespace MindKeeper.Pages
{
    public partial class RemindersPage : Page
    {
        private RemindersViewModel _viewModel;

        public RemindersPage()
        {
            InitializeComponent();
            _viewModel = new RemindersViewModel();
            DataContext = _viewModel;
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel.SelectedReminder != null)
            {
                var notesPage = new NotesPage(_viewModel.SelectedReminder.NoteID);
                NavigationService?.Navigate(notesPage);
            }
        }
    }

    public class RemindersViewModel : INotifyPropertyChanged
    {
        private readonly int _currentUserId;
        private ObservableCollection<Note> _reminders;
        private Note _selectedReminder;

        public ObservableCollection<Note> Reminders
        {
            get => _reminders;
            set { _reminders = value; OnPropertyChanged(); }
        }

        public Note SelectedReminder
        {
            get => _selectedReminder;
            set { _selectedReminder = value; OnPropertyChanged(); }
        }

        public ICommand ToggleCompletedCommand { get; }

        public RemindersViewModel()
        {
            _currentUserId = Manager.CurrentUser?.UserID ?? 0;
            ToggleCompletedCommand = new RelayCommand(param => ToggleCompleted((Note)param));
            LoadReminders();
        }

        private void LoadReminders()
        {
            using (var context = DataEntities.GetContext())
            {
                var reminders = context.Notes
                    .Where(n => n.UserID == _currentUserId && n.IsDeleted == false && n.ReminderDate.HasValue)
                    .OrderBy(n => n.ReminderDate)
                    .ToList();
                Reminders = new ObservableCollection<Note>(reminders);
            }
        }

        private void ToggleCompleted(Note note)
        {
            if (note == null) return;
            using (var context = DataEntities.GetContext())
            {
                var n = context.Notes.Find(note.NoteID);
                if (n != null)
                {
                    n.IsReminderCompleted = !n.IsReminderCompleted;
                    context.SaveChanges();
                }
            }
            LoadReminders();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}