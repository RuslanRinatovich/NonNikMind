using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindKeeper.Entity;

namespace MindKeeper.Pages
{
    public partial class SettingsPage : Page
    {
        private User _currentUser;
        public ICommand ToggleThemeCommand { get; }

        public SettingsPage()
        {
            InitializeComponent();
            ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
            DataContext = this; // Важно: устанавливаем DataContext на саму страницу
            Loaded += SettingsPage_Loaded;
        }

        private void ToggleTheme()
        {
            var theme = App.Current.Resources.MergedDictionaries
       .OfType<MaterialDesignThemes.Wpf.BundledTheme>()
       .FirstOrDefault();

            if (theme != null)
            {
                if (theme.BaseTheme == MaterialDesignThemes.Wpf.BaseTheme.Light)
                    theme.BaseTheme = MaterialDesignThemes.Wpf.BaseTheme.Dark;
                else
                    theme.BaseTheme = MaterialDesignThemes.Wpf.BaseTheme.Light;
            }
           
        }
        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _currentUser = Manager.CurrentUser;
            if (_currentUser != null)
            {
                TbFullName.Text = _currentUser.FullName ?? "";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string newFullName = TbFullName.Text.Trim();
            string oldPassword = TbOldPassword.Password;
            string newPassword = TbNewPassword.Password;
            string confirmPassword = TbConfirmPassword.Password;

            using (var context = DataEntities.GetContext())
            {
                var user = context.Users.Find(_currentUser.UserID);
                if (user == null)
                {
                    TxtMessage.Text = "Ошибка: пользователь не найден.";
                    return;
                }

                // 1. Обновляем ФИО
                user.FullName = string.IsNullOrEmpty(newFullName) ? null : newFullName;

                // 2. Если заполнены поля смены пароля – проверяем и меняем
                bool changePassword = !string.IsNullOrEmpty(oldPassword) || !string.IsNullOrEmpty(newPassword);
                if (changePassword)
                {
                    if (string.IsNullOrEmpty(oldPassword))
                    {
                        TxtMessage.Text = "Введите текущий пароль.";
                        return;
                    }
                    if (string.IsNullOrEmpty(newPassword))
                    {
                        TxtMessage.Text = "Введите новый пароль.";
                        return;
                    }
                    if (newPassword.Length < 4)
                    {
                        TxtMessage.Text = "Новый пароль должен быть не менее 4 символов.";
                        return;
                    }
                    if (newPassword != confirmPassword)
                    {
                        TxtMessage.Text = "Новый пароль и подтверждение не совпадают.";
                        return;
                    }

                    string oldHash = ComputeSha256Hash(oldPassword);
                    if (user.PasswordHash != oldHash)
                    {
                        TxtMessage.Text = "Неверный текущий пароль.";
                        return;
                    }

                    user.PasswordHash = ComputeSha256Hash(newPassword);
                }

                context.SaveChanges();

                // Обновляем Manager.CurrentUser
                Manager.CurrentUser = user;
                _currentUser = user;

                TxtMessage.Foreground = System.Windows.Media.Brushes.Green;
                TxtMessage.Text = "Данные успешно сохранены!";

                // Очищаем поля паролей
                TbOldPassword.Password = "";
                TbNewPassword.Password = "";
                TbConfirmPassword.Password = "";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Возвращаем исходные данные
            TbFullName.Text = _currentUser?.FullName ?? "";
            TbOldPassword.Password = "";
            TbNewPassword.Password = "";
            TbConfirmPassword.Password = "";
            TxtMessage.Text = "";
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }
    }
}