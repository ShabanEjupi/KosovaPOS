using System;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using KosovaPOS.Models;
using KosovaPOS.Database;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace KosovaPOS.Helpers
{
    public class SqlServerToSQLiteImporter
    {
        private readonly POSDbContext _context;
        private int _correctionCount = 0;
        private List<string> _validationWarnings = new List<string>();
        private HashSet<string> _usedBarcodes = new HashSet<string>();
        private int _duplicateBarcodeCount = 0;
        
        public SqlServerToSQLiteImporter(POSDbContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Import all data from SQL Server script file
        /// </summary>
        public async Task<(int articles, int partners, int purchases, int receipts)> ImportAllDataFromSqlScript(string scriptPath)
        {
            int articlesCount = await ImportArticlesFromSqlScript(scriptPath);
            int partnersCount = await ImportBusinessPartnersFromSqlScript(scriptPath);
            int purchasesCount = await ImportPurchasesFromSqlScript(scriptPath);
            int receiptsCount = await ImportReceiptsFromSqlScript(scriptPath);
            
            return (articlesCount, partnersCount, purchasesCount, receiptsCount);
        }
        
        /// <summary>
        /// Parse and import business partners from SQL Server script file
        /// </summary>
        public async Task<int> ImportBusinessPartnersFromSqlScript(string scriptPath)
        {
            int importedCount = 0;
            
            try
            {
                Console.WriteLine("\n========================================");
                Console.WriteLine("Importing Business Partners...");
                Console.WriteLine("========================================");
                
                using (var reader = new StreamReader(scriptPath))
                {
                    string line;
                    string currentInsert = "";
                    int lineNumber = 0;
                    
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        lineNumber++;
                        
                        if (line.Contains("INSERT [dbo].[FurnitoriNew]"))
                        {
                            currentInsert = line;
                            
                            if (currentInsert.Contains(") VALUES (") && currentInsert.TrimEnd().EndsWith(")"))
                            {
                                var partner = ParseBusinessPartnerFromInsert(currentInsert);
                                if (partner != null)
                                {
                                    _context.BusinessPartners.Add(partner);
                                    importedCount++;
                                    
                                    if (importedCount % 100 == 0)
                                    {
                                        await _context.SaveChangesAsync();
                                        Console.WriteLine($"Imported {importedCount} business partners...");
                                    }
                                }
                                currentInsert = "";
                            }
                        }
                        else if (!string.IsNullOrEmpty(currentInsert))
                        {
                            currentInsert += " " + line.Trim();
                            
                            if (currentInsert.TrimEnd().EndsWith(")"))
                            {
                                var partner = ParseBusinessPartnerFromInsert(currentInsert);
                                if (partner != null)
                                {
                                    _context.BusinessPartners.Add(partner);
                                    importedCount++;
                                    
                                    if (importedCount % 100 == 0)
                                    {
                                        await _context.SaveChangesAsync();
                                        Console.WriteLine($"Imported {importedCount} business partners...");
                                    }
                                }
                                currentInsert = "";
                            }
                        }
                    }
                }
                
                await _context.SaveChangesAsync();
                Console.WriteLine($"Business Partners import complete: {importedCount} partners\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing business partners: {ex.Message}");
            }
            
            return importedCount;
        }
        
        /// <summary>
        /// Parse and import purchases from SQL Server script file
        /// </summary>
        public async Task<int> ImportPurchasesFromSqlScript(string scriptPath)
        {
            int importedCount = 0;
            var purchaseGroups = new Dictionary<string, List<PurchaseItemData>>();
            
            try
            {
                Console.WriteLine("\n========================================");
                Console.WriteLine("Importing Purchase Receipts...");
                Console.WriteLine("========================================");
                
                // First pass: collect all purchase items grouped by document number
                using (var reader = new StreamReader(scriptPath))
                {
                    string line;
                    string currentInsert = "";
                    
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Contains("INSERT [dbo].[DitariH]"))
                        {
                            currentInsert = line;
                            
                            if (currentInsert.Contains(") VALUES (") && currentInsert.TrimEnd().EndsWith(")"))
                            {
                                var item = ParsePurchaseItemFromInsert(currentInsert);
                                if (item != null)
                                {
                                    if (!purchaseGroups.ContainsKey(item.DocumentNumber))
                                    {
                                        purchaseGroups[item.DocumentNumber] = new List<PurchaseItemData>();
                                    }
                                    purchaseGroups[item.DocumentNumber].Add(item);
                                }
                                currentInsert = "";
                            }
                        }
                        else if (!string.IsNullOrEmpty(currentInsert))
                        {
                            currentInsert += " " + line.Trim();
                            
                            if (currentInsert.TrimEnd().EndsWith(")"))
                            {
                                var item = ParsePurchaseItemFromInsert(currentInsert);
                                if (item != null)
                                {
                                    if (!purchaseGroups.ContainsKey(item.DocumentNumber))
                                    {
                                        purchaseGroups[item.DocumentNumber] = new List<PurchaseItemData>();
                                    }
                                    purchaseGroups[item.DocumentNumber].Add(item);
                                }
                                currentInsert = "";
                            }
                        }
                    }
                }
                
                // Second pass: create Purchase records with their items
                Console.WriteLine($"Found {purchaseGroups.Count} unique purchase documents");
                
                foreach (var group in purchaseGroups)
                {
                    var firstItem = group.Value.First();
                    var purchase = new Purchase
                    {
                        DocumentNumber = group.Key,
                        Date = firstItem.Date,
                        SupplierId = firstItem.SupplierId,
                        PurchaseType = firstItem.PurchaseType,
                        TotalAmount = group.Value.Sum(i => i.TotalValue),
                        VATAmount = group.Value.Sum(i => i.VATValue),
                        IsPaid = firstItem.IsPaid
                    };
                    
                    foreach (var itemData in group.Value)
                    {
                        // Find the article by barcode
                        var article = await _context.Articles.FirstOrDefaultAsync(a => a.Barcode == itemData.Barcode);
                        if (article != null)
                        {
                            purchase.Items.Add(new PurchaseItem
                            {
                                ArticleId = article.Id,
                                Quantity = itemData.Quantity,
                                PurchasePrice = itemData.PurchasePrice,
                                VATRate = itemData.VATRate,
                                TotalValue = itemData.TotalValue
                            });
                            
                            // Update article stock if needed
                            if (article.StockQuantity == 0 && itemData.Quantity > 0)
                            {
                                article.StockQuantity = itemData.Quantity;
                            }
                        }
                    }
                    
                    if (purchase.Items.Count > 0)
                    {
                        _context.Purchases.Add(purchase);
                        importedCount++;
                        
                        if (importedCount % 50 == 0)
                        {
                            await _context.SaveChangesAsync();
                            Console.WriteLine($"Imported {importedCount} purchase documents...");
                        }
                    }
                }
                
                await _context.SaveChangesAsync();
                Console.WriteLine($"Purchase receipts import complete: {importedCount} documents\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing purchases: {ex.Message}");
            }
            
            return importedCount;
        }
        
        private class PurchaseItemData
        {
            public string DocumentNumber { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public int SupplierId { get; set; }
            public string PurchaseType { get; set; } = string.Empty;
            public string Barcode { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal PurchasePrice { get; set; }
            public decimal VATRate { get; set; }
            public decimal VATValue { get; set; }
            public decimal TotalValue { get; set; }
            public bool IsPaid { get; set; }
        }
        
        /// <summary>
        /// Parse and import articles from SQL Server script file
        /// </summary>
        public async Task<int> ImportArticlesFromSqlScript(string scriptPath)
        {
            int importedCount = 0;
            
            try
            {
                Console.WriteLine("Reading SQL script...");
                
                // Read the script in chunks to handle large file
                var insertPattern = @"INSERT \[dbo\]\.\[Artikujt\] \(\[id\], \[Barkodi\], \[Emertimi\].*?\) VALUES \((.*?)\)";
                
                using (var reader = new StreamReader(scriptPath))
                {
                    string line;
                    string currentInsert = "";
                    int lineNumber = 0;
                    
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        lineNumber++;
                        
                        if (line.Contains("INSERT [dbo].[Artikujt]"))
                        {
                            currentInsert = line;
                            
                            // Check if the INSERT is complete on one line
                            if (currentInsert.Contains(") VALUES (") && currentInsert.TrimEnd().EndsWith(")"))
                            {
                                var article = ParseArticleFromInsert(currentInsert);
                                if (article != null)
                                {
                                    _context.Articles.Add(article);
                                    importedCount++;
                                    
                                    if (importedCount % 1000 == 0)
                                    {
                                        await _context.SaveChangesAsync();
                                        Console.WriteLine($"Imported {importedCount} articles...");
                                    }
                                }
                                currentInsert = "";
                            }
                        }
                        else if (!string.IsNullOrEmpty(currentInsert))
                        {
                            currentInsert += " " + line.Trim();
                            
                            if (currentInsert.TrimEnd().EndsWith(")"))
                            {
                                var article = ParseArticleFromInsert(currentInsert);
                                if (article != null)
                                {
                                    _context.Articles.Add(article);
                                    importedCount++;
                                    
                                    if (importedCount % 1000 == 0)
                                    {
                                        await _context.SaveChangesAsync();
                                        Console.WriteLine($"Imported {importedCount} articles...");
                                    }
                                }
                                currentInsert = "";
                            }
                        }
                        
                        if (lineNumber % 100000 == 0)
                        {
                            Console.WriteLine($"Processed {lineNumber} lines...");
                        }
                    }
                }
                
                // Save remaining changes
                await _context.SaveChangesAsync();
                Console.WriteLine($"\n========================================");
                Console.WriteLine($"Import complete!");
                Console.WriteLine($"Total articles imported: {importedCount}");
                Console.WriteLine($"Articles corrected: {_correctionCount}");
                Console.WriteLine($"Duplicate barcodes handled: {_duplicateBarcodeCount}");
                Console.WriteLine($"Validation warnings: {_validationWarnings.Count}");
                Console.WriteLine($"========================================\n");
                
                // Show sample of warnings
                if (_validationWarnings.Count > 0)
                {
                    Console.WriteLine("Sample validation warnings (first 10):");
                    foreach (var warning in _validationWarnings.Take(10))
                    {
                        Console.WriteLine($"  - {warning}");
                    }
                    if (_validationWarnings.Count > 10)
                    {
                        Console.WriteLine($"  ... and {_validationWarnings.Count - 10} more warnings");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing articles: {ex.Message}");
                throw;
            }
            
            return importedCount;
        }
        
        private Article? ParseArticleFromInsert(string insertStatement)
        {
            try
            {
                // Extract VALUES clause
                var valuesMatch = Regex.Match(insertStatement, @"VALUES \((.*)\)");
                if (!valuesMatch.Success) return null;
                
                var values = valuesMatch.Groups[1].Value;
                var parts = SplitSqlValues(values);
                
                if (parts.Count < 10) return null;
                
                // SQL Column mapping based on [Artikujt] table structure:
                // 0=id, 1=Barkodi (Barcode), 2=Emertimi (Name), 3=NjesiaP (Purchase Unit), 4=NjesiaSH (Sales Unit)
                // 5=Kategoria (Category), 6=PaBarkod (Without Barcode), 7=IRregullt (Irregular), 8=CFurnizimit (Purchase Price)
                // 9=Marzha (Margin), 10=Paketimi (Package), 11=CPaketimit (Package Price), 12=CShumices (Wholesale Price)
                // 13=CShitjes (Sales Price), 14=CShitjes1 (Sales Price 1), 15=Sasia (Stock Quantity), 16=Afati (Expiry Date)
                // 17=Furnitori (Supplier), 18=Verejtje (Notes), 19=Filiala (Branch), 20=Sektori (Sector)
                // 21=SasiaHyrje (Stock In), 22=SasiaDalje (Stock Out), 23=CMesatarShites (Avg Sales Price)
                // 24=CMesatarFurnizues (Avg Purchase Price), 25=Vendi (Location), 26=Prodhuesi (Brand)
                // 27=Importuesi (Importer), 28=tatiminr (VAT Type), 29=Tatimi (VAT Rate), 30=Shpenzim (Expense)
                // 31=Peshore (Weighed), 32=Vat (VAT), 33=Tipi (Product Type), 34=Foto (Photo), 35=KategoriaPos_ID
                
                var article = new Article
                {
                    // Don't set Id, let EF handle it
                    Barcode = CleanSqlString(parts[1]) ?? "N/A", // Barkodi with fallback
                    Name = CleanSqlString(parts[2]) ?? "Artikull pa emër", // Emertimi with fallback
                    Unit = CleanSqlString(parts[3]) ?? "Copë", // NjesiaP (Purchase Unit)
                    SalesUnit = CleanSqlString(parts[4]) ?? "Copë", // NjesiaSH (Sales Unit)
                    Category = CleanSqlString(parts[5]), // Kategoria
                    HasBarcode = CleanSqlString(parts[6]) != "Y", // PaBarkod - Y means WITHOUT barcode, so invert
                    IsRegular = CleanSqlString(parts[7]) != "True", // IRregullt - irregular, so invert
                    PurchasePrice = ParseDecimal(parts[8]), // CFurnizimit
                    Margin = ParseDecimal(parts[9]), // Marzha
                    Pack = ParseDecimal(parts[10]), // Paketimi
                    PackagePrice = ParseDecimal(parts[11]), // CPaketimit
                    WholesalePrice = ParseDecimal(parts[12]), // CShumices
                    SalesPrice = ParseDecimal(parts[13]), // CShitjes
                    SalesPrice1 = ParseDecimal(parts[14]), // CShitjes1
                    StockQuantity = ParseDecimal(parts[15]), // Sasia
                    ExpiryDate = null, // Afati - parts[16] if needed
                    SupplierId = 0,
                    Notes = CleanSqlString(parts[18]), // Verejtje
                    Branch = CleanSqlString(parts[19]), // Filiala
                    Sector = CleanSqlString(parts[20]), // Sektori
                    StockIn = ParseDecimal(parts[21]), // SasiaHyrje
                    StockOut = ParseDecimal(parts[22]), // SasiaDalje
                    AverageSalesPrice = ParseDecimal(parts[23]), // CMesatarShites
                    AveragePurchasePrice = ParseDecimal(parts[24]), // CMesatarFurnizues
                    Location = CleanSqlString(parts[25]), // Vendi
                    Brand = CleanSqlString(parts[26]), // Prodhuesi
                    Importer = CleanSqlString(parts[27]), // Importuesi
                    VATType = (int)ParseDecimal(parts[28]), // tatiminr
                    IsWeighed = parts.Count > 31 && CleanSqlString(parts[31]) == "1", // Peshore
                    ProductType = parts.Count > 33 ? (int)ParseDecimal(parts[33]) : 1, // Tipi
                    POSCategoryId = parts.Count > 35 ? ParseInt(parts[35]) : null, // KategoriaPos_ID
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                
                // Set VAT rate based on type
                article.VATRate = article.VATType switch
                {
                    3 => 18, // Standard rate
                    2 => 8,  // Reduced rate
                    1 => 0,  // Zero rate
                    _ => 18
                };
                
                // Data validation and correction
                ValidateAndCorrectArticle(article);
                
                return article;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing article: {ex.Message}");
                return null;
            }
        }
        
        private BusinessPartner? ParseBusinessPartnerFromInsert(string insertStatement)
        {
            try
            {
                var valuesMatch = Regex.Match(insertStatement, @"VALUES \((.*)\)");
                if (!valuesMatch.Success) return null;
                
                var values = valuesMatch.Groups[1].Value;
                var parts = SplitSqlValues(values);
                
                if (parts.Count < 10) return null;
                
                // FurnitoriNew structure:
                // 0=Id, 1=Emri (Name), 2=NRF, 3=NIT (NUI), 4=Personi (Contact Person), 5=Adresa (Address)
                // 6=Qyteti (City ID), 7=Telefoni (Phone), 8=Xhirollogaria (Bank Account), 9=Email
                // 10=F (is Supplier), 11=K (is Customer), 12=Data (Date), 13=Prejashtuar_TVSH (VAT Exempt)
                
                var name = CleanSqlString(parts[1]);
                if (string.IsNullOrWhiteSpace(name)) return null;
                
                var isSupplier = CleanSqlString(parts[10]) == "1";
                var isCustomer = CleanSqlString(parts[11]) == "1";
                
                var partnerType = (isSupplier && isCustomer) ? "Të dy" :
                                 isSupplier ? "Furnizues" : "Klient";
                
                return new BusinessPartner
                {
                    Name = name,
                    NRF = CleanSqlString(parts[2]),
                    NUI = CleanSqlString(parts[3]),
                    Address = CleanSqlString(parts[5]),
                    City = GetCityName(ParseInt(parts[6])),
                    Phone = CleanSqlString(parts[7]),
                    Email = CleanSqlString(parts[9]),
                    PartnerType = partnerType,
                    Balance = 0,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing business partner: {ex.Message}");
                return null;
            }
        }
        
        private PurchaseItemData? ParsePurchaseItemFromInsert(string insertStatement)
        {
            try
            {
                var valuesMatch = Regex.Match(insertStatement, @"VALUES \((.*)\)");
                if (!valuesMatch.Success) return null;
                
                var values = valuesMatch.Groups[1].Value;
                var parts = SplitSqlValues(values);
                
                if (parts.Count < 30) return null;
                
                // DitariH structure:
                // 0=ID, 1=DATA (Date), 2=ORA (Time), 3=NUMRI (Number), 4=KUPONI (Document Number), 5=NrFatures (Invoice Number)
                // 6=NrDUD, 7=FILIALA (Branch), 8=SUBJEKTI (Supplier ID), 9=PUNETORI (Worker), 10=TIPI (Type)
                // 11=BARKODI (Barcode), 12=ARTIKULLI (Article Name), 13=NJESIA (Unit), 14=Sasia (Quantity)
                // 15=Cmimi_Furn (Purchase Price), 16=Rabati_Per (Discount %), 17=Rabati_Vl (Discount Value)
                // 18=Vlera_Furn (Purchase Value), ... 25=Tvsh_Vl (VAT Value), 26=Tvsh_Per (VAT %), ...
                // 35=PerPagese (For Payment), 36=Pagoi (Paid), 37=Mbeti (Remaining), ...
                
                var documentNumber = CleanSqlString(parts[4]) ?? $"DOC-{ParseInt(parts[3])}";
                var barcode = CleanSqlString(parts[11]);
                if (string.IsNullOrWhiteSpace(barcode)) return null;
                
                var quantity = ParseDecimal(parts[14]);
                if (quantity <= 0) return null;
                
                var purchasePrice = ParseDecimal(parts[15]);
                var purchaseValue = ParseDecimal(parts[18]);
                var vatValue = ParseDecimal(parts[25]);
                var vatRate = ParseDecimal(parts[26]);
                
                // Try to parse date from ORA field (parts[2]) which contains readable date string
                // Format: N'09.07.2023 14:32:25' or similar
                DateTime date = DateTime.Now;
                var oraField = CleanSqlString(parts[2]);
                if (!string.IsNullOrWhiteSpace(oraField))
                {
                    // Try multiple date formats
                    if (DateTime.TryParseExact(oraField, new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "MM/dd/yyyy", "yyyy-MM-dd" },
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                    {
                        date = parsedDate;
                    }
                    else if (DateTime.TryParse(oraField, out parsedDate))
                    {
                        date = parsedDate;
                    }
                }
                
                // Fallback to DATA field (parts[1]) if ORA failed
                if (date == DateTime.Now)
                {
                    date = ParseSqlDate(parts[1]) ?? DateTime.Now;
                }
                
                var supplierId = ParseInt(parts[8]);
                var purchaseType = CleanSqlString(parts[10]) ?? "Vendore";
                
                var forPayment = ParseDecimal(parts[35]);
                var paid = ParseDecimal(parts[36]);
                var isPaid = paid >= forPayment;
                
                return new PurchaseItemData
                {
                    DocumentNumber = documentNumber,
                    Date = date,
                    SupplierId = supplierId,
                    PurchaseType = purchaseType,
                    Barcode = barcode,
                    Quantity = quantity,
                    PurchasePrice = purchasePrice,
                    VATRate = vatRate,
                    VATValue = vatValue,
                    TotalValue = purchaseValue,
                    IsPaid = isPaid
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing purchase item: {ex.Message}");
                return null;
            }
        }
        
        private string GetCityName(int cityId)
        {
            return cityId switch
            {
                9 => "Prishtinë",
                1 => "Mitrovicë",
                2 => "Pejë",
                3 => "Prizren",
                4 => "Gjakovë",
                5 => "Gjilan",
                6 => "Ferizaj",
                7 => "Podujevë",
                _ => "Tjetër"
            };
        }
        
        private List<string> SplitSqlValues(string values)
        {
            var parts = new List<string>();
            var current = "";
            bool inString = false;
            bool inCast = false;
            
            for (int i = 0; i < values.Length; i++)
            {
                char c = values[i];
                
                if (c == '\'' && (i == 0 || values[i - 1] != '\\'))
                {
                    inString = !inString;
                    current += c;
                }
                else if (!inString && c == '(' && i < values.Length - 4 && values.Substring(i, 4) == "CAST")
                {
                    inCast = true;
                    current += c;
                }
                else if (!inString && inCast && c == ')')
                {
                    inCast = false;
                    current += c;
                }
                else if (!inString && !inCast && c == ',')
                {
                    parts.Add(current.Trim());
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            
            if (!string.IsNullOrEmpty(current))
            {
                parts.Add(current.Trim());
            }
            
            return parts;
        }
        
        private string? CleanSqlString(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.ToUpper() == "NULL")
                return null;
            
            value = value.Trim();
            
            // Remove N' prefix and surrounding quotes
            if (value.StartsWith("N'") && value.EndsWith("'"))
                value = value.Substring(2, value.Length - 3);
            else if (value.StartsWith("'") && value.EndsWith("'"))
                value = value.Substring(1, value.Length - 2);
            
            // Unescape single quotes
            value = value.Replace("''", "'");
            
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        
        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.ToUpper() == "NULL")
                return 0;
            
            value = value.Trim();
            
            // Handle CAST(value AS Decimal(18,2)) or CAST(value AS Numeric(18,2)) format
            var castMatch = Regex.Match(value, @"CAST\(([\d.]+)\s+AS\s+(Decimal|Numeric)", RegexOptions.IgnoreCase);
            if (castMatch.Success)
            {
                value = castMatch.Groups[1].Value;
            }
            
            // Handle CONVERT(Decimal(18,2), value) format  
            var convertMatch = Regex.Match(value, @"CONVERT\((Decimal|Numeric)\([^)]+\),\s*([\d.]+)\)", RegexOptions.IgnoreCase);
            if (convertMatch.Success)
            {
                value = convertMatch.Groups[2].Value;
            }
            
            // Use InvariantCulture to ensure decimal point is interpreted correctly
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result))
            {
                // Round to 2 decimal places to avoid floating point precision issues
                return Math.Round(result, 2, MidpointRounding.AwayFromZero);
            }
            
            return 0;
        }
        
        /// <summary>
        /// Validates and corrects article data based on business rules
        /// </summary>
        private void ValidateAndCorrectArticle(Article article)
        {
            bool needsCorrection = false;
            var warnings = new List<string>();
            
            // Rule 0: Ensure required fields are never null
            if (string.IsNullOrWhiteSpace(article.Barcode))
            {
                article.Barcode = $"GEN-{Guid.NewGuid().ToString().Substring(0, 8)}";
                article.HasBarcode = false;
                needsCorrection = true;
                warnings.Add($"Generated barcode for article '{article.Name}' because it was missing");
            }
            
            // Rule 0.5: Handle duplicate barcodes
            if (_usedBarcodes.Contains(article.Barcode))
            {
                var originalBarcode = article.Barcode;
                article.Barcode = $"{article.Barcode}-{_duplicateBarcodeCount++}";
                needsCorrection = true;
                warnings.Add($"Duplicate barcode detected: '{originalBarcode}' changed to '{article.Barcode}' for '{article.Name}'");
            }
            _usedBarcodes.Add(article.Barcode);
            
            if (string.IsNullOrWhiteSpace(article.Name))
            {
                article.Name = $"Artikull pa emër ({article.Barcode})";
                needsCorrection = true;
                warnings.Add($"Generated name for article with barcode '{article.Barcode}' because it was missing");
            }
            
            // Rule 1: If sales price is 0, calculate from purchase price + margin
            if (article.SalesPrice == 0 && article.PurchasePrice > 0)
            {
                article.SalesPrice = article.PurchasePrice + (article.PurchasePrice * article.Margin / 100);
                needsCorrection = true;
                warnings.Add($"Sales price calculated from purchase price for '{article.Name}'");
            }
            
            // Rule 2: Validate price ranges - detect obvious errors (prices too low)
            if (article.SalesPrice > 0 && article.SalesPrice < 0.10m)
            {
                // Likely a decimal point error - prices below 0.10 EUR are suspicious
                warnings.Add($"CRITICAL: Very low price detected ({article.SalesPrice:F4} EUR) for '{article.Name}' - possible decimal error");
                
                // Try to detect if it's a factor of 100 error (12.00 -> 0.12)
                if (article.PurchasePrice > 0)
                {
                    decimal expectedSalesFromPurchase = article.PurchasePrice * (1 + article.Margin / 100);
                    decimal ratio = expectedSalesFromPurchase / article.SalesPrice;
                    
                    // If ratio is close to 100, it's likely a 100x scaling error
                    if (ratio >= 90 && ratio <= 110)
                    {
                        article.SalesPrice *= 100;
                        article.PurchasePrice *= 100;
                        if (article.WholesalePrice > 0) article.WholesalePrice *= 100;
                        if (article.SalesPrice1 > 0) article.SalesPrice1 *= 100;
                        if (article.PackagePrice > 0) article.PackagePrice *= 100;
                        if (article.AverageSalesPrice > 0) article.AverageSalesPrice *= 100;
                        if (article.AveragePurchasePrice > 0) article.AveragePurchasePrice *= 100;
                        
                        needsCorrection = true;
                        warnings.Add($"CORRECTED: Prices multiplied by 100 for '{article.Name}' - now {article.SalesPrice:F2} EUR");
                    }
                }
            }
            
            // Rule 3: Validate purchase price vs sales price relationship
            if (article.PurchasePrice > 0 && article.SalesPrice > 0)
            {
                if (article.SalesPrice < article.PurchasePrice)
                {
                    warnings.Add($"WARNING: Sales price ({article.SalesPrice:F2}) is lower than purchase price ({article.PurchasePrice:F2}) for '{article.Name}'");
                }
                
                // Recalculate margin to ensure consistency
                decimal actualMargin = ((article.SalesPrice - article.PurchasePrice) / article.PurchasePrice) * 100;
                if (Math.Abs(actualMargin - article.Margin) > 1) // Allow 1% tolerance
                {
                    article.Margin = Math.Round(actualMargin, 2);
                    needsCorrection = true;
                }
            }
            
            // Rule 4: Ensure wholesale price is between purchase and sales price
            if (article.WholesalePrice > 0)
            {
                if (article.WholesalePrice < article.PurchasePrice)
                {
                    article.WholesalePrice = article.PurchasePrice * 1.05m; // 5% above purchase
                    needsCorrection = true;
                }
                if (article.WholesalePrice > article.SalesPrice)
                {
                    article.WholesalePrice = article.SalesPrice * 0.95m; // 5% below sales
                    needsCorrection = true;
                }
            }
            
            // Rule 5: Validate stock quantities
            if (article.StockQuantity < 0)
            {
                warnings.Add($"WARNING: Negative stock ({article.StockQuantity}) for '{article.Name}'");
                article.StockQuantity = 0;
                needsCorrection = true;
            }
            
            // Rule 6: Ensure Pack is at least 1
            if (article.Pack <= 0)
            {
                article.Pack = 1;
                needsCorrection = true;
            }
            
            // Rule 7: Validate and sanitize barcode
            if (!string.IsNullOrWhiteSpace(article.Barcode))
            {
                article.Barcode = article.Barcode.Trim();
                if (article.Barcode.Length > 50)
                {
                    article.Barcode = article.Barcode.Substring(0, 50);
                    needsCorrection = true;
                }
            }
            else
            {
                // Generate barcode from ID if missing
                article.HasBarcode = false;
            }
            
            // Rule 8: Round all prices to 2 decimal places
            article.PurchasePrice = Math.Round(article.PurchasePrice, 2, MidpointRounding.AwayFromZero);
            article.SalesPrice = Math.Round(article.SalesPrice, 2, MidpointRounding.AwayFromZero);
            article.WholesalePrice = Math.Round(article.WholesalePrice, 2, MidpointRounding.AwayFromZero);
            article.SalesPrice1 = Math.Round(article.SalesPrice1, 2, MidpointRounding.AwayFromZero);
            article.PackagePrice = Math.Round(article.PackagePrice, 2, MidpointRounding.AwayFromZero);
            article.AverageSalesPrice = Math.Round(article.AverageSalesPrice, 2, MidpointRounding.AwayFromZero);
            article.AveragePurchasePrice = Math.Round(article.AveragePurchasePrice, 2, MidpointRounding.AwayFromZero);
            
            if (needsCorrection)
            {
                _correctionCount++;
            }
            
            if (warnings.Count > 0)
            {
                _validationWarnings.AddRange(warnings);
                foreach (var warning in warnings)
                {
                    Console.WriteLine($"  [VALIDATION] {warning}");
                }
            }
        }
        
        private int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.ToUpper() == "NULL")
                return 0;
            
            value = value.Trim();
            if (int.TryParse(value, out int result))
                return result;
            
            return 0;
        }
        
        private int? ParseNullableInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.ToUpper() == "NULL")
                return null;
            
            value = value.Trim();
            if (int.TryParse(value, out int result))
                return result;
            
            return null;
        }
        
        private DateTime? ParseSqlDateTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.ToUpper() == "NULL")
                return null;
            
            // Handle CAST(0x0000B1A200F76BEB AS DateTime) format
            var castMatch = Regex.Match(value, @"CAST\((0x[0-9A-F]+)\s+AS\s+DateTime\)", RegexOptions.IgnoreCase);
            if (castMatch.Success)
            {
                // SQL Server datetime is stored as 2 integers (days since 1900-01-01, milliseconds since midnight)
                // This is complex to parse, so we'll skip it for now
                return null;
            }
            
            if (DateTime.TryParse(value, out DateTime result))
                return result;
            
            return null;
        }
        
        private DateTime? ParseSqlDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.ToUpper() == "NULL")
                return null;
            
            value = value.Trim();
            
            // Handle CAST(0x93450B00 AS Date) format
            var castMatch = Regex.Match(value, @"CAST\((0x[0-9A-F]+)\s+AS\s+Date\)", RegexOptions.IgnoreCase);
            if (castMatch.Success)
            {
                try
                {
                    // SQL Server date is stored as days since 1900-01-01
                    var hexValue = castMatch.Groups[1].Value.Substring(2); // Remove 0x
                    
                    // Convert hex to bytes and interpret as integer (little-endian)
                    var bytes = new List<byte>();
                    for (int i = 0; i < hexValue.Length; i += 2)
                    {
                        bytes.Add(Convert.ToByte(hexValue.Substring(i, 2), 16));
                    }
                    
                    // SQL Server stores dates as days since 1900-01-01
                    var daysSince1900 = BitConverter.ToInt32(bytes.ToArray(), 0);
                    var baseDate = new DateTime(1900, 1, 1);
                    return baseDate.AddDays(daysSince1900);
                }
                catch
                {
                    return null;
                }
            }
            
            if (DateTime.TryParse(value, out DateTime result))
                return result;
            
            return null;
        }
        
        /// <summary>
        /// Parse and import receipts (sales) from SQL Server script file
        /// </summary>
        public async Task<int> ImportReceiptsFromSqlScript(string scriptPath)
        {
            int importedCount = 0;
            var receiptGroups = new Dictionary<string, List<ReceiptItemData>>();
            
            try
            {
                Console.WriteLine("\n========================================");
                Console.WriteLine("Importing Sales Receipts (DitariD)...");
                Console.WriteLine("========================================");
                
                // First pass: collect all receipt items grouped by coupon number (KUPONI)
                using (var reader = new StreamReader(scriptPath))
                {
                    string line;
                    string currentInsert = "";
                    
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Contains("INSERT [dbo].[DitariD]"))
                        {
                            currentInsert = line;
                            
                            if (currentInsert.Contains(") VALUES (") && currentInsert.TrimEnd().EndsWith(")"))
                            {
                                var item = ParseReceiptItemFromInsert(currentInsert);
                                if (item != null)
                                {
                                    if (!receiptGroups.ContainsKey(item.ReceiptNumber))
                                    {
                                        receiptGroups[item.ReceiptNumber] = new List<ReceiptItemData>();
                                    }
                                    receiptGroups[item.ReceiptNumber].Add(item);
                                }
                                currentInsert = "";
                            }
                        }
                        else if (!string.IsNullOrEmpty(currentInsert))
                        {
                            currentInsert += " " + line.Trim();
                            
                            if (currentInsert.TrimEnd().EndsWith(")"))
                            {
                                var item = ParseReceiptItemFromInsert(currentInsert);
                                if (item != null)
                                {
                                    if (!receiptGroups.ContainsKey(item.ReceiptNumber))
                                    {
                                        receiptGroups[item.ReceiptNumber] = new List<ReceiptItemData>();
                                    }
                                    receiptGroups[item.ReceiptNumber].Add(item);
                                }
                                currentInsert = "";
                            }
                        }
                    }
                }
                
                // Second pass: create Receipt records with their items
                Console.WriteLine($"Found {receiptGroups.Count} unique sales receipts");
                
                foreach (var group in receiptGroups)
                {
                    var firstItem = group.Value.First();
                    var receipt = new Receipt
                    {
                        ReceiptNumber = group.Key,
                        Date = firstItem.Date,
                        BuyerName = firstItem.CustomerName,
                        CashierName = firstItem.CashierName,
                        CashierNumber = firstItem.CashierNumber,
                        PaymentMethod = firstItem.PaymentMethod,
                        Remark = firstItem.Remark,
                        TotalAmount = group.Value.Sum(i => i.TotalValue),
                        TaxAmount = group.Value.Sum(i => i.VATValue),
                        PaidAmount = group.Value.Sum(i => i.PaidAmount),
                        LeftAmount = group.Value.Sum(i => i.LeftAmount),
                        IsFiscal = firstItem.IsFiscal,
                        IsPrinted = true,
                        ReceiptType = firstItem.IsFiscal ? ReceiptType.Fiscal : ReceiptType.Simple
                    };
                    
                    foreach (var itemData in group.Value)
                    {
                        // Find the article by barcode
                        var article = await _context.Articles.FirstOrDefaultAsync(a => a.Barcode == itemData.Barcode);
                        if (article != null)
                        {
                            receipt.Items.Add(new ReceiptItem
                            {
                                ArticleId = article.Id,
                                Barcode = itemData.Barcode,
                                ArticleName = itemData.ArticleName,
                                Quantity = itemData.Quantity,
                                Price = itemData.Price,
                                DiscountPercent = itemData.DiscountPercent,
                                DiscountValue = itemData.DiscountValue,
                                VATRate = itemData.VATRate,
                                VATValue = itemData.VATValue,
                                TotalValue = itemData.TotalValue
                            });
                        }
                    }
                    
                    if (receipt.Items.Count > 0)
                    {
                        _context.Receipts.Add(receipt);
                        importedCount++;
                        
                        if (importedCount % 50 == 0)
                        {
                            await _context.SaveChangesAsync();
                            Console.WriteLine($"Imported {importedCount} sales receipts...");
                        }
                    }
                }
                
                await _context.SaveChangesAsync();
                Console.WriteLine($"Sales receipts import complete: {importedCount} receipts\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing receipts: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            return importedCount;
        }
        
        private class ReceiptItemData
        {
            public string ReceiptNumber { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public string CustomerName { get; set; } = "Qytetar";
            public string CashierName { get; set; } = string.Empty;
            public string CashierNumber { get; set; } = "01";
            public string PaymentMethod { get; set; } = "Para në dorë";
            public string Remark { get; set; } = string.Empty;
            public string Barcode { get; set; } = string.Empty;
            public string ArticleName { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal Price { get; set; }
            public decimal DiscountPercent { get; set; }
            public decimal DiscountValue { get; set; }
            public decimal VATRate { get; set; }
            public decimal VATValue { get; set; }
            public decimal TotalValue { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal LeftAmount { get; set; }
            public bool IsFiscal { get; set; }
        }
        
        private ReceiptItemData? ParseReceiptItemFromInsert(string insertStatement)
        {
            try
            {
                var valuesMatch = Regex.Match(insertStatement, @"VALUES \((.*)\)");
                if (!valuesMatch.Success) return null;
                
                var values = valuesMatch.Groups[1].Value;
                var parts = SplitSqlValues(values);
                
                if (parts.Count < 40) return null;
                
                // DitariD structure (based on the SQL):
                // 0=ID, 1=DATA (Date), 2=ORA (Time), 3=NUMRI (Number), 4=KUPONI (Receipt Number), 5=ARKA (Cashier)
                // 6=SEKTORI, 7=SUBJEKTI, 8=PUNETORI (Worker/Cashier Name), 9=MUAJI, 10=VITI
                // 11=VEREJTJE (Remark), 12=BARKODI (Barcode), 13=ARTIKULLI (Article Name), 14=NJESIA (Unit)
                // 15=SASIA (Quantity), 16=QMIMI (Price), 17=QMIMI1, 18=QMIMIPATVSH, 19=QMIMIPATVSH1
                // 20=QMIMIF, 21=RABATI (Discount %), 22=VLERARABATIT (Discount Value), 23=VLERARABATIT1
                // 24=TVSH (VAT Value), 25=TVSH1, 26=VLERAPATVSH (Value without VAT), 27=VLERAPATVSH1
                // 28=VLERAMETVSH (Value with VAT), 29=VLERAMETVSH1, ...
                // 39=PAGOI (Paid), 40=MBETI (Remaining/Change), 41=chk, 42=MUAJINR, 43=VAT (VAT Rate)
                // 44=MetodaP (Payment Method), ...
                
                var receiptNumber = CleanSqlString(parts[4]);
                if (string.IsNullOrWhiteSpace(receiptNumber)) return null;
                
                var barcode = CleanSqlString(parts[12]);
                if (string.IsNullOrWhiteSpace(barcode)) return null;
                
                var quantity = ParseDecimal(parts[15]);
                if (quantity <= 0) return null;
                
                // Parse date from ORA field (parts[2]) which contains readable date string
                DateTime date = DateTime.Now;
                var oraField = CleanSqlString(parts[2]);
                if (!string.IsNullOrWhiteSpace(oraField))
                {
                    if (DateTime.TryParseExact(oraField, new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "MM/dd/yyyy", "yyyy-MM-dd" },
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                    {
                        date = parsedDate;
                    }
                    else if (DateTime.TryParse(oraField, out parsedDate))
                    {
                        date = parsedDate;
                    }
                }
                
                // Fallback to DATA field
                if (date == DateTime.Now)
                {
                    date = ParseSqlDate(parts[1]) ?? DateTime.Now;
                }
                
                var price = ParseDecimal(parts[16]);
                var discountPercent = ParseDecimal(parts[21]);
                var discountValue = ParseDecimal(parts[22]);
                var vatValue = ParseDecimal(parts[24]);
                var vatRate = ParseDecimal(parts[43]);
                var valueWithVat = ParseDecimal(parts[28]);
                var paidAmount = ParseDecimal(parts[39]);
                var leftAmount = ParseDecimal(parts[40]);
                
                var customerName = "Qytetar"; // Default customer name
                var cashierName = CleanSqlString(parts[8]) ?? "Administrator";
                var cashierNumber = CleanSqlString(parts[5]) ?? "01";
                var remark = CleanSqlString(parts[11]) ?? "";
                var articleName = CleanSqlString(parts[13]) ?? "Artikull";
                var paymentMethod = parts.Count > 44 ? (CleanSqlString(parts[44]) ?? "Para në dorë") : "Para në dorë";
                
                // Map payment method codes
                paymentMethod = paymentMethod switch
                {
                    "1" => "Para në dorë",
                    "2" => "Kartë",
                    "3" => "Transfer bankar",
                    "4" => "Çek",
                    _ => paymentMethod
                };
                
                // Check if fiscal receipt
                bool isFiscal = remark.Contains("KUPON", StringComparison.OrdinalIgnoreCase) || 
                               remark.Contains("FISCAL", StringComparison.OrdinalIgnoreCase);
                
                return new ReceiptItemData
                {
                    ReceiptNumber = receiptNumber,
                    Date = date,
                    CustomerName = customerName,
                    CashierName = cashierName,
                    CashierNumber = cashierNumber,
                    PaymentMethod = paymentMethod,
                    Remark = remark,
                    Barcode = barcode,
                    ArticleName = articleName,
                    Quantity = quantity,
                    Price = price,
                    DiscountPercent = discountPercent,
                    DiscountValue = discountValue,
                    VATRate = vatRate,
                    VATValue = vatValue,
                    TotalValue = valueWithVat,
                    PaidAmount = paidAmount,
                    LeftAmount = leftAmount,
                    IsFiscal = isFiscal
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing receipt item: {ex.Message}");
                return null;
            }
        }
    }
}
