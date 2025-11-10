using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using KosovaPOS.Database;
using Microsoft.EntityFrameworkCore;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace KosovaPOS.Windows
{
    public partial class AnalyticsWindow : Window
    {
        private DateTime _startDate;
        private DateTime _endDate;

        public AnalyticsWindow()
        {
            InitializeComponent();
            SetPeriod(0); // Default to today
            LoadAllData();
        }

        private void SetPeriod(int index)
        {
            var now = DateTime.Now;
            _endDate = now;

            switch (index)
            {
                case 0: // Sot
                    _startDate = now.Date;
                    break;
                case 1: // Kjo javë
                    _startDate = now.Date.AddDays(-(int)now.DayOfWeek);
                    break;
                case 2: // Ky muaj
                    _startDate = new DateTime(now.Year, now.Month, 1);
                    break;
                case 3: // 3 muaj
                    _startDate = now.AddMonths(-3);
                    break;
                case 4: // 6 muaj
                    _startDate = now.AddMonths(-6);
                    break;
                case 5: // Ky vit
                    _startDate = new DateTime(now.Year, 1, 1);
                    break;
                case 6: // Të gjitha
                    _startDate = DateTime.MinValue;
                    break;
            }
        }

        private async void LoadAllData()
        {
            try
            {
                // Show loading indicator
                this.Cursor = System.Windows.Input.Cursors.Wait;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var context = new POSDbContext();

                    // Load data to memory first to avoid SQLite limitations
                    var receipts = context.Receipts
                        .Where(r => r.Date >= _startDate && r.Date <= _endDate)
                        .ToList();

                    var receiptItems = context.ReceiptItems
                        .Include(ri => ri.Article)
                        .Where(ri => ri.Receipt != null && ri.Receipt.Date >= _startDate && ri.Receipt.Date <= _endDate)
                        .ToList();

                    var purchases = context.Purchases
                        .Where(p => p.Date >= _startDate && p.Date <= _endDate)
                        .ToList();

                    Dispatcher.Invoke(() =>
                    {
                        LoadSalesAnalysis(receipts, receiptItems);
                        LoadProductAnalysis(receiptItems);
                        LoadFinancialAnalysis(receipts, purchases);
                    });
                });

                // Load inventory analysis separately to avoid blocking
                await LoadInventoryAnalysisAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ngarkimit të të dhënave: {ex.Message}\n\nDetaje: {ex.InnerException?.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide loading indicator
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private void LoadSalesAnalysis(List<Models.Receipt> receipts, List<Models.ReceiptItem> receiptItems)
        {
            // KPIs
            var totalSales = receipts.Sum(r => (double)r.TotalAmount);
            var totalTransactions = receipts.Count;
            var avgTransaction = totalTransactions > 0 ? totalSales / totalTransactions : 0;
            var totalItemsSold = receiptItems.Sum(ri => (double)ri.Quantity);
            var uniqueProducts = receiptItems.Select(ri => ri.ArticleId).Distinct().Count();

            TotalSalesKPI.Text = $"{totalSales:N2} €";
            TotalTransactionsKPI.Text = totalTransactions.ToString("N0");
            AvgTransactionKPI.Text = $"Mesatare: {avgTransaction:N2} €";
            TotalItemsSoldKPI.Text = totalItemsSold.ToString("N0");
            UniqueProductsKPI.Text = $"{uniqueProducts} produkte unike";

            // Sales Trend Chart
            var salesByDay = receipts
                .GroupBy(r => r.Date.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(r => (double)r.TotalAmount) })
                .OrderBy(x => x.Date)
                .ToList();

            if (salesByDay.Any())
            {
                SalesTrendChart.Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = salesByDay.Select(x => x.Total).ToArray(),
                        Name = "Shitjet (€)",
                        Fill = null,
                        GeometrySize = 8,
                        Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 3 }
                    }
                };

                SalesTrendChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = salesByDay.Select(x => x.Date.ToString("dd/MM")).ToArray(),
                        LabelsRotation = 45
                    }
                };
            }

            // Hourly Sales Pattern
            var hourlyPattern = receipts
                .GroupBy(r => r.Date.Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(r => (double)r.TotalAmount) })
                .OrderBy(x => x.Hour)
                .ToList();

            if (hourlyPattern.Any())
            {
                var peakHour = hourlyPattern.OrderByDescending(x => x.Total).First();
                PeakHoursText.Text = $"Orët më të ngarkuara: {peakHour.Hour}:00 - {peakHour.Hour + 1}:00 ({peakHour.Total:N2} €)";
                
                HourlySalesChart.Series = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = Enumerable.Range(0, 24).Select(hour =>
                            hourlyPattern.FirstOrDefault(x => x.Hour == hour)?.Total ?? 0).ToArray(),
                        Name = "Shitjet (€)",
                        Fill = new SolidColorPaint(SKColors.DodgerBlue)
                    }
                };

                HourlySalesChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = Enumerable.Range(0, 24).Select(h => $"{h}:00").ToArray(),
                        LabelsRotation = 45
                    }
                };
            }
            else
            {
                PeakHoursText.Text = "Orët më të ngarkuara: Nuk ka të dhëna";
            }
        }

        private void LoadProductAnalysis(List<Models.ReceiptItem> receiptItems)
        {
            // Top 10 Products
            var topProducts = receiptItems
                .Where(ri => ri.Article != null && ri.Article.Name != null)
                .GroupBy(ri => new { ri.ArticleId, ri.Article!.Name })
                .Select(g => new
                {
                    ProductName = g.Key.Name,
                    Quantity = g.Sum(ri => (double)ri.Quantity),
                    Revenue = g.Sum(ri => (double)ri.TotalValue)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToList();

            if (topProducts.Any())
            {
                TopProductsChart.Series = new ISeries[]
                {
                    new RowSeries<double>
                    {
                        Values = topProducts.Select(x => x.Revenue).ToArray(),
                        Name = "Të ardhurat (€)",
                        Fill = new SolidColorPaint(SKColors.Orange),
                        DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                        DataLabelsSize = 12,
                        DataLabelsFormatter = point => $"{point.Model:N0}€"
                    }
                };

                TopProductsChart.YAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = topProducts.Select(x =>
                            x.ProductName.Length > 20 ? x.ProductName.Substring(0, 17) + "..." : x.ProductName).ToArray()
                    }
                };
            }

            // Category Distribution
            var categoryData = receiptItems
                .Where(ri => ri.Article != null)
                .GroupBy(ri => ri.Article!.Category ?? "Pa kategori")
                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Sum(ri => (double)ri.TotalValue)
                })
                .OrderByDescending(x => x.Total)
                .Take(8)
                .ToList();

            if (categoryData.Any())
            {
                CategoryPieChart.Series = categoryData.Select(x => new PieSeries<double>
                {
                    Values = new[] { x.Total },
                    Name = x.Category,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsSize = 12,
                    DataLabelsFormatter = point => $"{point.Model:N0}€"
                }).ToArray();
            }

            // Product Performance Grid - Use a proper class instead of anonymous type
            var productPerformance = receiptItems
                .Where(ri => ri.Article != null && ri.Article.Name != null)
                .GroupBy(ri => new { ri.ArticleId, ri.Article!.Name, ri.Article.PurchasePrice })
                .Select(g =>
                {
                    var quantity = g.Sum(ri => (double)ri.Quantity);
                    var revenue = g.Sum(ri => (double)ri.TotalValue);
                    var cost = quantity * (double)g.Key.PurchasePrice;
                    var profit = revenue - cost;
                    var margin = revenue > 0 ? (profit / revenue) * 100 : 0;

                    return new ProductPerformance
                    {
                        ProductName = g.Key.Name,
                        Quantity = quantity,
                        Revenue = revenue,
                        Cost = cost,
                        Profit = profit,
                        Margin = margin
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .Take(20)
                .ToList();

            ProductPerformanceGrid.ItemsSource = productPerformance;
        }

        // Helper class for product performance
        private class ProductPerformance
        {
            public string ProductName { get; set; } = string.Empty;
            public double Quantity { get; set; }
            public double Revenue { get; set; }
            public double Cost { get; set; }
            public double Profit { get; set; }
            public double Margin { get; set; }
        }

        private async System.Threading.Tasks.Task LoadInventoryAnalysisAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var context = new POSDbContext();
                    var articles = context.Articles.Where(a => a.IsActive).ToList();

                    // KPIs - Fix stock value calculation to use SalesPrice instead of PurchasePrice for inventory value
                    var totalStockValue = articles.Sum(a => (double)a.StockQuantity * (double)a.SalesPrice);
                    var lowStockItems = articles.Count(a => a.StockQuantity < 10);
                    
                    // Slow moving items: have stock but haven't sold much (low StockOut)
                    var slowMovingItems = articles.Count(a => a.StockQuantity > 20 && a.StockOut < 5);

                    Dispatcher.Invoke(() =>
                    {
                        TotalStockValueKPI.Text = $"{totalStockValue:N2} €";
                        LowStockItemsKPI.Text = $"{lowStockItems} artikuj";
                        SlowMovingItemsKPI.Text = $"{slowMovingItems} artikuj";
                    });

                    // Stock Value by Category
                    var stockByCategory = articles
                        .Where(a => a.StockQuantity > 0)
                        .GroupBy(a => a.Category ?? "Pa kategori")
                        .Select(g => new
                        {
                            Category = g.Key,
                            Value = g.Sum(a => (double)a.StockQuantity * (double)a.SalesPrice)
                        })
                        .OrderByDescending(x => x.Value)
                        .Take(10)
                        .ToList();

                    if (stockByCategory.Any())
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StockValueChart.Series = new ISeries[]
                            {
                                new ColumnSeries<double>
                                {
                                    Values = stockByCategory.Select(x => x.Value).ToArray(),
                                    Name = "Vlera (€)",
                                    Fill = new SolidColorPaint(SKColors.Teal),
                                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                                    DataLabelsSize = 11,
                                    DataLabelsFormatter = point => $"{point.Model:N0}€"
                                }
                            };

                            StockValueChart.XAxes = new Axis[]
                            {
                                new Axis
                                {
                                    Labels = stockByCategory.Select(x => 
                                        x.Category.Length > 15 ? x.Category.Substring(0, 12) + "..." : x.Category).ToArray(),
                                    LabelsRotation = 45
                                }
                            };
                        });
                    }

                    // Low Stock Grid - Get articles with stock below 10, ordered by quantity
                    var lowStockArticles = articles
                        .Where(a => a.StockQuantity < 10)
                        .OrderBy(a => a.StockQuantity)
                        .Take(50)
                        .Select(a => new
                        {
                            a.Barcode,
                            a.Name,
                            a.StockQuantity,
                            Category = a.Category ?? "Pa kategori"
                        })
                        .ToList();

                    Dispatcher.Invoke(() =>
                    {
                        LowStockGrid.ItemsSource = lowStockArticles;
                    });
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Gabim gjatë ngarkimit të analizës së stoqit: {ex.Message}",
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void LoadFinancialAnalysis(List<Models.Receipt> receipts, List<Models.Purchase> purchases)
        {
            // Calculate profit (simplified - actual profit calculation would need cost basis)
            var totalRevenue = receipts.Sum(r => (double)r.TotalAmount);
            var totalCost = purchases.Sum(p => (double)p.TotalAmount);
            var netProfit = totalRevenue - totalCost;
            var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;

            NetProfitKPI.Text = $"{netProfit:N2} €";
            ProfitMarginKPI.Text = $"Marzha: {profitMargin:N1}%";

            // Revenue vs Cost by Day
            var revenueByDay = receipts
                .GroupBy(r => r.Date.Date)
                .Select(g => new { Date = g.Key, Amount = g.Sum(r => (double)r.TotalAmount) })
                .OrderBy(x => x.Date)
                .ToList();

            var costByDay = purchases
                .GroupBy(p => p.Date.Date)
                .Select(g => new { Date = g.Key, Amount = g.Sum(p => (double)p.TotalAmount) })
                .OrderBy(x => x.Date)
                .ToList();

            var allDates = revenueByDay.Select(x => x.Date)
                .Union(costByDay.Select(x => x.Date))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (allDates.Any())
            {
                RevenueVsCostChart.Series = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = allDates.Select(date =>
                            revenueByDay.FirstOrDefault(x => x.Date == date)?.Amount ?? 0).ToArray(),
                        Name = "Të ardhurat",
                        Fill = new SolidColorPaint(SKColors.Green)
                    },
                    new ColumnSeries<double>
                    {
                        Values = allDates.Select(date =>
                            costByDay.FirstOrDefault(x => x.Date == date)?.Amount ?? 0).ToArray(),
                        Name = "Kostot",
                        Fill = new SolidColorPaint(SKColors.Red)
                    }
                };

                RevenueVsCostChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = allDates.Select(d => d.ToString("dd/MM")).ToArray(),
                        LabelsRotation = 45
                    }
                };

                // Profit Trend
                var profitByDay = allDates.Select(date => new
                {
                    Date = date,
                    Profit = (revenueByDay.FirstOrDefault(x => x.Date == date)?.Amount ?? 0) -
                            (costByDay.FirstOrDefault(x => x.Date == date)?.Amount ?? 0)
                }).ToList();

                ProfitTrendChart.Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = profitByDay.Select(x => x.Profit).ToArray(),
                        Name = "Fitimi (€)",
                        Fill = null,
                        GeometrySize = 8,
                        Stroke = new SolidColorPaint(SKColors.Purple) { StrokeThickness = 3 }
                    }
                };

                ProfitTrendChart.XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = profitByDay.Select(x => x.Date.ToString("dd/MM")).ToArray(),
                        LabelsRotation = 45
                    }
                };
            }
        }

        private void PeriodComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PeriodComboBox.SelectedIndex >= 0)
            {
                SetPeriod(PeriodComboBox.SelectedIndex);
                LoadAllData();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAllData();
            MessageBox.Show("Të dhënat u rifreskuan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Eksporto Analytics në Excel",
                    FileName = $"Analytics_{DateTime.Now:yyyyMMdd}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using var workbook = new XLWorkbook();
                    
                    // Create sheets for different analyses
                    var summarySheet = workbook.Worksheets.Add("Përmbledhje");
                    summarySheet.Cell(1, 1).Value = "Raporti analitik";
                    summarySheet.Cell(2, 1).Value = $"Periudha: {_startDate:dd/MM/yyyy} - {_endDate:dd/MM/yyyy}";
                    summarySheet.Cell(4, 1).Value = "Shitjet totale:";
                    summarySheet.Cell(4, 2).Value = TotalSalesKPI.Text;
                    summarySheet.Cell(5, 1).Value = "Transaksionet:";
                    summarySheet.Cell(5, 2).Value = TotalTransactionsKPI.Text;
                    summarySheet.Cell(6, 1).Value = "Fitimi neto:";
                    summarySheet.Cell(6, 2).Value = NetProfitKPI.Text;

                    // Add product performance data
                    if (ProductPerformanceGrid.ItemsSource != null)
                    {
                        var perfSheet = workbook.Worksheets.Add("Performanca e produkteve");
                        perfSheet.Cell(1, 1).Value = "Produkti";
                        perfSheet.Cell(1, 2).Value = "Sasia";
                        perfSheet.Cell(1, 3).Value = "Vlera";
                        perfSheet.Cell(1, 4).Value = "Fitimi";
                        perfSheet.Cell(1, 5).Value = "Marzha %";

                        int row = 2;
                        foreach (dynamic item in ProductPerformanceGrid.ItemsSource)
                        {
                            perfSheet.Cell(row, 1).Value = item.ProductName;
                            perfSheet.Cell(row, 2).Value = item.Quantity;
                            perfSheet.Cell(row, 3).Value = item.Revenue;
                            perfSheet.Cell(row, 4).Value = item.Profit;
                            perfSheet.Cell(row, 5).Value = item.Margin;
                            row++;
                        }
                    }

                    workbook.SaveAs(saveFileDialog.FileName);
                    MessageBox.Show("Raporti u eksportua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë eksportimit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                
                if (printDialog.ShowDialog() == true)
                {
                    // Create a FlowDocument for printing
                    var doc = new System.Windows.Documents.FlowDocument();
                    doc.PagePadding = new Thickness(50);
                    doc.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

                    // Title
                    var titlePara = new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("📊 RAPORTI ANALITIK")
                        {
                            FontSize = 24,
                            FontWeight = FontWeights.Bold
                        });
                    titlePara.TextAlignment = TextAlignment.Center;
                    doc.Blocks.Add(titlePara);

                    // Period
                    var periodPara = new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"Periudha: {_startDate:dd/MM/yyyy} - {_endDate:dd/MM/yyyy}")
                        {
                            FontSize = 14
                        });
                    periodPara.TextAlignment = TextAlignment.Center;
                    periodPara.Margin = new Thickness(0, 0, 0, 20);
                    doc.Blocks.Add(periodPara);

                    // Sales Summary
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("PËRMBLEDHJE E SHITJEVE")
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Shitjet totale: {TotalSalesKPI.Text}")
                        {
                            FontSize = 14
                        }));
                    
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Transaksionet: {TotalTransactionsKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• {AvgTransactionKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Artikujt e shitur: {TotalItemsSoldKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• {UniqueProductsKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    // Financial Summary
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("\nPËRMBLEDHJE FINANCIARE")
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Fitimi neto: {NetProfitKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• {ProfitMarginKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    // Inventory Summary
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run("\nPËRMBLEDHJE E STOKUT")
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Vlera totale: {TotalStockValueKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Stok i ulët: {LowStockItemsKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"• Pa lëvizje: {SlowMovingItemsKPI.Text}")
                        {
                            FontSize = 14
                        }));

                    // Footer
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph(
                        new System.Windows.Documents.Run($"\n\nRaport i gjeneruar më: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        {
                            FontSize = 10,
                            FontStyle = FontStyles.Italic
                        })
                    {
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    });

                    // Print the document
                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Raporti analitik");

                    MessageBox.Show("Raporti u printua me sukses!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë printimit të raportit: {ex.Message}", "Gabim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
