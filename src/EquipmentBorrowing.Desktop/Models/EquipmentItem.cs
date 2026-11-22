// Models/EquipmentItem.cs
namespace EquipmentBorrowing.Desktop.Models;

public sealed record EquipmentItem(int Id, string EquipmentCode, string Name, string? Description, bool IsAvailable);
