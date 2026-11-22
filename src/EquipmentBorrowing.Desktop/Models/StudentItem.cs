// Models/StudentItem.cs
namespace EquipmentBorrowing.Desktop.Models;

// Property names match what StudentsView / HistoryView / EquipmentView already bind to.
public sealed record StudentItem(int Id, string StudentNumber, string Name, string? Contact, bool IsAllowedToBorrow);
