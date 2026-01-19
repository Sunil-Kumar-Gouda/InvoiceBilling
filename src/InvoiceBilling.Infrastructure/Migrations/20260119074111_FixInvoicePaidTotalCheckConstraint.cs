using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixInvoicePaidTotalCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices",
                sql: "PaidTotal - GrandTotal <= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices",
                sql: "PaidTotal <= GrandTotal");
        }
    }
}
