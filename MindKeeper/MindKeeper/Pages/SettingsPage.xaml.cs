using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MindKeeper.Entity;
using System.Windows.Media;
using MindKeeper.Services; // добавить
using MaterialDesignThemes.Wpf;

namespace MindKeeper.Pages
{
    public partial class SettingsPage : Page, INotifyPropertyChanged
    {
        private User _currentUser;
        private bool _isDarkTheme;
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand ImportCsvCommand { get; }
        public ICommand ImportJsonCommand { get; }


        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                _isDarkTheme = value;
                OnPropertyChanged();
                ToggleTheme(); // при изменении свойства вызываем переключение темы
            }
        }

        public ICommand ToggleThemeCommand { get; }

        public SettingsPage()
        {
            InitializeComponent();
            ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
            DataContext = this; // важно для привязки
            Loaded += SettingsPage_Loaded;
            ExportCsvCommand = new RelayCommand(_ => ExportCsv());
            ExportJsonCommand = new RelayCommand(_ => ExportJson());
            ImportCsvCommand = new RelayCommand(_ => ImportCsv());
            ImportJsonCommand = new RelayCommand(_ => ImportJson());
        }
        private void ExportCsv()
        {
            using (var context = DataEntities.GetContext())
            {
                var notes = context.Notes.Include("Tags")
                    .Where(n => n.UserID == Manager.CurrentUser.UserID && n.IsDeleted == false)
                    .ToList();
                if (DataExportImportService.ExportNotesToCsv(notes))
                {
                    MessageBox.Show($"Экспорт CSV завершён. Экспортировано {notes.Count} заметок.",
                                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ExportJson()
        {
            using (var context = DataEntities.GetContext())
            {
                var notes = context.Notes.Include("Tags")
                    .Where(n => n.UserID == Manager.CurrentUser.UserID && n.IsDeleted == false)
                    .ToList();
                if (DataExportImportService.ExportNotesToJson(notes))
                {
                    MessageBox.Show($"Экспорт JSON завершён. Экспортировано {notes.Count} заметок.",
                                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ImportCsv()
        {
            var imported = DataExportImportService.ImportNotesFromCsv();
            if (imported != null && imported.Count > 0)
            {
                DataExportImportService.SaveImportedNotesWithHierarchy(imported, Manager.CurrentUser.UserID);
                MessageBox.Show($"Импорт CSV завершён. Импортировано {imported.Count} заметок.",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshNotesPage();
            }
            else
            {
                MessageBox.Show("Не удалось импортировать данные из CSV-файла.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void ImportJson()
        {
            var imported = DataExportImportService.ImportNotesFromJson();
            if (imported != null && imported.Count > 0)
            {
                DataExportImportService.SaveImportedNotesWithHierarchy(imported, Manager.CurrentUser.UserID);
                MessageBox.Show($"Импорт JSON завершён. Импортировано {imported.Count} заметок.",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshNotesPage();
            }
            else
            {
                MessageBox.Show("Не удалось импортировать данные из JSON-файла.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshNotesPage()
        {
            if (Application.Current.Windows.OfType<MainWindow>().FirstOrDefault() is MainWindow main)
            {
                if (main.MainFrame.Content is NotesPage notesPage)
                {
                    notesPage.RefreshNotes(); // теперь доступно
                }
            }
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _currentUser = Manager.CurrentUser;
            if (_currentUser != null)
            {
                TbFullName.Text = _currentUser.FullName ?? "";
            }

            // Определяем текущую тему (по наличию тёмной темы в ресурсах)
            _isDarkTheme = IsDarkThemeActive();
            OnPropertyChanged(nameof(IsDarkTheme));
        }

        private bool IsDarkThemeActive()
        {
            return App.Current.Resources.MergedDictionaries
                .Any(d => d.Source != null && d.Source.ToString().Contains("CustomDarkTheme"));
        }


        private void ToggleTheme()
        {
            var paletteHelper = new PaletteHelper();
            ITheme theme = paletteHelper.GetTheme();

            // Правильное сравнение интерфейса IBaseTheme с перечислением через метод расширения
            if (theme.GetBaseTheme() == BaseTheme.Light)
            {
                theme.SetBaseTheme(Theme.Dark);
            }
            else
            {
                theme.SetBaseTheme(Theme.Light);
            }

            // Применяем тему. Библиотека сама обновит цвета текста во всех элементах
            paletteHelper.SetTheme(theme);
        }
        private void ApplyLightTheme()
        {
            // Удаляем кастомную тёмную тему
            var dark = App.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.ToString().Contains("CustomDarkTheme"));
            if (dark != null)
                App.Current.Resources.MergedDictionaries.Remove(dark);

            // Убеждаемся, что светлая тема присутствует
            var light = App.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.ToString().Contains("MaterialDesignTheme.Light"));
            if (light == null)
            {
                var lightTheme = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml", UriKind.Absolute)
                };
                App.Current.Resources.MergedDictionaries.Add(lightTheme);
            }
            EnsureDefaults();
        }

        private void ApplyDarkTheme()
        {
            // Удаляем существующую кастомную тему (если есть)
            var existingDark = App.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.ToString().Contains("CustomDarkTheme"));
            if (existingDark != null)
                App.Current.Resources.MergedDictionaries.Remove(existingDark);

            // Добавляем нашу кастомную тёмную тему
            var customDark = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MindKeeper;component/Themes/CustomDarkTheme.xaml", UriKind.Absolute)
            };
            App.Current.Resources.MergedDictionaries.Add(customDark);
            EnsureDefaults();
        }

        private void EnsureDefaults()
        {
            // Добавляем словарь со стандартными стилями, если его нет
            var defaults = App.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.ToString().Contains("MaterialDesignTheme.Defaults"));
            if (defaults == null)
            {
                var defaultDict = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml", UriKind.Absolute)
                };
                App.Current.Resources.MergedDictionaries.Add(defaultDict);
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

                // Обновляем ФИО
                user.FullName = string.IsNullOrEmpty(newFullName) ? null : newFullName;

                // Смена пароля
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
                TbOldPassword.Password = "";
                TbNewPassword.Password = "";
                TbConfirmPassword.Password = "";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}