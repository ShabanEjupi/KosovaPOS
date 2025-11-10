using Microsoft.EntityFrameworkCore;
using KosovaPOS.Models;
using System;

namespace KosovaPOS.Database
{
    public class POSDbContext : DbContext
    {
        public DbSet<Article> Articles { get; set; }
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<BusinessPartner> BusinessPartners { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<User> Users { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "./Database/KosovaPOS.db";
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Article configuration
            modelBuilder.Entity<Article>()
                .HasIndex(a => a.Barcode);
            
            modelBuilder.Entity<Article>()
                .HasIndex(a => a.Name);
            
            modelBuilder.Entity<Article>()
                .HasIndex(a => a.Category);
            
            // AuditLog configuration
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.Timestamp);
            
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.UserId);
            
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.EntityType, a.EntityId });
            
            // User configuration
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
            
            // Receipt configuration
            modelBuilder.Entity<Receipt>()
                .HasMany(r => r.Items)
                .WithOne(i => i.Receipt)
                .HasForeignKey(i => i.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Purchase configuration
            modelBuilder.Entity<Purchase>()
                .HasMany(p => p.Items)
                .WithOne(i => i.Purchase)
                .HasForeignKey(i => i.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Seed initial data
            SeedData(modelBuilder);
        }
        
        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed sample articles
            modelBuilder.Entity<Article>().HasData(
                new Article
                {
                    Id = 1,
                    Barcode = "0001",
                    Name = "Ujë Mineral 1.5L",
                    Unit = "Copë",
                    PurchasePrice = 0.30m,
                    SalesPrice = 0.50m,
                    VATRate = 18,
                    Category = "Pije",
                    StockQuantity = 100,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                },
                new Article
                {
                    Id = 2,
                    Barcode = "0002",
                    Name = "Bukë e Bardhë",
                    Unit = "Copë",
                    PurchasePrice = 0.20m,
                    SalesPrice = 0.35m,
                    VATRate = 8,
                    Category = "Ushqim",
                    StockQuantity = 50,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }
            );
        }
    }
}
