namespace EquipmentBorrowing.Desktop.Api;

public interface IEquipmentApi
{
    Task<ApiResult<IReadOnlyList<EquipmentDto>>> GetEquipmentAsync(bool availableOnly, CancellationToken ct = default);
    Task<ApiResult<EquipmentDto>> RegisterEquipmentAsync(EquipmentRequest request);
    Task<ApiResult<EquipmentDto>> UpdateEquipmentAsync(int id, EquipmentRequest request);

    Task<ApiResult<IReadOnlyList<StudentDto>>> GetStudentsAsync(string? search, CancellationToken ct = default);
    Task<ApiResult<StudentDto>> RegisterStudentAsync(StudentRequest request);
    Task<ApiResult<StudentDto>> UpdateStudentAsync(int id, StudentRequest request);
    Task<ApiResult<StudentDto>> ChangeEligibilityAsync(int id, bool isEligible);

    Task<ApiResult<BorrowingDto>> BorrowAsync(BorrowRequest request);
    Task<ApiResult<CheckoutResponse>> CheckoutAsync(CheckoutRequest request);
    Task<ApiResult<BorrowingDto>> ReturnAsync(int borrowingId);

    Task<ApiResult<IReadOnlyList<BorrowingDto>>> SearchBorrowingsAsync(
        string? search, string status, string sort, string direction, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<BorrowingDto>>> GetStudentBorrowingsAsync(int studentId, CancellationToken ct = default);
    Task<ApiResult<IReadOnlyList<BorrowingDto>>> GetEquipmentBorrowingsAsync(int equipmentId, CancellationToken ct = default);
}
