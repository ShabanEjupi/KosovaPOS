using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KosovaPOS.Database;
using KosovaPOS.Models;
using ClosedXML.Excel;
using System.IO;

namespace KosovaPOS.Windows
{
    public partial class BusinessPartnersWindow : Window
    {
        private List<BusinessPartner> _allPartners = new List<BusinessPartner>();
        
        public BusinessPartnersWindow()
        {
            InitializeComponent();
            
            // Setup partner types
            PartnerTypeComboBox.Items.Add("Të gjithë");
            PartnerTypeComboBox.Items.Add("Klient");
            PartnerTypeComboBox.Items.Add("Furnizues");
            PartnerTypeComboBox.Items.Add("Të dy");
            PartnerTypeComboBox.SelectedIndex = 0;
            
            LoadPartnersData();
        }
        
        private void LoadPartnersData()
        {
            try
            {
                using var context = new POSDbContext();
                
                _allPartners = context.BusinessPartners
                    .Where(bp => bp.IsActive)
                    .OrderBy(bp => bp.Name)
                    .ToList();
                
                ApplyFilters();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të të dhënave: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ApplyFilters()
        {
            var filtered = _allPartners.AsEnumerable();
            
            // Filter by type
            var selectedType = PartnerTypeComboBox.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedType) && selectedType != "Të gjithë")
            {
                filtered = filtered.Where(p => p.PartnerType == selectedType);
            }
            
            // Filter by search text
            var searchText = SearchTextBox.Text?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(p =>
                    p.Name.ToLower().Contains(searchText) ||
                    (p.NRF?.ToLower().Contains(searchText) ?? false) ||
                    (p.NUI?.ToLower().Contains(searchText) ?? false) ||
                    (p.Phone?.ToLower().Contains(searchText) ?? false));
            }
            
            PartnersDataGrid.ItemsSource = filtered.ToList();
        }
        
        private void UpdateSummary()
        {
            var totalPartners = _allPartners.Count;
            var totalCustomers = _allPartners.Count(p => p.PartnerType == "Klient" || p.PartnerType == "Të dy");
            var totalSuppliers = _allPartners.Count(p => p.PartnerType == "Furnizues" || p.PartnerType == "Të dy");
            var totalBalance = _allPartners.Sum(p => p.Balance);
            
            TotalPartnersText.Text = totalPartners.ToString();
            TotalCustomersText.Text = totalCustomers.ToString();
            TotalSuppliersText.Text = totalSuppliers.ToString();
            TotalBalanceText.Text = $"{totalBalance:N2} €";
        }
        
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }
        
        private void PartnerTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }
        
        private void NewPartner_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new BusinessPartnerEditWindow();
            if (dialog.ShowDialog() == true)
            {
                LoadPartnersData();
            }
        }
        
        private void EditPartner_Click(object sender, RoutedEventArgs e)
        {
            var selected = PartnersDataGrid.SelectedItem as BusinessPartner;
            if (selected == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një partner për ndryshim!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var dialog = new BusinessPartnerEditWindow(selected);
            if (dialog.ShowDialog() == true)
            {
                LoadPartnersData();
            }
        }
        
        private void DeletePartner_Click(object sender, RoutedEventArgs e)
        {
            var selected = PartnersDataGrid.SelectedItem as BusinessPartner;
            if (selected == null)
            {
                MessageBox.Show("Ju lutem zgjidhni një partner për fshirje!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var result = MessageBox.Show($"A jeni të sigurt që dëshironi të fshini partnerin '{selected.Name}'?",
                "Konfirmim", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var context = new POSDbContext();
                    var partner = context.BusinessPartners.Find(selected.Id);
                    if (partner != null)
                    {
                        partner.IsActive = false;
                        context.SaveChanges();
                        
                        MessageBox.Show("Partneri u fshi me sukses!", "Sukses",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        LoadPartnersData();
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
                var worksheet = workbook.Worksheets.Add("Partnerët Biznesorë");
                
                // Headers
                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "Emri";
                worksheet.Cell(1, 3).Value = "NRF";
                worksheet.Cell(1, 4).Value = "NUI";
                worksheet.Cell(1, 5).Value = "Adresa";
                worksheet.Cell(1, 6).Value = "Qyteti";
                worksheet.Cell(1, 7).Value = "Telefoni";
                worksheet.Cell(1, 8).Value = "Email";
                worksheet.Cell(1, 9).Value = "Lloji";
                worksheet.Cell(1, 10).Value = "Balanca (€)";
                
                var headerRange = worksheet.Range(1, 1, 1, 10);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                
                var partners = PartnersDataGrid.ItemsSource as List<BusinessPartner>;
                if (partners != null)
                {
                    int row = 2;
                    foreach (var partner in partners)
                    {
                        worksheet.Cell(row, 1).Value = partner.Id;
                        worksheet.Cell(row, 2).Value = partner.Name;
                        worksheet.Cell(row, 3).Value = partner.NRF ?? "";
                        worksheet.Cell(row, 4).Value = partner.NUI ?? "";
                        worksheet.Cell(row, 5).Value = partner.Address ?? "";
                        worksheet.Cell(row, 6).Value = partner.City ?? "";
                        worksheet.Cell(row, 7).Value = partner.Phone ?? "";
                        worksheet.Cell(row, 8).Value = partner.Email ?? "";
                        worksheet.Cell(row, 9).Value = partner.PartnerType;
                        worksheet.Cell(row, 10).Value = partner.Balance;
                        row++;
                    }
                }
                
                worksheet.Columns().AdjustToContents();
                
                var fileName = $"Partneret_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
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
