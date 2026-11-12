using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EquipmentBorrowing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    NormalizedEquipmentCode = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(btrim(\"EquipmentCode\"))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.CheckConstraint("CK_Equipment_Description_NotBlank", "\"Description\" IS NULL OR btrim(\"Description\") <> ''");
                    table.CheckConstraint("CK_Equipment_EquipmentCode_NotBlank", "btrim(\"EquipmentCode\") <> ''");
                    table.CheckConstraint("CK_Equipment_Name_NotBlank", "btrim(\"Name\") <> ''");
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Contact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsAllowedToBorrow = table.Column<bool>(type: "boolean", nullable: false),
                    NormalizedStudentNumber = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(btrim(\"StudentNumber\"))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.CheckConstraint("CK_Students_Contact_NotBlank", "\"Contact\" IS NULL OR btrim(\"Contact\") <> ''");
                    table.CheckConstraint("CK_Students_Name_NotBlank", "btrim(\"Name\") <> ''");
                    table.CheckConstraint("CK_Students_StudentNumber_NotBlank", "btrim(\"StudentNumber\") <> ''");
                });

            migrationBuilder.CreateTable(
                name: "Borrowings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    DateBorrowed = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DateReturned = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Borrowings", x => x.Id);
                    table.CheckConstraint("CK_Borrowings_ExpectedAfterBorrowed", "\"ExpectedReturnDate\" > \"DateBorrowed\"");
                    table.CheckConstraint("CK_Borrowings_StatusDates", "(\"Status\" = 'Active' AND \"DateReturned\" IS NULL) OR (\"Status\" = 'Returned' AND \"DateReturned\" IS NOT NULL AND \"DateReturned\" >= \"DateBorrowed\")");
                    table.ForeignKey(
                        name: "FK_Borrowings_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Borrowings_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_EquipmentId_DateBorrowed",
                table: "Borrowings",
                columns: new[] { "EquipmentId", "DateBorrowed" });

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_StudentId_Status",
                table: "Borrowings",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Borrowings_OneActivePerEquipment",
                table: "Borrowings",
                column: "EquipmentId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "UX_Equipment_NormalizedEquipmentCode",
                table: "Equipment",
                column: "NormalizedEquipmentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Students_NormalizedStudentNumber",
                table: "Students",
                column: "NormalizedStudentNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Borrowings");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "Students");
        }
    }
}
