using System;
using System.ComponentModel.DataAnnotations;

namespace KosovaPOS.Models
{
    public class BusinessPartner
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(50)]
        public string? NRF { get; set; } // Numri i Regjistrimit Fiskal
        
        [StringLength(50)]
        public string? NUI { get; set; } // Numri Unik i Identifikimit
        
        [StringLength(300)]
        public string? Address { get; set; }
        
        [StringLength(100)]
        public string? City { get; set; }
        
        [StringLength(50)]
        public string? Phone { get; set; }
        
        [StringLength(100)]
        public string? Email { get; set; }
        
        [StringLength(50)]
        public string PartnerType { get; set; } = "Klient"; // Klient, Furnizues, Të dy
        
        public decimal Balance { get; set; } = 0;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
