using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EquipmentViewModel EquipmentViewModel { get; }
    public BorrowingsViewModel BorrowingsViewModel { get; }
    public StudentsViewModel StudentsViewModel { get; }

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
        IServiceScopeFactory scopeFactory)
    {
        EquipmentViewModel = equipmentViewModel;
        BorrowingsViewModel = borrowingsViewModel;
        StudentsViewModel = studentsViewModel;
        _scopeFactory = scopeFactory;
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
    private async Task ShowStudentsAsync()
    {
        CurrentViewModel = StudentsViewModel;
        await StudentsViewModel.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private Task RetryStartupAsync() => InitializeAsync();

    public async Task InitializeAsync()
    {
        IsStarting = true;
        StartupError = null;

        try
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await initializer.InitializeAsync();
            }

            IsStarting = false;
            await ShowEquipmentAsync();
        }
        catch (DatabaseAccessException ex)
        {
            StartupError = ex.Message; // safe by contract: no credentials (BR-18)
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