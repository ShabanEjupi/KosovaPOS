using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    public class Purchase
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string DocumentNumber { get; set; } = string.Empty;
        
        public DateTime Date { get; set; } = DateTime.Now;
        
        public int SupplierId { get; set; }
        public BusinessPartner? Supplier { get; set; }
        
        [StringLength(100)]
        public string PurchaseType { get; set; } = "Vendore"; // Vendore, Import, Shpenzime
        
        public decimal TotalAmount { get; set; }
        
        public decimal VATAmount { get; set; }
        
        public bool IsPaid { get; set; } = false;
        
        public List<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    }
    
    public class PurchaseItem
    {
        [Key]
        public int Id { get; set; }
        
        public int PurchaseId { get; set; }
        public Purchase? Purchase { get; set; }
        
        public int ArticleId { get; set; }
        public Article? Article { get; set; }
        
        public decimal Quantity { get; set; }
        
        public decimal PurchasePrice { get; set; }
        
        public decimal VATRate { get; set; }
        
        public decimal TotalValue { get; set; }
    }
}
