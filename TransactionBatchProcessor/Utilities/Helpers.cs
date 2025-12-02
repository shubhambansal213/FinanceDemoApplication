using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace TransactionBatchProcessor.Utilities
{
    public static class Helpers
    {
        const string ClassNAme = "Helpers";

        /// <summary>
        /// Masks account number leaving only last 4 digits visible. Non-digit characters are preserved.
        /// Example: 123456789012 -> XXXXXXXXX012
        /// </summary>
        public static string MaskAccount(string account)
        {
            if (string.IsNullOrEmpty(account))
                return account;

            // Keep only last 4 digits visible, replace other digits with 'X'
            int digitsSeen = 0;
            var chars = account.ToCharArray();
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(chars[i]))
                {
                    digitsSeen++;
                    if (digitsSeen > 4)
                        chars[i] = 'X';
                }
            }

            return new string(chars);
        }

        /// <summary>
        /// Computes SHA256 hex lowercase of the input UTF-8 bytes.
        /// </summary>
        public static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Used CSV Parser
        /// Returns array of fields (may include empty strings).
        /// </summary>
        public static string[] ParseCsvLine(string line)
        {
            try
            {
                 if (line == null)
                    return Array.Empty<string>();
                
                var list = ParseCsvLineWithHelper(line);
                return list?.ToArray() ?? Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, ClassNAme+".ParseCsvLine");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Example: parse one raw CSV line using CsvHelper (requires CsvHelper NuGet)
        /// </summary>
        public static List<string> ParseCsvLineWithHelper(string rawLine)
        {
            try
            {
                using var reader = new StringReader(rawLine);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false,
                    TrimOptions = TrimOptions.None
                };
                using var csv = new CsvReader(reader, config);
                csv.Read();
                var fields = new List<string>();
                for (int i = 0; csv.TryGetField<string>(i, out var f); i++)
                    fields.Add(f ?? string.Empty);

                return fields;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
