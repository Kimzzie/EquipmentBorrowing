namespace EquipmentBorrowing.Domain;

public class Borrowing
{
    public int Id { get; private set; }
    public int StudentId { get; }
    public int EquipmentId { get; }
    public DateOnly DateBorrowed { get; }
    public DateOnly ExpectedReturnDate { get; }
    public DateOnly? DateReturned { get; private set; }
    public BorrowingStatus Status { get; private set; }

    public Borrowing(
        int id,
        int studentId,
        int equipmentId,
        DateOnly dateBorrowed,
        DateOnly expectedReturnDate)
    {
        // BR-11 safety net. The service checks this first and gives a friendly message.
        if (expectedReturnDate <= dateBorrowed)
            throw new ArgumentException(
                "Expected return date must be later than the checkout date.",
                nameof(expectedReturnDate));

        Id = id;
        StudentId = studentId;
        EquipmentId = equipmentId;
        DateBorrowed = dateBorrowed;
        ExpectedReturnDate = expectedReturnDate;
        Status = BorrowingStatus.Active;
    }

    public void MarkAsReturned(DateOnly returnedDate)
    {
        if (Status == BorrowingStatus.Returned)
            throw new InvalidOperationException("Borrowing is already marked as returned.");

        // BR-13: the return date can't precede checkout.
        if (returnedDate < DateBorrowed)
            throw new ArgumentException(
                "Return date cannot be earlier than the checkout date.",
                nameof(returnedDate));

        Status = BorrowingStatus.Returned;
        DateReturned = returnedDate;
    }
}