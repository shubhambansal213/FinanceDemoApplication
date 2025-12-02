using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace TransactionBatchProcessor
{
    public partial class MainWindow : Window
    {
        private string _selectedFile = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog()
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    Title = "Select transactions CSV"
                };

                if (dlg.ShowDialog(this) == true)
                {
                    _selectedFile = dlg.FileName;
                    TxtFilePath.Text = _selectedFile;
                    AppendLog($"Selected: {_selectedFile}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error selecting file: {ex.Message}");
                try { TransactionBatchProcessor.Utilities.Logger.LogException(ex, "MainWindow.BtnSelect_Click"); } catch { }
            }
        }

        private async void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedFile))
            {
                MessageBox.Show(this, "Please select a CSV file first.", "No File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnProcess.IsEnabled = false;
            BtnSelect.IsEnabled = false;
            Progress.Value = 0;
            TxtLog.Clear();

            var processor = new Services.TransactionProcessor();

            var start = DateTime.UtcNow;

            // progress callback updates UI
            void ProgressCallback(string text, double percent)
            {
                Dispatcher.Invoke(() =>
                {
                    if (percent >= 0)
                        Progress.Value = percent;
                    AppendLog(text);
                });
            }

            try
            {
                var result = await Task.Run(() => processor.ProcessFile(_selectedFile, ProgressCallback));

                var took = DateTime.UtcNow - start;
                AppendLog($"\nProcessing complete. TimeTaken: {took}");
                AppendLog($"Total: {result.TotalRecords}, Success: {result.SuccessCount}, Failed: {result.FailedCount}, MaxWorkers: {result.MaxWorkerThreadsUsed}");
            }
            catch (Exception ex)
            {
                AppendLog($"Error during processing: {ex.Message}");
                try { TransactionBatchProcessor.Utilities.Logger.LogException(ex, "MainWindow.BtnProcess_Click"); } catch { }
            }
            finally
            {
                BtnProcess.IsEnabled = true;
                BtnSelect.IsEnabled = true;
                Progress.Value = 100;
            }
        }

        private void AppendLog(string text)
        {
            TxtLog.AppendText(text + Environment.NewLine);
            TxtLog.ScrollToEnd();
        }
    }
}
