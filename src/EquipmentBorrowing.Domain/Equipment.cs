namespace EquipmentBorrowing.Domain;

public class Equipment
{
    public int Id { get; private set; }

    public string Name { get; }
    public string Type { get; }
    public string? Description { get; }
    public bool IsAvailable { get; private set; }

    public Equipment(int id, string name, string type, bool isAvailable = true, string? description = null)
    {
        Id = id;
        Name = name;
        Type = type;
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