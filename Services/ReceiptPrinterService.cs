using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using KosovaPOS.Models;

namespace KosovaPOS.Services
{
    public class ReceiptPrinterService
    {
        private readonly string _businessName;
        private readonly string _businessAddress;
        private readonly string _businessPhone;
        private readonly string _businessEmail;
        private readonly string _fiscalNumber;
        private Receipt? _currentReceipt;
        
        public ReceiptPrinterService()
        {
            _businessName = Environment.GetEnvironmentVariable("BUSINESS_NAME") ?? "Dyqani Juaj";
            _businessAddress = Environment.GetEnvironmentVariable("BUSINESS_ADDRESS") ?? "Prishtinë, Kosovë";
            _businessPhone = Environment.GetEnvironmentVariable("BUSINESS_PHONE") ?? "+383 XX XXX XXX";
            _businessEmail = Environment.GetEnvironmentVariable("BUSINESS_EMAIL") ?? "center.enisi@gmail.com";
            _fiscalNumber = Environment.GetEnvironmentVariable("FISCAL_NUMBER") ?? "811223344";
        }
        
        public void PrintNonFiscalReceipt(Receipt receipt)
        {
            _currentReceipt = receipt;
            
            var printDocument = new PrintDocument();
            printDocument.PrintPage += PrintNonFiscalReceipt_PrintPage;
            
            // Get printer name from environment
            var printerName = Environment.GetEnvironmentVariable("RECEIPT_PRINTER");
            
            // Validate printer exists
            if (!string.IsNullOrEmpty(printerName))
            {
                bool printerExists = false;
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.Equals(printerName, StringComparison.OrdinalIgnoreCase))
                    {
                        printerExists = true;
                        break;
                    }
                }
                
                if (printerExists)
                {
                    printDocument.PrinterSettings.PrinterName = printerName;
                }
                else
                {
                    // If configured printer doesn't exist, try to use default printer
                    if (PrinterSettings.InstalledPrinters.Count > 0)
                    {
                        // Use the first available printer
                        var firstPrinter = PrinterSettings.InstalledPrinters.Cast<string>().FirstOrDefault();
                        if (firstPrinter != null)
                        {
                            printDocument.PrinterSettings.PrinterName = firstPrinter;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Printeri i konfiguruar '{printerName}' nuk u gjet në sistem. " +
                            "Të lutem kontrollo konfigurimet e printerit në Konfigurimet.");
                    }
                }
            }
            
            // Verify printer is valid before printing
            if (!printDocument.PrinterSettings.IsValid)
            {
                throw new InvalidOperationException(
                    $"Konfigurimet e printerit '{printDocument.PrinterSettings.PrinterName}' nuk janë të vlefshme. " +
                    "Të lutem kontrollo konfigurimet e printerit në Konfigurimet.");
            }
            
