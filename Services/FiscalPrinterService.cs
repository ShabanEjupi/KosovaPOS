using System;
using System.IO;
using System.Text;
using KosovaPOS.Models;

namespace KosovaPOS.Services
{
    public class FiscalPrinterService
    {
        private readonly string _tempPath;
        private readonly string _printerModel;
        
        public FiscalPrinterService()
        {
            _tempPath = Environment.GetEnvironmentVariable("FISCAL_TEMP_PATH") ?? "C:/Temp/";
            _printerModel = Environment.GetEnvironmentVariable("FISCAL_PRINTER_MODEL") ?? "FP700+";
            
            // Ensure temp directory exists
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
        }
        
        public string GenerateZReport()
        {
            var fileName = Path.Combine(_tempPath, "Fatura.inp");
            var content = $"Z,1,_______,_,__;\n";
            
            File.WriteAllText(fileName, content, Encoding.UTF8);
            return fileName;
        }
        
        public string GenerateXReport()
        {
            var fileName = Path.Combine(_tempPath, "Fatura.inp");
            var content = $"X,1,_______,_,__;\n";
            
            File.WriteAllText(fileName, content, Encoding.UTF8);
            return fileName;
        }
        
        public string GenerateFiscalReceipt(Receipt receipt)
        {
            var receiptNumber = receipt.ReceiptNumber.Replace("-", "");
            var fileName = Path.Combine(_tempPath, $"Fat {receipt.CashierNumber}-{receiptNumber}.inp");
            var sb = new StringBuilder();
            
            // Add receipt items
            foreach (var item in receipt.Items)
            {
                // Format: S,1,______,_,__;ArticleName;Quantity;Price;TaxGroup;Department;VATRate;0;ProductCode;0;0
                var taxGroup = GetTaxGroup(item.VATRate);
                sb.AppendLine($"S,1,______,_,__;{item.ArticleName};{item.Quantity:F2};{item.Price:F2};{taxGroup};1;{item.VATRate:F0};0;{item.Barcode};0;0");
            }
            
            // Add payment information
            sb.AppendLine($"Q,1,______,_,__;1;Pagoi: {receipt.PaidAmount:F2}");
            sb.AppendLine($"Q,1,______,_,__;2;Kusur: {(receipt.PaidAmount - receipt.TotalAmount):F2}");
            
            // End transaction
            sb.AppendLine($"T,1,______,_,__;");
            
            File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
            return fileName;
        }
        
        private int GetTaxGroup(decimal vatRate)
        {
            // Kosovo VAT groups
            // 1 = 18% (Standard)
            // 2 = 8% (Reduced)
            // 3 = 0% (Zero-rated)
            return vatRate switch
            {
                18 => 1,
                8 => 2,
                _ => 3
            };
        }
        
        public bool SendToFiscalPrinter(string filePath)
        {
            try
            {
                // In a real implementation, this would send the file to the fiscal printer
                // via serial port or network connection using the F-Link KS protocol
                
                // For now, just verify the file exists
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to fiscal printer: {ex.Message}");
                return false;
            }
        }
    }
}
