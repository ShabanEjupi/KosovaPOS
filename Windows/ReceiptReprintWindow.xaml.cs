using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class ReceiptReprintWindow : Window
    {
        private ObservableCollection<ReceiptViewModel> _receipts = new ObservableCollection<ReceiptViewModel>();
        
        public ReceiptReprintWindow()
        {
            InitializeComponent();
            InitializeWindow();
        }
        
        private void InitializeWindow()
        {
            // Set default date range to today
            FromDatePicker.SelectedDate = DateTime.Today;
            ToDatePicker.SelectedDate = DateTime.Today;
            
            // Load receipts
            LoadReceipts();
            
            // Bind DataGrid
            ReceiptsDataGrid.ItemsSource = _receipts;
        }
        
        private void LoadReceipts()
        {
            try
            {
                var fromDate = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-7);
                var toDate = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1); // Include end of day
                
                using var context = new POSDbContext();
                
                var receipts = context.Receipts
                    .Include(r => r.Items)
                        .ThenInclude(i => i.Article)
                    .Where(r => r.Date >= fromDate && r.Date < toDate)
                    .OrderByDescending(r => r.Date)
                    .Take(100) // Limit to last 100 receipts
                    .ToList();
                
                _receipts.Clear();
                foreach (var receipt in receipts)
                {
                    _receipts.Add(new ReceiptViewModel(receipt));
                }
                
                ReceiptCountText.Text = $"({_receipts.Count} fatura)";
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të faturave: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            LoadReceipts();
        }
        
        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var receipt in _receipts)
            {
                receipt.IsSelected = true;
            }
            UpdateSelectedCount();
        }
        
        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var receipt in _receipts)
            {
                receipt.IsSelected = false;
            }
            UpdateSelectedCount();
        }
        
        private void ReceiptsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedCount();
        }
        
        private void UpdateSelectedCount()
        {
            var selectedCount = _receipts.Count(r => r.IsSelected);
            SelectedCountText.Text = $"| {selectedCount} të zgjedhura";
            PrintSelectedButton.IsEnabled = selectedCount > 0;
            
            // Update Select All checkbox state
            if (selectedCount == 0)
            {
                SelectAllCheckBox.IsChecked = false;
            }
            else if (selectedCount == _receipts.Count)
            {
                SelectAllCheckBox.IsChecked = true;
            }
            else
            {
                SelectAllCheckBox.IsChecked = null; // Indeterminate state
            }
        }
        
        private async void PrintSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedReceipts = _receipts.Where(r => r.IsSelected).ToList();
            
            if (selectedReceipts.Count == 0)
            {
                MessageBox.Show("Nuk ka fatura të zgjedhura!", "Paralajmërim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var confirmMessage = $"A jeni të sigurt që dëshironi të rishtypni {selectedReceipts.Count} fatura?";
            var result = MessageBox.Show(confirmMessage, "Konfirmo rishtypjen",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            
            try
            {
                // Show progress
                this.IsEnabled = false;
                this.Cursor = System.Windows.Input.Cursors.Wait;
                
                var fiscalPrinterService = new FiscalPrinterService();
                var receiptPrinterService = new ReceiptPrinterService();
                var successCount = 0;
                var failCount = 0;
                
                foreach (var receiptVM in selectedReceipts)
                {
                    try
                    {
                        var receipt = receiptVM.Receipt;
                        
                        if (receipt.ReceiptType == ReceiptType.Fiscal)
                        {
                            // Generate fiscal receipt file and send to printer
                            var filePath = fiscalPrinterService.GenerateFiscalReceipt(receipt);
                            fiscalPrinterService.SendToFiscalPrinter(filePath);
                        }
                        else
                        {
                            // Print non-fiscal receipt
                            receiptPrinterService.PrintNonFiscalReceipt(receipt);
                        }
                        
                        successCount++;
                        
                        // Small delay between prints
                        await System.Threading.Tasks.Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        System.Diagnostics.Debug.WriteLine($"Failed to print receipt {receiptVM.ReceiptNumber}: {ex.Message}");
                    }
                }
                
                // Show results
                var message = $"Rishtypja u përfundua!\n\nTë suksesshme: {successCount}\nTë dështuara: {failCount}";
                MessageBox.Show(message, "Rezultati i rishtypjes",
                    MessageBoxButton.OK, 
                    failCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                
                // Deselect all after printing
                foreach (var receipt in _receipts)
                {
                    receipt.IsSelected = false;
                }
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë rishtypjes: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
    
    // ViewModel for receipt with selection support
    public class ReceiptViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public Receipt Receipt { get; }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
        
        public string ReceiptNumber => Receipt.ReceiptNumber;
        public DateTime Date => Receipt.Date;
        public string BuyerName => Receipt.BuyerName;
        public string CashierName => Receipt.CashierName;
        public string PaymentMethod => Receipt.PaymentMethod;
        public decimal TotalAmount => Receipt.TotalAmount;
        public ReceiptType ReceiptType => Receipt.ReceiptType;
        public bool IsPrinted => Receipt.IsPrinted;
        public string IsPrintedText => Receipt.IsPrinted ? "✓ Po" : "✗ Jo";
        public string IsPrintedColor => Receipt.IsPrinted ? "#4CAF50" : "#F44336";
        
        public ReceiptViewModel(Receipt receipt)
        {
            Receipt = receipt;
            _isSelected = false;
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
