using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IEquipmentApi _api;

    public EquipmentViewModel EquipmentViewModel { get; }
    public BorrowingsViewModel BorrowingsViewModel { get; }
    public StudentsViewModel StudentsViewModel { get; }
    public EquipmentManagementViewModel EquipmentManagementViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }
    public BorrowingSearchViewModel BorrowingSearchViewModel { get; }

    [ObservableProperty]
    private ObservableObject currentViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    private bool isStarting = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartupError))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    private string? startupError;

    public bool HasStartupError => StartupError is not null;
    public bool IsReady => !IsStarting && !HasStartupError;

    public MainWindowViewModel(
        EquipmentViewModel equipmentViewModel,
        BorrowingsViewModel borrowingsViewModel,
        StudentsViewModel studentsViewModel,
        EquipmentManagementViewModel equipmentManagementViewModel,
        HistoryViewModel historyViewModel,
        BorrowingSearchViewModel borrowingSearchViewModel,
        IEquipmentApi api)
    {
        EquipmentViewModel = equipmentViewModel;
        BorrowingsViewModel = borrowingsViewModel;
        StudentsViewModel = studentsViewModel;
        EquipmentManagementViewModel = equipmentManagementViewModel;
        HistoryViewModel = historyViewModel;
        BorrowingSearchViewModel = borrowingSearchViewModel;
        _api = api;
        currentViewModel = equipmentViewModel;
    }

    [RelayCommand]
    private async Task ShowEquipmentAsync()
    {
        CurrentViewModel = EquipmentViewModel;
        await EquipmentViewModel.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ShowBorrowingsAsync()
    {
        CurrentViewModel = BorrowingsViewModel;
        await BorrowingsViewModel.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ShowBorrowingSearchAsync()
    {
        CurrentViewModel = BorrowingSearchViewModel;
        await BorrowingSearchViewModel.SearchCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ShowHistoryAsync()
    {
        CurrentViewModel = HistoryViewModel;
        await HistoryViewModel.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ShowStudentsAsync()
    {
        CurrentViewModel = StudentsViewModel;
        await StudentsViewModel.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ShowEquipmentManagementAsync()
    {
        CurrentViewModel = EquipmentManagementViewModel;
        await EquipmentManagementViewModel.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private Task RetryStartupAsync() => InitializeAsync();

    // Startup only checks that the API (and, through it, the database) answers. One GET, no DB code here.
    public async Task InitializeAsync()
    {
        IsStarting = true;
        StartupError = null;

        try
        {
            var probe = await _api.GetEquipmentAsync(availableOnly: false);
            if (!probe.IsSuccess)
            {
                StartupError = probe.Message; // already says "cannot reach" vs "service reported an error"
                return;
            }

            IsStarting = false;
            await ShowEquipmentAsync();
        }
        catch (Exception)
        {
            StartupError = "The application could not start because of an unexpected error.";
        }
        finally
        {
            IsStarting = false;
        }
    }
}
