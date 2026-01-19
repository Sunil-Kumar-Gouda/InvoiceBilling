using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsAndBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BalanceDue",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidTotal",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount_Positive", "Amount > 0");
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices",
                column: "DueDate");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_BalanceDue_NonNegative",
                table: "Invoices",
                sql: "BalanceDue >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices",
                sql: "PaidTotal <= GrandTotal");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_PaidTotal_NonNegative",
                table: "Invoices",
                sql: "PaidTotal >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId_PaidAt",
                table: "Payments",
                columns: new[] { "InvoiceId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaidAt",
                table: "Payments",
                column: "PaidAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_BalanceDue_NonNegative",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_PaidTotal_NonNegative",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BalanceDue",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaidTotal",
                table: "Invoices");
        }
    }
}
