using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace KosovaPOS.Models
{
    public class Receipt
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string ReceiptNumber { get; set; } = string.Empty;
        
        public DateTime Date { get; set; } = DateTime.Now;
        
        [StringLength(200)]
        public string BuyerName { get; set; } = "Qytetar";
        
        // Add compatibility properties
        public string CustomerName => BuyerName;
        public decimal VATAmount => TaxAmount;
        public string Cashier => CashierName;
        
        [StringLength(500)]
        public string? Remark { get; set; }
        
        [Required]
        [StringLength(50)]
        public string CashierNumber { get; set; } = "01";
        
        [StringLength(100)]
        public string CashierName { get; set; } = string.Empty;
        
        public decimal TotalAmount { get; set; }
        
        public decimal PaidAmount { get; set; }
        
        public decimal LeftAmount { get; set; }
        
        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Para në dorë";
        
        public ReceiptType ReceiptType { get; set; } = ReceiptType.Fiscal;
        
        public decimal TaxAmount { get; set; }
        
        public bool IsFiscal { get; set; } = true;
        
        public bool IsPrinted { get; set; } = false;
        
        [StringLength(200)]
        public string? FiscalFilePath { get; set; }
        
        public List<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();
    }
    
    public class ReceiptItem : INotifyPropertyChanged
    {
        private decimal _quantity;
        private decimal _price;
        private decimal _discountPercent;
        private decimal _discountValue;
        private decimal _vatValue;
        private decimal _totalValue;
        private decimal _vatRate;
        private bool _isUpdatingDiscount = false; // Prevent circular updates

        [Key]
        public int Id { get; set; }
        
        public int ReceiptId { get; set; }
        public Receipt? Receipt { get; set; }
        
        public int ArticleId { get; set; }
        public Article? Article { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Barcode { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        public string ArticleName { get; set; } = string.Empty;
        
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    RecalculateTotals();
                }
            }
        }
        
        public decimal Price
        {
            get => _price;
            set
            {
                if (_price != value)
                {
                    _price = value;
                    OnPropertyChanged();
                    RecalculateTotals();
                }
            }
        }
        
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent != value && !_isUpdatingDiscount)
                {
                    _discountPercent = value;
                    OnPropertyChanged();
                    RecalculateFromPercent();
                }
            }
        }
        
        public decimal DiscountValue
        {
            get => _discountValue;
            set
            {
                if (_discountValue != value && !_isUpdatingDiscount)
                {
                    _discountValue = value;
                    OnPropertyChanged();
                    RecalculateFromValue();
                }
            }
        }
        
        public decimal VATRate
        {
            get => _vatRate;
            set
            {
                if (_vatRate != value)
                {
                    _vatRate = value;
                    OnPropertyChanged();
                    RecalculateTotals();
                }
            }
        }
        
        public decimal VATValue
        {
            get => _vatValue;
            set
            {
                if (_vatValue != value)
                {
                    _vatValue = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public decimal TotalValue
        {
            get => _totalValue;
            set
            {
                if (_totalValue != value)
                {
                    _totalValue = value;
                    OnPropertyChanged();
                }
            }
        }

        private void RecalculateTotals()
        {
            if (_isUpdatingDiscount) return;
            
            _isUpdatingDiscount = true;
            
            var subtotal = Quantity * Price;
            _discountValue = subtotal * (DiscountPercent / 100);
            var afterDiscount = subtotal - _discountValue;
            _vatValue = afterDiscount * (VATRate / 100);
            _totalValue = afterDiscount;
            
            OnPropertyChanged(nameof(DiscountValue));
            OnPropertyChanged(nameof(VATValue));
            OnPropertyChanged(nameof(TotalValue));
            
            _isUpdatingDiscount = false;
        }
        
        private void RecalculateFromPercent()
        {
            if (_isUpdatingDiscount) return;
            
            _isUpdatingDiscount = true;
            
            var subtotal = Quantity * Price;
            _discountValue = subtotal * (DiscountPercent / 100);
            var afterDiscount = subtotal - _discountValue;
            _vatValue = afterDiscount * (VATRate / 100);
            _totalValue = afterDiscount;
            
            OnPropertyChanged(nameof(DiscountValue));
            OnPropertyChanged(nameof(VATValue));
            OnPropertyChanged(nameof(TotalValue));
            
            _isUpdatingDiscount = false;
        }
        
        private void RecalculateFromValue()
        {
            if (_isUpdatingDiscount) return;
            
            _isUpdatingDiscount = true;
            
            var subtotal = Quantity * Price;
            if (subtotal > 0)
            {
                _discountPercent = (_discountValue / subtotal) * 100;
            }
            else
            {
                _discountPercent = 0;
            }
            
            var afterDiscount = subtotal - _discountValue;
            _vatValue = afterDiscount * (VATRate / 100);
            _totalValue = afterDiscount;
            
            OnPropertyChanged(nameof(DiscountPercent));
            OnPropertyChanged(nameof(VATValue));
            OnPropertyChanged(nameof(TotalValue));
            
            _isUpdatingDiscount = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
