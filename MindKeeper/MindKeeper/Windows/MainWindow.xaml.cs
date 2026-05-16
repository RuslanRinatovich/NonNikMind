using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MindKeeper.Entity;      // Ваш класс Manager, DataEntities и User
using MindKeeper.Pages;
using MindKeeper.Windows;

namespace MindKeeper
{
    public partial class MainWindow : Window
    {

        private bool _isLoggingOut = false;
        public MainWindow()
        {
            InitializeComponent();
            // Передаём фрейм в статический менеджер (удобно для навигации из страниц)
            Manager.MainFrame = MainFrame;
        }

        // Загрузка окна – заполняем информацию о пользователе и загружаем первую страницу
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            if (Manager.CurrentUser != null)
            {
                TxtUserInfo.Text = $"{Manager.CurrentUser.FullName ?? Manager.CurrentUser.Username}";

                if (Manager.CurrentUser.Role == "Admin")
                {
                    BtnAdmin.Visibility = Visibility.Visible;
                    BtnTags.Visibility = Visibility.Visible; // только админ видит страницу тегов
                }
            }
            MainFrame.Navigate(new NotesPage());
            // Загружаем страницу по умолчанию
            //MainFrame.Navigate(new NotesPage());
        }
        private void BtnCalendar_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new CalendarPage());
        }
        private void BtnReminders_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new RemindersPage());
        }
        // Обработчик закрытия окна (по нажатию на крестик)
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Если окно закрывается не из-за явного выхода (Logout), то показываем окно входа
            // Для этого добавим флаг
            if (!_isLoggingOut)
            {
                MessageBoxResult result = MessageBox.Show("Вы действительно хотите выйти из приложения?",
                                                          "Выход",
                                                          MessageBoxButton.YesNo,
                                                          MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true; // отменяем закрытие
                    return;
                }
            }

            // Показываем окно входа, если оно ещё существует
            if (Owner != null && Owner is LoginWindow login && !_isLoggingOut)
            {
                login.Show();
            }
        }

        // Навигация по меню
        private void BtnNotes_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new NotesPage());
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SearchPage());
        }

        private void BtnTags_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new TagsPage());
        }

        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AdminPage());
        }

        // Кнопка "Настройки"
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SettingsPage());
        }

        // Кнопка "Выход" – явный выход
        // Кнопка выхода (явный выход)
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}