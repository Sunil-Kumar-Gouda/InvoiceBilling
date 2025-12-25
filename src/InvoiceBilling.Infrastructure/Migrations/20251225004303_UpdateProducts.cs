using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Products",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "Products",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true,
                filter: "[Sku] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Products",
                type: "TEXT",
                nullable: true);
        }
    }
}