            printDocument.Print();
        }
        
        private void PrintNonFiscalReceipt_PrintPage(object sender, PrintPageEventArgs e)
        {
            if (_currentReceipt == null || e.Graphics == null) return;
            
            var graphics = e.Graphics;
            var font = new Font("Courier New", 10);
            var boldFont = new Font("Courier New", 10, FontStyle.Bold);
            var titleFont = new Font("Courier New", 14, FontStyle.Bold);
            var smallFont = new Font("Courier New", 8);
            var brush = Brushes.Black;
            
            float y = 10;
            var lineHeight = font.GetHeight(graphics);
            var x = 10f;
            
            // Print based on receipt type
            switch (_currentReceipt.ReceiptType)
            {
                case ReceiptType.Simple:
                    PrintSimpleReceipt(graphics, font, boldFont, titleFont, brush, ref y, x, lineHeight);
                    break;
                case ReceiptType.Waybill:
                    PrintWaybill(graphics, font, boldFont, titleFont, smallFont, brush, ref y, x, lineHeight);
                    break;
                case ReceiptType.Fiscal:
                default:
                    PrintFiscalStyleReceipt(graphics, font, boldFont, titleFont, brush, ref y, x, lineHeight);
                    break;
            }
            
            font.Dispose();
            boldFont.Dispose();
            titleFont.Dispose();
            smallFont.Dispose();
        }
        
        private void PrintSimpleReceipt(Graphics graphics, Font font, Font boldFont, Font titleFont, 
            Brush brush, ref float y, float x, float lineHeight)
        {
            // Simple receipt - no business name
            graphics.DrawString("FATURË SHITJEJE", titleFont, brush, x + 20, y);
            y += lineHeight * 2;
            
            graphics.DrawString("SalesCupon", font, brush, x + 40, y);
            y += lineHeight * 2;
            
            // Separator
            graphics.DrawString(new string('-', 40), font, brush, x, y);
            y += lineHeight;
            
            // Items
            foreach (var item in _currentReceipt!.Items)
            {
                graphics.DrawString(item.ArticleName, font, brush, x, y);
                y += lineHeight;
                
                var itemLine = $"{item.Quantity:F2} x {item.Price:F2} = {item.TotalValue:F2}";
                graphics.DrawString(itemLine, font, brush, x, y);
                y += lineHeight;
                
                // Show discount if present
                if (item.DiscountPercent > 0)
                {
                    graphics.DrawString($"  Zbritje: {item.DiscountPercent:F1}% (-{item.DiscountValue:F2})", font, brush, x, y);
                    y += lineHeight;
                }
            }
            
            // Separator
            y += lineHeight;
            graphics.DrawString(new string('-', 40), font, brush, x, y);
            y += lineHeight;
            
            // Totals with discount breakdown
            var totalDiscount = _currentReceipt.Items.Sum(i => i.DiscountValue);
            if (totalDiscount > 0)
            {
                var subtotalBeforeDiscount = _currentReceipt.TotalAmount + totalDiscount;
                graphics.DrawString($"Nën-Total: {subtotalBeforeDiscount:F2} EUR", font, brush, x, y);
                y += lineHeight;
                graphics.DrawString($"Zbritje: -{totalDiscount:F2} EUR", font, brush, x, y);
                y += lineHeight;
            }
            
            graphics.DrawString($"Total: {_currentReceipt.TotalAmount:F2} EUR", boldFont, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Paguar: {_currentReceipt.PaidAmount:F2} EUR", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Kusur: {(_currentReceipt.PaidAmount - _currentReceipt.TotalAmount):F2} EUR", font, brush, x, y);
            y += lineHeight * 2;
            
            // Date and receipt number
            graphics.DrawString($"Data: {_currentReceipt.Date:dd/MM/yyyy HH:mm}", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Nr: {_currentReceipt.ReceiptNumber}", font, brush, x, y);
        }
        
        private void PrintWaybill(Graphics graphics, Font font, Font boldFont, Font titleFont, Font smallFont,
            Brush brush, ref float y, float x, float lineHeight)
        {
            // Waybill - full business details
            graphics.DrawString("FLETËHYRJE / WAYBILL", titleFont, brush, x + 10, y);
            y += lineHeight * 2;
            
            // Business information header
            graphics.DrawString(_businessName.ToUpper(), boldFont, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Nr. Fiskal: {_fiscalNumber}", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Adresa: {_businessAddress}", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Tel: {_businessPhone}", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Email: {_businessEmail}", font, brush, x, y);
            y += lineHeight * 2;
            
            // Separator
            graphics.DrawString(new string('=', 42), font, brush, x, y);
            y += lineHeight;
            
            // Receipt details
            graphics.DrawString($"Fatura Nr: {_currentReceipt!.ReceiptNumber}", boldFont, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Data: {_currentReceipt.Date:dd/MM/yyyy HH:mm:ss}", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Klienti: {_currentReceipt.BuyerName}", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Arkëtar: {_currentReceipt.CashierName}", font, brush, x, y);
            y += lineHeight * 2;
            
            // Separator
            graphics.DrawString(new string('-', 42), font, brush, x, y);
            y += lineHeight;
            
            // Column headers
            graphics.DrawString("Artikulli              Sasia    Çmimi   Shuma", smallFont, brush, x, y);
            y += lineHeight;
            graphics.DrawString(new string('-', 42), font, brush, x, y);
            y += lineHeight;
            
            // Items
            foreach (var item in _currentReceipt.Items)
            {
                var name = item.ArticleName.Length > 20 ? item.ArticleName.Substring(0, 20) : item.ArticleName;
                graphics.DrawString(name, font, brush, x, y);
                y += lineHeight;
                
                var itemLine = $"  {item.Quantity,6:F2} x {item.Price,7:F2} = {item.TotalValue,8:F2}";
                graphics.DrawString(itemLine, font, brush, x, y);
                y += lineHeight;
                
                if (item.DiscountPercent > 0)
                {
                    graphics.DrawString($"  Zbritje: {item.DiscountPercent:F1}%", smallFont, brush, x, y);
                    y += lineHeight;
                }
            }
            
            // Separator
            y += lineHeight;
            graphics.DrawString(new string('=', 42), font, brush, x, y);
            y += lineHeight;
            
            // Totals
            var subtotal = _currentReceipt.Items.Sum(i => i.TotalValue + i.DiscountValue);
            var discount = _currentReceipt.Items.Sum(i => i.DiscountValue);
            
            if (discount > 0)
            {
                graphics.DrawString($"Nën-totali:              {subtotal:F2} EUR", font, brush, x, y);
                y += lineHeight;
                graphics.DrawString($"Zbritje:                 {discount:F2} EUR", font, brush, x, y);
                y += lineHeight;
            }
            
            graphics.DrawString($"TVSH ({(_currentReceipt.Items.FirstOrDefault()?.VATRate ?? 18):F0}%):                 {_currentReceipt.VATAmount:F2} EUR", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"TOTALI:                  {_currentReceipt.TotalAmount:F2} EUR", boldFont, brush, x, y);
            y += lineHeight * 2;
            
            // Payment info
            graphics.DrawString($"Metoda e pagesës: {_currentReceipt.PaymentMethod}", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Paguar:                  {_currentReceipt.PaidAmount:F2} EUR", font, brush, x, y);
            y += lineHeight;
            var change = _currentReceipt.PaidAmount - _currentReceipt.TotalAmount;
            if (change > 0)
            {
                graphics.DrawString($"Kusur:                   {change:F2} EUR", font, brush, x, y);
                y += lineHeight;
            }
            
            y += lineHeight;
            graphics.DrawString(new string('=', 42), font, brush, x, y);
            y += lineHeight;
            
            // Footer
            graphics.DrawString("Faleminderit për blerjen!", font, brush, x + 5, y);
            y += lineHeight;
            graphics.DrawString("Thank you for your purchase!", smallFont, brush, x + 5, y);
        }
        
        private void PrintFiscalStyleReceipt(Graphics graphics, Font font, Font boldFont, Font titleFont, 
            Brush brush, ref float y, float x, float lineHeight)
        {
            // Fiscal style - standard header
            graphics.DrawString("SALE", titleFont, brush, x + 50, y);
            y += lineHeight * 2;
            graphics.DrawString("SalesCupon", font, brush, x + 30, y);
            y += lineHeight * 2;
            
            // Business info
            graphics.DrawString(_businessName, boldFont, brush, x, y);
            y += lineHeight;
            graphics.DrawString(_businessAddress, font, brush, x, y);
            y += lineHeight;
            if (!string.IsNullOrEmpty(_businessPhone))
            {
                graphics.DrawString($"Tel: {_businessPhone}", font, brush, x, y);
                y += lineHeight;
            }
            y += lineHeight;
            
            // Separator
            graphics.DrawString(new string('-', 40), font, brush, x, y);
            y += lineHeight;
            
            // Items
            foreach (var item in _currentReceipt!.Items)
            {
                graphics.DrawString(item.ArticleName, font, brush, x, y);
                y += lineHeight;
                
                var itemLine = $"{item.Quantity:F2} x {item.Price:F2} = {item.TotalValue:F2}";
                graphics.DrawString(itemLine, font, brush, x, y);
                y += lineHeight;
                
                // Show discount if present
                if (item.DiscountPercent > 0)
                {
                    graphics.DrawString($"  Zbritje: {item.DiscountPercent:F1}% (-{item.DiscountValue:F2})", font, brush, x, y);
                    y += lineHeight;
                }
            }
            
            // Separator
            y += lineHeight;
            graphics.DrawString(new string('-', 40), font, brush, x, y);
            y += lineHeight;
            
            // Totals with discount breakdown
            var totalDiscount = _currentReceipt.Items.Sum(i => i.DiscountValue);
            if (totalDiscount > 0)
            {
                var subtotalBeforeDiscount = _currentReceipt.TotalAmount + totalDiscount;
                graphics.DrawString($"Nën-Total: {subtotalBeforeDiscount:F2} EUR", font, brush, x, y);
                y += lineHeight;
                graphics.DrawString($"Zbritje: -{totalDiscount:F2} EUR", font, brush, x, y);
                y += lineHeight;
            }
            
            graphics.DrawString($"Total: {_currentReceipt.TotalAmount:F2} EUR", boldFont, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Paguar: {_currentReceipt.PaidAmount:F2} EUR", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Kusur: {(_currentReceipt.PaidAmount - _currentReceipt.TotalAmount):F2} EUR", font, brush, x, y);
            y += lineHeight * 2;
            
            // Date
            graphics.DrawString($"Data: {_currentReceipt.Date:dd/MM/yyyy HH:mm}", font, brush, x, y);
            y += lineHeight;
            graphics.DrawString($"Fatura: {_currentReceipt.ReceiptNumber}", font, brush, x, y);
        }
    }
}
