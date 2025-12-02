using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace TransactionBatchProcessor.Utilities
{
    /// <summary>
    /// Simple thread-safe file logger for exceptions and messages.
    /// Writes UTF-8 entries to Error.log in the application base folder.
    /// Use Logger.LogException(ex, "optional context") in catch blocks.
    /// </summary>
    public static class Logger
    {
        private static readonly object _sync = new object();
        private static readonly string LogFilePath;

        static Logger()
        {
            // Put the log next to the application binary so it's easy to find during demos/tests.
            var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            LogFilePath = Path.Combine(baseDir, "Error.log");
        }

        /// <summary>
        /// Log an exception with timestamp, message, stack trace and the first available method/class frame.
        /// This method never throws.
        /// </summary>
        public static void LogException(Exception ex, string? context = null)
        {
            if (ex == null) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(new string('-', 120));
                sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
                if (!string.IsNullOrEmpty(context)) sb.AppendLine($"Context: {context}");

                // Top-level exception info
                sb.AppendLine($"ExceptionType: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");

                // Try to extract the method and class where this exception originated
                try
                {
                    var st = new StackTrace(ex, true);
                    var frames = st.GetFrames();
                    if (frames != null && frames.Length > 0)
                    {
                        // pick the first frame that has a declaring type (likely the origin)
                        foreach (var f in frames)
                        {
                            var m = f.GetMethod();
                            if (m == null) continue;
                            var declaring = m.DeclaringType;
                            if (declaring == null) continue;
                            sb.AppendLine($"Origin: {declaring.FullName}.{m.Name} (File: {f.GetFileName()} Line: {f.GetFileLineNumber()})");
                            break;
                        }
                    }
                    else if (ex.TargetSite != null)
                    {
                        var m = ex.TargetSite;
                        sb.AppendLine($"Origin: {m.DeclaringType?.FullName}.{m.Name}");
                    }
                }
                catch
                {
                    // ignore internal stack frame extraction errors
                }

                // Full stack trace and inner exceptions
                sb.AppendLine("StackTrace:");
                sb.AppendLine(ex.StackTrace ?? "(no stack trace)");

                var inner = ex.InnerException;
                while (inner != null)
                {
                    sb.AppendLine("--- Inner Exception ---");
                    sb.AppendLine($"Type: {inner.GetType().FullName}");
                    sb.AppendLine($"Message: {inner.Message}");
                    sb.AppendLine(inner.StackTrace ?? "(no stack trace)");
                    inner = inner.InnerException;
                }

                sb.AppendLine();

                // Write to file in a thread-safe manner
                lock (_sync)
                {
                    File.AppendAllText(LogFilePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Intentionally swallow any exception to avoid bringing down the host
            }
        }

        /// <summary>
        /// Log a plain informational message to the same log file with timestamp.
        /// </summary>
        public static void LogMessage(string message)
        {
            try
            {
                var entry = $"[{DateTime.UtcNow:O}] INFO: {message}{Environment.NewLine}";
                lock (_sync)
                {
                    File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
                }
            }
            catch
            {
                // swallow
            }
        }
    }
}
