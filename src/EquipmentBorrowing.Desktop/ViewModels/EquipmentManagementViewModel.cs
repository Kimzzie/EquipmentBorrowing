using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class EquipmentManagementViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private ObservableCollection<Equipment> equipmentItems = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    [NotifyPropertyChangedFor(nameof(AvailabilityText))]
    private Equipment? selectedEquipment;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool availableOnly;

    // Form fields. They hold what staff typed; the service validates them.
    [ObservableProperty] private string equipmentCode = string.Empty;
    [ObservableProperty] private string itemName = string.Empty;
    [ObservableProperty] private string description = string.Empty;

    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool loadFailed;

    public bool IsEditing => SelectedEquipment is not null;
    public string FormTitle => IsEditing ? "Edit equipment" : "Register new equipment";
    public string SaveButtonText => IsEditing ? "Save changes" : "Register equipment";

    // Read-only on purpose (BR-07): availability changes only by borrowing and returning.
    public string AvailabilityText => SelectedEquipment is null
        ? string.Empty
        : SelectedEquipment.IsAvailable
            ? "Available"
            : "Not available (currently issued)";

    // "No results" and "the database failed" must look different (BR-19).
    public bool ShowEmptyState => !IsBusy && !LoadFailed && EquipmentItems.Count == 0;

    public EquipmentManagementViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // Selecting an item copies its values into the form; selecting nothing clears it.
    partial void OnSelectedEquipmentChanged(Equipment? value)
    {
        EquipmentCode = value?.EquipmentCode ?? string.Empty;
        ItemName = value?.Name ?? string.Empty;
        Description = value?.Description ?? string.Empty;
    }

    // Ticking or unticking the filter refreshes the list right away.
    partial void OnAvailableOnlyChanged(bool value) => LoadCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = null;
        await ReloadAsync(SelectedEquipment?.Id);
    }

    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        AvailableOnly = false; // its change handler reloads; the explicit load below is harmless
        await LoadAsync();
    }

    [RelayCommand]
    private void NewEquipment()
    {
        SelectedEquipment = null;
        EquipmentCode = string.Empty;
        ItemName = string.Empty;
        Description = string.Empty;
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            OperationResult<Equipment> result;
            if (SelectedEquipment is null)
            {
                var register = scope.ServiceProvider.GetRequiredService<RegisterEquipmentService>();
                result = await register.ExecuteAsync(EquipmentCode, ItemName, Description);
            }
            else
            {
                var update = scope.ServiceProvider.GetRequiredService<UpdateEquipmentService>();
                result = await update.ExecuteAsync(SelectedEquipment.Id, EquipmentCode, ItemName, Description);
            }

            await ShowResultAsync(result, SelectedEquipment?.Id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // One place decides what each outcome means for the screen.
    private async Task ShowResultAsync(OperationResult<Equipment> result, int? currentEquipmentId)
    {
        if (result.IsSuccess)
        {
            // Reload from the database and select the saved item, so the screen
            // shows what is actually stored.
            await ReloadAsync(result.Value!.Id);
            Show(result.Message, isError: false);
            return;
        }

        switch (result.Failure)
        {
            case OperationFailure.Validation:
            case OperationFailure.Duplicate:
                // Keep the form exactly as typed so staff can correct it.
                Show(result.Message, isError: true);
                break;

            default:
                // NotFound, Conflict, DatabaseError, UnknownOutcome: the screen may be out
                // of date, so refresh first, then show the reason. Nothing is retried automatically.
                await ReloadAsync(currentEquipmentId);
                Show(result.Message, isError: true);
                break;
        }
    }

    private async Task ReloadAsync(int? selectId)
    {
        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var find = scope.ServiceProvider.GetRequiredService<FindEquipmentService>();
            var found = await find.ExecuteAsync(SearchText, AvailableOnly);

            EquipmentItems = new ObservableCollection<Equipment>(found);
            LoadFailed = false;

            // Select AFTER replacing the list. If the item is gone (or filtered out),
            // this selects nothing and the form returns to "register" mode.
            SelectedEquipment = selectId is null ? null : EquipmentItems.FirstOrDefault(e => e.Id == selectId);
        }
        catch (DatabaseAccessException ex)
        {
            LoadFailed = true;
            EquipmentItems = new ObservableCollection<Equipment>();
            Show(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Show(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }
}