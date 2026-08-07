using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuestBoard.UI.Models;
using QuestBoard.UI.Services;

namespace QuestBoard.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly QuestRepository _repository;
        private readonly QuestCliService _cliService;

        private string _actorId;
        private string _selectedFilter = "All Quests";
        private bool _isAudioMuted = false;
        private QuestCardViewModel? _selectedQuest;
        private bool _isDetailOpen = false;
        private bool _isCreateOpen = false;
        private string _statusMessage = "Quest Board Ready";

        private List<QuestCardViewModel> _allFilteredCards = new();
        private int _currentPage = 1;
        private int _pageSize = 6;
        private int _columns = 3;
        private int _rows = 2;

        public ObservableCollection<QuestCardViewModel> QuestCards { get; } = new();

        public ICommand SelectQuestCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel(string repoRoot)
        {
            _repository = new QuestRepository(repoRoot);
            _cliService = new QuestCliService(repoRoot);

            _actorId = Environment.UserName;

            SelectQuestCommand = new RelayCommand(param =>
            {
                if (param is QuestCardViewModel card)
                {
                    SelectedQuest = card;
                }
            });
            PreviousPageCommand = new RelayCommand(_ => PreviousPage(), _ => CanGoPrevious);
            NextPageCommand = new RelayCommand(_ => NextPage(), _ => CanGoNext);
            FirstPageCommand = new RelayCommand(_ => FirstPage(), _ => CanGoPrevious);
            LastPageCommand = new RelayCommand(_ => LastPage(), _ => CanGoNext);

            _repository.QuestsChanged += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(RefreshQuests);
            };

            RefreshQuests();
        }

        public string ActorId
        {
            get => _actorId;
            set { _actorId = value; OnPropertyChanged(); }
        }

        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (_selectedFilter != value)
                {
                    _selectedFilter = value;
                    OnPropertyChanged();
                    _currentPage = 1;
                    RefreshQuests();
                }
            }
        }

        public bool IsAudioMuted
        {
            get => _isAudioMuted;
            set
            {
                _isAudioMuted = value;
                AudioService.Instance.IsMuted = value;
                OnPropertyChanged();
            }
        }

        public QuestCardViewModel? SelectedQuest
        {
            get => _selectedQuest;
            set
            {
                _selectedQuest = value;
                OnPropertyChanged();
                IsDetailOpen = value != null;
                if (value != null)
                {
                    AudioService.Instance.PlayPaperRustle();
                }
            }
        }

        public bool IsDetailOpen
        {
            get => _isDetailOpen;
            set { _isDetailOpen = value; OnPropertyChanged(); }
        }

        public bool IsCreateOpen
        {
            get => _isCreateOpen;
            set { _isCreateOpen = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                int newPage = Math.Clamp(value, 1, TotalPages);
                if (_currentPage != newPage)
                {
                    _currentPage = newPage;
                    OnPropertyChanged();
                    UpdatePagedCards();
                }
            }
        }

        public int PageSize
        {
            get => _pageSize;
            private set
            {
                if (_pageSize != value && value > 0)
                {
                    _pageSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPages));
                    CurrentPage = Math.Clamp(_currentPage, 1, TotalPages);
                    UpdatePagedCards();
                }
            }
        }

        public int Columns
        {
            get => _columns;
            private set
            {
                if (_columns != value)
                {
                    _columns = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Rows
        {
            get => _rows;
            private set
            {
                if (_rows != value)
                {
                    _rows = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TotalQuestsCount => _allFilteredCards.Count;

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalQuestsCount / (double)PageSize));

        public string PageInfoText => $"Page {CurrentPage} of {TotalPages} ({TotalQuestsCount} total)";

        public bool CanGoPrevious => CurrentPage > 1;

        public bool CanGoNext => CurrentPage < TotalPages;

        public void SetPageCapacity(int columns, int rows)
        {
            columns = Math.Max(1, columns);
            rows = Math.Max(1, rows);
            int newPageSize = columns * rows;

            bool capacityChanged = _columns != columns || _rows != rows || _pageSize != newPageSize;

            Columns = columns;
            Rows = rows;
            PageSize = newPageSize;

            if (capacityChanged)
            {
                CurrentPage = Math.Clamp(_currentPage, 1, TotalPages);
                UpdatePagedCards();
            }
        }

        public void PreviousPage()
        {
            if (CanGoPrevious)
            {
                AudioService.Instance.PlayWoodTap();
                CurrentPage--;
            }
        }

        public void NextPage()
        {
            if (CanGoNext)
            {
                AudioService.Instance.PlayWoodTap();
                CurrentPage++;
            }
        }

        public void FirstPage()
        {
            if (CanGoPrevious)
            {
                AudioService.Instance.PlayWoodTap();
                CurrentPage = 1;
            }
        }

        public void LastPage()
        {
            if (CanGoNext)
            {
                AudioService.Instance.PlayWoodTap();
                CurrentPage = TotalPages;
            }
        }

        public void RefreshQuests()
        {
            var rawList = _repository.LoadAllQuests();
            _allFilteredCards = rawList.Where(q =>
            {
                switch (SelectedFilter)
                {
                    case "Ready for Me":
                        return q.Status.Equals("ready", StringComparison.OrdinalIgnoreCase);
                    case "My Active Quests":
                        return q.Status.Equals("active", StringComparison.OrdinalIgnoreCase) &&
                               !string.IsNullOrEmpty(q.Owner) &&
                               q.Owner.IndexOf(ActorId, StringComparison.OrdinalIgnoreCase) >= 0;
                    case "Review Needed":
                        return q.Status.Equals("review", StringComparison.OrdinalIgnoreCase);
                    default:
                        return true;
                }
            }).Select(m => new QuestCardViewModel(m)).ToList();

            _currentPage = Math.Clamp(_currentPage, 1, TotalPages);
            UpdatePagedCards();
        }

        private void UpdatePagedCards()
        {
            var pagedItems = _allFilteredCards
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            QuestCards.Clear();
            foreach (var card in pagedItems)
            {
                QuestCards.Add(card);
            }

            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(TotalQuestsCount));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfoText));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));

            StatusMessage = $"Showing page {CurrentPage} of {TotalPages} ({TotalQuestsCount} quest cards total) - {DateTime.Now:T}";
        }

        public async Task ClaimSelectedQuestAsync()
        {
            if (SelectedQuest == null) return;
            StatusMessage = $"Claiming quest {SelectedQuest.Id}...";
            var (success, output) = await _cliService.ClaimQuestAsync(SelectedQuest.Id, ActorId);
            if (success)
            {
                AudioService.Instance.PlaySuccessSound();
                StatusMessage = $"Claimed {SelectedQuest.Id}";
                RefreshQuests();
                IsDetailOpen = false;
            }
            else
            {
                MessageBox.Show(output, "Claim Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task HandoffSelectedQuestAsync(string nextAction, string note)
        {
            if (SelectedQuest == null) return;
            StatusMessage = $"Updating quest {SelectedQuest.Id}...";
            var (success, output) = await _cliService.HandoffQuestAsync(SelectedQuest.Id, ActorId, nextAction, note);
            if (success)
            {
                AudioService.Instance.PlayWoodTap();
                StatusMessage = $"Updated {SelectedQuest.Id}";
                RefreshQuests();
                IsDetailOpen = false;
            }
            else
            {
                MessageBox.Show(output, "Handoff Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task MoveSelectedQuestAsync(string status, string nextAction, string note, string? blocker = null)
        {
            if (SelectedQuest == null) return;
            StatusMessage = $"Moving quest {SelectedQuest.Id} to {status}...";
            var (success, output) = await _cliService.MoveQuestAsync(SelectedQuest.Id, status, ActorId, nextAction, note, blocker);
            if (success)
            {
                AudioService.Instance.PlayWoodTap();
                StatusMessage = $"Moved {SelectedQuest.Id} to {status}";
                RefreshQuests();
                IsDetailOpen = false;
            }
            else
            {
                MessageBox.Show(output, "Move Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task FinishSelectedQuestAsync(string note)
        {
            if (SelectedQuest == null) return;
            StatusMessage = $"Finishing quest {SelectedQuest.Id}...";
            var (success, output) = await _cliService.FinishQuestAsync(SelectedQuest.Id, ActorId, note);
            if (success)
            {
                AudioService.Instance.PlaySuccessSound();
                StatusMessage = $"Completed {SelectedQuest.Id}";
                RefreshQuests();
                IsDetailOpen = false;
            }
            else
            {
                MessageBox.Show(output, "Finish Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task CreateNewQuestAsync(string title, string context, string nextAction, string priority)
        {
            StatusMessage = "Posting new quest...";
            var (success, output) = await _cliService.AddQuestAsync(title, context, nextAction, priority, ActorId);
            if (success)
            {
                AudioService.Instance.PlayWoodTap();
                StatusMessage = "New quest posted to board!";
                RefreshQuests();
                IsCreateOpen = false;
            }
            else
            {
                MessageBox.Show(output, "Create Quest Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
