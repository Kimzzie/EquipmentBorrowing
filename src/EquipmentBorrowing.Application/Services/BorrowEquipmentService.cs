using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class BorrowEquipmentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private const int MaxActiveBorrowings = 3;

    public BorrowEquipmentService(
        IStudentRepository studentRepository,
        IEquipmentRepository equipmentRepository,
        IBorrowingRepository borrowingRepository,
        IUnitOfWork unitOfWork)
    {
        _studentRepository = studentRepository;
        _equipmentRepository = equipmentRepository;
        _borrowingRepository = borrowingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<BorrowResult> ExecuteAsync(
        int studentId,
        int equipmentId,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetByIdAsync(studentId, cancellationToken);
        if (student is null)
            return BorrowResult.Fail("Student not found.");

        if (!student.IsAllowedToBorrow)
            return BorrowResult.Fail("Student is not allowed to borrow.");

        var equipment = await _equipmentRepository.GetByIdAsync(equipmentId, cancellationToken);
        if (equipment is null)
            return BorrowResult.Fail("Equipment not found.");

        if (!equipment.IsAvailable)
            return BorrowResult.Fail("Equipment is not available.");

        var activeCount = await _borrowingRepository.CountActiveByStudentIdAsync(studentId, cancellationToken);
        if (activeCount >= MaxActiveBorrowings)
            return BorrowResult.Fail("Student has reached the maximum number of active borrowings.");

        equipment.MarkAsBorrowed();

        var borrowing = new Borrowing(
            id: 0, // the database generates the real ID on save
            studentId: studentId,
            equipmentId: equipmentId,
            dateBorrowed: DateTime.UtcNow,
            expectedReturnDate: expectedReturnDate);

        await _equipmentRepository.UpdateAsync(equipment, cancellationToken);
        await _borrowingRepository.AddAsync(borrowing, cancellationToken);

        // One save — the equipment update and the new borrowing commit
        // together, or neither does.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BorrowResult.Success(borrowing);
    }
}