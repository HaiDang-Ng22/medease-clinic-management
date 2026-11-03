using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Healthy_System.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFieldsToAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CashierName",
                table: "Appointments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceCode",
                table: "Appointments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Appointments",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Appointments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Appointments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "LabResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppointmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    LabTechId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DetailedResult = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    LabTechNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ImageFilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ImageFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ImageFileType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ResultDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResults_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabResults_Users_LabTechId",
                        column: x => x.LabTechId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_AppointmentId",
                table: "LabResults",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_LabTechId",
                table: "LabResults",
                column: "LabTechId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabResults");

            migrationBuilder.DropColumn(
                name: "CashierName",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "InvoiceCode",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Appointments");
        }
    }
}
