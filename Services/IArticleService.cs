using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KosovaPOS.Models;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Article service interface following enterprise patterns
    /// </summary>
    public interface IArticleService
    {
        Task<Article?> GetArticleByIdAsync(int id);
        Task<Article?> GetArticleByBarcodeAsync(string barcode);
        Task<IEnumerable<Article>> GetAllArticlesAsync();
        Task<IEnumerable<Article>> SearchArticlesAsync(string searchTerm);
        Task<IEnumerable<Article>> GetArticlesByCategoryAsync(string category);
        Task<IEnumerable<Article>> GetLowStockArticlesAsync(decimal threshold = 10);
        Task<Article> CreateArticleAsync(Article article);
        Task<Article> UpdateArticleAsync(Article article);
        Task<bool> DeleteArticleAsync(int id);
        Task<bool> AdjustStockAsync(int articleId, decimal quantity, string reason);
        Task<decimal> CalculateTotalInventoryValueAsync();
        Task<Dictionary<string, decimal>> GetInventoryValueByCategoryAsync();
    }
}
