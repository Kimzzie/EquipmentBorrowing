namespace EquipmentBorrowing.Domain;

public class Equipment
{
    // Single source of truth for column sizes (the EF configuration uses these too).
    public const int EquipmentCodeMaxLength = 30;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public int Id { get; private set; }
    public string EquipmentCode { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsAvailable { get; private set; }

    public Equipment(
        int id,
        string equipmentCode,
        string name,
        string? description = null,
        bool isAvailable = true)
    {
        Id = id;
        EquipmentCode = NormalizeEquipmentCode(equipmentCode);
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsAvailable = isAvailable;
    }

    // BR-07: descriptive fields only. Availability is NOT editable here; it changes
    // only through MarkAsBorrowed / MarkAsReturned (the borrow and return workflows).
    public void UpdateDetails(string equipmentCode, string name, string? description)
    {
        // Validate everything first, then assign, so a bad value never leaves
        // the object half-edited.
        var newCode = NormalizeEquipmentCode(equipmentCode);
        var newName = NormalizeName(name);
        var newDescription = NormalizeDescription(description);

        EquipmentCode = newCode;
        Name = newName;
        Description = newDescription;
    }

    public void MarkAsBorrowed()
    {
        if (!IsAvailable)
            throw new InvalidOperationException($"Equipment '{Name}' is already borrowed.");

        IsAvailable = false;
    }

    public void MarkAsReturned() => IsAvailable = true;

    public static string NormalizeEquipmentCode(string? value) =>
        TextRules.Require(value, "Equipment identifier");

    public static string NormalizeName(string? value) =>
        TextRules.Require(value, "Equipment name");

    public static string? NormalizeDescription(string? value) =>
        TextRules.Optional(value);
}