using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class BorrowingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private ObservableCollection<BorrowingListItem> activeBorrowings = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReturnCommand))]
    private BorrowingListItem? selectedBorrowing;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isError;

    public BorrowingsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var borrowingRepository = scope.ServiceProvider.GetRequiredService<IBorrowingRepository>();

        var active = await borrowingRepository.GetActiveWithDetailsAsync();

        var items = active.Select(b => new BorrowingListItem
        {
            Id = b.Id,
            StudentName = b.StudentName,
            EquipmentName = b.EquipmentName,
            DateBorrowed = b.DateBorrowed,
            ExpectedReturnDate = b.ExpectedReturnDate
        });

        ActiveBorrowings = new ObservableCollection<BorrowingListItem>(items);
    }

    private bool CanReturn() => SelectedBorrowing is not null;

    [RelayCommand(CanExecute = nameof(CanReturn))]
    private async Task ReturnAsync()
    {
        if (SelectedBorrowing is null)
        {
            StatusMessage = "Please select a borrowing to return.";
            IsError = true;
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var returnEquipmentService = scope.ServiceProvider.GetRequiredService<ReturnEquipmentService>();

        var result = await returnEquipmentService.ExecuteAsync(SelectedBorrowing.Id);

        if (result.IsSuccess)
        {
            StatusMessage = $"Returned '{SelectedBorrowing.EquipmentName}' successfully.";
            IsError = false;
            await LoadAsync();
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "Return failed.";
            IsError = true;
        }
    }
}
