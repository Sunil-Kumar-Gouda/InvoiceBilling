using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTaxRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxRatePercent",
                table: "Invoices",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxRatePercent",
                table: "Invoices");
        }
    }
}
