using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MindKeeper.Entity;

namespace MindKeeper.Pages
{


    public partial class TagsPage : System.Windows.Controls.Page
    {
        private TagsViewModel _viewModel;

        public TagsPage()
        {
            InitializeComponent();
            _viewModel = new TagsViewModel();
            DataContext = _viewModel;
        }
    }

    public class TagWithCount
    {
        public int TagID { get; set; }
        public string TagName { get; set; }
        public int UserNoteCount { get; set; }      // Заметки текущего пользователя
        public int GlobalNoteCount { get; set; }    // Все заметки в системе
    }

    public class TagsViewModel : INotifyPropertyChanged
    {
        private readonly int _currentUserId;
        private ObservableCollection<TagWithCount> _tags;
        private string _newTagName;

        public ObservableCollection<TagWithCount> Tags
        {
            get => _tags;
            set { _tags = value; OnPropertyChanged(); }
        }

        public string NewTagName
        {
            get => _newTagName;
            set { _newTagName = value; OnPropertyChanged(); }
        }

        public ICommand AddTagCommand { get; }
        public ICommand DeleteTagCommand { get; }

        public TagsViewModel()
        {
            _currentUserId = Manager.CurrentUser?.UserID ?? 0;
            AddTagCommand = new RelayCommand(_ => AddTag(), _ => !string.IsNullOrWhiteSpace(NewTagName));
            DeleteTagCommand = new RelayCommand(param => DeleteTag((TagWithCount)param));
            LoadTags();
        }

        private void LoadTags()
        {
            using (var context = DataEntities.GetContext())
            {
                var tags = context.Tags
                    .Select(t => new TagWithCount
                    {
                        TagID = t.TagID,
                        TagName = t.TagName,
                        UserNoteCount = t.Notes.Count(n => n.UserID == _currentUserId && n.IsDeleted == false),
                        GlobalNoteCount = t.Notes.Count(n => n.IsDeleted == false)
                    })
                    .OrderBy(t => t.TagName)
                    .ToList();
                Tags = new ObservableCollection<TagWithCount>(tags);
            }
        }

        private void AddTag()
        {
            using (var context = DataEntities.GetContext())
            {
                if (!context.Tags.Any(t => t.TagName == NewTagName))
                {
                    context.Tags.Add(new Tag { TagName = NewTagName });
                    context.SaveChanges();
                }
            }
            NewTagName = "";
            LoadTags();
        }

        private void DeleteTag(TagWithCount tag)
        {
            if (tag == null) return;

            // Админ может удалять любые теги
            var message = $"Удалить тег \"{tag.TagName}\"?\n\n" +
                          $"Он используется в {tag.GlobalNoteCount} заметках.\n\n" +
                          "Удаление тега удалит его из всех заметок. Отменить нельзя.";

            var result = System.Windows.MessageBox.Show(message,
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                using (var context = DataEntities.GetContext())
                {
                    var tagToDelete = context.Tags.Find(tag.TagID);
                    if (tagToDelete != null)
                    {
                        context.Tags.Remove(tagToDelete);
                        context.SaveChanges();
                    }
                }
                LoadTags();
                System.Windows.MessageBox.Show($"Тег \"{tag.TagName}\" удалён.",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
