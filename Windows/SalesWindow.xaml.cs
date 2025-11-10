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
    public partial class SalesWindow : Window
    {
        private Receipt? _selectedReceipt;
        
        public SalesWindow()
        {
            InitializeComponent();
            
            // Set default date range (last 30 days)
            ToDatePicker.SelectedDate = DateTime.Now;
            FromDatePicker.SelectedDate = DateTime.Now.AddDays(-30);
            
            LoadSalesData();
        }
        
        private void LoadSalesData()
        {
            try
            {
                using var context = new POSDbContext();
                
                var fromDate = FromDatePicker.SelectedDate ?? DateTime.Now.AddDays(-30);
                var toDate = ToDatePicker.SelectedDate ?? DateTime.Now;
                
                // Load receipts
                var receipts = context.Receipts
                    .Include(r => r.Items)
                    .ThenInclude(i => i.Article)
                    .Where(r => r.Date >= fromDate && r.Date <= toDate.AddDays(1))
                    .OrderByDescending(r => r.Date)
                    .ToList();
                
                SalesDataGrid.ItemsSource = receipts;
                
                // Calculate summary
                var totalSales = receipts.Sum(r => r.TotalAmount);
                var totalVAT = receipts.Sum(r => r.VATAmount);
                var receiptCount = receipts.Count;
                var averageSale = receiptCount > 0 ? totalSales / receiptCount : 0;
                
                TotalSalesText.Text = $"{totalSales:N2} €";
                TotalVATText.Text = $"{totalVAT:N2} €";
                TotalReceiptsText.Text = receiptCount.ToString();
                AverageSaleText.Text = $"{averageSale:N2} €";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të të dhënave: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSalesData();
        }
        
        private void SalesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedReceipt = SalesDataGrid.SelectedItem as Receipt;
            
            if (_selectedReceipt != null)
            {
                try
                {
                    // Ensure items are loaded with their articles
                    using var context = new POSDbContext();
                    var receipt = context.Receipts
                        .Include(r => r.Items)
                        .ThenInclude(i => i.Article)
                        .FirstOrDefault(r => r.Id == _selectedReceipt.Id);
                    
                    if (receipt != null)
                    {
                        ReceiptItemsDataGrid.ItemsSource = receipt.Items;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të artikujve: {ex.Message}", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                ReceiptItemsDataGrid.ItemsSource = null;
            }
        }
        
        private void SalesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewReceipt_Click(sender, e);
        }
        
        private void ViewReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedReceipt == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një faturë!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                using var context = new POSDbContext();
                var receipt = context.Receipts
                    .Include(r => r.Items)
                    .ThenInclude(i => i.Article)
                    .FirstOrDefault(r => r.Id == _selectedReceipt.Id);
                
                if (receipt == null)
                {
                    MessageBox.Show("Fatura nuk u gjet!", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Create detailed receipt view
                var detailsText = new System.Text.StringBuilder();
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine("              FATURA - DETAJE              ");
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine();
                detailsText.AppendLine($"Nr. Faturës:     {receipt.ReceiptNumber}");
                detailsText.AppendLine($"Data:            {receipt.Date:dd/MM/yyyy HH:mm:ss}");
                detailsText.AppendLine($"Klienti:         {receipt.BuyerName}");
                detailsText.AppendLine($"Arkëtari:        {receipt.CashierName}");
                detailsText.AppendLine($"Mënyra e pagesës: {receipt.PaymentMethod}");
                detailsText.AppendLine();
                detailsText.AppendLine("───────────────────────────────────────────");
                detailsText.AppendLine("ARTIKUJT:");
                detailsText.AppendLine("───────────────────────────────────────────");
                detailsText.AppendLine();
                
                int itemNo = 1;
                foreach (var item in receipt.Items)
                {
                    detailsText.AppendLine($"{itemNo}. {item.ArticleName}");
                    detailsText.AppendLine($"   Barkodi:   {item.Barcode}");
                    detailsText.AppendLine($"   Sasia:     {item.Quantity:N2} x {item.Price:N2} € = {item.Quantity * item.Price:N2} €");
                    
                    if (item.DiscountPercent > 0)
                    {
                        detailsText.AppendLine($"   Zbritja:   -{item.DiscountPercent:N2}% (-{item.DiscountValue:N2} €)");
                    }
                    
                    detailsText.AppendLine($"   TVSH {item.VATRate}%:   {item.VATValue:N2} €");
                    detailsText.AppendLine($"   Totali:    {item.TotalValue:N2} €");
                    detailsText.AppendLine();
                    itemNo++;
                }
                
                detailsText.AppendLine("═══════════════════════════════════════════");
                detailsText.AppendLine($"Totali:          {receipt.TotalAmount:N2} €");
                detailsText.AppendLine($"TVSH:            {receipt.TaxAmount:N2} €");
                detailsText.AppendLine($"Paguar:          {receipt.PaidAmount:N2} €");
                
                if (receipt.LeftAmount > 0)
                {
                    detailsText.AppendLine($"Kusur:           {receipt.LeftAmount:N2} €");
                }
                
                detailsText.AppendLine("═══════════════════════════════════════════");
                
                if (!string.IsNullOrWhiteSpace(receipt.Remark))
                {
                    detailsText.AppendLine();
                    detailsText.AppendLine($"Vërejtje: {receipt.Remark}");
                }
                
                MessageBox.Show(detailsText.ToString(), $"Fatura - {receipt.ReceiptNumber}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë shfaqjes së faturës: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void PrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedReceipt == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një faturë për printim!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                var printerService = new Services.ReceiptPrinterService();
                printerService.PrintNonFiscalReceipt(_selectedReceipt);
                
                MessageBox.Show("Fatura u printua me sukses!", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Shitjet");
                
                // Headers
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "Nr. Faturës";
                worksheet.Cell(1, 3).Value = "Data";
                worksheet.Cell(1, 4).Value = "Klienti";
                worksheet.Cell(1, 5).Value = "Totali (€)";
                worksheet.Cell(1, 6).Value = "TVSH (€)";
                worksheet.Cell(1, 7).Value = "Mënyra e pagesës";
                worksheet.Cell(1, 8).Value = "Arkëtari";
                
                // Style headers
                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                
                // Data
                var receipts = SalesDataGrid.ItemsSource as List<Receipt>;
                if (receipts != null)
                {
                    int row = 2;
                    foreach (var receipt in receipts)
                    {
                        worksheet.Cell(row, 1).Value = receipt.Id;
                        worksheet.Cell(row, 2).Value = receipt.ReceiptNumber;
                        worksheet.Cell(row, 3).Value = receipt.Date.ToString("dd/MM/yyyy HH:mm");
                        worksheet.Cell(row, 4).Value = receipt.CustomerName ?? "";
                        worksheet.Cell(row, 5).Value = receipt.TotalAmount;
                        worksheet.Cell(row, 6).Value = receipt.VATAmount;
                        worksheet.Cell(row, 7).Value = receipt.PaymentMethod ?? "";
                        worksheet.Cell(row, 8).Value = receipt.Cashier ?? "";
                        row++;
                    }
                }
                
                // Auto-fit columns
                worksheet.Columns().AdjustToContents();
                
                // Save
                var fileName = $"Shitjet_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
                workbook.SaveAs(filePath);
                
                MessageBox.Show($"Të dhënat u eksportuan me sukses në:\n{filePath}", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Open file
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
