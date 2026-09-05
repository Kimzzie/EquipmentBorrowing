using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class EquipmentViewModel : ObservableObject
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly BorrowEquipmentService _borrowEquipmentService;

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

    public EquipmentViewModel(
        IEquipmentRepository equipmentRepository,
        IStudentRepository studentRepository,
        BorrowEquipmentService borrowEquipmentService)
    {
        _equipmentRepository = equipmentRepository;
        _studentRepository = studentRepository;
        _borrowEquipmentService = borrowEquipmentService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var equipment = await _equipmentRepository.GetAllAsync();
        EquipmentList = new ObservableCollection<Equipment>(equipment);

        var students = await _studentRepository.GetAllAsync();
        Students = new ObservableCollection<Student>(students);
    }

    private bool CanBorrow() => SelectedStudent is not null && SelectedEquipment is not null;

    [RelayCommand(CanExecute = nameof(CanBorrow))]
    private async Task BorrowAsync()
    {
        // ---- Presentation validation (belongs here, in the ViewModel) ----
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

        if (ExpectedReturnDate is null || ExpectedReturnDate.Value.Date < DateTimeOffset.Now.Date)
        {
            SetStatus("Please select a valid return date that is not in the past.", isError: true);
            return;
        }

        // ---- Business validation happens inside BorrowEquipmentService, not here ----
        var result = await _borrowEquipmentService.ExecuteAsync(
            SelectedStudent.Id,
            SelectedEquipment.Id,
            ExpectedReturnDate.Value.DateTime);

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