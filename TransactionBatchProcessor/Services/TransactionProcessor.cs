using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TransactionBatchProcessor.Models;
using TransactionBatchProcessor.Utilities;

namespace TransactionBatchProcessor.Services
{
    public class TransactionProcessor
    {
        /// <summary>
        /// Result summary after processing.
        /// </summary>
        public class Result
        {
            public int TotalRecords { get; set; }
            public int SuccessCount { get; set; }
            public int FailedCount { get; set; }
            public TimeSpan TimeTaken { get; set; }
            public int MaxWorkerThreadsUsed { get; set; }
        }

        /// <summary>
        /// Processes the CSV file. progressCallback receives (message, percent) where percent may be -1 if not applicable.
        /// </summary>
        public Result ProcessFile(string filePath, Action<string, double> progressCallback)
        {
            try
            {
                var sw = Stopwatch.StartNew();

                if (!File.Exists(filePath))
                {
                    MessageBox.Show("Input CSV file not found: " + filePath, "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    return new Result { TotalRecords = 0, SuccessCount = 0, FailedCount = 0, TimeTaken = sw.Elapsed, MaxWorkerThreadsUsed = 0 };
                }

                var folder = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
                var successPath = Path.Combine(folder, "Success.csv");
                var errorPath = Path.Combine(folder, "Error.log");

                var allLines = ReadNonEmptyLines(filePath);
                if (allLines.Length == 0)
                {
                    progressCallback?.Invoke("No records found.", -1);
                    MessageBox.Show("No records found in the CSV file.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return new Result { TotalRecords = 0, SuccessCount = 0, FailedCount = 0, TimeTaken = sw.Elapsed, MaxWorkerThreadsUsed = 0 };
                }

                int startIndex = DetectHeaderIndex(allLines);
                var linesToProcess = allLines.Skip(startIndex).ToArray();
                int total = linesToProcess.Length;

                EnsureSuccessHeader(successPath);

                var (tTotal, successCount, failedCount, maxWorkers) = ProcessLinesParallel(linesToProcess, successPath, errorPath, progressCallback);

                sw.Stop();

                progressCallback?.Invoke("Processing finished", 100);

                return new Result
                {
                    TotalRecords = tTotal,
                    SuccessCount = successCount,
                    FailedCount = failedCount,
                    TimeTaken = sw.Elapsed,
                    MaxWorkerThreadsUsed = maxWorkers
                };
            }
            catch (Exception ex)
            {
                try { Logger.LogException(ex, $"TransactionProcessor.ProcessFile file:{filePath}"); } catch { }
                throw;
            }
        }

        // Read all non-empty lines from file
        private string[] ReadNonEmptyLines(string filePath)
        {
            return File.ReadAllLines(filePath)
                       .Where(l => !string.IsNullOrWhiteSpace(l))
                       .ToArray();
        }

        // Detect whether the first non-empty line is a header and return start index (0 or 1)
        private int DetectHeaderIndex(string[] allLines)
        {
            int startIndex = 0;
            var firstLine = allLines.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstLine))
            {
                firstLine = firstLine.Trim('\uFEFF'); // remove BOM if present
                var firstFields = Helpers.ParseCsvLine(firstLine);
                if (firstFields.Length > 0 && string.Equals(firstFields[0].Trim(), "TransactionId", StringComparison.OrdinalIgnoreCase))
                    startIndex = 1;
            }
            return startIndex;
        }

        // Ensure Success.csv has header if it doesn't exist
        private void EnsureSuccessHeader(string successPath)
        {
            if (!File.Exists(successPath))
            {
                File.WriteAllText(successPath, "TransactionId,MaskedAccountNumber,Amount,Currency,Date,Description,SHA256" + Environment.NewLine);
            }
        }

        // Core processing moved into a helper to keep ProcessFile concise.
        // Returns (total, successCount, failedCount, maxWorkers)
        private (int total, int successCount, int failedCount, int maxWorkers) ProcessLinesParallel(string[] linesToProcess, string successPath, string errorPath, Action<string, double> progressCallback)
        {
            int total = linesToProcess.Length;

            var successLock = new object();
            var errorLock = new object();

            var seenIds = new ConcurrentDictionary<string, byte>();

            int successCount = 0;
            int failedCount = 0;
            int processed = 0;

            int maxWorkers = 0;
            int currentWorkers = 0;

            var po = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            Parallel.ForEach(linesToProcess, po, () => 0, (line, state, local) =>
            {
                int now = Interlocked.Increment(ref currentWorkers);
                try
                {
                    // update maxWorkers atomically
                    int prevMax;
                    do
                    {
                        prevMax = maxWorkers;
                        if (now <= prevMax) break;
                    } while (Interlocked.CompareExchange(ref maxWorkers, now, prevMax) != prevMax);

                    var original = line;
                    var fields = Utilities.Helpers.ParseCsvLine(line);

                    if (!Services.Validator.TryParseFields(original, fields, out Transaction tx, out string parseError))
                    {
                        var reason = parseError;
                        var sha = Helpers.Sha256Hex(original);
                        var maskedAcc = Helpers.MaskAccount(fields.Length > 1 ? fields[1].Trim() : string.Empty);
                        lock (errorLock)
                        {
                            File.AppendAllText(errorPath, $"[{DateTime.UtcNow:O}] ParseFailed: {reason} | {maskedAcc} | {sha}{Environment.NewLine}");
                        }
                        Interlocked.Increment(ref failedCount);
                    }
                    else
                    {
                        var reasons = new List<string>();

                        // check uniqueness thread-safely
                        if (!seenIds.TryAdd(tx.TransactionId, 0))
                        {
                            reasons.Add("Duplicate TransactionId");
                        }

                        // validate other rules
                        if (!Validator.Validate(tx, out var vReasons))
                            reasons.AddRange(vReasons);

                        if (reasons.Count == 0)
                        {
                            // success - write to Success.csv
                            var masked = Helpers.MaskAccount(tx.AccountNumber);
                            var sha = Helpers.Sha256Hex(original);
                            var outLine = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.00},{3},{4},{5},{6}",
                                tx.TransactionId,
                                masked,
                                tx.Amount,
                                tx.Currency.ToUpperInvariant(),
                                tx.Date.ToString("dd-MM-yyyy"),
                                tx.Description.Replace(',', ' '),
                                sha);

                            lock (successLock)
                            {
                                File.AppendAllText(successPath, outLine + Environment.NewLine);
                            }
                            Interlocked.Increment(ref successCount);
                        }
                        else
                        {
                            var maskedAcc = Helpers.MaskAccount(tx.AccountNumber);
                            var sha = Helpers.Sha256Hex(original);
                            lock (errorLock)
                            {
                                File.AppendAllText(errorPath, $"[{DateTime.UtcNow:O}] ValidationFailed: {string.Join("; ", reasons)} | {maskedAcc} | {sha}{Environment.NewLine}");
                            }
                            Interlocked.Increment(ref failedCount);
                        }
                    }

                    var done = Interlocked.Increment(ref processed);
                    if (done % Math.Max(1, total / 20) == 0 || done == total)
                    {
                        double percent = (double)done / total * 100.0;
                        progressCallback?.Invoke($"Processed {done} / {total}", percent);
                    }

                    return local;
                }
                finally
                {
                    Interlocked.Decrement(ref currentWorkers);
                }
            }, _ => { });

            return (total, successCount, failedCount, maxWorkers);
        }
    }
}
