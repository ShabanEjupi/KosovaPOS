using System;

namespace KosovaPOS.Services
{
    public class ServiceLocator
    {
        private static ServiceLocator? _instance;
        
        public static ServiceLocator Instance => _instance ?? throw new InvalidOperationException("ServiceLocator not initialized");
        
        public FiscalPrinterService FiscalPrinter { get; private set; }
        public ReceiptPrinterService ReceiptPrinter { get; private set; }
        
        private ServiceLocator()
        {
            FiscalPrinter = new FiscalPrinterService();
            ReceiptPrinter = new ReceiptPrinterService();
        }
        
        public static void Initialize()
        {
            _instance = new ServiceLocator();
        }
    }
}
