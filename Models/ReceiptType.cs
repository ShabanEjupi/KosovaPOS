namespace KosovaPOS.Models
{
    public enum ReceiptType
    {
        /// <summary>
        /// Faturë fiskale me të gjitha detajet e biznesit
        /// </summary>
        Fiscal = 1,
        
        /// <summary>
        /// Faturë e thjeshtë pa emrin e biznesit
        /// </summary>
        Simple = 2,
        
        /// <summary>
        /// Fletëhyrje me emrin e biznesit, numrin fiskal, adresën, email dhe telefon
        /// </summary>
        Waybill = 3
    }
}
