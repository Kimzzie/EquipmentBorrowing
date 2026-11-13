using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class EquipmentViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private ObservableCollection<Equipment> equipmentList = new();

    [ObservableProperty]
    private ObservableCollection<Student> students = new();

    // The items staff have added for a multi-item checkout (UC-06).
    // Duplicates are NOT filtered here on purpose: BR-12 says the rule rejects them
    // visibly instead of silently ignoring them, and the service owns that rule.
    [ObservableProperty]
    private ObservableCollection<Equipment> checkoutItems = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BorrowCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddToCheckoutCommand))]
    private Equipment? selectedEquipment;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BorrowCommand))]
    private Student? selectedStudent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveFromCheckoutCommand))]
    private Equipment? selectedCheckoutItem;

    [ObservableProperty]
    private DateTimeOffset? expectedReturnDate = DateTimeOffset.Now.AddDays(7);

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isError;

    public EquipmentViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Remember the selections, so a refresh does not make staff pick everything again.
        var studentId = SelectedStudent?.Id;
        var equipmentId = SelectedEquipment?.Id;

        try
        {
            // A fresh scope means a fresh DbContext, so this always sees the latest committed data.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var findEquipment = scope.ServiceProvider.GetRequiredService<FindEquipmentService>();
            var findStudents = scope.ServiceProvider.GetRequiredService<FindStudentsService>();

            var equipmentFound = await findEquipment.ExecuteAsync(searchText: null, availableOnly: false);
            var studentsFound = await findStudents.ExecuteAsync(searchText: null);

            EquipmentList = new ObservableCollection<Equipment>(equipmentFound);
            Students = new ObservableCollection<Student>(studentsFound);

            SelectedEquipment = EquipmentList.FirstOrDefault(e => e.Id == equipmentId);
            SelectedStudent = Students.FirstOrDefault(s => s.Id == studentId);
        }
        catch (DatabaseAccessException ex)
        {
            Show(ex.Message, isError: true);
        }
    }

    // ---------- UC-03: borrow ONE item ----------

    private bool CanBorrow() => SelectedStudent is not null && SelectedEquipment is not null;

    [RelayCommand(CanExecute = nameof(CanBorrow))]
    private async Task BorrowAsync()
    {
        // Presentation validation only: is every input present?
        if (SelectedStudent is null)
        {
            Show("Please select a student.", isError: true);
            return;
        }

        if (SelectedEquipment is null)
        {
            Show("Please select equipment.", isError: true);
            return;
        }

        if (ExpectedReturnDate is null)
        {
            Show("Please select a return date.", isError: true);
            return;
        }

        var expectedReturn = DateOnly.FromDateTime(ExpectedReturnDate.Value.DateTime);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<BorrowEquipmentService>();
        var result = await service.ExecuteAsync(SelectedStudent.Id, SelectedEquipment.Id, expectedReturn);

        await ShowOutcomeAsync(result.IsSuccess, result.Failure, result.Message, clearCheckout: false);
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
        if (SelectedStudent is null)
        {
            Show("Please select a student.", isError: true);
            return;
        }

        if (ExpectedReturnDate is null)
        {
            Show("Please select a return date.", isError: true);
            return;
        }

        if (CheckoutItems.Count == 0)
        {
            Show("Add items to the checkout list first.", isError: true);
            return;
        }

        var expectedReturn = DateOnly.FromDateTime(ExpectedReturnDate.Value.DateTime);
        var equipmentIds = CheckoutItems.Select(item => item.Id).ToList();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CheckOutMultipleEquipmentService>();
        var result = await service.ExecuteAsync(SelectedStudent.Id, equipmentIds, expectedReturn);

        await ShowOutcomeAsync(result.IsSuccess, result.Failure, result.Message, clearCheckout: true);
    }

    // One place decides what each outcome means for the screen.
    private async Task ShowOutcomeAsync(bool success, OperationFailure failure, string message, bool clearCheckout)
    {
        if (success)
        {
            if (clearCheckout)
                CheckoutItems.Clear();

            await LoadAsync(); // refresh so availability updates on screen
            Show(message, isError: false);
            return;
        }

        // A plain input mistake needs no refresh. Everything else (rule rejection, stale
        // data, conflict, database failure, unknown outcome) means the screen may be out of
        // date, so refresh first (BR-18: reconnect and refresh before a staff decision),
        // then show the reason. Nothing is retried automatically. The checkout list is kept
        // so staff can adjust it.
        if (failure != OperationFailure.Validation)
            await LoadAsync();

        Show(message, isError: true);
    }

    private void Show(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }
}