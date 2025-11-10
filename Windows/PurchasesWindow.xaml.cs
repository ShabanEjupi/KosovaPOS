using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KosovaPOS.Database;
using KosovaPOS.Models;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.IO;

namespace KosovaPOS.Windows
{
    public partial class PurchasesWindow : Window
    {
        private Purchase? _selectedPurchase;
        
        public PurchasesWindow()
        {
            InitializeComponent();
            
            // Set default date range - show all data (10 years in past and 1 year in future to catch any date issues)
            ToDatePicker.SelectedDate = DateTime.Now.AddYears(1);
            FromDatePicker.SelectedDate = DateTime.Now.AddYears(-10);
            
            LoadPurchasesData();
        }
        
        private void LoadPurchasesData()
        {
            try
            {
                using var context = new POSDbContext();
                
                // First, check total count
                var totalCount = context.Purchases.Count();
                System.Diagnostics.Debug.WriteLine($"Total purchases in database: {totalCount}");
                
                var fromDate = FromDatePicker.SelectedDate ?? DateTime.Now.AddYears(-5);
                var toDate = ToDatePicker.SelectedDate ?? DateTime.Now;
                
                System.Diagnostics.Debug.WriteLine($"Date range filter: {fromDate:yyyy-MM-dd HH:mm:ss} to {toDate.AddDays(1):yyyy-MM-dd HH:mm:ss}");
                
                // Get ALL purchases first to inspect dates
                var allPurchases = context.Purchases.ToList();
                
                if (allPurchases.Any())
                {
                    var minDate = allPurchases.Min(p => p.Date);
                    var maxDate = allPurchases.Max(p => p.Date);
                    System.Diagnostics.Debug.WriteLine($"Actual date range in DB: {minDate:yyyy-MM-dd HH:mm:ss} to {maxDate:yyyy-MM-dd HH:mm:ss}");
                    System.Diagnostics.Debug.WriteLine($"Current date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    
                    // Count future dates
                    var futureDates = allPurchases.Count(p => p.Date > DateTime.Now);
                    System.Diagnostics.Debug.WriteLine($"Purchases with future dates: {futureDates}");
                }
                
                var purchases = context.Purchases
                    .Include(p => p.Supplier)
                    .Include(p => p.Items)
                    .ThenInclude(i => i.Article)
                    .Where(p => p.Date >= fromDate && p.Date <= toDate.AddDays(1))
                    .OrderByDescending(p => p.Date)
                    .ToList();
                
                System.Diagnostics.Debug.WriteLine($"Filtered purchases: {purchases.Count}");
                
                PurchasesDataGrid.ItemsSource = purchases;
                
                // Calculate summary
                var totalPurchases = purchases.Sum(p => p.TotalAmount);
                var unpaidAmount = purchases.Where(p => !p.IsPaid).Sum(p => p.TotalAmount);
                var paidAmount = purchases.Where(p => p.IsPaid).Sum(p => p.TotalAmount);
                var purchaseCount = purchases.Count;
                
                TotalPurchasesText.Text = $"{totalPurchases:N2} €";
                UnpaidAmountText.Text = $"{unpaidAmount:N2} €";
                PaidAmountText.Text = $"{paidAmount:N2} €";
                PurchaseCountText.Text = purchaseCount.ToString();
                
                // Enhanced message if no purchases found
                if (purchaseCount == 0 && totalCount > 0)
                {
                    var dateInfo = "";
                    if (allPurchases.Any())
                    {
                        var minDate = allPurchases.Min(p => p.Date);
                        var maxDate = allPurchases.Max(p => p.Date);
                        var futureDates = allPurchases.Count(p => p.Date > DateTime.Now);
                        
                        dateInfo = $"\n\nData aktuale: {DateTime.Now:yyyy-MM-dd}\nFiltr: {fromDate:yyyy-MM-dd} deri {toDate:yyyy-MM-dd}\nBlerje në DB: {minDate:yyyy-MM-dd} deri {maxDate:yyyy-MM-dd}";
                        
                        if (futureDates > 0)
                        {
                            dateInfo += $"\n\nPROBLEM: {futureDates} blerje kanë data në të ardhmen!";
                        }
                    }
                    
                    MessageBox.Show($"Nuk u gjetën blerje në periudhën e zgjedhur.\nTotal në databazë: {totalCount}{dateInfo}", 
                        "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (totalCount == 0)
                {
                    MessageBox.Show("Nuk ka blerje në databazë. Importoni të dhënat nga SQL script-i.", 
                        "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të të dhënave: {ex.Message}\n\nStack Trace: {ex.StackTrace}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPurchasesData();
        }
        
        private void PurchasesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedPurchase = PurchasesDataGrid.SelectedItem as Purchase;
            
            if (_selectedPurchase != null)
            {
                PurchaseItemsDataGrid.ItemsSource = _selectedPurchase.Items;
            }
            else
            {
                PurchaseItemsDataGrid.ItemsSource = null;
            }
        }
        
        private void PurchasesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewPurchase_Click(sender, e);
        }
        
        private void ViewPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPurchase == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një blerje!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                using var context = new POSDbContext();
                var purchase = context.Purchases
                    .Include(p => p.Supplier)
                    .Include(p => p.Items)
                    .ThenInclude(i => i.Article)
                    .FirstOrDefault(p => p.Id == _selectedPurchase.Id);
                
                if (purchase == null)
                {
                    MessageBox.Show("Blerja nuk u gjet!", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Create detailed purchase view
                var detailsText = new System.Text.StringBuilder();
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine("            BLERJA - DETAJE              ");
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine();
                detailsText.AppendLine($"Nr. Dokumentit:  {purchase.DocumentNumber}");
                detailsText.AppendLine($"Data:            {purchase.Date:dd/MM/yyyy}");
                detailsText.AppendLine($"Furnizuesi:      {purchase.Supplier?.Name ?? "N/A"}");
                detailsText.AppendLine($"Lloji:           {purchase.PurchaseType}");
                detailsText.AppendLine($"Paguar:          {(purchase.IsPaid ? "Po" : "Jo")}");
                detailsText.AppendLine();
                detailsText.AppendLine("───────────────────────────────────────────");
                detailsText.AppendLine("ARTIKUJT:");
                detailsText.AppendLine("───────────────────────────────────────────");
                detailsText.AppendLine();
                
                int itemNo = 1;
                foreach (var item in purchase.Items)
                {
                    detailsText.AppendLine($"{itemNo}. {item.Article?.Name ?? "N/A"}");
                    detailsText.AppendLine($"   Barkodi:        {item.Article?.Barcode ?? "N/A"}");
                    detailsText.AppendLine($"   Sasia:          {item.Quantity:N2}");
                    detailsText.AppendLine($"   Çmimi blerje:   {item.PurchasePrice:N2} €");
                    detailsText.AppendLine($"   TVSH {item.VATRate}%:      {(item.TotalValue * item.VATRate / 100):N2} €");
                    detailsText.AppendLine($"   Totali:         {item.TotalValue:N2} €");
                    detailsText.AppendLine();
                    itemNo++;
                }
                
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine($"Totali:          {purchase.TotalAmount:N2} €");
                detailsText.AppendLine($"TVSH:            {purchase.VATAmount:N2} €");
                detailsText.AppendLine("═══════════════════════════════════════════");
                
                MessageBox.Show(detailsText.ToString(), $"Blerja - {purchase.DocumentNumber}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë shfaqjes së blerjes: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void NewPurchase_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PurchaseEditWindow();
            if (dialog.ShowDialog() == true)
            {
                LoadPurchasesData();
            }
        }
        
        private void EditPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPurchase == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një blerje për ndryshim!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var dialog = new PurchaseEditWindow(_selectedPurchase);
            if (dialog.ShowDialog() == true)
            {
                LoadPurchasesData();
            }
        }
        
        private void MarkAsPaid_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPurchase == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një blerje!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var result = MessageBox.Show($"A jeni të sigurt që dëshironi të shënoni blerjen '{_selectedPurchase.DocumentNumber}' si të paguar?",
                "Konfirmim", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var context = new POSDbContext();
                    var purchase = context.Purchases.Find(_selectedPurchase.Id);
                    if (purchase != null)
                    {
                        purchase.IsPaid = true;
                        context.SaveChanges();
                        
                        MessageBox.Show("Blerja u shënua si e paguar!", "Sukses",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        LoadPurchasesData();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim: {ex.Message}", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Blerjet");
                
                // Headers
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "Nr. Dokumentit";
                worksheet.Cell(1, 3).Value = "Data";
                worksheet.Cell(1, 4).Value = "Furnizuesi";
                worksheet.Cell(1, 5).Value = "Lloji";
                worksheet.Cell(1, 6).Value = "Totali (€)";
                worksheet.Cell(1, 7).Value = "TVSH (€)";
                worksheet.Cell(1, 8).Value = "Paguar";
                
                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                
                var purchases = PurchasesDataGrid.ItemsSource as List<Purchase>;
                if (purchases != null)
                {
                    int row = 2;
                    foreach (var purchase in purchases)
                    {
                        worksheet.Cell(row, 1).Value = purchase.Id;
                        worksheet.Cell(row, 2).Value = purchase.DocumentNumber;
                        worksheet.Cell(row, 3).Value = purchase.Date.ToString("dd/MM/yyyy");
                        worksheet.Cell(row, 4).Value = purchase.Supplier?.Name ?? "";
                        worksheet.Cell(row, 5).Value = purchase.PurchaseType;
                        worksheet.Cell(row, 6).Value = purchase.TotalAmount;
                        worksheet.Cell(row, 7).Value = purchase.VATAmount;
                        worksheet.Cell(row, 8).Value = purchase.IsPaid ? "Po" : "Jo";
                        row++;
                    }
                }
                
                worksheet.Columns().AdjustToContents();
                
                var fileName = $"Blerjet_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
                workbook.SaveAs(filePath);
                
                MessageBox.Show($"Të dhënat u eksportuan me sukses në:\n{filePath}", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë eksportimit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
