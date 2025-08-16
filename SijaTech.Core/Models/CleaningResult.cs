using System;
using System.Collections.Generic;

namespace SijaTech.Core.Models
{
    /// <summary>
    /// Hasil dari operasi pembersihan
    /// </summary>
    public class CleaningResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long BytesFreed { get; set; }
        public int FilesDeleted { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> DeletedFiles { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime CompletedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Format ukuran file yang user-friendly
        /// </summary>
        public string FormattedSize => FormatBytes(BytesFreed);

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }

    /// <summary>
    /// Progress callback untuk operasi pembersihan
    /// </summary>
    public class CleaningProgress
    {
        public int PercentComplete { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
        public long BytesProcessed { get; set; }
        public int FilesProcessed { get; set; }
    }

    /// <summary>
    /// Konfigurasi untuk operasi pembersihan
    /// </summary>
    public class CleaningOptions
    {
        public bool CleanTempFiles { get; set; } = true;
        public bool CleanBrowserData { get; set; } = true;
        public bool CleanRecycleBin { get; set; } = false;
        public bool CleanSystemLogs { get; set; } = false;
        public bool CleanRecentDocuments { get; set; } = false;
        public bool CreateBackup { get; set; } = true;
        public bool ConfirmDeletion { get; set; } = true;
        public List<string> ExcludedPaths { get; set; } = new();
        public List<string> ExcludedExtensions { get; set; } = new();
    }
}
