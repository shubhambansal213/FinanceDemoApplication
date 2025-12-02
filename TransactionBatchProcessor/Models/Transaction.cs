using System;

namespace TransactionBatchProcessor.Models
{
    /// <summary>
    /// Represents a transaction parsed from a CSV line.
    /// </summary>
    public class Transaction
    {
        public string OriginalLine { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty; // digits only
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
