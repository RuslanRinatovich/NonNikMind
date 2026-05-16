using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Controls;
using MindKeeper.Entity;

namespace MindKeeper.Pages
{
    public partial class CalendarPage : Page
    {
        private CalendarViewModel _viewModel;

        public CalendarPage()
        {
            InitializeComponent();
            _viewModel = new CalendarViewModel();
            DataContext = _viewModel;
        }

        private void ReminderCard_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            var note = border?.DataContext as Note;
            if (note != null)
            {
                var notesPage = new NotesPage(note.NoteID);
                NavigationService?.Navigate(notesPage);
            }
        }
    }

    public class DayModel
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; }
        public ObservableCollection<Note> Reminders { get; set; } = new ObservableCollection<Note>();
        public System.Windows.Visibility HasNoRemindersVisibility => Reminders.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public class CalendarViewModel : INotifyPropertyChanged
    {
        private DateTime _currentDate = DateTime.Today;
        private ObservableCollection<DayModel> _weekDays;

        public ObservableCollection<DayModel> WeekDays
        {
            get => _weekDays;
            set { _weekDays = value; OnPropertyChanged(); }
        }

        public string WeekRange
        {
            get
            {
                var start = _currentDate.StartOfWeek(DayOfWeek.Monday);
                var end = start.AddDays(6);
                return $"{start:dd.MM} – {end:dd.MM.yyyy}";
            }
        }

        public ICommand PreviousWeekCommand { get; }
        public ICommand NextWeekCommand { get; }
        public ICommand ToggleCompletedCommand { get; }

        public CalendarViewModel()
        {
            PreviousWeekCommand = new RelayCommand(_ => { _currentDate = _currentDate.AddDays(-7); LoadWeek(); });
            NextWeekCommand = new RelayCommand(_ => { _currentDate = _currentDate.AddDays(7); LoadWeek(); });
            ToggleCompletedCommand = new RelayCommand(param => ToggleCompleted((Note)param));
            LoadWeek();
        }

        private void LoadWeek()
        {
            var start = _currentDate.StartOfWeek(DayOfWeek.Monday);
            var week = new ObservableCollection<DayModel>();
            for (int i = 0; i < 7; i++)
            {
                var day = start.AddDays(i);
                week.Add(new DayModel
                {
                    Date = day,
                    DayName = GetDayName(day),
                    Reminders = GetRemindersForDay(day)
                });
            }
            WeekDays = week;
            OnPropertyChanged(nameof(WeekRange));
        }
        private ObservableCollection<Note> GetRemindersForDay(DateTime day)
        {
            using (var context = DataEntities.GetContext())
            {
                var notes = context.Notes
                    .Where(n => n.UserID == Manager.CurrentUser.UserID &&
                                n.ReminderDate.HasValue &&
                                n.IsDeleted == false)
                    .ToList() // Выполняем запрос и загружаем в память
                    .Where(n => n.ReminderDate.Value.Date == day.Date)
                    .OrderBy(n => n.ReminderDate)
                    .ToList();
                return new ObservableCollection<Note>(notes);
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
            LoadWeek(); // обновляем текущую неделю
        }

        private string GetDayName(DateTime date)
        {
            return date.ToString("dddd", new System.Globalization.CultureInfo("ru-RU"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Вспомогательный метод расширения для начала недели
    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }
}