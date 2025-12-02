# FinanceDemo
# Transaction Batch Processor

Simple WPF desktop app (C#, .NET 7) to process transaction CSV files concurrently.

# Transaction Batch Processor

This is a small WPF app that reads a CSV of transactions, validates each row,
and writes two outputs: `Success.csv` (valid rows) and `Error.log` (invalid rows).

Quick points
- Simple UI: pick a CSV, press Process, see progress and a log.
- Validations: unique TransactionId, 10–16 digit account, Amount &gt; 0, Currency INR/USD, Date dd-MM-yyyy.
- Outputs mask account numbers (only last 4 visible) and include SHA256 of the original CSV line.

Run (short)
1. Build in Visual Studio or using dotnet CLI.
2. Run the app and open `SampleData/Sample.csv`.
3. Click Process. Check `Success.csv` and `Error.log` next to the input file.
