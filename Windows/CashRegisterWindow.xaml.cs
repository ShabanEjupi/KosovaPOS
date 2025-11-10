using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KosovaPOS.Database;
using KosovaPOS.Models;
using KosovaPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class CashRegisterWindow : Window
    {
        private ObservableCollection<ReceiptItem> _receiptItems = new ObservableCollection<ReceiptItem>();
        private Receipt _currentReceipt;
        private ObservableCollection<Article> _searchResults = new ObservableCollection<Article>();
        
        public CashRegisterWindow()
        {
            InitializeComponent();
            InitializeWindow();
        }
        
        private void InitializeWindow()
        {
            // Load business info from environment
            BusinessNameText.Text = Environment.GetEnvironmentVariable("BUSINESS_NAME") ?? "EMRI I BIZNESIT";
            CashierNumberText.Text = Environment.GetEnvironmentVariable("DEFAULT_CASHIER") ?? "01";
            CashierNameText.Text = Environment.GetEnvironmentVariable("CASHIER_NAME") ?? "Arkëtari";
            
            // Initialize new receipt
            NewReceipt();
            
            // Bind data grid
            ReceiptItemsDataGrid.ItemsSource = _receiptItems;
            SearchResultsListBox.ItemsSource = _searchResults;
            
            // Focus on search box with delay to ensure cursor visibility
            Dispatcher.BeginInvoke(new Action(() =>
            {
                BarcodeSearchBox.Focus();
                BarcodeSearchBox.CaretIndex = BarcodeSearchBox.Text.Length;
                BarcodeSearchBox.SelectionStart = BarcodeSearchBox.Text.Length;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        private void BarcodeSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Ensure cursor is visible when focus is gained
            BarcodeSearchBox.CaretIndex = BarcodeSearchBox.Text.Length;
            BarcodeSearchBox.SelectionStart = BarcodeSearchBox.Text.Length;
        }
        
        private void BarcodeSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Keep placeholder behavior on focus lost
        }
        
        private void NewReceipt()
        {
            _currentReceipt = new Receipt
            {
                ReceiptNumber = GenerateReceiptNumber(),
                Date = DateTime.Now,
                CashierNumber = CashierNumberText.Text,
                CashierName = CashierNameText.Text,
                BuyerName = BuyerNameTextBox.Text
            };
            
            _receiptItems.Clear();
            UpdateTotals();
        }
        
        private string GenerateReceiptNumber()
        {
            var cashierNumber = CashierNumberText.Text;
            var sequenceNumber = GetNextSequenceNumber();
            return $"{cashierNumber}-KO{sequenceNumber:D6}";
        }
        
        private int GetNextSequenceNumber()
        {
            using var context = new POSDbContext();
            var lastReceipt = context.Receipts
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();
            return (lastReceipt?.Id ?? 0) + 1;
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle keyboard shortcuts
            if (e.Key == Key.F5)
            {
                PrintReceiptButton_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.F6)
            {
                CopyButton_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.F3)
            {
                ClearRowButton_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.F10)
            {
                DiscardReceiptButton_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.F7)
            {
                // Switch to Cash payment
                CashPaymentRadio.IsChecked = true;
                e.Handled = true;
            }
            else if (e.Key == Key.F8)
            {
                // Switch to Card payment
                CardPaymentRadio.IsChecked = true;
                e.Handled = true;
            }
            else if (e.Key == Key.F9)
            {
                // Switch to Fiscal receipt
                FiscalReceiptRadio.IsChecked = true;
                e.Handled = true;
            }
            else if (e.Key == Key.F11)
            {
                // Switch to Simple receipt
                SimpleReceiptRadio.IsChecked = true;
                e.Handled = true;
            }
            else if (e.Key == Key.F12)
            {
                // Switch to Waybill
                WaybillReceiptRadio.IsChecked = true;
                e.Handled = true;
            }
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                ZReportButton_Click(this, new RoutedEventArgs());
            }
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.X)
            {
                XReportButton_Click(this, new RoutedEventArgs());
            }
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                SoldArticlesButton_Click(this, new RoutedEventArgs());
            }
            else if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                // Handle barcode scanning
                // In a real implementation, accumulate digits and search when Enter is pressed
            }
        }
        
        public void AddArticleToReceipt(Article article, decimal quantity = 1)
        {
            // Check if article already exists in receipt
            var existingItem = _receiptItems.FirstOrDefault(i => i.ArticleId == article.Id);
            
            if (existingItem != null)
            {
                // Increase quantity of existing item - automatic recalculation via property change
                existingItem.Quantity += quantity;
            }
            else
            {
                // Add new item - set VAT to 0 for now
                var item = new ReceiptItem
                {
                    ArticleId = article.Id,
                    Article = article,
                    Barcode = article.Barcode,
                    ArticleName = article.Name,
                    Quantity = quantity,
                    Price = article.SalesPrice,
                    VATRate = 0, // Remove VAT for now
                    DiscountPercent = 0,
                    DiscountValue = 0
                };
                
                // Subscribe to property changes to update totals automatically
                item.PropertyChanged += ReceiptItem_PropertyChanged;
                
                _receiptItems.Add(item);
            }
            
            UpdateTotals();
            
            // Visual feedback
            System.Media.SystemSounds.Beep.Play();
            
            // Return focus to search box
            Dispatcher.BeginInvoke(new Action(() =>
            {
                BarcodeSearchBox.Focus();
                BarcodeSearchBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        
        private void ReceiptItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Update totals whenever any receipt item property changes
            UpdateTotals();
        }
        
        private void UpdateTotals()
        {
            var total = _receiptItems.Sum(i => i.TotalValue);
            var tax = _receiptItems.Sum(i => i.VATValue);
            
            TotalDisplayText.Text = total.ToString("F2");
            TaxDisplayText.Text = $"T: {tax:F2}";
            
            UpdateLeftAmount();
        }
        
        private void PaidAmountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateLeftAmount();
        }
        
        private void UpdateLeftAmount()
        {
            if (PaidAmountTextBox == null || TotalDisplayText == null || LeftAmountTextBox == null)
                return;
                
            if (decimal.TryParse(PaidAmountTextBox.Text, out var paid))
            {
                if (decimal.TryParse(TotalDisplayText.Text, out var total))
                {
                    var left = total - paid;
                    LeftAmountTextBox.Text = left.ToString("F2");
                }
            }
        }
        
        private void PaymentMethodChanged(object sender, RoutedEventArgs e)
        {
            // When switching to card payment, automatically set paid amount to total
            if (CardPaymentRadio != null && CardPaymentRadio.IsChecked == true)
            {
                if (decimal.TryParse(TotalDisplayText?.Text, out var total))
                {
                    PaidAmountTextBox.Text = total.ToString("F2");
                }
            }
        }
        
        private void PrintReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_receiptItems.Count == 0)
            {
                MessageBox.Show("Nuk ka artikuj në faturë!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Calculate total
            var total = decimal.Parse(TotalDisplayText.Text);
            
            // Show quick payment dialog
            var paymentWindow = new QuickPaymentWindow(total);
            if (paymentWindow.ShowDialog() != true)
            {
                // User cancelled
                return;
            }
            
            // Update paid amount from dialog
            PaidAmountTextBox.Text = paymentWindow.CashReceived.ToString("F2");
            UpdateLeftAmount();
            
            try
            {
                // Update receipt
                _currentReceipt.BuyerName = BuyerNameTextBox.Text;
                _currentReceipt.Remark = RemarkTextBox.Text;
                _currentReceipt.Items = _receiptItems.ToList();
                _currentReceipt.TotalAmount = total;
                _currentReceipt.PaidAmount = paymentWindow.CashReceived;
                _currentReceipt.LeftAmount = decimal.Parse(LeftAmountTextBox.Text);
                
                // Get payment method from radio buttons
                _currentReceipt.PaymentMethod = CashPaymentRadio.IsChecked == true ? "Para në dorë" : "Kartë";
                
                _currentReceipt.TaxAmount = _receiptItems.Sum(i => i.VATValue);
                
                // Get receipt type from radio buttons
                if (FiscalReceiptRadio.IsChecked == true)
                {
                    _currentReceipt.ReceiptType = ReceiptType.Fiscal;
                }
                else if (SimpleReceiptRadio.IsChecked == true)
                {
                    _currentReceipt.ReceiptType = ReceiptType.Simple;
                }
                else if (WaybillReceiptRadio.IsChecked == true)
                {
                    _currentReceipt.ReceiptType = ReceiptType.Waybill;
                }
                else
                {
                    _currentReceipt.ReceiptType = ReceiptType.Fiscal; // Default
                }
                
                // Save to database first
                using (var context = new POSDbContext())
                {
                    // Clear Article references to avoid navigation property issues
                    foreach (var item in _currentReceipt.Items)
                    {
                        item.Article = null;
                        item.Receipt = null;
                    }
                    
                    context.Receipts.Add(_currentReceipt);
                    context.SaveChanges();
                    
                    // Update stock quantities
                    foreach (var item in _currentReceipt.Items)
                    {
                        var article = context.Articles.Find(item.ArticleId);
                        if (article != null)
                        {
                            article.StockQuantity -= item.Quantity;
                            article.StockOut += item.Quantity;
                            article.UpdatedAt = DateTime.Now;
                        }
                    }
                    context.SaveChanges();
                }
                
                // Print receipt based on type
                if (_currentReceipt.ReceiptType == ReceiptType.Fiscal)
                {
                    var isFiscal = bool.Parse(Environment.GetEnvironmentVariable("FISCAL_ENABLED") ?? "false");
                    
                    if (isFiscal)
                    {
                        try
                        {
                            var fiscalService = ServiceLocator.Instance.FiscalPrinter;
                            var filePath = fiscalService.GenerateFiscalReceipt(_currentReceipt);
                            fiscalService.SendToFiscalPrinter(filePath);
                            _currentReceipt.IsFiscal = true;
                            _currentReceipt.FiscalFilePath = filePath;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Gabim në printim fiskal: {ex.Message}\nDo të printohet si faturë jo-fiskale.", 
                                "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                            
                            var receiptService = ServiceLocator.Instance.ReceiptPrinter;
                            receiptService.PrintNonFiscalReceipt(_currentReceipt);
                            _currentReceipt.IsFiscal = false;
                        }
                    }
                    else
                    {
                        var receiptService = ServiceLocator.Instance.ReceiptPrinter;
                        receiptService.PrintNonFiscalReceipt(_currentReceipt);
                        _currentReceipt.IsFiscal = false;
                    }
                }
                else
                {
                    // Simple receipt or Waybill - always use non-fiscal printer
                    var receiptService = ServiceLocator.Instance.ReceiptPrinter;
                    receiptService.PrintNonFiscalReceipt(_currentReceipt);
                    _currentReceipt.IsFiscal = false;
                }
                
                _currentReceipt.IsPrinted = true;
                
                // Update printed status in database
                using (var context = new POSDbContext())
                {
                    var receipt = context.Receipts.Find(_currentReceipt.Id);
                    if (receipt != null)
                    {
                        receipt.IsPrinted = true;
                        receipt.IsFiscal = _currentReceipt.IsFiscal;
                        receipt.FiscalFilePath = _currentReceipt.FiscalFilePath;
                        context.SaveChanges();
                    }
                }
                
                // Start new receipt without success message
                NewReceipt();
                BuyerNameTextBox.Text = "Qytetar";
                RemarkTextBox.Text = "";
                PaidAmountTextBox.Text = "0.00";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit të faturës:\n{ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_receiptItems.Count == 0)
            {
                MessageBox.Show("Nuk ka artikuj për tu kopjuar!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Copy last receipt items to new receipt
            var copiedItems = new ObservableCollection<ReceiptItem>(
                _receiptItems.Select(i => new ReceiptItem
                {
                    ArticleId = i.ArticleId,
                    Barcode = i.Barcode,
                    ArticleName = i.ArticleName,
                    Quantity = i.Quantity,
                    Price = i.Price,
                    VATRate = i.VATRate,
                    DiscountPercent = i.DiscountPercent,
                    DiscountValue = i.DiscountValue,
                    TotalValue = i.TotalValue
                })
            );
            
            // Subscribe to PropertyChanged events for all copied items
            foreach (var item in copiedItems)
            {
                item.PropertyChanged += ReceiptItem_PropertyChanged;
            }
            
            NewReceipt();
            _receiptItems = copiedItems;
            ReceiptItemsDataGrid.ItemsSource = _receiptItems;
            UpdateTotals();
            
            MessageBox.Show("Fatura u kopjua!", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void ClearRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReceiptItemsDataGrid.SelectedItem is ReceiptItem selectedItem)
            {
                _receiptItems.Remove(selectedItem);
                UpdateTotals();
            }
            else
            {
                MessageBox.Show("Zgjidhni një rresht për ta fshirë!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void ApplyDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if user has permission to give discounts
            var userSession = UserSessionService.Instance;
            if (!userSession.CanGiveDiscounts)
            {
                MessageBox.Show("Nuk keni të drejta për të aplikuar zbritje!\nKontaktoni administratorin për të dhënë të drejta.", 
                    "Qasje e kufizuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (ReceiptItemsDataGrid.SelectedItem is ReceiptItem selectedItem)
            {
                // Open discount dialog
                var discountWindow = new DiscountWindow(
                    selectedItem.ArticleName,
                    selectedItem.Price * selectedItem.Quantity,
                    selectedItem.DiscountPercent
                );
                
                if (discountWindow.ShowDialog() == true)
                {
                    // Check if discount is within allowed limit
                    if (discountWindow.DiscountPercent > userSession.MaxDiscountPercent)
                    {
                        MessageBox.Show($"Zbritja maksimale e lejuar për ju është {userSession.MaxDiscountPercent:F1}%.\nJu lutemi kërkoni autorizim nga menaxheri.", 
                            "Zbritje e pa-lejuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    // Apply discount - automatic recalculation via property change
                    selectedItem.DiscountPercent = discountWindow.DiscountPercent;
                    UpdateTotals();
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një artikull për të aplikuar zbritje!", 
                    "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void DiscardReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_receiptItems.Count > 0)
            {
                var result = MessageBox.Show("A jeni i sigurt që doni ta anuloni faturën?", 
                    "Konfirmimi", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    NewReceipt();
                    BuyerNameTextBox.Text = "Qytetar";
                    RemarkTextBox.Text = "";
                    PaidAmountTextBox.Text = "0.00";
                }
            }
        }
        
        private void ZReportButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("A jeni i sigurt që doni të gjeneroni Z-Raportin?\nKy veprim do të mbyllë ditën fiskale!", 
                "Konfirmimi", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                var fiscalService = ServiceLocator.Instance.FiscalPrinter;
                var filePath = fiscalService.GenerateZReport();
                fiscalService.SendToFiscalPrinter(filePath);
                
                MessageBox.Show($"Z-Raporti u gjenerua: {filePath}", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void XReportButton_Click(object sender, RoutedEventArgs e)
        {
            var fiscalService = ServiceLocator.Instance.FiscalPrinter;
            var filePath = fiscalService.GenerateXReport();
            fiscalService.SendToFiscalPrinter(filePath);
            
            MessageBox.Show($"X-Raporti u gjenerua: {filePath}", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void SoldArticlesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new POSDbContext();
                
                // Get today's sold articles
                var today = DateTime.Now.Date;
                var tomorrow = today.AddDays(1);
                
                var soldArticles = context.ReceiptItems
                    .Include(ri => ri.Receipt)
                    .Include(ri => ri.Article)
                    .Where(ri => ri.Receipt != null && ri.Receipt.Date >= today && ri.Receipt.Date < tomorrow)
                    .ToList()
                    .GroupBy(ri => new { ri.ArticleId, ri.ArticleName, ri.Barcode, ri.Price })
                    .Select(g => new
                    {
                        Barcode = g.Key.Barcode,
                        ArticleName = g.Key.ArticleName,
                        Quantity = g.Sum(ri => ri.Quantity),
                        Price = g.Key.Price,
                        TotalValue = g.Sum(ri => ri.TotalValue),
                        TransactionCount = g.Count()
                    })
                    .OrderByDescending(x => x.TotalValue)
                    .ToList();
                
                // Create a window to show the sold articles
                var soldWindow = new Window
                {
                    Title = "Raporti i Artikujve të Shitur - Sot",
                    Width = 1000,
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
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                
                // Header
                var headerPanel = new System.Windows.Controls.StackPanel();
                var titleText = new System.Windows.Controls.TextBlock
                {
                    Text = "📊 Raporti i Artikujve të Shitur",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryColor"]
                };
                var subtitleText = new System.Windows.Controls.TextBlock
                {
                    Text = $"Data: {today:dd/MM/yyyy} | Artikuj të ndryshëm: {soldArticles.Count}",
                    FontSize = 14,
                    Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondary"],
                    Margin = new Thickness(0, 5, 0, 0)
                };
                headerPanel.Children.Add(titleText);
                headerPanel.Children.Add(subtitleText);
                System.Windows.Controls.Grid.SetRow(headerPanel, 0);
                grid.Children.Add(headerPanel);
                
                // DataGrid with sold articles
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
                    Binding = new System.Windows.Data.Binding("ArticleName"),
                    Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Sasia",
                    Binding = new System.Windows.Data.Binding("Quantity") { StringFormat = "N2" },
                    Width = new System.Windows.Controls.DataGridLength(100)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Çmimi (€)",
                    Binding = new System.Windows.Data.Binding("Price") { StringFormat = "N2" },
                    Width = new System.Windows.Controls.DataGridLength(100)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Vlera Totale (€)",
                    Binding = new System.Windows.Data.Binding("TotalValue") { StringFormat = "N2" },
                    Width = new System.Windows.Controls.DataGridLength(130)
                });
                dataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Nr. Transaksioneve",
                    Binding = new System.Windows.Data.Binding("TransactionCount"),
                    Width = new System.Windows.Controls.DataGridLength(140)
                });
                
                dataGrid.ItemsSource = soldArticles;
                System.Windows.Controls.Grid.SetRow(dataGrid, 1);
                grid.Children.Add(dataGrid);
                
                // Summary
                var totalQuantity = soldArticles.Sum(x => x.Quantity);
                var totalValue = soldArticles.Sum(x => x.TotalValue);
                
                var summaryPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                
                var summaryText = new System.Windows.Controls.TextBlock
                {
                    Text = $"TOTALI: Sasia = {totalQuantity:N2} | Vlera = {totalValue:N2} €",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessColor"]
                };
                summaryPanel.Children.Add(summaryText);
                System.Windows.Controls.Grid.SetRow(summaryPanel, 2);
                grid.Children.Add(summaryPanel);
                
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
                closeButton.Click += (s, args) => soldWindow.Close();
                System.Windows.Controls.Grid.SetRow(closeButton, 3);
                grid.Children.Add(closeButton);
                
                soldWindow.Content = grid;
                
                if (soldArticles.Count == 0)
                {
                    MessageBox.Show("Nuk ka shitje për ditën e sotme!", "Informacion",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    soldWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë gjenerimit të raportit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ReceiptItemsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Handle selection change if needed
        }
        
        private void ReceiptItemsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Update totals after cell edit completes
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateTotals();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        
        // Product Search Methods
        private void BarcodeSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchAndAddProduct();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && SearchResultsPopup.IsOpen && SearchResultsListBox.Items.Count > 0)
            {
                SearchResultsListBox.SelectedIndex = 0;
                SearchResultsListBox.Focus();
                e.Handled = true;
            }
        }
        
        private void BarcodeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = BarcodeSearchBox.Text.Trim();
            
            if (string.IsNullOrEmpty(searchText))
            {
                SearchResultsPopup.IsOpen = false;
                return;
            }
            
            // Auto-search as user types (after 2 characters)
            if (searchText.Length >= 2)
            {
                PerformSearch(searchText);
            }
        }
        
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchAndAddProduct();
        }
        
        private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Add product on single click selection
            if (SearchResultsListBox.SelectedItem is Article selectedArticle)
            {
                AddArticleToReceipt(selectedArticle);
                BarcodeSearchBox.Clear();
                SearchResultsPopup.IsOpen = false;
                BarcodeSearchBox.Focus();
            }
        }
        
        private void SearchResultsListBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Keep for backward compatibility but single click already handles it
            if (SearchResultsListBox.SelectedItem is Article selectedArticle)
            {
                AddArticleToReceipt(selectedArticle);
                BarcodeSearchBox.Clear();
                SearchResultsPopup.IsOpen = false;
                BarcodeSearchBox.Focus();
            }
        }
        
        private void PerformSearch(string searchText)
        {
            try
            {
                using var context = new POSDbContext();
                
                // Convert search text to lowercase for case-insensitive search
                var searchLower = searchText.ToLower();
                
                // Load articles to memory and perform case-insensitive search
                var results = context.Articles
                    .Where(a => a.IsActive)
                    .AsEnumerable()  // Switch to client-side evaluation
                    .Where(a => 
                        (a.Barcode != null && a.Barcode.ToLower().Contains(searchLower)) || 
                        (a.Name != null && a.Name.ToLower().Contains(searchLower)))
                    .Take(20)
                    .ToList();
                
                _searchResults.Clear();
                foreach (var article in results)
                {
                    _searchResults.Add(article);
                }
                
                SearchResultsPopup.IsOpen = _searchResults.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë kërkimit: {ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SearchAndAddProduct()
        {
            var searchText = BarcodeSearchBox.Text.Trim();
            
            if (string.IsNullOrEmpty(searchText))
                return;
            
            try
            {
                using var context = new POSDbContext();
                
                // Try exact barcode match first (case-insensitive)
                var searchLower = searchText.ToLower();
                var article = context.Articles
                    .AsEnumerable()  // Switch to client-side evaluation
                    .FirstOrDefault(a => a.IsActive && 
                                        a.Barcode != null && 
                                        a.Barcode.ToLower() == searchLower);
                
                if (article != null)
                {
                    // Exact match - add immediately
                    AddArticleToReceipt(article);
                    BarcodeSearchBox.Clear();
                    SearchResultsPopup.IsOpen = false;
                }
                else
                {
                    // No exact match - show search results
                    PerformSearch(searchText);
                    
                    if (_searchResults.Count == 1)
                    {
                        // Only one result - add it automatically
                        AddArticleToReceipt(_searchResults[0]);
                        BarcodeSearchBox.Clear();
                        SearchResultsPopup.IsOpen = false;
                    }
                    else if (_searchResults.Count == 0)
                    {
                        MessageBox.Show($"Nuk u gjet asnjë artikull me barkod ose emër: {searchText}", 
                            "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim: {ex.Message}", "Gabim", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
