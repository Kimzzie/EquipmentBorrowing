using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public HistoryPanelViewModel StudentPanel { get; }
    public HistoryPanelViewModel EquipmentPanel { get; }

    [ObservableProperty]
    private ObservableCollection<Student> students = new();

    [ObservableProperty]
    private ObservableCollection<Equipment> equipmentItems = new();

    [ObservableProperty]
    private Student? selectedStudent;

    [ObservableProperty]
    private Equipment? selectedEquipmentItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasListError))]
    private string? listError;

    public bool HasListError => ListError is not null;

    public HistoryViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;

        StudentPanel = new HistoryPanelViewModel(
            LoadStudentHistoryAsync, "Select a student to see their borrowing history.");
        EquipmentPanel = new HistoryPanelViewModel(
            LoadEquipmentHistoryAsync, "Select an item to see its borrowing history.");
    }

    // Selecting someone starts the (cancellable) async load. The panel handles all states.
    partial void OnSelectedStudentChanged(Student? value) => _ = StudentPanel.LoadAsync(value?.Id);

    partial void OnSelectedEquipmentItemChanged(Equipment? value) => _ = EquipmentPanel.LoadAsync(value?.Id);

    [RelayCommand]
    private async Task LoadAsync()
    {
        var studentId = SelectedStudent?.Id;
        var equipmentId = SelectedEquipmentItem?.Id;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var findStudents = scope.ServiceProvider.GetRequiredService<FindStudentsService>();
            var findEquipment = scope.ServiceProvider.GetRequiredService<FindEquipmentService>();

            var studentsFound = await findStudents.ExecuteAsync(searchText: null);
            var equipmentFound = await findEquipment.ExecuteAsync(searchText: null, availableOnly: false);

            ListError = null;
            Students = new ObservableCollection<Student>(studentsFound);
            EquipmentItems = new ObservableCollection<Equipment>(equipmentFound);

            // Restore by Id. A selection that no longer exists simply clears (stale selection).
            SelectedStudent = Students.FirstOrDefault(s => s.Id == studentId);
            SelectedEquipmentItem = EquipmentItems.FirstOrDefault(e => e.Id == equipmentId);
        }
        catch (DatabaseAccessException ex)
        {
            ListError = ex.Message;
        }
    }

    private async Task<OperationResult<BorrowingHistory>> LoadStudentHistoryAsync(int id, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ViewStudentHistoryService>();
        return await service.ExecuteAsync(id, ct);
    }

    private async Task<OperationResult<BorrowingHistory>> LoadEquipmentHistoryAsync(int id, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ViewEquipmentHistoryService>();
        return await service.ExecuteAsync(id, ct);
    }
}