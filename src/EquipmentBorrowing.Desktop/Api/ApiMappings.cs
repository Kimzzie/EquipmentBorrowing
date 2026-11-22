// Api/ApiMappings.cs
using EquipmentBorrowing.Desktop.Models;

namespace EquipmentBorrowing.Desktop.Api;

public static class ApiMappings
{
    public static StudentItem ToItem(this StudentDto s) => new(s.Id, s.StudentNumber, s.FullName, s.Contact, s.IsEligible);
    public static EquipmentItem ToItem(this EquipmentDto e) => new(e.Id, e.EquipmentCode, e.Name, e.Description, e.IsAvailable);
}
