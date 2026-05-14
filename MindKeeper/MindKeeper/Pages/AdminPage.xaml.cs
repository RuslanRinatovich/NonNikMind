using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MindKeeper.Entity;
using MindKeeper.Services;

namespace MindKeeper.Pages
{
    public partial class AdminPage : System.Windows.Controls.Page
    {
        private AdminViewModel _viewModel;

        public AdminPage()
        {
            InitializeComponent();
            _viewModel = new AdminViewModel();
            DataContext = _viewModel;
        }
    }

    public class AdminViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<User> _users;
        private User _selectedUser;

        public ObservableCollection<User> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(); }
        }

        public User SelectedUser
        {
            get => _selectedUser;
            set { _selectedUser = value; OnPropertyChanged(); }
        }

        public ICommand LockUserCommand { get; }
        public ICommand UnlockUserCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand ChangeRoleCommand { get; }
        public AdminViewModel()
        {
            LockUserCommand = new RelayCommand(user => LockUser((User)user));
            UnlockUserCommand = new RelayCommand(user => UnlockUser((User)user));
            ResetPasswordCommand = new RelayCommand(user => ResetPassword((User)user));
            DeleteUserCommand = new RelayCommand(user => DeleteUser((User)user));
            ChangeRoleCommand = new RelayCommand(user => ChangeRole((User)user));
            LoadUsers();
        }

        private void ChangeRole(User user)
        {
            if (user == null) return;
            if (user.UserID == Manager.CurrentUser?.UserID)
            {
                MessageBox.Show("Нельзя изменить свою роль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            using (var context = DataEntities.GetContext())
            {
                var u = context.Users.Find(user.UserID);
                if (u != null)
                {
                    u.Role = u.Role == "User" ? "Admin" : "User";
                    context.SaveChanges();
                }
            }
            LoadUsers();
        }

        private void LoadUsers()
        {
            using (var context = DataEntities.GetContext())
            {
                Users = new ObservableCollection<User>(context.Users.OrderBy(u => u.UserID).ToList());
            }
        }

        private void LockUser(User user)
        {
            if (user == null || user.UserID == Manager.CurrentUser?.UserID) return;
            using (var context = DataEntities.GetContext())
            {
                var u = context.Users.Find(user.UserID);
                if (u != null)
                {
                    u.IsLocked = true;
                    context.SaveChanges();
                }
            }
            LoadUsers();
        }

        private void UnlockUser(User user)
        {
            if (user == null) return;
            using (var context = DataEntities.GetContext())
            {
                var u = context.Users.Find(user.UserID);
                if (u != null)
                {
                    u.IsLocked = false;
                    context.SaveChanges();
                }
            }
            LoadUsers();
        }

        private void ResetPassword(User user)
        {
            if (user == null) return;
            var result = System.Windows.MessageBox.Show($"Сбросить пароль для пользователя {user.Username} на 'default123'?",
                                                        "Подтверждение",
                                                        System.Windows.MessageBoxButton.YesNo,
                                                        System.Windows.MessageBoxImage.Question);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                string newHash = ComputeSha256Hash("default123");
                using (var context = DataEntities.GetContext())
                {
                    var u = context.Users.Find(user.UserID);
                    if (u != null)
                    {
                        u.PasswordHash = newHash;
                        context.SaveChanges();
                    }
                }
                System.Windows.MessageBox.Show("Пароль сброшен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        private void DeleteUser(User user)
        {
            if (user == null) return;
            if (user.UserID == Manager.CurrentUser?.UserID)
            {
                System.Windows.MessageBox.Show("Нельзя удалить самого себя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var result = System.Windows.MessageBox.Show($"Удалить пользователя {user.Username}? Будут удалены все его заметки, теги и связи.",
                                                        "Подтверждение",
                                                        System.Windows.MessageBoxButton.YesNo,
                                                        System.Windows.MessageBoxImage.Question);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                using (var context = DataEntities.GetContext())
                {
                    var u = context.Users.Find(user.UserID);
                    if (u != null)
                    {
                        context.Users.Remove(u);
                        context.SaveChanges();
                    }
                }
                LoadUsers();
            }
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}