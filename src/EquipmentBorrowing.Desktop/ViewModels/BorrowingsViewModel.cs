using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Models;
using System.Collections.ObjectModel;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class BorrowingsViewModel : ObservableObject
{
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly ReturnEquipmentService _returnEquipmentService;

    [ObservableProperty]
    private ObservableCollection<BorrowingListItem> activeBorrowings = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReturnCommand))]
    private BorrowingListItem? selectedBorrowing;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isError;

    public BorrowingsViewModel(
        IBorrowingRepository borrowingRepository,
        ReturnEquipmentService returnEquipmentService)
    {
        _borrowingRepository = borrowingRepository;
        _returnEquipmentService = returnEquipmentService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var active = await _borrowingRepository.GetActiveWithDetailsAsync();

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

        var result = await _returnEquipmentService.ExecuteAsync(SelectedBorrowing.Id);

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