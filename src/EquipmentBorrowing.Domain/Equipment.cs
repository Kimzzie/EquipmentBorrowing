namespace EquipmentBorrowing.Domain;

public class Equipment
{
    public int Id { get; }
    public string Name { get; }
    public string? Description { get; }   // nullable — not every item needs one
    public bool IsAvailable { get; private set; }

    public Equipment(int id, string name, bool isAvailable = true, string? description = null)
    {
        Id = id;
        Name = name;
        Description = description;
        IsAvailable = isAvailable;
    }

    public void MarkAsBorrowed()
    {
        if (!IsAvailable)
            throw new InvalidOperationException($"Equipment '{Name}' is already borrowed.");

        IsAvailable = false;
    }

    public void MarkAsReturned() => IsAvailable = true;
}