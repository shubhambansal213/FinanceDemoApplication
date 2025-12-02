using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TransactionBatchProcessor.Models;

namespace TransactionBatchProcessor.Services
{
    /// <summary>
    /// Transaction validation helper.
    /// </summary>
    public static class Validator
    {
        private static readonly Regex AccountDigits = new("^[0-9]{10,16}$");

        /// <summary>
        /// Validates a parsed transaction. Returns true if valid, false and reasons otherwise.
        /// </summary>
        /// <summary>
        /// Validates a Transaction (excluding uniqueness). The caller should check uniqueness separately in a thread-safe way.
        /// </summary>
        public static bool Validate(Transaction tx, out List<string> reasons)
        {
            reasons = new List<string>();

            if (string.IsNullOrWhiteSpace(tx.TransactionId))
                reasons.Add("TransactionId is missing");

            if (string.IsNullOrWhiteSpace(tx.AccountNumber) || !AccountDigits.IsMatch(tx.AccountNumber))
                reasons.Add("AccountNumber must be 10-16 digits");

            if (tx.Amount <= 0)
                reasons.Add("Amount must be > 0");

            if (string.IsNullOrWhiteSpace(tx.Currency) || !(string.Equals(tx.Currency, "INR", StringComparison.OrdinalIgnoreCase) || string.Equals(tx.Currency, "USD", StringComparison.OrdinalIgnoreCase)))
                reasons.Add("Currency must be INR or USD");

            if (tx.Date == default)
                reasons.Add("Date is invalid or missing");

            return reasons.Count == 0;
        }

        /// <summary>
        /// Try parse fields into a Transaction. Returns false on parse failures (insufficient columns etc.)
        /// </summary>
        public static bool TryParseFields(string originalLine, string[] fields, out Transaction tx, out string parseError)
        {
            tx = new Transaction { OriginalLine = originalLine };
            parseError = string.Empty;

            if (fields == null || fields.Length < 6)
            {
                parseError = "Insufficient columns";
                return false;
            }

            try
            {
                tx.TransactionId = fields[0].Trim();
                tx.AccountNumber = Regex.Replace(fields[1].Trim(), "\\s+", ""); // remove spaces
                if (!decimal.TryParse(fields[2].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amt))
                    amt = -1;
                tx.Amount = amt;
                tx.Currency = fields[3].Trim();

                if (!DateTime.TryParseExact(fields[4].Trim(), "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    dt = default;
                tx.Date = dt;

                tx.Description = fields[5].Trim();

                return true;
            }
            catch (Exception ex)
            {
                parseError = "Parse error: " + ex.Message;
                // log for diagnosis
                try { Utilities.Logger.LogException(ex, "Validator.TryParseFields"); } catch { }
                return false;
            }
        }
    }
}
