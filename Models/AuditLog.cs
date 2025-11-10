using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    /// <summary>
    /// Audit log entry for tracking all system operations
    /// Enterprise-grade audit trail for compliance and security
    /// </summary>
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [Required]
        [StringLength(100)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty; // CREATE, UPDATE, DELETE, LOGIN, LOGOUT, SALE, etc.

        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty; // Article, Receipt, Purchase, etc.

        public int? EntityId { get; set; }

        [StringLength(500)]
        public string? EntityName { get; set; }

        public string? OldValue { get; set; } // JSON serialized old state

        public string? NewValue { get; set; } // JSON serialized new state

        [StringLength(200)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? Details { get; set; }

        public bool IsSuccess { get; set; } = true;

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }
    }
}
