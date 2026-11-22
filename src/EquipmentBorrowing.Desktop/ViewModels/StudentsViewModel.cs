using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Desktop.Models;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class StudentsViewModel : ObservableObject
{
    private readonly IEquipmentApi _api;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private ObservableCollection<StudentItem> students = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    [NotifyCanExecuteChangedFor(nameof(PermitBorrowingCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestrictBorrowingCommand))]
    private StudentItem? selectedStudent;

    [ObservableProperty] private string searchText = string.Empty;

    // Form fields. They hold what staff typed; the API validates them.
    [ObservableProperty] private string studentNumber = string.Empty;
    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string contact = string.Empty;

    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool loadFailed;

    public bool IsEditing => SelectedStudent is not null;
    public string FormTitle => IsEditing ? "Edit student" : "Register new student";
    public string SaveButtonText => IsEditing ? "Save changes" : "Register student";

    // "No results" and "the service failed" must look different (BR-19).
    public bool ShowEmptyState => !IsBusy && !LoadFailed && Students.Count == 0;

    public StudentsViewModel(IEquipmentApi api)
    {
        _api = api;
    }

    // Selecting a student copies his/her values into the form; selecting nothing clears it.
    partial void OnSelectedStudentChanged(StudentItem? value)
    {
        StudentNumber = value?.StudentNumber ?? string.Empty;
        FullName = value?.Name ?? string.Empty;
        Contact = value?.Contact ?? string.Empty;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = null;
        await ReloadAsync(SelectedStudent?.Id);
    }

    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private void NewStudent()
    {
        SelectedStudent = null;
        StudentNumber = string.Empty;
        FullName = string.Empty;
        Contact = string.Empty;
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var request = new StudentRequest(StudentNumber, FullName, Contact);
            var editingId = SelectedStudent?.Id;

            var result = editingId is null
                ? await _api.RegisterStudentAsync(request)
                : await _api.UpdateStudentAsync(editingId.Value, request);

            await ShowResultAsync(result, editingId, editingId is null ? "Student registered." : "Student updated.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanChangeEligibility() => SelectedStudent is not null;

    [RelayCommand(CanExecute = nameof(CanChangeEligibility))]
    private Task PermitBorrowingAsync() => ChangeEligibilityAsync(allowBorrowing: true);

    [RelayCommand(CanExecute = nameof(CanChangeEligibility))]
    private Task RestrictBorrowingAsync() => ChangeEligibilityAsync(allowBorrowing: false);

    private async Task ChangeEligibilityAsync(bool allowBorrowing)
    {
        if (SelectedStudent is null)
            return;

        var studentId = SelectedStudent.Id;

        IsBusy = true;
        try
        {
            var result = await _api.ChangeEligibilityAsync(studentId, allowBorrowing);
            await ShowResultAsync(result, studentId,
                allowBorrowing ? "Borrowing permitted." : "Borrowing restricted.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // One place decides what each outcome means for the screen.
    private async Task ShowResultAsync(ApiResult<StudentDto> result, int? currentStudentId, string successMessage)
    {
        if (result.IsSuccess)
        {
            // Reload from the API and select the saved student, so the screen shows what is actually stored.
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
                // NotFound, Conflict, Server, Unreachable...: the screen may be out of date, so refresh
                // first (BR-18), then show the reason. Nothing is retried automatically.
                await ReloadAsync(currentStudentId);
                Show(result.Message, isError: true);
                break;
        }
    }

    private async Task ReloadAsync(int? selectId)
    {
        IsBusy = true;
        try
        {
            var result = await _api.GetStudentsAsync(SearchText);

            if (!result.IsSuccess)
            {
                LoadFailed = true;
                Students = new ObservableCollection<StudentItem>();
                Show(result.Message, isError: true);
                return;
            }

            Students = new ObservableCollection<StudentItem>(result.Value!.Select(s => s.ToItem()));
            LoadFailed = false;

            // Select AFTER replacing the list. If the student is gone, this selects nothing
            // and the form returns to "register" mode.
            SelectedStudent = selectId is null ? null : Students.FirstOrDefault(s => s.Id == selectId);
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
