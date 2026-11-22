using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Desktop.Models;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class EquipmentManagementViewModel : ObservableObject
{
    private readonly IEquipmentApi _api;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private ObservableCollection<EquipmentItem> equipmentItems = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    [NotifyPropertyChangedFor(nameof(AvailabilityText))]
    private EquipmentItem? selectedEquipment;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool availableOnly;

    // Form fields. They hold what staff typed; the API validates them.
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

    // "No results" and "the service failed" must look different (BR-19).
    public bool ShowEmptyState => !IsBusy && !LoadFailed && EquipmentItems.Count == 0;

    public EquipmentManagementViewModel(IEquipmentApi api)
    {
        _api = api;
    }

    // Selecting an item copies its values into the form; selecting nothing clears it.
    partial void OnSelectedEquipmentChanged(EquipmentItem? value)
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
            var request = new EquipmentRequest(EquipmentCode, ItemName, Description);
            var editingId = SelectedEquipment?.Id;

            var result = editingId is null
                ? await _api.RegisterEquipmentAsync(request)
                : await _api.UpdateEquipmentAsync(editingId.Value, request);

            await ShowResultAsync(result, editingId, editingId is null ? "Equipment registered." : "Equipment updated.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // One place decides what each outcome means for the screen.
    private async Task ShowResultAsync(ApiResult<EquipmentDto> result, int? currentEquipmentId, string successMessage)
    {
        if (result.IsSuccess)
        {
            await ReloadAsync(result.Value!.Id);
            Show(successMessage, isError: false);
            return;
        }

        switch (result.Failure)
        {
            case ApiFailure.Validation:
            case ApiFailure.Duplicate:
                // Keep the form exactly as typed so staff can correct it.
                Show(result.Message, isError: true);
                break;

            default:
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
            var result = await _api.GetEquipmentAsync(AvailableOnly);

            if (!result.IsSuccess)
            {
                LoadFailed = true;
                EquipmentItems = new ObservableCollection<EquipmentItem>();
                Show(result.Message, isError: true);
                return;
            }

            // The API (API-01) filters by availability only, so the text search is applied here, on identifier or name.
            var text = SearchText.Trim();
            var items = result.Value!
                .Select(e => e.ToItem())
                .Where(e => text.Length == 0
                    || e.EquipmentCode.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || e.Name.Contains(text, StringComparison.OrdinalIgnoreCase));

            EquipmentItems = new ObservableCollection<EquipmentItem>(items);
            LoadFailed = false;

            // Select AFTER replacing the list. If the item is gone (or filtered out), this selects nothing.
            SelectedEquipment = selectId is null ? null : EquipmentItems.FirstOrDefault(e => e.Id == selectId);
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
