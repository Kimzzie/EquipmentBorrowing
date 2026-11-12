namespace EquipmentBorrowing.Domain;

public class Student
{
    public int Id { get; private set; }
    public string StudentNumber { get; private set; }
    public string Name { get; private set; }
    public string? Contact { get; private set; }
    public bool IsAllowedToBorrow { get; private set; }

    public Student(
        int id,
        string studentNumber,
        string name,
        bool isAllowedToBorrow = true,
        string? contact = null)
    {
        Id = id;
        StudentNumber = NormalizeStudentNumber(studentNumber);
        Name = NormalizeName(name);
        Contact = NormalizeContact(contact);
        IsAllowedToBorrow = isAllowedToBorrow;
    }

    // BR-03: editable fields only. Id, history and eligibility are untouched.
    public void UpdateDetails(string studentNumber, string name, string? contact)
    {
        StudentNumber = NormalizeStudentNumber(studentNumber);
        Name = NormalizeName(name);
        Contact = NormalizeContact(contact);
    }

    // BR-04: returns false when nothing changed, so the service can say
    // "already eligible" instead of pretending something happened.
    public bool SetEligibility(bool isEligible)
    {
        if (IsAllowedToBorrow == isEligible)
            return false;

        IsAllowedToBorrow = isEligible;
        return true;
    }

    public static string NormalizeStudentNumber(string? value) => TextRules.Require(value, "Student number");
    public static string NormalizeName(string? value) => TextRules.Require(value, "Full name");
    public static string? NormalizeContact(string? value) => TextRules.Optional(value);
}