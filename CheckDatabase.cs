using System;
using System.Linq;
using KosovaPOS.Database;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS
{
    public class DatabaseChecker
    {
        public static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== Database Check ===\n");
            
            try
            {
                using var context = new POSDbContext();
                
                // Check articles
                var articlesCount = context.Articles.Count();
                Console.WriteLine($"Articles: {articlesCount}");
                
                // Check business partners
                var partnersCount = context.BusinessPartners.Count();
                Console.WriteLine($"Business Partners: {partnersCount}");
                
                // Check purchases
                var purchasesCount = context.Purchases.Count();
                Console.WriteLine($"Purchases: {purchasesCount}");
                
                // Check receipts (sales)
                var receiptsCount = context.Receipts.Count();
                Console.WriteLine($"Receipts (Sales): {receiptsCount}");
                
                // Check receipt items
                var receiptItemsCount = context.ReceiptItems.Count();
                Console.WriteLine($"Receipt Items: {receiptItemsCount}");
                
                if (receiptsCount > 0)
                {
                    Console.WriteLine("\n=== Sample Receipt Data ===");
                    var sampleReceipt = context.Receipts
                        .Include(r => r.Items)
                        .ThenInclude(i => i.Article)
                        .FirstOrDefault();
                    
                    if (sampleReceipt != null)
                    {
                        Console.WriteLine($"Receipt #: {sampleReceipt.ReceiptNumber}");
                        Console.WriteLine($"Date: {sampleReceipt.Date}");
                        Console.WriteLine($"Total: {sampleReceipt.TotalAmount:N2} €");
                        Console.WriteLine($"Items count: {sampleReceipt.Items.Count}");
                        
                        foreach (var item in sampleReceipt.Items.Take(3))
                        {
                            Console.WriteLine($"  - {item.ArticleName} x {item.Quantity} = {item.TotalValue:N2} €");
                        }
                    }
                }
                
                if (receiptItemsCount > 0)
                {
                    Console.WriteLine("\n=== Top Products by Revenue ===");
                    var topProducts = context.ReceiptItems
                        .Include(ri => ri.Article)
                        .Where(ri => ri.Article != null)
                        .GroupBy(ri => new { ri.ArticleId, ri.Article!.Name })
                        .Select(g => new
                        {
                            ProductName = g.Key.Name,
                            TotalRevenue = g.Sum(ri => ri.TotalValue),
                            TotalQuantity = g.Sum(ri => ri.Quantity)
                        })
                        .OrderByDescending(x => x.TotalRevenue)
                        .Take(10)
                        .ToList();
                    
                    foreach (var product in topProducts)
                    {
                        Console.WriteLine($"{product.ProductName}: {product.TotalRevenue:N2} € ({product.TotalQuantity} sold)");
                    }
                }
                else
                {
                    Console.WriteLine("\n⚠️  No receipt items found in database!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
    }
}
