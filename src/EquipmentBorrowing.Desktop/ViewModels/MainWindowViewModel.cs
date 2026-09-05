using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public EquipmentViewModel EquipmentViewModel { get; }
    public BorrowingsViewModel BorrowingsViewModel { get; }

    [ObservableProperty]
    private ObservableObject currentViewModel;

    public MainWindowViewModel(
        EquipmentViewModel equipmentViewModel,
        BorrowingsViewModel borrowingsViewModel)
    {
        EquipmentViewModel = equipmentViewModel;
        BorrowingsViewModel = borrowingsViewModel;
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

    public async Task InitializeAsync() => await ShowEquipmentAsync();
}