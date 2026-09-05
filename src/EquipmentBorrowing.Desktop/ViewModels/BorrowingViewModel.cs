using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Models;
using System.Collections.ObjectModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class BorrowingsViewModel : ObservableObject
{
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly ReturnEquipmentService _returnEquipmentService;

    [ObservableProperty]
    private ObservableCollection<BorrowingListItem> activeBorrowings = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReturnCommand))]
    private BorrowingListItem? selectedBorrowing;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isError;

    public BorrowingsViewModel(
        IBorrowingRepository borrowingRepository,
        IStudentRepository studentRepository,
        IEquipmentRepository equipmentRepository,
        ReturnEquipmentService returnEquipmentService)
    {
        _borrowingRepository = borrowingRepository;
        _studentRepository = studentRepository;
        _equipmentRepository = equipmentRepository;
        _returnEquipmentService = returnEquipmentService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var active = await _borrowingRepository.GetActiveAsync();
        var items = new List<BorrowingListItem>();

        foreach (var borrowing in active)
        {
            var student = await _studentRepository.GetByIdAsync(borrowing.StudentId);
            var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId);

            items.Add(new BorrowingListItem
            {
                Id = borrowing.Id,
                StudentName = student?.Name ?? "Unknown student",
                EquipmentName = equipment?.Name ?? "Unknown equipment",
                DateBorrowed = borrowing.DateBorrowed,
                ExpectedReturnDate = borrowing.ExpectedReturnDate
            });
        }

        ActiveBorrowings = new ObservableCollection<BorrowingListItem>(items);
    }

    private bool CanReturn() => SelectedBorrowing is not null;

    [RelayCommand(CanExecute = nameof(CanReturn))]
    private async Task ReturnAsync()
    {
        if (SelectedBorrowing is null)
        {
            StatusMessage = "Please select a borrowing to return.";
            IsError = true;
            return;
        }

        var result = await _returnEquipmentService.ExecuteAsync(SelectedBorrowing.Id);

        if (result.IsSuccess)
        {
            StatusMessage = $"Returned '{SelectedBorrowing.EquipmentName}' successfully.";
            IsError = false;
            await LoadAsync();
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "Return failed.";
            IsError = true;
        }
    }
}