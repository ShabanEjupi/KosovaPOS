using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReceiptType",
                table: "Receipts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptType",
                table: "Receipts");
        }
    }
}
