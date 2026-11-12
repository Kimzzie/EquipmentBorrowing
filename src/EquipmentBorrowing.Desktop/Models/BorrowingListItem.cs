namespace EquipmentBorrowing.Desktop.Models;

public class BorrowingListItem
{
    public required int Id { get; init; }
    public required string StudentName { get; init; }
    public required string EquipmentName { get; init; }
    public required DateOnly DateBorrowed { get; init; }
    public required DateOnly ExpectedReturnDate { get; init; }
}