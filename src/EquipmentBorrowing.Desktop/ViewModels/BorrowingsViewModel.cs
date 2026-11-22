using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Desktop.Models;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class BorrowingsViewModel : ObservableObject
{
    private readonly IEquipmentApi _api;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<BorrowingListItem> activeBorrowings = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReturnCommand))]
    private BorrowingListItem? selectedBorrowing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool isLoading;

    // A failed LOAD (shown with Retry). Kept apart from StatusMessage, which reports return outcomes.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? loadError;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isError;

    public bool HasLoadError => LoadError is not null;

    // Empty means: a load finished successfully and found nothing. Not loading, not failed.
    public bool IsEmpty => !IsLoading && !HasLoadError && ActiveBorrowings.Count == 0;

    public BorrowingsViewModel(IEquipmentApi api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadError = null;

        try
        {
            var result = await _api.SearchBorrowingsAsync(
                search: null, status: "Active", sort: "borrowedDate", direction: "asc");

            if (result.IsSuccess)
            {
                var items = result.Value!.Select(b => new BorrowingListItem
                {
                    Id = b.Id,
                    StudentName = b.StudentName,
                    EquipmentName = b.EquipmentName,
                    DateBorrowed = b.BorrowedDate,
                    ExpectedReturnDate = b.ExpectedReturnDate
                });

                ActiveBorrowings = new ObservableCollection<BorrowingListItem>(items);
            }
            else
            {
                // Never show stale rows as if they were current.
                ActiveBorrowings = new ObservableCollection<BorrowingListItem>();
                LoadError = result.Message;
            }
        }
        finally
        {
            IsLoading = false;
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

        var item = SelectedBorrowing;
        var result = await _api.ReturnAsync(item.Id);

        // Reload first so the list shows the real state (a repeated or stale return disappears),
        // then report the outcome.
        await LoadAsync();
        StatusMessage = result.IsSuccess ? $"'{item.EquipmentName}' returned by {item.StudentName}." : result.Message;
        IsError = !result.IsSuccess;
    }
}
