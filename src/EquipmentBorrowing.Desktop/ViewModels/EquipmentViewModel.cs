using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Desktop.Models;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class EquipmentViewModel : ObservableObject
{
    private readonly IEquipmentApi _api;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<EquipmentItem> equipmentList = new();

    [ObservableProperty]
    private ObservableCollection<StudentItem> students = new();

    // Duplicates are NOT filtered here: BR-12 says the rule rejects them visibly, and the API owns that rule.
    [ObservableProperty]
    private ObservableCollection<EquipmentItem> checkoutItems = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BorrowCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddToCheckoutCommand))]
    private EquipmentItem? selectedEquipment;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BorrowCommand))]
    private StudentItem? selectedStudent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveFromCheckoutCommand))]
    private EquipmentItem? selectedCheckoutItem;

    [ObservableProperty]
    private DateTimeOffset? expectedReturnDate = DateTimeOffset.Now.AddDays(7);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyMessage))]
    private bool showAvailableOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool isLoading;

    // A failed LOAD, shown with Retry. Separate from StatusMessage so an error is never mistaken for "no equipment".
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? loadError;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isError;

    public bool HasLoadError => LoadError is not null;
    public bool IsEmpty => !IsLoading && !HasLoadError && EquipmentList.Count == 0;
    public string EmptyMessage => ShowAvailableOnly
        ? "No equipment is currently available."
        : "No equipment has been registered yet.";

    public EquipmentViewModel(IEquipmentApi api)
    {
        _api = api;
    }

    // Changing the filter re-queries the API; the filter is not applied in memory.
    partial void OnShowAvailableOnlyChanged(bool value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        // A newer load supersedes an older one; the older one must never overwrite the screen.
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();

        var studentId = SelectedStudent?.Id;
        var equipmentId = SelectedEquipment?.Id;

        IsLoading = true;
        LoadError = null;

        try
        {
            var equipment = await _api.GetEquipmentAsync(ShowAvailableOnly, cts.Token);
            var studentsResult = await _api.GetStudentsAsync(null, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            if (!equipment.IsSuccess || !studentsResult.IsSuccess)
            {
                // Never leave stale availability on screen as if it were current.
                EquipmentList = new ObservableCollection<EquipmentItem>();
                LoadError = !equipment.IsSuccess ? equipment.Message : studentsResult.Message;
                return;
            }

            EquipmentList = new ObservableCollection<EquipmentItem>(equipment.Value!.Select(e => e.ToItem()));
            Students = new ObservableCollection<StudentItem>(studentsResult.Value!.Select(s => s.ToItem()));

            SelectedEquipment = EquipmentList.FirstOrDefault(e => e.Id == equipmentId);
            SelectedStudent = Students.FirstOrDefault(s => s.Id == studentId);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer load
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
                IsLoading = false;
        }
    }

    // ---------- UC-03: borrow ONE item ----------

    private bool CanBorrow() => SelectedStudent is not null && SelectedEquipment is not null;

    [RelayCommand(CanExecute = nameof(CanBorrow))]
    private async Task BorrowAsync()
    {
        // Presentation validation only: is every input present? Business rules live in the API.
        if (SelectedStudent is null) { Show("Please select a student.", isError: true); return; }
        if (SelectedEquipment is null) { Show("Please select equipment.", isError: true); return; }
        if (ExpectedReturnDate is null) { Show("Please select a return date.", isError: true); return; }

        var expectedReturn = DateOnly.FromDateTime(ExpectedReturnDate.Value.DateTime);
        var student = SelectedStudent;
        var item = SelectedEquipment;

        var result = await _api.BorrowAsync(new BorrowRequest(student.Id, item.Id, expectedReturn));

        await ShowOutcomeAsync(
            result.IsSuccess,
            result.Failure,
            result.IsSuccess
                ? $"'{item.Name}' borrowed by {student.Name}. Due {expectedReturn:yyyy-MM-dd}."
                : result.Message,
            clearCheckout: false);
    }

    // ---------- UC-06: several items at once ----------

    private bool CanAddToCheckout() => SelectedEquipment is not null;

    [RelayCommand(CanExecute = nameof(CanAddToCheckout))]
    private void AddToCheckout()
    {
        if (SelectedEquipment is null)
            return;

        CheckoutItems.Add(SelectedEquipment);
        Show($"Added '{SelectedEquipment.Name}'. The checkout list has {CheckoutItems.Count} item(s).", isError: false);
    }

    private bool CanRemoveFromCheckout() => SelectedCheckoutItem is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveFromCheckout))]
    private void RemoveFromCheckout()
    {
        if (SelectedCheckoutItem is null)
            return;

        CheckoutItems.Remove(SelectedCheckoutItem);
    }

    [RelayCommand]
    private void ClearCheckout()
    {
        CheckoutItems.Clear();
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task CheckOutAsync()
    {
        if (SelectedStudent is null) { Show("Please select a student.", isError: true); return; }
        if (ExpectedReturnDate is null) { Show("Please select a return date.", isError: true); return; }
        if (CheckoutItems.Count == 0) { Show("Add items to the checkout list first.", isError: true); return; }

        var expectedReturn = DateOnly.FromDateTime(ExpectedReturnDate.Value.DateTime);
        var equipmentIds = CheckoutItems.Select(item => item.Id).ToList();
        var studentName = SelectedStudent.Name;

        var result = await _api.CheckoutAsync(new CheckoutRequest(SelectedStudent.Id, equipmentIds, expectedReturn));

        await ShowOutcomeAsync(
            result.IsSuccess,
            result.Failure,
            result.IsSuccess
                ? $"{result.Value!.Borrowings.Count} item(s) checked out for {studentName}. Due {expectedReturn:yyyy-MM-dd}."
                : result.Message,
            clearCheckout: true);
    }

    // One place decides what each outcome means for the screen.
    private async Task ShowOutcomeAsync(bool success, ApiFailure failure, string message, bool clearCheckout)
    {
        if (success)
        {
            if (clearCheckout)
                CheckoutItems.Clear();

            await LoadAsync(); // refresh so availability updates on screen
            Show(message, isError: false);
            return;
        }

        // A plain input mistake needs no refresh. Everything else (rule rejection, stale data, conflict,
        // server failure, unreachable, unknown outcome) means the screen may be out of date, so refresh
        // first (BR-18), then show the reason. Nothing is retried automatically.
        // The checkout list is kept so staff can adjust it.
        if (failure != ApiFailure.Validation)
            await LoadAsync();

        Show(message, isError: true);
    }

    private void Show(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }
}
