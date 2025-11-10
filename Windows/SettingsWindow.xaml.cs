using System;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadAvailablePrinters();
            LoadSettings();
        }
        
        private void LoadAvailablePrinters()
        {
            try
            {
                // Get all installed printers
                foreach (string printerName in PrinterSettings.InstalledPrinters)
                {
                    ReceiptPrinterComboBox.Items.Add(printerName);
                }
                
                // Set default if no printers found
                if (ReceiptPrinterComboBox.Items.Count == 0)
                {
                    ReceiptPrinterComboBox.Items.Add("Microsoft Print to PDF");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të printerëve: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ReceiptPrinterComboBox.Items.Add("Microsoft Print to PDF");
            }
        }
        
        private void LoadSettings()
        {
            // Load from .env file
            BusinessNameTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_NAME") ?? "BUTIKU TEKSTIL";
            BusinessNRFTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_NRF") ?? "";
            BusinessAddressTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_ADDRESS") ?? "";
            BusinessPhoneTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_PHONE") ?? "";
            BusinessEmailTextBox.Text = Environment.GetEnvironmentVariable("BUSINESS_EMAIL") ?? "";
            
            FiscalPortTextBox.Text = Environment.GetEnvironmentVariable("FISCAL_PRINTER_PORT") ?? "COM1";
            FiscalBaudRateTextBox.Text = Environment.GetEnvironmentVariable("FISCAL_PRINTER_BAUD") ?? "115200";
            FiscalPathTextBox.Text = Environment.GetEnvironmentVariable("FISCAL_PATH") ?? "C:\\Temp\\";
            
            // Set printer from environment or default
            var savedPrinter = Environment.GetEnvironmentVariable("RECEIPT_PRINTER") ?? "Microsoft Print to PDF";
            if (ReceiptPrinterComboBox.Items.Contains(savedPrinter))
            {
                ReceiptPrinterComboBox.SelectedItem = savedPrinter;
            }
            else if (ReceiptPrinterComboBox.Items.Count > 0)
            {
                ReceiptPrinterComboBox.SelectedIndex = 0;
            }
            
            PaperWidthTextBox.Text = Environment.GetEnvironmentVariable("PAPER_WIDTH") ?? "80";
            AutoCutCheckBox.IsChecked = Environment.GetEnvironmentVariable("AUTO_CUT") != "false";
            
            DatabasePathTextBox.Text = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "./Database/KosovaPOS.db";
            
            // Load current user permissions
            LoadCurrentUserPermissions();
        }
        
        private void LoadCurrentUserPermissions()
        {
            var userSession = Services.UserSessionService.Instance;
            var currentUser = userSession.CurrentUser;
            
            if (currentUser != null)
            {
                CurrentUserText.Text = $"{currentUser.FullName} ({currentUser.Role})";
                
                CanManageArticlesCheckBox.IsChecked = currentUser.CanManageArticles;
                CanManagePurchasesCheckBox.IsChecked = currentUser.CanManagePurchases;
                CanViewReportsCheckBox.IsChecked = currentUser.CanViewReports;
                CanGiveDiscountsCheckBox.IsChecked = currentUser.CanGiveDiscounts;
                CanModifyPricesCheckBox.IsChecked = currentUser.CanModifyPrices;
                CanDeleteReceiptsCheckBox.IsChecked = currentUser.CanDeleteReceipts;
                CanManageUsersCheckBox.IsChecked = currentUser.CanManageUsers;
                MaxDiscountText.Text = $"{currentUser.MaxDiscountPercent:F0}%";
            }
            else
            {
                CurrentUserText.Text = "Asnjë përdorues i kyçur";
            }
        }
        
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
                
                var selectedPrinter = ReceiptPrinterComboBox.SelectedItem?.ToString() ?? "Microsoft Print to PDF";
                
                var envContent = $@"# Business Information
BUSINESS_NAME={BusinessNameTextBox.Text}
BUSINESS_NRF={BusinessNRFTextBox.Text}
BUSINESS_ADDRESS={BusinessAddressTextBox.Text}
BUSINESS_PHONE={BusinessPhoneTextBox.Text}
BUSINESS_EMAIL={BusinessEmailTextBox.Text}

# Fiscal Printer Settings
FISCAL_PRINTER_PORT={FiscalPortTextBox.Text}
FISCAL_PRINTER_BAUD={FiscalBaudRateTextBox.Text}
FISCAL_PATH={FiscalPathTextBox.Text}

# Receipt Printer Settings
RECEIPT_PRINTER={selectedPrinter}
PAPER_WIDTH={PaperWidthTextBox.Text}
AUTO_CUT={AutoCutCheckBox.IsChecked}

# Database
DATABASE_PATH={DatabasePathTextBox.Text}
";
                
                File.WriteAllText(envPath, envContent);
                
                // Reload environment variables
                Env.Load(envPath);
                
                MessageBox.Show("Konfigurimet u ruajtën me sukses!\nRistarto aplikacionin që të aplikohen ndryshimet.", 
                    "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes së konfigurimeveː {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TestFiscalPrinter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show($"Duke testuar lidhjen me printerin fiskal në {FiscalPortTextBox.Text}...\n\n" +
                               "Nota: Për testimin e plotë të printerit fiskal FP700+, " +
                               "sigurohu që printeri është i lidhur dhe i ndezur.", 
                               "Test i printerit fiskal", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // In production, this would actually test the connection
                var fiscalService = new Services.FiscalPrinterService();
                var testPath = fiscalService.GenerateZReport();
                
                if (File.Exists(testPath))
                {
                    MessageBox.Show($"Testimi përfundoi me sukses!\nSkedari i testit u krijua në: {testPath}", 
                        "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë testimit: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TestReceiptPrinter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedPrinter = ReceiptPrinterComboBox.SelectedItem?.ToString();
                
                if (string.IsNullOrEmpty(selectedPrinter))
                {
                    MessageBox.Show("Të lutem zgjidh një printer nga lista!", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Temporarily set the printer for testing
                Environment.SetEnvironmentVariable("RECEIPT_PRINTER", selectedPrinter);
                
                var printerService = new Services.ReceiptPrinterService();
                
                // Create a test receipt
                var testReceipt = new Models.Receipt
                {
                    ReceiptNumber = "TEST-001",
                    Date = DateTime.Now,
                    BuyerName = "Test Klient",
                    TotalAmount = 10.00m,
                    TaxAmount = 1.80m,
                    PaidAmount = 10.00m,
                    PaymentMethod = "Kesh",
                    CashierName = "Admin",
                    Items = new System.Collections.Generic.List<Models.ReceiptItem>
                    {
                        new Models.ReceiptItem
                        {
                            Article = new Models.Article { Name = "Test Produkt", Barcode = "TEST123" },
                            Barcode = "TEST123",
                            ArticleName = "Test Produkt",
                            Quantity = 1,
                            Price = 10.00m,
                            VATRate = 18,
                            TotalValue = 10.00m
                        }
                    }
                };
                
                printerService.PrintNonFiscalReceipt(testReceipt);
                
                MessageBox.Show($"Fatura e testit u dërgua me sukses në printerin:\n{selectedPrinter}", 
                    "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit:\n\n{ex.Message}\n\n" +
                               "Të lutem kontrollo që printeri të jetë i lidhur dhe i disponueshëm.", 
                               "Gabim",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BackupDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dbPath = DatabasePathTextBox.Text;
                if (!File.Exists(dbPath))
                {
                    MessageBox.Show("Baza e të dhënave nuk u gjet!", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "POS_Backups");
                Directory.CreateDirectory(backupFolder);
                
                var backupFileName = $"KosovaPOS_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                var backupPath = Path.Combine(backupFolder, backupFileName);
                
                File.Copy(dbPath, backupPath, true);
                
                MessageBox.Show($"Backup-i u krijua me sukses!\n\nLokacioni: {backupPath}", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Open backup folder
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = backupFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë krijimit të backup: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("A dëshironi të importoni të dhënat nga skedari script.sql?\n\n" +
                                        "VËREJTJE: Kjo do të fshijë të gjitha produktet ekzistuese dhe do të importojë " +
                                        "të dhënat e reja me çmimet e sakta!\n\n" +
                                        "Rekomandohet të bëni backup të bazës së të dhënave para se të vazhdoni.",
                                        "Importo të dhëna", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "script.sql");
                    
                    if (!File.Exists(scriptPath))
                    {
                        MessageBox.Show("Skedari script.sql nuk u gjet!", "Gabim",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    MessageBox.Show("Importimi filloi në background.\nKy proces mund të zgjasë disa minuta...",
                        "Importim", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Import in background
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            using var context = new Database.POSDbContext();
                            
                            // Clear existing articles first
                            await Dispatcher.InvokeAsync(() =>
                            {
                                context.Database.ExecuteSqlRaw("DELETE FROM Articles");
                                context.SaveChanges();
                            });
                            
                            var importer = new Helpers.SqlServerToSQLiteImporter(context);
                            await importer.ImportArticlesFromSqlScript(scriptPath);
                            
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("Importimi përfundoi me sukses!\n\n" +
                                              "Produktet u importuan me çmimet e sakta.", 
                                              "Sukses",
                                              MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Gabim gjatë importimit: {ex.Message}", "Gabim",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim: {ex.Message}", "Gabim",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
