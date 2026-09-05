namespace EquipmentBorrowing.Desktop.Models;

public class BorrowingListItem
{
    public required int Id { get; init; }
    public required string StudentName { get; init; }
    public required string EquipmentName { get; init; }
    public required DateTime DateBorrowed { get; init; }
    public required DateTime ExpectedReturnDate { get; init; }
}