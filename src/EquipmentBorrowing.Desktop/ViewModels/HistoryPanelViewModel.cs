using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Models;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class HistoryPanelViewModel : ObservableObject
{
    private readonly Func<int, CancellationToken, Task<OperationResult<BorrowingHistory>>> _loader;
    private CancellationTokenSource? _cts;
    private int? _currentId;

    public string Prompt { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowPrompt))]
    private bool hasSelection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<BorrowingRow> rows = new();

    [ObservableProperty]
    private string title = "";

    [ObservableProperty]
    private string subtitle = "";

    public bool HasError => ErrorMessage is not null;
    public bool HasRows => Rows.Count > 0;
    public bool ShowPrompt => !HasSelection;

    // "Empty" is only true for a finished, successful load with zero records.
    // Errors and loading are separate states, so they are never mistaken for "no history".
    public bool IsEmpty => HasSelection && !IsLoading && !HasError && Rows.Count == 0;

    public HistoryPanelViewModel(
        Func<int, CancellationToken, Task<OperationResult<BorrowingHistory>>> loader,
        string prompt)
    {
        _loader = loader;
        Prompt = prompt;
    }

    public async Task LoadAsync(int? id)
    {
        // A newer selection cancels the older request; the older one must never show its result.
        _cts?.Cancel();
        _currentId = id;

        if (id is null)
        {
            HasSelection = false;
            IsLoading = false;
            ErrorMessage = null;
            Rows = new ObservableCollection<BorrowingRow>();
            Title = "";
            Subtitle = "";
            return;
        }

        var cts = _cts = new CancellationTokenSource();

        // Clear immediately: the previous person's/item's rows disappear the moment the selection changes.
        HasSelection = true;
        IsLoading = true;
        ErrorMessage = null;
        Rows = new ObservableCollection<BorrowingRow>();

        try
        {
            var result = await _loader(id.Value, cts.Token);
            if (cts.IsCancellationRequested)
                return; // a newer request took over

            if (result.IsSuccess)
            {
                Title = result.Value!.Title;
                Subtitle = result.Value.Subtitle;
                Rows = new ObservableCollection<BorrowingRow>(result.Value.Records.Select(BorrowingRow.From));
            }
            else
            {
                Title = "";
                Subtitle = "";
                ErrorMessage = result.Message;
            }
        }
        catch (OperationCanceledException)
        {
            // superseded or cancelled: nothing to show
        }
        catch (Exception)
        {
            if (!cts.IsCancellationRequested)
                ErrorMessage = "An unexpected error occurred while loading the history.";
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                IsLoading = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync(_currentId);
}