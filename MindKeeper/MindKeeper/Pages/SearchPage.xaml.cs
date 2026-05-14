using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindKeeper.Entity;

namespace MindKeeper.Pages
{
    public partial class SearchPage : Page
    {
        private SearchViewModel _viewModel;

        public SearchPage()
        {
            InitializeComponent();
            _viewModel = new SearchViewModel();
            DataContext = _viewModel;
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Получаем DataGrid
            var dataGrid = sender as DataGrid;
            if (dataGrid == null) return;

            // Получаем выбранный элемент напрямую из DataGrid
            var selectedNote = dataGrid.SelectedItem as Note;
            if (selectedNote != null)
            {
                var notesPage = new NotesPage(selectedNote.NoteID);
                NavigationService?.Navigate(notesPage);
            }
            else
            {
                // Отладка: показываем, что выбрано
                System.Diagnostics.Debug.WriteLine($"SelectedItem is null or not Note type. Type: {dataGrid.SelectedItem?.GetType()}");
            }
        }
    }

    public class TagSelectItem
    {
        public int? TagId { get; set; }
        public string DisplayName { get; set; }
    }

    public class SearchViewModel : INotifyPropertyChanged
    {
        private readonly int _currentUserId;
        private string _searchQuery;
        private int? _selectedTagId;
        private bool _onlyTitle;
        private ObservableCollection<Note> _searchResults;
        private Note _selectedNote;
        private ObservableCollection<TagSelectItem> _allTagsWithEmpty;

        public ObservableCollection<TagSelectItem> AllTagsWithEmpty
        {
            get => _allTagsWithEmpty;
            set { _allTagsWithEmpty = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Note> SearchResults
        {
            get => _searchResults;
            set { _searchResults = value; OnPropertyChanged(); OnPropertyChanged(nameof(NoResultsVisibility)); }
        }

        public Note SelectedNote
        {
            get => _selectedNote;
            set
            {
                _selectedNote = value;
                OnPropertyChanged();
                // Для отладки
                System.Diagnostics.Debug.WriteLine($"SelectedNote установлен: {value?.Title ?? "null"}");
            }
        }

        public System.Windows.Visibility NoResultsVisibility =>
            SearchResults?.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string SearchQuery
        {
            get => _searchQuery;
            set { _searchQuery = value; OnPropertyChanged(); PerformSearch(); }
        }

        public int? SelectedTagId
        {
            get => _selectedTagId;
            set { _selectedTagId = value; OnPropertyChanged(); PerformSearch(); }
        }

        public bool OnlyTitle
        {
            get => _onlyTitle;
            set { _onlyTitle = value; OnPropertyChanged(); PerformSearch(); }
        }

        public SearchViewModel()
        {
            _currentUserId = Manager.CurrentUser?.UserID ?? 0;
            LoadAllTags();
            SearchResults = new ObservableCollection<Note>();
        }

        private void LoadAllTags()
        {
            using (var context = DataEntities.GetContext())
            {
                var items = new ObservableCollection<TagSelectItem>();
                items.Add(new TagSelectItem { TagId = null, DisplayName = "📌 Все теги" });

                var tags = context.Tags.OrderBy(t => t.TagName).ToList();
                foreach (var tag in tags)
                {
                    items.Add(new TagSelectItem { TagId = tag.TagID, DisplayName = tag.TagName });
                }
                AllTagsWithEmpty = items;
            }
        }

        private void PerformSearch()
        {
            using (var context = DataEntities.GetContext())
            {
                var query = context.Notes.Include("Tags")
                    .Where(n => n.UserID == _currentUserId && n.IsDeleted == false);

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    if (OnlyTitle)
                        query = query.Where(n => n.Title.Contains(SearchQuery));
                    else
                        query = query.Where(n => n.Title.Contains(SearchQuery) ||
                                                 (n.Content != null && n.Content.Contains(SearchQuery)));
                }

                if (SelectedTagId.HasValue)
                {
                    query = query.Where(n => n.Tags.Any(t => t.TagID == SelectedTagId.Value));
                }

                var results = query.OrderByDescending(n => n.UpdatedAt).ToList();
                SearchResults = new ObservableCollection<Note>(results);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}