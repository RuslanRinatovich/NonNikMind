using MindKeeper.Entity;
using System;
using System.Linq;
using System.Windows;

namespace MindKeeper.Windows
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            TbLogin.Text = "alexey";
            TbPass.Password = "password";
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        private void BtnOkClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string hashedPassword = ComputeSha256Hash(TbPass.Password);

                // Используем using – контекст будет создан и корректно удалён
                using (var context = DataEntities.GetContext())
                {
                    User u = context.Users.FirstOrDefault(p => p.PasswordHash == hashedPassword && p.Username == TbLogin.Text);

                    if (u != null)
                    {
                        Manager.CurrentUser = u;
                        MainWindow mainWindow = new MainWindow();
                        mainWindow.Owner = this;
                        this.Hide();
                        mainWindow.Show();
                    }
                    else
                    {
                        MessageBox.Show("Неверный логин или пароль");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult x = MessageBox.Show("Вы действительно хотите выйти?",
                "Выйти", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (x == MessageBoxResult.Cancel)
                e.Cancel = true;
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}