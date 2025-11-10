using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KosovaPOS.Models;
using KosovaPOS.Database;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Enterprise-grade article service with caching, validation, and audit logging
    /// Implements Repository pattern with Unit of Work
    /// </summary>
    public class ArticleService : IArticleService
    {
        private readonly POSDbContext _context;
        private readonly ILogger<ArticleService>? _logger;
        private readonly Dictionary<int, Article> _cache;
        private DateTime _lastCacheRefresh;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public ArticleService(POSDbContext context, ILogger<ArticleService>? logger = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
            _cache = new Dictionary<int, Article>();
            _lastCacheRefresh = DateTime.MinValue;
        }

        public async Task<Article?> GetArticleByIdAsync(int id)
        {
            try
            {
                // Check cache first
                if (_cache.TryGetValue(id, out var cachedArticle) && 
                    DateTime.Now - _lastCacheRefresh < _cacheExpiration)
                {
                    _logger?.LogDebug($"Cache hit for article {id}");
                    return cachedArticle;
                }

                var article = await _context.Articles.FindAsync(id);
                
                if (article != null)
                {
                    _cache[id] = article;
                }

                return article;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error retrieving article {id}");
                throw;
            }
        }

        public async Task<Article?> GetArticleByBarcodeAsync(string barcode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(barcode))
                    return null;

                return await _context.Articles
                    .FirstOrDefaultAsync(a => a.Barcode == barcode && a.IsActive);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error retrieving article by barcode {barcode}");
                throw;
            }
        }

        public async Task<IEnumerable<Article>> GetAllArticlesAsync()
        {
            try
            {
                return await _context.Articles
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving all articles");
                throw;
            }
        }

        public async Task<IEnumerable<Article>> SearchArticlesAsync(string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return await GetAllArticlesAsync();

                searchTerm = searchTerm.ToLower();

                return await _context.Articles
                    .Where(a => a.IsActive && 
                           (a.Name.ToLower().Contains(searchTerm) ||
                            a.Barcode.ToLower().Contains(searchTerm) ||
                            (a.Category != null && a.Category.ToLower().Contains(searchTerm))))
                    .OrderBy(a => a.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error searching articles with term: {searchTerm}");
                throw;
            }
        }

        public async Task<IEnumerable<Article>> GetArticlesByCategoryAsync(string category)
        {
            try
            {
                return await _context.Articles
                    .Where(a => a.IsActive && a.Category == category)
                    .OrderBy(a => a.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error retrieving articles by category: {category}");
                throw;
            }
        }

        public async Task<IEnumerable<Article>> GetLowStockArticlesAsync(decimal threshold = 10)
        {
            try
            {
                // Load to memory first, then order by decimal to avoid SQLite limitation
                var articles = await _context.Articles
                    .Where(a => a.IsActive && a.StockQuantity <= threshold)
                    .ToListAsync();
                
                return articles.OrderBy(a => a.StockQuantity).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving low stock articles");
                throw;
            }
        }

        public async Task<Article> CreateArticleAsync(Article article)
        {
            try
            {
                ValidateArticle(article);

                article.CreatedAt = DateTime.Now;
                article.UpdatedAt = DateTime.Now;
                article.IsActive = true;

                _context.Articles.Add(article);
                await _context.SaveChangesAsync();

                _logger?.LogInformation($"Article created: {article.Name} (ID: {article.Id})");
                InvalidateCache();

                return article;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creating article: {article.Name}");
                throw;
            }
        }

        public async Task<Article> UpdateArticleAsync(Article article)
        {
            try
            {
                ValidateArticle(article);

                var existing = await _context.Articles.FindAsync(article.Id);
                if (existing == null)
                    throw new InvalidOperationException($"Article with ID {article.Id} not found");

                // Update properties
                existing.Barcode = article.Barcode;
                existing.Name = article.Name;
                existing.Unit = article.Unit;
                existing.SalesUnit = article.SalesUnit;
                existing.Pack = article.Pack;
                existing.PurchasePrice = article.PurchasePrice;
                existing.Margin = article.Margin;
                existing.PackagePrice = article.PackagePrice;
                existing.WholesalePrice = article.WholesalePrice;
                existing.SalesPrice = article.SalesPrice;
                existing.SalesPrice1 = article.SalesPrice1;
                existing.VATRate = article.VATRate;
                existing.VATType = article.VATType;
                existing.Category = article.Category;
                existing.Supplier = article.Supplier;
                existing.Size = article.Size;
                existing.Color = article.Color;
                existing.Brand = article.Brand;
                existing.Importer = article.Importer;
                existing.Location = article.Location;
                existing.Notes = article.Notes;
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger?.LogInformation($"Article updated: {article.Name} (ID: {article.Id})");
                InvalidateCache();

                return existing;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error updating article: {article.Id}");
                throw;
            }
        }

        public async Task<bool> DeleteArticleAsync(int id)
        {
            try
            {
                var article = await _context.Articles.FindAsync(id);
                if (article == null)
                    return false;

                // Soft delete
                article.IsActive = false;
                article.UpdatedAt = DateTime.Now;
                
                await _context.SaveChangesAsync();

                _logger?.LogInformation($"Article deleted (soft): {article.Name} (ID: {id})");
                InvalidateCache();

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error deleting article: {id}");
                throw;
            }
        }

        public async Task<bool> AdjustStockAsync(int articleId, decimal quantity, string reason)
        {
            try
            {
                var article = await _context.Articles.FindAsync(articleId);
                if (article == null)
                    return false;

                var oldQuantity = article.StockQuantity;
                article.StockQuantity += quantity;
                
                if (quantity > 0)
                    article.StockIn += quantity;
                else
                    article.StockOut += Math.Abs(quantity);

                article.UpdatedAt = DateTime.Now;
                
                await _context.SaveChangesAsync();

                _logger?.LogInformation($"Stock adjusted for {article.Name}: {oldQuantity} -> {article.StockQuantity} (Reason: {reason})");
                InvalidateCache();

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error adjusting stock for article: {articleId}");
                throw;
            }
        }

        public async Task<decimal> CalculateTotalInventoryValueAsync()
        {
            try
            {
                return await _context.Articles
                    .Where(a => a.IsActive)
                    .SumAsync(a => a.StockQuantity * a.PurchasePrice);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error calculating total inventory value");
                throw;
            }
        }

        public async Task<Dictionary<string, decimal>> GetInventoryValueByCategoryAsync()
        {
            try
            {
                return await _context.Articles
                    .Where(a => a.IsActive)
                    .GroupBy(a => a.Category ?? "Uncategorized")
                    .Select(g => new 
                    { 
                        Category = g.Key, 
                        Value = g.Sum(a => a.StockQuantity * a.PurchasePrice) 
                    })
                    .ToDictionaryAsync(x => x.Category, x => x.Value);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error calculating inventory value by category");
                throw;
            }
        }

        private void ValidateArticle(Article article)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(article.Name))
                errors.Add("Article name is required");

            if (article.PurchasePrice < 0)
                errors.Add("Purchase price cannot be negative");

            if (article.SalesPrice < 0)
                errors.Add("Sales price cannot be negative");

            if (article.SalesPrice > 0 && article.PurchasePrice > 0 && article.SalesPrice < article.PurchasePrice)
                errors.Add($"Sales price ({article.SalesPrice:F2}) is lower than purchase price ({article.PurchasePrice:F2})");

            if (article.Pack <= 0)
                errors.Add("Pack must be greater than 0");

            if (errors.Any())
                throw new ArgumentException($"Validation errors: {string.Join(", ", errors)}");
        }

        private void InvalidateCache()
        {
            _cache.Clear();
            _lastCacheRefresh = DateTime.MinValue;
        }
    }
}
