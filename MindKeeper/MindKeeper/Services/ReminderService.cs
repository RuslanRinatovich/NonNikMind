using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MindKeeper.Entity;

namespace MindKeeper.Services
{
    public static class ReminderService
    {
        private static System.Timers.Timer _timer;

        public static void Start()
        {
            _timer = new System.Timers.Timer(60000); // каждую минуту
            _timer.Elapsed += (s, e) => CheckReminders();
            _timer.Start();
        }

        private static void CheckReminders()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                using (var context = DataEntities.GetContext())
                {
                    var now = DateTime.Now;
                    var upcoming = context.Notes
                        .Where(n => n.ReminderDate.HasValue && n.ReminderDate <= now && !n.IsReminderCompleted && n.IsDeleted == false)
                        .ToList();

                    foreach (var note in upcoming)
                    {
                        MessageBox.Show($"Напоминание: {note.Title}\n{note.ReminderNote}",
                                        "MindKeeper", MessageBoxButton.OK, MessageBoxImage.Information);
                        // Отмечаем как отправленное, чтобы не спамить каждый раз
                        note.IsReminderCompleted = true;
                        context.SaveChanges();
                    }
                }
            });
        }
    }
}