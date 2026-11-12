using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BorrowCommand))]
    private Equipment? selectedEquipment;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BorrowCommand))]
    private Student? selectedStudent;

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
        // A fresh scope means a fresh DbContext, so this always sees the latest committed data.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var equipmentRepository = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();

        var equipment = await equipmentRepository.GetAllAsync();
        EquipmentList = new ObservableCollection<Equipment>(equipment);

        var students = await studentRepository.GetAllAsync();
        Students = new ObservableCollection<Student>(students);
    }

    private bool CanBorrow() => SelectedStudent is not null && SelectedEquipment is not null;

    [RelayCommand(CanExecute = nameof(CanBorrow))]
    private async Task BorrowAsync()
    {
        // Presentation validation only: is every input present?
        if (SelectedStudent is null)
        {
            SetStatus("Please select a student.", isError: true);
            return;
        }

        if (SelectedEquipment is null)
        {
            SetStatus("Please select equipment.", isError: true);
            return;
        }

        if (ExpectedReturnDate is null)
        {
            SetStatus("Please select a return date.", isError: true);
            return;
        }

        // The date RULE (must be later than today) lives in BorrowEquipmentService, not here.
        var expectedReturn = DateOnly.FromDateTime(ExpectedReturnDate.Value.DateTime);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var borrowEquipmentService = scope.ServiceProvider.GetRequiredService<BorrowEquipmentService>();

        var result = await borrowEquipmentService.ExecuteAsync(
            SelectedStudent.Id,
            SelectedEquipment.Id,
            expectedReturn);

        if (result.IsSuccess)
        {
            SetStatus($"Borrowed '{SelectedEquipment.Name}' successfully.", isError: false);
            await LoadAsync(); // refresh so availability updates on screen
        }
        else
        {
            SetStatus(result.ErrorMessage ?? "Borrowing failed.", isError: true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }
}