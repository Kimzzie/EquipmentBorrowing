using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Models;
using Microsoft.Extensions.DependencyInjection;

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
        try
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
        catch (DatabaseAccessException ex)
        {
            ActiveBorrowings = new ObservableCollection<BorrowingListItem>();
            StatusMessage = ex.Message;
            IsError = true;
        }
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
        var service = scope.ServiceProvider.GetRequiredService<ReturnEquipmentService>();
        var result = await service.ExecuteAsync(SelectedBorrowing.Id);

        // Success or failure, reload so the list shows the real state (a stale or repeated
        // return disappears from the list), then show the outcome.
        await LoadAsync();
        StatusMessage = result.IsSuccess ? result.Message : result.Message;
        IsError = !result.IsSuccess;
    }
}