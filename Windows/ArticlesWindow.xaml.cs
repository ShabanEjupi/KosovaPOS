using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KosovaPOS.Database;
using KosovaPOS.Models;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace KosovaPOS.Windows
{
    public partial class ArticlesWindow : Window
    {
        private ObservableCollection<Article> _articles = new ObservableCollection<Article>();
        
        public ArticlesWindow()
        {
            InitializeComponent();
            LoadArticles();
        }
        
        private void LoadArticles()
        {
            try
            {
                using var context = new POSDbContext();
                var articles = context.Articles.Where(a => a.IsActive).ToList();
                _articles = new ObservableCollection<Article>(articles);
                ArticlesDataGrid.ItemsSource = _articles;
                
                // Update status
                this.Title = $"Artikujt - {articles.Count} artikuj";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të artikujve:\n\n{ex.Message}\n\nDetaje: {ex.InnerException?.Message}", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Initialize empty collection to prevent further errors
                _articles = new ObservableCollection<Article>();
                ArticlesDataGrid.ItemsSource = _articles;
            }
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt)
            {
                switch (e.Key)
                {
                    case Key.F:
                        if (ArticlesDataGrid.SelectedItem != null)
                            DeleteButton_Click(this, new RoutedEventArgs());
                        break;
                    case Key.E:
                        ExcelButton_Click(this, new RoutedEventArgs());
                        break;
                    case Key.R:
                        RefreshButton_Click(this, new RoutedEventArgs());
                        break;
                    case Key.N:
                        EditButton_Click(this, new RoutedEventArgs());
                        break;
                    case Key.O:
                        AddButton_Click(this, new RoutedEventArgs());
                        break;
                    case Key.B:
                        BarcodeButton_Click(this, new RoutedEventArgs());
                        break;
                    case Key.M:
                        CloseButton_Click(this, new RoutedEventArgs());
                        break;
                }
            }
        }
        
        private void ListOfArticles_Click(object sender, RoutedEventArgs e)
        {
            LoadArticles();
        }
        
        private void PrintListButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Printimi i listës (në zhvillim)", "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void ExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Ruaj listën si Excel",
                    FileName = $"Artikujt_{DateTime.Now:yyyyMMdd}.xlsx"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Artikujt");
                    
                    // Headers
                    worksheet.Cell(1, 1).Value = "Barkodi";
                    worksheet.Cell(1, 2).Value = "Emri i Artikullit";
                    worksheet.Cell(1, 3).Value = "Njësia";
                    worksheet.Cell(1, 4).Value = "Paketa";
                    worksheet.Cell(1, 5).Value = "Çmimi i Shitjes";
                    worksheet.Cell(1, 6).Value = "Kategoria";
                    worksheet.Cell(1, 7).Value = "Furnizuesi";
                    worksheet.Cell(1, 8).Value = "Stoku";
                    
                    // Style headers
                    var headerRange = worksheet.Range(1, 1, 1, 8);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    
                    // Data
                    int row = 2;
                    foreach (var article in _articles)
                    {
                        worksheet.Cell(row, 1).Value = article.Barcode;
                        worksheet.Cell(row, 2).Value = article.Name;
                        worksheet.Cell(row, 3).Value = article.Unit;
                        worksheet.Cell(row, 4).Value = article.Pack;
                        worksheet.Cell(row, 5).Value = article.SalesPrice;
                        worksheet.Cell(row, 6).Value = article.Category;
                        worksheet.Cell(row, 7).Value = article.Supplier;
                        worksheet.Cell(row, 8).Value = article.StockQuantity;
                        row++;
                    }
                    
                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();
                    
                    workbook.SaveAs(saveFileDialog.FileName);
                    
                    MessageBox.Show("Lista u eksportua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë eksportimit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadArticles();
        }
        
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if user has permission to manage articles
            var userSession = Services.UserSessionService.Instance;
            if (!userSession.CanManageArticles)
            {
                MessageBox.Show("Nuk keni të drejta për të fshirë artikuj!\nKontaktoni administratorin për të dhënë të drejta.", 
                    "Qasje e kufizuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (ArticlesDataGrid.SelectedItem is Article selectedArticle)
            {
                var result = MessageBox.Show($"A jeni i sigurt që doni ta fshini artikullin '{selectedArticle.Name}'?", 
                    "Konfirmimi", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    using var context = new POSDbContext();
                    var article = context.Articles.Find(selectedArticle.Id);
                    if (article != null)
                    {
                        article.IsActive = false;
                        context.SaveChanges();
                    }
                    
                    LoadArticles();
                    MessageBox.Show("Artikulli u fshi me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një artikull për ta fshirë!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if user has permission to manage articles
            var userSession = Services.UserSessionService.Instance;
            if (!userSession.CanManageArticles)
            {
                MessageBox.Show("Nuk keni të drejta për të ndryshuar artikuj!\nKontaktoni administratorin për të dhënë të drejta.", 
                    "Qasje e kufizuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (ArticlesDataGrid.SelectedItem is Article selectedArticle)
            {
                var editWindow = new ArticleEditWindow(selectedArticle);
                if (editWindow.ShowDialog() == true)
                {
                    LoadArticles();
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një artikull për ta ndryshuar!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if user has permission to manage articles
            var userSession = Services.UserSessionService.Instance;
            if (!userSession.CanManageArticles)
            {
                MessageBox.Show("Nuk keni të drejta për të shtuar artikuj!\nKontaktoni administratorin për të dhënë të drejta.", 
                    "Qasje e kufizuar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var addWindow = new ArticleEditWindow(null);
            if (addWindow.ShowDialog() == true)
            {
                LoadArticles();
            }
        }
        
        private void BarcodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ArticlesDataGrid.SelectedItem is Article selectedArticle)
            {
                if (int.TryParse(BarcodeCopiesTextBox.Text, out int copies) && copies > 0 && copies <= 1000)
                {
                    MessageBox.Show($"Printimi i {copies} kopjeve të barkodës për '{selectedArticle.Name}' (në zhvillim)", 
                        "Informacion", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Ju lutem shkruani një numër valid (1-1000) të kopjeve!", 
                        "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BarcodeCopiesTextBox.Focus();
                    BarcodeCopiesTextBox.SelectAll();
                }
            }
            else
            {
                MessageBox.Show("Zgjidhni një artikull për të printuar barkodën!", "Paralajmërim", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void BarcodeCopiesTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow numeric input
            e.Handled = !int.TryParse(e.Text, out _);
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
