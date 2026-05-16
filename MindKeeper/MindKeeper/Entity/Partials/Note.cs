using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace MindKeeper.Entity
{
    public partial class Note
    {
       
   
        [NotMapped]
        public string ReminderTime
        {
            get => ReminderDate?.ToString("HH:mm") ?? "";
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    ReminderDate = null;
                }
                else if (TimeSpan.TryParse(value, out var time))
                {
                    var date = ReminderDate?.Date ?? DateTime.Now.Date;
                    ReminderDate = date.Add(time);
                }
            }
        }
    }
}