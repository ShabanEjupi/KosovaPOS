using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// User model for authentication and authorization
    /// Supports role-based access control (RBAC)
    /// </summary>
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty; // BCrypt hashed

        [StringLength(100)]
        public string? Email { get; set; }

        [Required]
        [StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Role { get; set; } = "Cashier"; // Admin, Manager, Cashier, Warehouse, Accountant

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLogin { get; set; }

        [StringLength(200)]
        public string? Branch { get; set; }

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        // Permissions
        public bool CanManageArticles { get; set; } = true;
        public bool CanManagePurchases { get; set; } = false;
        public bool CanManageUsers { get; set; } = false;
        public bool CanViewReports { get; set; } = true;
        public bool CanModifyPrices { get; set; } = false;
        public bool CanDeleteReceipts { get; set; } = false;
        public bool CanGiveDiscounts { get; set; } = false;
        public decimal MaxDiscountPercent { get; set; } = 0;
    }
}
