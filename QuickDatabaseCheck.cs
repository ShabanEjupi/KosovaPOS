using System;
using System.Linq;
using System.IO;
using KosovaPOS.Database;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS
{
    /// <summary>
    /// Quick verification tool to check database contents
    /// Run this to see if sales data has been imported
    /// </summary>
    public class QuickDatabaseCheck
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          Kosovo POS - Quick Database Check                          ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            
            try
            {
                var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "Database", "KosovaPOS.db");
                
                if (!File.Exists(dbPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Database file not found!");
                    Console.WriteLine($"  Expected location: {dbPath}");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("Please run the application first to create the database.");
                    return;
                }
                
                Console.WriteLine($"✓ Database found: {dbPath}");
                Console.WriteLine($"  Size: {new FileInfo(dbPath).Length / 1024.0 / 1024.0:F2} MB");
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
                Console.WriteLine("DATABASE CONTENTS:");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
                Console.WriteLine();
                
                using var context = new POSDbContext();
                
                // Check all tables
                var articlesCount = context.Articles.Count();
                var partnersCount = context.BusinessPartners.Count();
                var purchasesCount = context.Purchases.Count();
                var purchaseItemsCount = context.PurchaseItems.Count();
                var receiptsCount = context.Receipts.Count();
                var receiptItemsCount = context.ReceiptItems.Count();
                var usersCount = context.Users.Count();
                var auditLogsCount = context.AuditLogs.Count();
                
                Console.WriteLine($"📦 Articles (Products):       {articlesCount,10:N0}");
                Console.WriteLine($"🏢 Business Partners:         {partnersCount,10:N0}");
                Console.WriteLine($"📋 Purchase Orders:           {purchasesCount,10:N0}");
                Console.WriteLine($"   └─ Purchase Items:         {purchaseItemsCount,10:N0}");
                Console.WriteLine();
                
                if (receiptsCount == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ Receipts (Sales):          {receiptsCount,10:N0}  ← MISSING!");
                    Console.WriteLine($"   └─ Receipt Items:          {receiptItemsCount,10:N0}  ← MISSING!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Receipts (Sales):           {receiptsCount,10:N0}");
                    Console.WriteLine($"   └─ Receipt Items:          {receiptItemsCount,10:N0}");
                    Console.ResetColor();
                }
                
                Console.WriteLine();
                Console.WriteLine($"👤 Users:                     {usersCount,10:N0}");
                Console.WriteLine($"📝 Audit Logs:                {auditLogsCount,10:N0}");
                
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
                
                // Show diagnosis
                if (receiptsCount == 0 && receiptItemsCount == 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠️  ISSUE DETECTED: Sales data (Receipts) has NOT been imported!");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("This means:");
                    Console.WriteLine("  • Analytics page will show zero/empty data");
                    Console.WriteLine("  • Top products will not display");
                    Console.WriteLine("  • Sales reports will be empty");
                    Console.WriteLine("  • Revenue charts will show no data");
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("SOLUTION:");
                    Console.ResetColor();
                    Console.WriteLine("  Run the import utility to import sales data from script.sql:");
                    Console.WriteLine();
                    Console.WriteLine("  Option 1: Double-click RunImport.bat");
                    Console.WriteLine("  Option 2: In Visual Studio, set ImportUtility as startup project and run");
                    Console.WriteLine("  Option 3: Run: cd ImportUtility && dotnet run");
                    Console.WriteLine();
                }
                else if (receiptsCount > 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Database looks good! Sales data is present.");
                    Console.ResetColor();
                    Console.WriteLine();
                    
                    // Show sample data
                    Console.WriteLine("SAMPLE SALES DATA:");
                    Console.WriteLine("───────────────────────────────────────────────────────────────────────");
                    
                    var sampleReceipt = context.Receipts
                        .Include(r => r.Items)
                        .OrderByDescending(r => r.Date)
                        .FirstOrDefault();
                    
                    if (sampleReceipt != null)
                    {
                        Console.WriteLine($"Latest Receipt: {sampleReceipt.ReceiptNumber}");
                        Console.WriteLine($"Date:           {sampleReceipt.Date:dd/MM/yyyy HH:mm:ss}");
                        Console.WriteLine($"Customer:       {sampleReceipt.BuyerName}");
                        Console.WriteLine($"Total:          {sampleReceipt.TotalAmount:N2} €");
                        Console.WriteLine($"Items:          {sampleReceipt.Items.Count}");
                    }
                    
                    Console.WriteLine();
                    Console.WriteLine("TOP 5 PRODUCTS BY REVENUE:");
                    Console.WriteLine("───────────────────────────────────────────────────────────────────────");
                    
                    var topProducts = context.ReceiptItems
                        .GroupBy(ri => new { ri.ArticleId, ri.ArticleName })
                        .Select(g => new
                        {
                            Product = g.Key.ArticleName,
                            Revenue = g.Sum(ri => ri.TotalValue),
                            Quantity = g.Sum(ri => ri.Quantity)
                        })
                        .OrderByDescending(x => x.Revenue)
                        .Take(5)
                        .ToList();
                    
                    foreach (var product in topProducts)
                    {
                        Console.WriteLine($"  • {product.Product,-40} {product.Revenue,10:N2} € ({product.Quantity:N0} sold)");
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Error checking database:");
                Console.WriteLine($"  {ex.Message}");
                Console.ResetColor();
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("Details:");
                    Console.WriteLine($"  {ex.InnerException.Message}");
                }
            }
            
            Console.WriteLine();
        }
    }
}
