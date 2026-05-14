using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
namespace MindKeeper.Entity
{
    public class Manager
    {
        /// <summary>
        /// Фрейм, в котором отбражаются Page
        /// </summary>
        public static Frame MainFrame { get; set; }
        /// <summary>
        /// Текущий пользователь системы
        /// </summary>
        public static User CurrentUser { get; set; }
    }
}
