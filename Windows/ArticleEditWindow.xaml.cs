using System;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;

namespace KosovaPOS.Windows
{
    public partial class ArticleEditWindow : Window
    {
        private Article? _article;
        private bool _isNewArticle;
        
        public ArticleEditWindow(Article? article)
        {
            InitializeComponent();
            _article = article;
            _isNewArticle = article == null;
            
            if (_isNewArticle)
            {
                Title = "Shto Artikull të Ri";
                _article = new Article();
            }
            else
            {
                Title = "Ndrysho Artikullin";
                LoadArticleData();
            }
        }
        
        private void LoadArticleData()
        {
            if (_article == null) return;
            
            BarcodeTextBox.Text = _article.Barcode;
            NameTextBox.Text = _article.Name;
            UnitTextBox.Text = _article.Unit;
            PackTextBox.Text = _article.Pack.ToString();
            PurchasePriceTextBox.Text = _article.PurchasePrice.ToString("F2");
            SalesPriceTextBox.Text = _article.SalesPrice.ToString("F2");
            VATRateComboBox.SelectedIndex = _article.VATRate == 18 ? 0 : 1;
            CategoryTextBox.Text = _article.Category;
            SupplierTextBox.Text = _article.Supplier;
            StockQuantityTextBox.Text = _article.StockQuantity.ToString();
            MinimumStockTextBox.Text = _article.MinimumStock.ToString();
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;
            
            if (_article == null) return;
            
            _article.Barcode = BarcodeTextBox.Text.Trim();
            _article.Name = NameTextBox.Text.Trim();
            _article.Unit = UnitTextBox.Text.Trim();
            _article.Pack = decimal.Parse(PackTextBox.Text);
            _article.PurchasePrice = decimal.Parse(PurchasePriceTextBox.Text);
            _article.SalesPrice = decimal.Parse(SalesPriceTextBox.Text);
            _article.VATRate = VATRateComboBox.SelectedIndex == 0 ? 18 : 8;
            _article.Category = CategoryTextBox.Text.Trim();
            _article.Supplier = SupplierTextBox.Text.Trim();
            _article.StockQuantity = decimal.Parse(StockQuantityTextBox.Text);
            _article.MinimumStock = decimal.Parse(MinimumStockTextBox.Text);
            _article.UpdatedAt = DateTime.Now;
            
            using var context = new POSDbContext();
            
            if (_isNewArticle)
            {
                _article.CreatedAt = DateTime.Now;
                context.Articles.Add(_article);
            }
            else
            {
                context.Articles.Update(_article);
            }
            
            context.SaveChanges();
            
            DialogResult = true;
            Close();
        }
        
        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(BarcodeTextBox.Text))
            {
                MessageBox.Show("Ju lutem shkruani barkodën!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Ju lutem shkruani emrin e artikullit!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (!decimal.TryParse(SalesPriceTextBox.Text, out _))
            {
                MessageBox.Show("Çmimi i shitjes duhet të jetë numër!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            return true;
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
