using System.Net;
using EquipmentBorrowing.Api.Contracts;
using EquipmentBorrowing.Tests.Infrastructure;
using Npgsql;

namespace EquipmentBorrowing.Tests.UseCases;

/// <summary>
/// TC6-E01 Normalized uniqueness (UC-12, 13, 15, 16; BR-01, 03, 05, 07, 16).
/// Two layers are proven separately: (1) the API normalizes and answers 409, and
/// (2) PostgreSQL itself rejects a duplicate even when the API is bypassed with raw SQL.
/// </summary>
[Collection("Api")]
public sealed class UniquenessTests : ApiTestBase
{
    public UniquenessTests(ApiFactory factory) : base(factory) { }

    // ---------------- API layer: students ----------------

    [Theory, Trait("TC6", "E01")]
    [InlineData("ST-100")]
    [InlineData("st-100")]
    [InlineData("  ST-100  ")]
    [InlineData("  sT-100 ")]
    public async Task E01_Registering_a_case_or_whitespace_variant_of_a_student_number_is_a_409(string variant)
    {
        var before = await TestDatabase.SnapshotAsync();

        var response = await RegisterStudentAsync(variant, "Impostor");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        AssertSafeError(body);
        Assert.Equal("duplicate", ErrorCode(body));

        // Fresh counts: still exactly one student whose normalized number is st-100.
        Assert.Equal(4, await TestDatabase.CountAsync("Students"));
        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Students\" WHERE lower(btrim(\"StudentNumber\")) = 'st-100'"));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Theory, Trait("TC6", "E01")]
    [InlineData("st-100")]
    [InlineData("  ST-100 ")]
    public async Task E01_Updating_another_student_to_a_variant_of_an_existing_number_is_a_409(string variant)
    {
        var s4 = await StudentIdAsync("ST-400");
        var before = await TestDatabase.SnapshotAsync();

        var response = await UpdateStudentAsync(s4, variant, "Dani Ong", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("duplicate", ErrorCode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Fact, Trait("TC6", "E01")]
    public async Task E01_A_student_may_keep_their_own_number_in_a_different_case()
    {
        // Uniqueness must not conflict with the record being edited itself.
        var s4 = await StudentIdAsync("ST-400");

        var response = await UpdateStudentAsync(s4, "  st-400 ", "Dani Ong", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, await TestDatabase.CountAsync("Students"));
    }

    // ---------------- API layer: equipment ----------------

    [Theory, Trait("TC6", "E01")]
    [InlineData("EQ-001")]
    [InlineData("eq-001")]
    [InlineData("  EQ-001  ")]
    [InlineData("  eQ-001 ")]
    public async Task E01_Registering_a_case_or_whitespace_variant_of_an_equipment_code_is_a_409(string variant)
    {
        var before = await TestDatabase.SnapshotAsync();

        var response = await RegisterEquipmentAsync(variant, "Impostor Camera");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        AssertSafeError(body);
        Assert.Equal("duplicate", ErrorCode(body));

        Assert.Equal(7, await TestDatabase.CountAsync("Equipment"));
        Assert.Equal(1, await TestDatabase.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Equipment\" WHERE lower(btrim(\"EquipmentCode\")) = 'eq-001'"));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Theory, Trait("TC6", "E01")]
    [InlineData("eq-001")]
    [InlineData("  EQ-001 ")]
    public async Task E01_Updating_another_item_to_a_variant_of_an_existing_code_is_a_409(string variant)
    {
        var e2 = await EquipmentIdAsync("EQ-002");
        var before = await TestDatabase.SnapshotAsync();

        var response = await UpdateEquipmentAsync(e2, variant, "Laptop", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("duplicate", ErrorCode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    // ---------------- PostgreSQL layer: bypass the API with raw SQL ----------------

    [Theory, Trait("TC6", "E01")]
    [InlineData("ST-100")]
    [InlineData("st-100")]
    [InlineData("  ST-100  ")]
    public async Task E01_PostgreSQL_rejects_a_duplicate_student_number_even_without_the_API(string variant)
    {
        var before = await TestDatabase.SnapshotAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => TestDatabase.ExecuteAsync(
            $"INSERT INTO \"Students\" (\"StudentNumber\", \"Name\", \"IsAllowedToBorrow\") VALUES ('{variant}', 'Bypass', true)"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("UX_Students_NormalizedStudentNumber", error.ConstraintName);
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Theory, Trait("TC6", "E01")]
    [InlineData("EQ-001")]
    [InlineData("eq-001")]
    [InlineData("  EQ-001  ")]
    public async Task E01_PostgreSQL_rejects_a_duplicate_equipment_code_even_without_the_API(string variant)
    {
        var before = await TestDatabase.SnapshotAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => TestDatabase.ExecuteAsync(
            $"INSERT INTO \"Equipment\" (\"EquipmentCode\", \"Name\", \"IsAvailable\") VALUES ('{variant}', 'Bypass', true)"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("UX_Equipment_NormalizedEquipmentCode", error.ConstraintName);
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }

    [Theory, Trait("TC6", "E01")]
    [InlineData("INSERT INTO \"Students\" (\"StudentNumber\", \"Name\", \"IsAllowedToBorrow\") VALUES ('   ', 'Blank Number', true)",
        "CK_Students_StudentNumber_NotBlank")]
    [InlineData("INSERT INTO \"Students\" (\"StudentNumber\", \"Name\", \"IsAllowedToBorrow\") VALUES ('ST-777', '   ', true)",
        "CK_Students_Name_NotBlank")]
    [InlineData("INSERT INTO \"Equipment\" (\"EquipmentCode\", \"Name\", \"IsAvailable\") VALUES ('   ', 'Blank Code', true)",
        "CK_Equipment_EquipmentCode_NotBlank")]
    [InlineData("INSERT INTO \"Equipment\" (\"EquipmentCode\", \"Name\", \"IsAvailable\") VALUES ('EQ-777', '   ', true)",
        "CK_Equipment_Name_NotBlank")]
    public async Task E01_PostgreSQL_rejects_blank_required_values_even_without_the_API(string sql, string constraint)
    {
        var before = await TestDatabase.SnapshotAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => TestDatabase.ExecuteAsync(sql));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
        Assert.Equal(before, await TestDatabase.SnapshotAsync());
    }
}
