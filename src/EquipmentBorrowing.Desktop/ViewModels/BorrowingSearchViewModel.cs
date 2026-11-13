using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Models;
using EquipmentBorrowing.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop.ViewModels;

public sealed record StatusOption(string Label, BorrowingStatus? Value)
{
    public override string ToString() => Label;
}

public sealed record SortOption(string Label, BorrowingSortField Field, bool Descending)
{
    public override string ToString() => Label;
}

public partial class BorrowingSearchViewModel : ObservableObject
{
    private static readonly StatusOption[] StatusChoices =
    [
        new("All statuses", null),
        new("Active", BorrowingStatus.Active),
        new("Returned", BorrowingStatus.Returned)
    ];

    private static readonly SortOption[] SortChoices =
    [
        new("Checkout date: newest first", BorrowingSortField.DateBorrowed, true),
        new("Checkout date: oldest first", BorrowingSortField.DateBorrowed, false),
        new("Expected return: latest first", BorrowingSortField.ExpectedReturnDate, true),
        new("Expected return: earliest first", BorrowingSortField.ExpectedReturnDate, false)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private CancellationTokenSource? _cts;

    public IReadOnlyList<StatusOption> StatusOptions => StatusChoices;
    public IReadOnlyList<SortOption> SortOptions => SortChoices;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private StatusOption selectedStatus = StatusChoices[0];

    [ObservableProperty]
    private SortOption selectedSort = SortChoices[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(ResultSummary))]
    private ObservableCollection<BorrowingRow> rows = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasSearched;

    public bool HasRows => Rows.Count > 0;
    public bool HasError => ErrorMessage is not null;
    public bool IsEmpty => HasSearched && !IsLoading && !HasError && Rows.Count == 0;
    public string ResultSummary => $"{Rows.Count} record(s) found";

    public BorrowingSearchViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // Runs when the screen opens, and when Search or Retry is pressed.
    [RelayCommand]
    private async Task SearchAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();

        IsLoading = true;
        ErrorMessage = null;

        var query = new BorrowingQuery(SearchText, SelectedStatus.Value, SelectedSort.Field, SelectedSort.Descending);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<SearchBorrowingsService>();
            var result = await service.ExecuteAsync(query, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            HasSearched = true;
            if (result.IsSuccess)
            {
                Rows = new ObservableCollection<BorrowingRow>(result.Value!.Select(BorrowingRow.From));
            }
            else
            {
                // An error is NOT an empty result: clear the rows and show the message instead.
                Rows = new ObservableCollection<BorrowingRow>();
                ErrorMessage = result.Message;
            }
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer search
        }
        catch (Exception)
        {
            if (!cts.IsCancellationRequested)
            {
                HasSearched = true;
                Rows = new ObservableCollection<BorrowingRow>();
                ErrorMessage = "An unexpected error occurred while searching.";
            }
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                IsLoading = false;
        }
    }

    // 4a: Clear restores the full list.
    [RelayCommand]
    private Task ClearAsync()
    {
        SearchText = "";
        SelectedStatus = StatusChoices[0];
        SelectedSort = SortChoices[0];
        return SearchAsync();
    }
}