using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Desktop.Models;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IEquipmentApi _api;

    public HistoryPanelViewModel StudentPanel { get; }
    public HistoryPanelViewModel EquipmentPanel { get; }

    [ObservableProperty]
    private ObservableCollection<StudentItem> students = new();

    [ObservableProperty]
    private ObservableCollection<EquipmentItem> equipmentItems = new();

    [ObservableProperty]
    private StudentItem? selectedStudent;

    [ObservableProperty]
    private EquipmentItem? selectedEquipmentItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasListError))]
    private string? listError;

    public bool HasListError => ListError is not null;

    public HistoryViewModel(IEquipmentApi api)
    {
        _api = api;

        StudentPanel = new HistoryPanelViewModel(
            LoadStudentHistoryAsync, "Select a student to see their borrowing history.");
        EquipmentPanel = new HistoryPanelViewModel(
            LoadEquipmentHistoryAsync, "Select an item to see its borrowing history.");
    }

    // Selecting someone starts the (cancellable) async load. The panel handles all states.
    partial void OnSelectedStudentChanged(StudentItem? value) => _ = StudentPanel.LoadAsync(value?.Id);

    partial void OnSelectedEquipmentItemChanged(EquipmentItem? value) => _ = EquipmentPanel.LoadAsync(value?.Id);

    [RelayCommand]
    private async Task LoadAsync()
    {
        var studentId = SelectedStudent?.Id;
        var equipmentId = SelectedEquipmentItem?.Id;

        var studentsFound = await _api.GetStudentsAsync(null);
        var equipmentFound = await _api.GetEquipmentAsync(availableOnly: false);

        if (!studentsFound.IsSuccess || !equipmentFound.IsSuccess)
        {
            ListError = !studentsFound.IsSuccess ? studentsFound.Message : equipmentFound.Message;
            return;
        }

        ListError = null;
        Students = new ObservableCollection<StudentItem>(studentsFound.Value!.Select(s => s.ToItem()));
        EquipmentItems = new ObservableCollection<EquipmentItem>(equipmentFound.Value!.Select(e => e.ToItem()));

        // Restore by Id. A selection that no longer exists simply clears (stale selection).
        SelectedStudent = Students.FirstOrDefault(s => s.Id == studentId);
        SelectedEquipmentItem = EquipmentItems.FirstOrDefault(e => e.Id == equipmentId);
    }

    private async Task<ApiResult<HistoryData>> LoadStudentHistoryAsync(int id, CancellationToken ct)
    {
        var result = await _api.GetStudentBorrowingsAsync(id, ct);
        if (!result.IsSuccess)
            return ApiResult<HistoryData>.Fail(result.Failure, result.Code, result.Message);

        var student = Students.FirstOrDefault(s => s.Id == id);
        return ApiResult<HistoryData>.Ok(new HistoryData(
            student?.Name ?? $"Student #{id}",
            $"{student?.StudentNumber} · {result.Value!.Count} record(s)",
            result.Value!));
    }

    private async Task<ApiResult<HistoryData>> LoadEquipmentHistoryAsync(int id, CancellationToken ct)
    {
        var result = await _api.GetEquipmentBorrowingsAsync(id, ct);
        if (!result.IsSuccess)
            return ApiResult<HistoryData>.Fail(result.Failure, result.Code, result.Message);

        var item = EquipmentItems.FirstOrDefault(e => e.Id == id);
        return ApiResult<HistoryData>.Ok(new HistoryData(
            item?.Name ?? $"Equipment #{id}",
            $"{item?.EquipmentCode} · {result.Value!.Count} record(s)",
            result.Value!));
    }
}
