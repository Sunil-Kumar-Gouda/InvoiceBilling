using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSqliteCheckConstraintCoercion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amount_Positive",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_BalanceDue_NonNegative",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_GrandTotal_NonNegative",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_PaidTotal_NonNegative",
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amount_Positive",
                table: "Payments",
                sql: "(Amount + 0) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_BalanceDue_NonNegative",
                table: "Invoices",
                sql: "(BalanceDue + 0) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_GrandTotal_NonNegative",
                table: "Invoices",
                sql: "(GrandTotal + 0) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices",
                sql: "(PaidTotal + 0) - (GrandTotal + 0) <= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_PaidTotal_NonNegative",
                table: "Invoices",
                sql: "(PaidTotal + 0) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Subtotal_NonNegative",
                table: "Invoices",
                sql: "(Subtotal + 0) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_TaxRatePercent_Range",
                table: "Invoices",
                sql: "(TaxRatePercent + 0) >= 0 AND (TaxRatePercent + 0) <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_TaxTotal_NonNegative",
                table: "Invoices",
                sql: "(TaxTotal + 0) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_LineTotal_NonNegative",
                table: "InvoiceLines",
                sql: "(LineTotal + 0) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_Quantity_Positive",
                table: "InvoiceLines",
                sql: "(Quantity + 0) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_UnitPrice_NonNegative",
                table: "InvoiceLines",
                sql: "(UnitPrice + 0) >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amount_Positive",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_BalanceDue_NonNegative",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_GrandTotal_NonNegative",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_PaidTotal_NonNegative",
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amount_Positive",
                table: "Payments",
                sql: "Amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_BalanceDue_NonNegative",
                table: "Invoices",
                sql: "BalanceDue >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_GrandTotal_NonNegative",
                table: "Invoices",
                sql: "GrandTotal >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_PaidTotal_LTE_GrandTotal",
                table: "Invoices",
                sql: "PaidTotal - GrandTotal <= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_PaidTotal_NonNegative",
                table: "Invoices",
                sql: "PaidTotal >= 0");

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
    }
}
