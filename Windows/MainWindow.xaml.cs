using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KosovaPOS.Database;
using KosovaPOS.Helpers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace KosovaPOS.Windows
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        
        // Track open windows to prevent duplicates
        private static Dictionary<string, Window> _openWindows = new Dictionary<string, Window>();
        
        public MainWindow()
        {
            InitializeComponent();
            InitializeWindow();
        }
        
        private void InitializeWindow()
        {
            // Load business name from environment
            var businessName = Environment.GetEnvironmentVariable("BUSINESS_NAME");
            if (!string.IsNullOrEmpty(businessName))
            {
                BusinessNameText.Text = businessName.ToUpper();
            }
            
            // Start clock
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            UpdateDateTime();
        }
        
        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateDateTime();
        }
        
        private void UpdateDateTime()
        {
            DateTimeText.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy - HH:mm:ss");
        }
        
        private void ShowOrFocusWindow<T>(string windowKey, Func<T> createWindow) where T : Window
        {
            // Check if window is already open
            if (_openWindows.ContainsKey(windowKey))
            {
                var existingWindow = _openWindows[windowKey];
                if (existingWindow != null && existingWindow.IsLoaded)
                {
                    // Bring existing window to front
                    existingWindow.Activate();
                    existingWindow.Focus();
                    
                    // If minimized, restore it
                    if (existingWindow.WindowState == WindowState.Minimized)
                    {
                        existingWindow.WindowState = WindowState.Normal;
                    }
                    
                    return;
                }
                else
                {
                    // Window was closed, remove from dictionary
                    _openWindows.Remove(windowKey);
                }
            }
            
            // Create new window
            var window = createWindow();
            _openWindows[windowKey] = window;
            
            // Remove from dictionary when closed
            window.Closed += (s, e) =>
            {
                if (_openWindows.ContainsKey(windowKey))
                {
                    _openWindows.Remove(windowKey);
                }
            };
            
            window.ShowDialog();
        }
        
        private void CashRegister_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("CashRegister", () => new CashRegisterWindow());
        }
        
        private void Articles_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("Articles", () => new ArticlesWindow());
        }
        
        private void Sales_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("Sales", () => new SalesWindow());
        }
        
        private void Purchases_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("Purchases", () => new PurchasesWindow());
        }
        
        private void BusinessPartners_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("BusinessPartners", () => new BusinessPartnersWindow());
        }
        
        private void Reports_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("Reports", () => new ReportsWindow());
        }
        
        private void Analytics_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("Analytics", () => new AnalyticsWindow());
        }
        
        private void Settings_Click(object sender, MouseButtonEventArgs e)
        {
            ShowOrFocusWindow("Settings", () => new SettingsWindow());
        }
        
        private async void ImportData_Click(object sender, MouseButtonEventArgs e)
        {
            var result = MessageBox.Show(
                "A jeni i sigurt që doni të importoni të dhënat nga script.sql?\n\n" +
                "PARALAJMËRIM: Ky proces mund të zgjasë disa minuta dhe do të shtojë të dhëna në databazë.\n\n" +
                "Sigurohuni që skedari script.sql është në dosjen e projektit.",
                "Konfirmimi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var scriptPath = @"C:\Users\Administrator\POS\script.sql";
                    
                    if (!System.IO.File.Exists(scriptPath))
                    {
                        MessageBox.Show($"Skedari script.sql nuk u gjet në:\n{scriptPath}", 
                            "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    // Show progress window
                    var progressWindow = new Window
                    {
                        Title = "Importimi i të Dhënave",
                        Width = 400,
                        Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        ResizeMode = ResizeMode.NoResize
                    };
                    
                    var stack = new System.Windows.Controls.StackPanel
                    {
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    var label = new System.Windows.Controls.TextBlock
                    {
                        Text = "Duke importuar të dhënat...\nJu lutem prisni.",
                        TextAlignment = TextAlignment.Center,
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    
                    var progressBar = new System.Windows.Controls.ProgressBar
                    {
                        IsIndeterminate = true,
                        Height = 20,
                        Width = 300
                    };
                    
                    stack.Children.Add(label);
                    stack.Children.Add(progressBar);
                    progressWindow.Content = stack;
                    
                    progressWindow.Show();
                    
                    // Run import in background
                    await Task.Run(async () =>
                    {
                        using (var context = new POSDbContext())
                        {
                            var importer = new SqlServerToSQLiteImporter(context);
                            await importer.ImportArticlesFromSqlScript(scriptPath);
                        }
                    });
                    
                    progressWindow.Close();
                    
                    MessageBox.Show("Importimi përfundoi me sukses!", "Sukses", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gabim gjatë importimit:\n{ex.Message}", "Gabim", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void Exit_Click(object sender, MouseButtonEventArgs e)
        {
            var result = MessageBox.Show("A jeni i sigurt që doni ta mbyllni aplikacionin?", 
                "Konfirmimi", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
        
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Exit_Click(this, null!);
        }
    }
}
