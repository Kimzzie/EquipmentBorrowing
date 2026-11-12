using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class StudentsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private ObservableCollection<Student> students = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    [NotifyCanExecuteChangedFor(nameof(PermitBorrowingCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestrictBorrowingCommand))]
    private Student? selectedStudent;

    [ObservableProperty] private string searchText = string.Empty;

    // Form fields. They hold what staff typed; the service validates them.
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

    // "No results" and "the database failed" must look different (BR-19).
    public bool ShowEmptyState => !IsBusy && !LoadFailed && Students.Count == 0;

    public StudentsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // Selecting a student copies his/her values into the form; selecting nothing clears it.
    partial void OnSelectedStudentChanged(Student? value)
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
            await using var scope = _scopeFactory.CreateAsyncScope();

            OperationResult<Student> result;
            if (SelectedStudent is null)
            {
                var register = scope.ServiceProvider.GetRequiredService<RegisterStudentService>();
                result = await register.ExecuteAsync(StudentNumber, FullName, Contact);
            }
            else
            {
                var update = scope.ServiceProvider.GetRequiredService<UpdateStudentService>();
                result = await update.ExecuteAsync(SelectedStudent.Id, StudentNumber, FullName, Contact);
            }

            await ShowResultAsync(result, SelectedStudent?.Id);
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
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ChangeStudentEligibilityService>();
            var result = await service.ExecuteAsync(studentId, allowBorrowing);

            await ShowResultAsync(result, studentId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // One place decides what each outcome means for the screen.
    private async Task ShowResultAsync(OperationResult<Student> result, int? currentStudentId)
    {
        if (result.IsSuccess)
        {
            // Reload from the database and select the saved student, so the screen
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
                // of date, so refresh first (BR-18: "reconnect and refresh before a staff
                // decision"), then show the reason. Nothing is retried automatically.
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
            await using var scope = _scopeFactory.CreateAsyncScope();
            var find = scope.ServiceProvider.GetRequiredService<FindStudentsService>();
            var found = await find.ExecuteAsync(SearchText);

            Students = new ObservableCollection<Student>(found);
            LoadFailed = false;

            // Select AFTER replacing the list. If the student is gone, this selects
            // nothing and the form returns to "register" mode.
            SelectedStudent = selectId is null ? null : Students.FirstOrDefault(s => s.Id == selectId);
        }
        catch (DatabaseAccessException ex)
        {
            LoadFailed = true;
            Students = new ObservableCollection<Student>();
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