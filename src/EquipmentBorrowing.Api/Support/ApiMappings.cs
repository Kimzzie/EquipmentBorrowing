using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Application.Dtos;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Api.Support;

public static class ApiMappings
{
    public static StudentDto ToDto(this Student s) =>
        new(s.Id, s.StudentNumber, s.Name, s.Contact, s.IsAllowedToBorrow);

    public static EquipmentDto ToDto(this Equipment e) =>
        new(e.Id, e.EquipmentCode, e.Name, e.Description, e.IsAvailable);

    public static BorrowingDto ToDto(this BorrowingRecord r) =>
        new(r.Id, r.StudentId, r.StudentNumber, r.StudentName,
            r.EquipmentId, r.EquipmentCode, r.EquipmentName,
            r.DateBorrowed, r.ExpectedReturnDate, r.DateReturned, r.Status.ToString());
}
