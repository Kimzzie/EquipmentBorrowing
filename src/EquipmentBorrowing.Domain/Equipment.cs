namespace EquipmentBorrowing.Domain;

public class Equipment
{
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

    // BR-07: descriptive fields only. Availability is NOT editable here.
    public void UpdateDetails(string equipmentCode, string name, string? description)
    {
        EquipmentCode = NormalizeEquipmentCode(equipmentCode);
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
    }

    public void MarkAsBorrowed()
    {
        if (!IsAvailable)
            throw new InvalidOperationException($"Equipment '{Name}' is already borrowed.");

        IsAvailable = false;
    }

    public void MarkAsReturned() => IsAvailable = true;

    public static string NormalizeEquipmentCode(string? value) => TextRules.Require(value, "Equipment identifier");
    public static string NormalizeName(string? value) => TextRules.Require(value, "Equipment name");
    public static string? NormalizeDescription(string? value) => TextRules.Optional(value);
}