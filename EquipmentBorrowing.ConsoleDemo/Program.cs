using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Infrastructure.Repositories;

// Manual dependency injection: create repositories, then inject into the services.
var studentRepository = new InMemoryStudentRepository();
var equipmentRepository = new InMemoryEquipmentRepository();
var borrowingRepository = new InMemoryBorrowingRepository(studentRepository, equipmentRepository);
var unitOfWork = new InMemoryUnitOfWork();
var clock = TimeProvider.System;

var processor = new CheckoutProcessor(
    studentRepository,
    equipmentRepository,
    borrowingRepository,
    unitOfWork,
    clock);
var borrowService = new BorrowEquipmentService(processor);

var returnDate = clock.GetLocalToday().AddDays(7);

Console.WriteLine("=== Successful Case: Eligible student borrows available equipment ===");
var successResult = await borrowService.ExecuteAsync(studentId: 1, equipmentId: 1, expectedReturnDate: returnDate);

if (successResult.IsSuccess)
{
    Console.WriteLine($"SUCCESS: {successResult.Message}");
    Console.WriteLine($"  Student ID: {successResult.Value!.StudentId}");
    Console.WriteLine($"  Equipment ID: {successResult.Value.EquipmentId}");
    Console.WriteLine($"  Status: {successResult.Value.Status}");
}
else
{
    Console.WriteLine($"UNEXPECTED FAILURE: {successResult.Message}");
}

Console.WriteLine();
Console.WriteLine("=== Failure Case: Suspended student attempts to borrow ===");
var failureResult = await borrowService.ExecuteAsync(studentId: 2, equipmentId: 1, expectedReturnDate: returnDate);
Console.WriteLine(failureResult.IsSuccess
    ? "UNEXPECTED SUCCESS - this should not happen."
    : $"EXPECTED FAILURE: {failureResult.Message}");

Console.WriteLine();
Console.WriteLine("=== Failure Case: Equipment already unavailable ===");
var equipmentFailureResult = await borrowService.ExecuteAsync(studentId: 1, equipmentId: 2, expectedReturnDate: returnDate);
Console.WriteLine(equipmentFailureResult.IsSuccess
    ? "UNEXPECTED SUCCESS - this should not happen."
    : $"EXPECTED FAILURE: {equipmentFailureResult.Message}");