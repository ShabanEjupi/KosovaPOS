using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class ReportsWindow : Window
    {
        public ReportsWindow()
        {
            InitializeComponent();
            LoadReportsData();
        }
        
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadReportsData();
            MessageBox.Show("Të dhënat u rifreskuan me sukses!", "Rifreskuar",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void LoadReportsData()
        {
            try
            {
                using var context = new POSDbContext();
                
                var now = DateTime.Now;
                var today = now.Date;
                var weekStart = today.AddDays(-(int)today.DayOfWeek);
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var yearStart = new DateTime(now.Year, 1, 1);
                
                // Sales summaries - Load to memory first to avoid SQLite decimal aggregation issue
                var receipts = context.Receipts.ToList();
                var todaySales = receipts.Where(r => r.Date.Date == today).Sum(r => (double)r.TotalAmount);
                var weekSales = receipts.Where(r => r.Date >= weekStart).Sum(r => (double)r.TotalAmount);
                var monthSales = receipts.Where(r => r.Date >= monthStart).Sum(r => (double)r.TotalAmount);
                var yearSales = receipts.Where(r => r.Date >= yearStart).Sum(r => (double)r.TotalAmount);
                
                TodaySalesText.Text = $"{todaySales:N2} €";
                WeekSalesText.Text = $"{weekSales:N2} €";
                MonthSalesText.Text = $"{monthSales:N2} €";
                YearSalesText.Text = $"{yearSales:N2} €";
                
                // Purchases summaries - Load to memory first
                var purchases = context.Purchases.ToList();
                
                // Debug information
                System.Diagnostics.Debug.WriteLine($"Total purchases in database: {purchases.Count}");
                if (purchases.Any())
                {
                    var minDate = purchases.Min(p => p.Date);
                    var maxDate = purchases.Max(p => p.Date);
                    System.Diagnostics.Debug.WriteLine($"Purchases date range: {minDate:yyyy-MM-dd} to {maxDate:yyyy-MM-dd}");
                    System.Diagnostics.Debug.WriteLine($"Today: {today:yyyy-MM-dd}, WeekStart: {weekStart:yyyy-MM-dd}, MonthStart: {monthStart:yyyy-MM-dd}");
                }
                
                var todayPurchases = purchases.Where(p => p.Date.Date == today).Sum(p => (double)p.TotalAmount);
                var weekPurchases = purchases.Where(p => p.Date.Date >= weekStart).Sum(p => (double)p.TotalAmount);
                var monthPurchases = purchases.Where(p => p.Date.Date >= monthStart).Sum(p => (double)p.TotalAmount);
                var yearPurchases = purchases.Where(p => p.Date.Date >= yearStart).Sum(p => (double)p.TotalAmount);
                
                TodayPurchasesText.Text = $"{todayPurchases:N2} €";
                WeekPurchasesText.Text = $"{weekPurchases:N2} €";
                MonthPurchasesText.Text = $"{monthPurchases:N2} €";
                YearPurchasesText.Text = $"{yearPurchases:N2} €";
                
                // Profit analysis - Calculate in memory
                var totalRevenue = receipts.Sum(r => (double)r.TotalAmount);
                var totalCost = purchases.Sum(p => (double)p.TotalAmount);
                var netProfit = totalRevenue - totalCost;
                
                TotalRevenueText.Text = $"{totalRevenue:N2} €";
                TotalCostText.Text = $"{totalCost:N2} €";
                NetProfitText.Text = $"{netProfit:N2} €";
                
                // Stock value - Load to memory first
                var articles = context.Articles.Where(a => a.IsActive).ToList();
                var stockValue = articles.Sum(a => (double)(a.StockQuantity * a.PurchasePrice));
                StockValueText.Text = $"{stockValue:N2} €";
                
                // VAT - Calculate in memory
                var vatPayable = receipts.Sum(r => (double)r.VATAmount);
                VATPayableText.Text = $"{vatPayable:N2} €";
                
                // Top products - Load to memory first with null check
                var receiptItemsCount = 0;
                try
                {
                    receiptItemsCount = context.ReceiptItems
                        .Include(ri => ri.Article)
                        .ToList()
                        .Where(ri => ri.Article != null) // Add null check
                        .GroupBy(ri => ri.ArticleId)
                        .Select(g => new { ArticleId = g.Key, TotalSold = g.Sum(ri => (double)ri.Quantity) })
                        .OrderByDescending(x => x.TotalSold)
                        .Take(10)
                        .Count();
                }
                catch
                {
                    receiptItemsCount = 0;
                }
                TopProductCountText.Text = $"{receiptItemsCount} artikuj";
                
                // Low stock
                var lowStockCount = articles.Count(a => a.StockQuantity < 10);
                LowStockCountText.Text = $"{lowStockCount} artikuj kanë stok të ulët";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të raporteve: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SalesDetails_Click(object sender, RoutedEventArgs e)
        {
            var salesWindow = new SalesWindow();
            salesWindow.ShowDialog();
        }
        
        private void PurchasesDetails_Click(object sender, RoutedEventArgs e)
        {
            var purchasesWindow = new PurchasesWindow();
            purchasesWindow.ShowDialog();
        }
        
        private void StockReport_Click(object sender, RoutedEventArgs e)
        {
            var articlesWindow = new ArticlesWindow();
            articlesWindow.ShowDialog();
        }
        
        private void VATReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                var receipts = context.Receipts.ToList();
                
                var vatReport = receipts
                    .GroupBy(r => r.Date.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        TotalSales = g.Sum(r => (double)r.TotalAmount),
                        TotalVAT = g.Sum(r => (double)r.VATAmount)
                    })
                    .OrderByDescending(x => x.Date)
                    .ToList();
                
                var report = "RAPORTI I TVSH-së\n\n";
                report += "Data\t\t\tShitjet\t\tTVSH\n";
                report += new string('-', 60) + "\n";
                
                foreach (var item in vatReport.Take(30))
                {
                    report += $"{item.Date:dd/MM/yyyy}\t\t{item.TotalSales:N2} €\t{item.TotalVAT:N2} €\n";
                }
                
                report += new string('-', 60) + "\n";
                report += $"TOTALI:\t\t{vatReport.Sum(x => x.TotalSales):N2} €\t{vatReport.Sum(x => x.TotalVAT):N2} €\n";
                
                MessageBox.Show(report, "Raporti i TVSH", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TopProducts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                
                var topProducts = context.ReceiptItems
                    .Include(ri => ri.Article)
                    .ToList()
                    .Where(ri => ri.Article != null) // Add null check
                    .GroupBy(ri => ri.ArticleId)
                    .Select(g => new
                    {
                        ArticleName = g.First().Article!.Name,
                        TotalQuantity = g.Sum(ri => (double)ri.Quantity),
                        TotalValue = g.Sum(ri => (double)ri.TotalValue)
                    })
                    .OrderByDescending(x => x.TotalValue)
                    .Take(20)
                    .ToList();
                
                var report = "TOP 20 ARTIKUJT MË TË SHITUR\n\n";
                report += "Emri\t\t\t\tSasia\t\tVlera\n";
                report += new string('-', 70) + "\n";
                
                foreach (var item in topProducts)
                {
                    var name = item.ArticleName.Length > 30 ? item.ArticleName.Substring(0, 30) : item.ArticleName;
                    report += $"{name}\t\t{item.TotalQuantity:N0}\t\t{item.TotalValue:N2} €\n";
                }
                
                MessageBox.Show(report, "Artikujt më të shitur", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void LowStockReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show loading cursor
                this.Cursor = System.Windows.Input.Cursors.Wait;
                
                // Run the query asynchronously to avoid blocking the UI
                var lowStock = await System.Threading.Tasks.Task.Run(() =>
                {
                    using var context = new POSDbContext();
                    
                    // Load to memory first, then order by decimal to avoid SQLite limitation
                    return context.Articles
                        .Where(a => a.IsActive && a.StockQuantity < 10)
                        .ToList()
                        .OrderBy(a => a.StockQuantity)
                        .ToList();
                });
                
                // Reset cursor
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                
                // Create a window to show the low stock items
                var lowStockWindow = new Window
                {
                    Title = "Artikujt me stok të ulët",
                    Width = 900,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = (System.Windows.Media.Brush)Application.Current.Resources["BackgroundColor"]
                };
                
                var grid = new System.Windows.Controls.Grid
                {
                    Margin = new Thickness(20)
                };
                
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                
                // Header
                var headerPanel = new System.Windows.Controls.StackPanel();
                var titleText = new System.Windows.Controls.TextBlock
                {
                    Text = "⚠️ Artikujt me stok të ulët (< 10 njësi)",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryColor"]
                };
                var subtitleText = new System.Windows.Controls.TextBlock
                {
                    Text = $"Gjetur {lowStock.Count} artikuj që kanë nevojë për rifurnizim",
                    FontSize = 14,
                    Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondary"],
                    Margin = new Thickness(0, 5, 0, 0)
                };
                headerPanel.Children.Add(titleText);
                headerPanel.Children.Add(subtitleText);
                System.Windows.Controls.Grid.SetRow(headerPanel, 0);
                grid.Children.Add(headerPanel);
                
                // DataGrid with scroll
                var dataGrid = new System.Windows.Controls.DataGrid
                {
                    Margin = new Thickness(0, 20, 0, 20),
                    AutoGenerateColumns = false,
                    IsReadOnly = true,
                    CanUserAddRows = false,
                    CanUserDeleteRows = false,
                    SelectionMode = System.Windows.Controls.DataGridSelectionMode.Single,
                    GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.Horizontal,
                    HeadersVisibility = System.Windows.Controls.DataGridHeadersVisibility.Column,
                    AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 0, 0, 0))
                };
                
                // Define columns
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Barkodi",
                    Binding = new System.Windows.Data.Binding("Barcode"),
                    Width = new System.Windows.Controls.DataGridLength(120)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Emri i Artikullit",
                    Binding = new System.Windows.Data.Binding("Name"),
                    Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Kategoria",
                    Binding = new System.Windows.Data.Binding("Category"),
                    Width = new System.Windows.Controls.DataGridLength(150)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Furnizuesi",
                    Binding = new System.Windows.Data.Binding("Supplier"),
                    Width = new System.Windows.Controls.DataGridLength(150)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Stoku",
                    Binding = new System.Windows.Data.Binding("StockQuantity") { StringFormat = "N2" },
                    Width = new System.Windows.Controls.DataGridLength(100)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Çmimi Shitës (€)",
                    Binding = new System.Windows.Data.Binding("SalesPrice") { StringFormat = "N2" },
                    Width = new System.Windows.Controls.DataGridLength(120)
                });
                
                dataGrid.ItemsSource = lowStock;
                System.Windows.Controls.Grid.SetRow(dataGrid, 1);
                grid.Children.Add(dataGrid);
                
                // Close button
                var closeButton = new System.Windows.Controls.Button
                {
                    Content = "🚪 Mbyll",
                    Width = 120,
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Style = (Style)Application.Current.Resources["ModernButton"],
                    Background = (System.Windows.Media.Brush)Application.Current.Resources["DangerColor"]
                };
                closeButton.Click += (s, args) => lowStockWindow.Close();
                System.Windows.Controls.Grid.SetRow(closeButton, 2);
                grid.Children.Add(closeButton);
                
                lowStockWindow.Content = grid;
                
                if (lowStock.Count == 0)
                {
                    MessageBox.Show("Nuk ka artikuj me stok të ulët!\nTë gjithë artikujt kanë stok të mjaftueshëm.", 
                        "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    lowStockWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Reset cursor
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        
        private void PrintDailySalesReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                var today = DateTime.Now.Date;
                
                var receipts = context.Receipts
                    .Where(r => r.Date.Date == today)
                    .ToList();
                
                if (receipts.Count == 0)
                {
                    MessageBox.Show("Nuk ka shitje për ditën e sotme!", "Informacion", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var report = GenerateDailySalesReport(receipts, today);
                SaveAndOpenReport(report, $"Raporti_Ditore_{today:yyyy-MM-dd}.txt");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void PrintMonthlySalesReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                var now = DateTime.Now;
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                
                var receipts = context.Receipts
                    .Where(r => r.Date >= monthStart && r.Date <= monthEnd)
                    .ToList();
                
                if (receipts.Count == 0)
                {
                    MessageBox.Show("Nuk ka shitje për këtë muaj!", "Informacion", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var report = GenerateMonthlySalesReport(receipts, monthStart);
                SaveAndOpenReport(report, $"Raporti_Mujor_{monthStart:yyyy-MM}.txt");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private string GenerateDailySalesReport(List<Receipt> receipts, DateTime date)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("                    RAPORTI DITORE I SHITJEVE                     ");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Data: {date:dd/MM/yyyy}");
            sb.AppendLine($"Gjeneruar: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            // Summary
            var totalSales = receipts.Sum(r => (double)r.TotalAmount);
            var totalVAT = receipts.Sum(r => (double)r.VATAmount);
            var totalReceipts = receipts.Count;
            
            sb.AppendLine($"Numri i faturave:        {totalReceipts}");
            sb.AppendLine($"Shitjet totale:          {totalSales:N2} €");
            sb.AppendLine($"TVSH totale:             {totalVAT:N2} €");
            sb.AppendLine();
            
            // Payment methods
            var paymentMethods = receipts.GroupBy(r => r.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(r => (double)r.TotalAmount), Count = g.Count() })
                .ToList();
            
            sb.AppendLine("METODAT E PAGESËS:");
            foreach (var pm in paymentMethods)
            {
                sb.AppendLine($"  {pm.Method,-20} {pm.Count,5} fatura    {pm.Total,12:N2} €");
            }
            sb.AppendLine();
            
            // Hourly breakdown
            var hourlyData = receipts.GroupBy(r => r.Date.Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(r => (double)r.TotalAmount), Count = g.Count() })
                .OrderBy(x => x.Hour)
                .ToList();
            
            sb.AppendLine("SHPËRNDARJA SIPAS ORËVE:");
            sb.AppendLine("  Ora        Fatura        Vlera");
            foreach (var h in hourlyData)
            {
                sb.AppendLine($"  {h.Hour:D2}:00      {h.Count,5}    {h.Total,12:N2} €");
            }
            sb.AppendLine();
            
            // Detailed transactions
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine("DETAJET E TRANSAKSIONEVE:");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            sb.AppendLine("Ora    Nr. Faturës        Klienti              Shuma      TVSH   ");
            sb.AppendLine("─────────────────────────────────────────────────────────────────");
            
            foreach (var receipt in receipts.OrderBy(r => r.Date))
            {
                var time = receipt.Date.ToString("HH:mm");
                var number = receipt.ReceiptNumber.Length > 15 ? 
                    receipt.ReceiptNumber.Substring(0, 15) : receipt.ReceiptNumber;
                var buyer = receipt.BuyerName.Length > 18 ? 
                    receipt.BuyerName.Substring(0, 18) : receipt.BuyerName;
                
                sb.AppendLine($"{time}  {number,-15}  {buyer,-18}  {receipt.TotalAmount,8:N2}  {receipt.VATAmount,6:N2}");
            }
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            
            return sb.ToString();
        }
        
        private string GenerateMonthlySalesReport(List<Receipt> receipts, DateTime month)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("                    RAPORTI MUJOR I SHITJEVE                      ");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Muaji: {month:MMMM yyyy}");
            sb.AppendLine($"Gjeneruar: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine();
            
            // Summary
            var totalSales = receipts.Sum(r => (double)r.TotalAmount);
            var totalVAT = receipts.Sum(r => (double)r.VATAmount);
            var totalReceipts = receipts.Count;
            var avgPerReceipt = totalReceipts > 0 ? totalSales / totalReceipts : 0;
            
            sb.AppendLine($"Numri i faturave:        {totalReceipts}");
            sb.AppendLine($"Shitjet totale:          {totalSales:N2} €");
            sb.AppendLine($"TVSH totale:             {totalVAT:N2} €");
            sb.AppendLine($"Mesatarja për faturë:    {avgPerReceipt:N2} €");
            sb.AppendLine();
            
            // Daily breakdown
            var dailyData = receipts.GroupBy(r => r.Date.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(r => (double)r.TotalAmount), Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList();
            
            sb.AppendLine("SHPËRNDARJA DITORE:");
            sb.AppendLine("  Data           Fatura        Vlera");
            sb.AppendLine("  ──────────────────────────────────────────");
            
            foreach (var d in dailyData)
            {
                sb.AppendLine($"  {d.Date:dd/MM/yyyy}     {d.Count,5}    {d.Total,12:N2} €");
            }
            sb.AppendLine();
            
            // Payment methods
            var paymentMethods = receipts.GroupBy(r => r.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(r => (double)r.TotalAmount), Count = g.Count() })
                .OrderByDescending(x => x.Total)
                .ToList();
            
            sb.AppendLine("METODAT E PAGESËS:");
            foreach (var pm in paymentMethods)
            {
                var percentage = (pm.Total / totalSales) * 100;
                sb.AppendLine($"  {pm.Method,-20} {pm.Count,5} ({percentage,5:F1}%)   {pm.Total,12:N2} €");
            }
            sb.AppendLine();
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            
            return sb.ToString();
        }
        
        private void SaveAndOpenReport(string content, string filename)
        {
            try
            {
                var reportsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    "KosovaPOS", 
                    "Raportet"
                );
                
                if (!Directory.Exists(reportsDir))
                {
                    Directory.CreateDirectory(reportsDir);
                }
                
                var filePath = Path.Combine(reportsDir, filename);
                System.IO.File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);
                
                var result = MessageBox.Show(
                    $"Raporti u ruajt në:\n{filePath}\n\nDëshironi ta hapni?", 
                    "Raporti u gjenerua", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Information
                );
                
                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes së raportit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
