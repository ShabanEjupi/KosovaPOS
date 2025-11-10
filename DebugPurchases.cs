using System;
using System.Linq;
using KosovaPOS.Database;
using Microsoft.EntityFrameworkCore;

class DebugPurchases
{
    static void Main()
    {
        using var context = new POSDbContext();
        
        Console.WriteLine("=== Purchase Debug Info ===\n");
        
        var totalCount = context.Purchases.Count();
        Console.WriteLine($"Total purchases in database: {totalCount}");
        
        if (totalCount == 0)
        {
            Console.WriteLine("No purchases found in database!");
            return;
        }
        
        var allPurchases = context.Purchases
            .Include(p => p.Supplier)
            .OrderBy(p => p.Date)
            .ToList();
        
        Console.WriteLine($"\nFirst 10 purchases:");
        foreach (var p in allPurchases.Take(10))
        {
            Console.WriteLine($"ID: {p.Id}, Doc: {p.DocumentNumber}, Date: {p.Date:yyyy-MM-dd HH:mm:ss}, " +
                            $"Supplier: {p.Supplier?.Name ?? "NULL"}, Amount: {p.TotalAmount:F2}");
        }
        
        var minDate = allPurchases.Min(p => p.Date);
        var maxDate = allPurchases.Max(p => p.Date);
        Console.WriteLine($"\nDate range in DB: {minDate:yyyy-MM-dd HH:mm:ss} to {maxDate:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Current date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        var futureDates = allPurchases.Count(p => p.Date > DateTime.Now);
        Console.WriteLine($"Purchases with future dates: {futureDates}");
        
        // Check date types
        Console.WriteLine($"\nDate type check:");
        Console.WriteLine($"Date.Kind for first purchase: {allPurchases.First().Date.Kind}");
        
        // Check what happens with the filter
        var testFrom = DateTime.Now.AddYears(-10);
        var testTo = DateTime.Now.AddYears(1);
        Console.WriteLine($"\nTest filter: {testFrom:yyyy-MM-dd} to {testTo:yyyy-MM-dd}");
        
        var filtered = allPurchases.Where(p => p.Date >= testFrom && p.Date <= testTo.AddDays(1)).Count();
        Console.WriteLine($"Filtered count: {filtered}");
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
