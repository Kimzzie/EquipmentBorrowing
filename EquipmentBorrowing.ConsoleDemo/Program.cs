using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Infrastructure.Repositories;

// Manual dependency injection: create repositories, then inject into the service.
var studentRepository = new InMemoryStudentRepository();
var equipmentRepository = new InMemoryEquipmentRepository();
var borrowingRepository = new InMemoryBorrowingRepository();

var borrowService = new BorrowEquipmentService(
    studentRepository,
    equipmentRepository,
    borrowingRepository);

Console.WriteLine("=== Successful Case: Eligible student borrows available equipment ===");
var successResult = await borrowService.ExecuteAsync(
    studentId: 1,           // Juan Dela Cruz - allowed to borrow
    equipmentId: 1,         // Digital Multimeter - available
    expectedReturnDate: DateTime.UtcNow.AddDays(7));

if (successResult.IsSuccess)
{
    Console.WriteLine($"SUCCESS: Borrowing #{successResult.Borrowing!.Id} created.");
    Console.WriteLine($"  Student ID: {successResult.Borrowing.StudentId}");
    Console.WriteLine($"  Equipment ID: {successResult.Borrowing.EquipmentId}");
    Console.WriteLine($"  Status: {successResult.Borrowing.Status}");
}
else
{
    Console.WriteLine($"UNEXPECTED FAILURE: {successResult.ErrorMessage}");
}

Console.WriteLine();
Console.WriteLine("=== Failure Case: Suspended student attempts to borrow ===");
var failureResult = await borrowService.ExecuteAsync(
    studentId: 2,            // Maria Santos - NOT allowed to borrow
    equipmentId: 1,
    expectedReturnDate: DateTime.UtcNow.AddDays(7));

if (!failureResult.IsSuccess)
{
    Console.WriteLine($"EXPECTED FAILURE: {failureResult.ErrorMessage}");
}
else
{
    Console.WriteLine("UNEXPECTED SUCCESS - this should not happen.");
}

Console.WriteLine();
Console.WriteLine("=== Failure Case: Equipment already unavailable ===");
var equipmentFailureResult = await borrowService.ExecuteAsync(
    studentId: 1,
    equipmentId: 2,           // Oscilloscope - already unavailable
    expectedReturnDate: DateTime.UtcNow.AddDays(7));

if (!equipmentFailureResult.IsSuccess)
{
    Console.WriteLine($"EXPECTED FAILURE: {equipmentFailureResult.ErrorMessage}");
}