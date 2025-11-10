using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Barcode { get; set; } = string.Empty;
        
        [Required]
        [StringLength(250)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(20)]
        public string Unit { get; set; } = "Copë"; // Copë, KG, M, etc.
        
        [StringLength(100)]
        public string? SalesUnit { get; set; } // Njësia Shitjes
        
        public decimal Pack { get; set; } = 1; // Paketimi
        
        [Required]
        public decimal PurchasePrice { get; set; } // Çmimi Furnizimit
        
        public decimal Margin { get; set; } = 0; // Marzha
        
        public decimal PackagePrice { get; set; } = 0; // Çmimi Paketimit
        
        public decimal WholesalePrice { get; set; } = 0; // Çmimi Shumicës
        
        [Required]
        public decimal SalesPrice { get; set; } // Çmimi Shitjes
        
        public decimal SalesPrice1 { get; set; } = 0; // Çmimi Shitjes 1 (alternative)
        
        public decimal VATRate { get; set; } = 18; // Kosovo standard VAT (18%, 8%, 0%)
        
        public int VATType { get; set; } = 3; // 3=18%, 2=8%, 1=0%
        
        [StringLength(150)]
        public string? Category { get; set; } // Kategoria (Tekstil, Rroba, etc.)
        
        [StringLength(200)]
        public string? Supplier { get; set; } // Furnitori
        
        public int SupplierId { get; set; } = 0;
        
        // Retail/Wholesale specific fields
        [StringLength(50)]
        public string? Size { get; set; } // Madhësia
        
        [StringLength(50)]
        public string? Color { get; set; } // Ngjyra
        
        [StringLength(100)]
        public string? Brand { get; set; } // Prodhuesi
        
        [StringLength(100)]
        public string? Importer { get; set; } // Importuesi
        
        [StringLength(100)]
        public string? Location { get; set; } // Vendi (store location/shelf)
        
        [StringLength(50)]
        public string? Sector { get; set; } // Sektori
        
        [StringLength(50)]
        public string? Branch { get; set; } // Filiala
        
        [StringLength(50)]
        public string? Season { get; set; } // Sezoni (Verë, Dimër, Pranverë, Vjeshtë)
        
        [StringLength(20)]
        public string? Gender { get; set; } // Gjinia (Meshkuj, Femra, Fëmijë, Unisex)
        
        [StringLength(500)]
        public string? Notes { get; set; } // Vërejtje
        
        public decimal StockQuantity { get; set; } = 0; // Sasia
        
        public decimal StockIn { get; set; } = 0; // Sasia Hyrje
        
        public decimal StockOut { get; set; } = 0; // Sasia Dalje
        
        public decimal AverageSalesPrice { get; set; } = 0; // Çmimi Mesatar Shitës
        
        public decimal AveragePurchasePrice { get; set; } = 0; // Çmimi Mesatar Furnizues
        
        public decimal MinimumStock { get; set; } = 0;
        
        public DateTime? ExpiryDate { get; set; } // Afati
        
        public bool HasBarcode { get; set; } = true; // PaBarkod (false=Y, true=N)
        
        public bool IsRegular { get; set; } = true; // IRregullt
        
        public bool IsWeighed { get; set; } = false; // Peshon (për peshoren)
        
        public bool IsActive { get; set; } = true;
        
        public int ProductType { get; set; } = 1; // Tipi (1=Normal, 2=Service, etc.)
        
        public int? POSCategoryId { get; set; } // KategoriaPos_ID
        
        [StringLength(500)]
        public string? PhotoPath { get; set; } // Foto
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
