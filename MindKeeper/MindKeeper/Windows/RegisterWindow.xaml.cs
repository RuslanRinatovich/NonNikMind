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
using System.Windows.Shapes;

using MindKeeper.Entity;

namespace MindKeeper.Windows
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
            Owner = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault();
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string login = TbLogin.Text.Trim();
            string password = TbPassword.Password;
            string confirm = TbConfirmPassword.Password;
            string fullName = TbFullName.Text.Trim();

            // Валидация
            if (string.IsNullOrEmpty(login))
            {
                TxtError.Text = "Введите логин.";
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                TxtError.Text = "Введите пароль.";
                return;
            }
            if (password != confirm)
            {
                TxtError.Text = "Пароли не совпадают.";
                return;
            }
            if (password.Length < 4)
            {
                TxtError.Text = "Пароль должен быть не менее 4 символов.";
                return;
            }

            using (var context = DataEntities.GetContext())
            {
                // Проверка уникальности логина
                if (context.Users.Any(u => u.Username == login))
                {
                    TxtError.Text = "Пользователь с таким логином уже существует.";
                    return;
                }

                // Хэшируем пароль
                string hash = ComputeSha256Hash(password);

                // Создаём нового пользователя
                var newUser = new User
                {
                    Username = login,
                    PasswordHash = hash,
                    FullName = fullName,
                    Role = "User",
                    CreatedAt = DateTime.Now
                };
                context.Users.Add(newUser);
                context.SaveChanges();
            }

            // Успех
            MessageBox.Show("Регистрация успешна! Теперь вы можете войти.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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