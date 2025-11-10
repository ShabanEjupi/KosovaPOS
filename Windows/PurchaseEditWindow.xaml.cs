using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class PurchaseEditWindow : Window
    {
        private Purchase? _purchase;
        private ObservableCollection<PurchaseItemDisplay> _items = new ObservableCollection<PurchaseItemDisplay>();
        
        public PurchaseEditWindow(Purchase? purchase = null)
        {
            InitializeComponent();
            _purchase = purchase;
            
            LoadComboBoxes();
            
            ItemsDataGrid.ItemsSource = _items;
            
            if (_purchase != null)
            {
                Title = "Ndrysho blerjen";
                LoadPurchaseData();
            }
            else
            {
                DatePicker.SelectedDate = DateTime.Now;
                DocumentNumberTextBox.Text = GenerateDocumentNumber();
            }
        }
        
        private void LoadComboBoxes()
        {
            using var context = new POSDbContext();
            
            // Load suppliers
            var suppliers = context.BusinessPartners
                .Where(bp => bp.IsActive && (bp.PartnerType == "Furnizues" || bp.PartnerType == "Të dy"))
                .ToList();
            SupplierComboBox.ItemsSource = suppliers;
            
            // Load articles
            var articles = context.Articles
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .ToList();
            ArticleComboBox.ItemsSource = articles;
            
            // Purchase types
            PurchaseTypeComboBox.ItemsSource = new List<string> { "Vendore", "Import", "Shpenzime" };
            PurchaseTypeComboBox.SelectedIndex = 0;
        }
        
        private void LoadPurchaseData()
        {
            if (_purchase == null) return;
            
            DocumentNumberTextBox.Text = _purchase.DocumentNumber;
            DatePicker.SelectedDate = _purchase.Date;
            SupplierComboBox.SelectedValue = _purchase.SupplierId;
            PurchaseTypeComboBox.Text = _purchase.PurchaseType;
            IsPaidCheckBox.IsChecked = _purchase.IsPaid;
            
            using var context = new POSDbContext();
            var items = context.PurchaseItems
                .Include(i => i.Article)
                .Where(i => i.PurchaseId == _purchase.Id)
                .ToList();
            
            foreach (var item in items)
            {
                _items.Add(new PurchaseItemDisplay
                {
                    ArticleId = item.ArticleId,
                    ArticleName = item.Article?.Name ?? "",
                    Quantity = item.Quantity,
                    PurchasePrice = item.PurchasePrice,
                    VATRate = item.VATRate,
                    TotalValue = item.TotalValue
                });
            }
            
            UpdateTotals();
        }
        
        private string GenerateDocumentNumber()
        {
            using var context = new POSDbContext();
            var lastPurchase = context.Purchases.OrderByDescending(p => p.Id).FirstOrDefault();
            var nextNumber = (lastPurchase?.Id ?? 0) + 1;
            return $"BL-{DateTime.Now:yyyyMMdd}-{nextNumber:D4}";
        }
        
        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            if (ArticleComboBox.SelectedItem == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një artikull!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!decimal.TryParse(QuantityTextBox.Text, out var quantity) || quantity <= 0)
            {
                MessageBox.Show("Ju lutem shkruani një sasi të vlefshme!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!decimal.TryParse(PurchasePriceTextBox.Text, out var price) || price <= 0)
            {
                MessageBox.Show("Ju lutem shkruani një çmim të vlefshëm!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var article = ArticleComboBox.SelectedItem as Article;
            if (article == null) return;
            
            // Check if item already exists
            var existingItem = _items.FirstOrDefault(i => i.ArticleId == article.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                existingItem.TotalValue = existingItem.Quantity * existingItem.PurchasePrice * (1 + existingItem.VATRate / 100);
                ItemsDataGrid.Items.Refresh();
            }
            else
            {
                var totalValue = quantity * price * (1 + article.VATRate / 100);
                
                _items.Add(new PurchaseItemDisplay
                {
                    ArticleId = article.Id,
                    ArticleName = article.Name,
                    Quantity = quantity,
                    PurchasePrice = price,
                    VATRate = article.VATRate,
                    TotalValue = totalValue
                });
            }
            
            // Update article purchase price
            using var context = new POSDbContext();
            var dbArticle = context.Articles.Find(article.Id);
            if (dbArticle != null)
            {
                dbArticle.PurchasePrice = price;
                context.SaveChanges();
            }
            
            UpdateTotals();
            
            // Reset inputs
            ArticleComboBox.SelectedIndex = -1;
            QuantityTextBox.Text = "1";
            PurchasePriceTextBox.Text = "0.00";
        }
        
        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var item = button?.DataContext as PurchaseItemDisplay;
            if (item != null)
            {
                _items.Remove(item);
                UpdateTotals();
            }
        }
        
        private void UpdateTotals()
        {
            var subtotal = _items.Sum(i => i.Quantity * i.PurchasePrice);
            var vat = _items.Sum(i => i.Quantity * i.PurchasePrice * (i.VATRate / 100));
            var total = subtotal + vat;
            
            SubtotalText.Text = $"{subtotal:N2} €";
            VATText.Text = $"{vat:N2} €";
            TotalText.Text = $"{total:N2} €";
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(DocumentNumberTextBox.Text))
            {
                MessageBox.Show("Ju lutem shkruani numrin e dokumentit!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (SupplierComboBox.SelectedValue == null)
            {
                MessageBox.Show("Ju lutem zgjidhni furnizuesin!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (_items.Count == 0)
            {
                MessageBox.Show("Ju lutem shtoni të paktën një artikull!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                using var context = new POSDbContext();
                
                Purchase purchase;
                if (_purchase != null)
                {
                    purchase = context.Purchases.Include(p => p.Items).FirstOrDefault(p => p.Id == _purchase.Id);
                    if (purchase == null) return;
                    
                    // Remove old items
                    context.PurchaseItems.RemoveRange(purchase.Items);
                }
                else
                {
                    purchase = new Purchase();
                    context.Purchases.Add(purchase);
                }
                
                purchase.DocumentNumber = DocumentNumberTextBox.Text;
                purchase.Date = DatePicker.SelectedDate ?? DateTime.Now;
                purchase.SupplierId = (int)SupplierComboBox.SelectedValue;
                purchase.PurchaseType = PurchaseTypeComboBox.Text;
                purchase.IsPaid = IsPaidCheckBox.IsChecked ?? false;
                
                var subtotal = _items.Sum(i => i.Quantity * i.PurchasePrice);
                var vat = _items.Sum(i => i.Quantity * i.PurchasePrice * (i.VATRate / 100));
                
                purchase.TotalAmount = subtotal + vat;
                purchase.VATAmount = vat;
                
                // Add items and update stock
                foreach (var item in _items)
                {
                    var purchaseItem = new PurchaseItem
                    {
                        ArticleId = item.ArticleId,
                        Quantity = item.Quantity,
                        PurchasePrice = item.PurchasePrice,
                        VATRate = item.VATRate,
                        TotalValue = item.TotalValue
                    };
                    purchase.Items.Add(purchaseItem);
                    
                    // Update article stock
                    var article = context.Articles.Find(item.ArticleId);
                    if (article != null)
                    {
                        article.StockQuantity += item.Quantity;
                    }
                }
                
                context.SaveChanges();
                
                MessageBox.Show("Blerja u ruajt me sukses!", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        public class PurchaseItemDisplay
        {
            public int ArticleId { get; set; }
            public string ArticleName { get; set; } = "";
            public decimal Quantity { get; set; }
            public decimal PurchasePrice { get; set; }
            public decimal VATRate { get; set; }
            public decimal TotalValue { get; set; }
        }
    }
}
