using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KosovaPOS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SalesUnit = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Pack = table.Column<decimal>(type: "TEXT", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Margin = table.Column<decimal>(type: "TEXT", nullable: false),
                    PackagePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    SalesPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    SalesPrice1 = table.Column<decimal>(type: "TEXT", nullable: false),
                    VATRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    VATType = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Supplier = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Importer = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Sector = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Season = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StockQuantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    StockIn = table.Column<decimal>(type: "TEXT", nullable: false),
                    StockOut = table.Column<decimal>(type: "TEXT", nullable: false),
                    AverageSalesPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    AveragePurchasePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinimumStock = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasBarcode = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRegular = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWeighed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProductType = table.Column<int>(type: "INTEGER", nullable: false),
                    POSCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    PhotoPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NRF = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    NUI = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PartnerType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Balance = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiptNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BuyerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CashierNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CashierName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    LeftAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsFiscal = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPrinted = table.Column<bool>(type: "INTEGER", nullable: false),
                    FiscalFilePath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Purchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchaseType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    VATAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsPaid = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Purchases_BusinessPartners_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiptId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ArticleName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    VATRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    VATValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalValue = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptItems_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptItems_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PurchaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    VATRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalValue = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Articles",
                columns: new[] { "Id", "AveragePurchasePrice", "AverageSalesPrice", "Barcode", "Branch", "Brand", "Category", "Color", "CreatedAt", "ExpiryDate", "Gender", "HasBarcode", "Importer", "IsActive", "IsRegular", "IsWeighed", "Location", "Margin", "MinimumStock", "Name", "Notes", "POSCategoryId", "Pack", "PackagePrice", "PhotoPath", "ProductType", "PurchasePrice", "SalesPrice", "SalesPrice1", "SalesUnit", "Season", "Sector", "Size", "StockIn", "StockOut", "StockQuantity", "Supplier", "SupplierId", "Unit", "UpdatedAt", "VATRate", "VATType", "WholesalePrice" },
                values: new object[,]
                {
                    { 1, 0m, 0m, "0001", null, null, "Pije", null, new DateTime(2025, 11, 9, 13, 32, 44, 548, DateTimeKind.Local).AddTicks(168), null, null, true, null, true, true, false, null, 0m, 0m, "Ujë Mineral 1.5L", null, null, 1m, 0m, null, 1, 0.30m, 0.50m, 0m, null, null, null, null, 0m, 0m, 100m, null, 0, "Copë", new DateTime(2025, 11, 9, 13, 32, 44, 548, DateTimeKind.Local).AddTicks(169), 18m, 3, 0m },
                    { 2, 0m, 0m, "0002", null, null, "Ushqim", null, new DateTime(2025, 11, 9, 13, 32, 44, 548, DateTimeKind.Local).AddTicks(175), null, null, true, null, true, true, false, null, 0m, 0m, "Bukë e Bardhë", null, null, 1m, 0m, null, 1, 0.20m, 0.35m, 0m, null, null, null, null, 0m, 0m, 50m, null, 0, "Copë", new DateTime(2025, 11, 9, 13, 32, 44, 548, DateTimeKind.Local).AddTicks(176), 8m, 3, 0m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Barcode",
                table: "Articles",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_ArticleId",
                table: "PurchaseItems",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_PurchaseId",
                table: "PurchaseItems",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_SupplierId",
                table: "Purchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ArticleId",
                table: "ReceiptItems",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ReceiptId",
                table: "ReceiptItems",
                column: "ReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseItems");

            migrationBuilder.DropTable(
                name: "ReceiptItems");

            migrationBuilder.DropTable(
                name: "Purchases");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "BusinessPartners");
        }
    }
}
