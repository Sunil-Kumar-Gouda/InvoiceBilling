using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DbHygiene_IndexesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_GrandTotal_NonNegative",
                table: "Invoices",
                sql: "GrandTotal >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Subtotal_NonNegative",
                table: "Invoices",
                sql: "Subtotal >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_TaxRatePercent_Range",
                table: "Invoices",
                sql: "TaxRatePercent >= 0 AND TaxRatePercent <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_TaxTotal_NonNegative",
                table: "Invoices",
                sql: "TaxTotal >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_LineTotal_NonNegative",
                table: "InvoiceLines",
                sql: "LineTotal >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_Quantity_Positive",
                table: "InvoiceLines",
                sql: "Quantity > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_UnitPrice_NonNegative",
                table: "InvoiceLines",
                sql: "UnitPrice >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_GrandTotal_NonNegative",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Subtotal_NonNegative",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_TaxRatePercent_Range",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_TaxTotal_NonNegative",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceLines_LineTotal_NonNegative",
                table: "InvoiceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceLines_Quantity_Positive",
                table: "InvoiceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceLines_UnitPrice_NonNegative",
                table: "InvoiceLines");
        }
    }
}
