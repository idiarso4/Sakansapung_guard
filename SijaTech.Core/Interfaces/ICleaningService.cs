using SijaTech.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Core.Interfaces
{
    /// <summary>
    /// Interface untuk service pembersihan sistem
    /// </summary>
    public interface ICleaningService
    {
        /// <summary>
        /// Melakukan scan untuk mengetahui file yang bisa dibersihkan
        /// </summary>
        Task<CleaningResult> ScanAsync(CleaningOptions options, 
            IProgress<CleaningProgress>? progress = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Melakukan pembersihan berdasarkan hasil scan
        /// </summary>
        Task<CleaningResult> CleanAsync(CleaningOptions options, 
            IProgress<CleaningProgress>? progress = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Membersihkan temporary files
        /// </summary>
        Task<CleaningResult> CleanTempFilesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Membersihkan browser data
        /// </summary>
        Task<CleaningResult> CleanBrowserDataAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Membersihkan recycle bin
        /// </summary>
        Task<CleaningResult> CleanRecycleBinAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Membersihkan system logs
        /// </summary>
        Task<CleaningResult> CleanSystemLogsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Membersihkan recent documents
        /// </summary>
        Task<CleaningResult> CleanRecentDocumentsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface untuk registry operations
    /// </summary>
    public interface IRegistryService
    {
        /// <summary>
        /// Scan registry untuk entries yang tidak valid
        /// </summary>
        Task<CleaningResult> ScanRegistryAsync(IProgress<CleaningProgress>? progress = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Membersihkan registry entries yang tidak valid
        /// </summary>
        Task<CleaningResult> CleanRegistryAsync(IProgress<CleaningProgress>? progress = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Backup registry sebelum pembersihan
        /// </summary>
        Task<bool> BackupRegistryAsync(string backupPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restore registry dari backup
        /// </summary>
        Task<bool> RestoreRegistryAsync(string backupPath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface untuk file operations
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// Menghitung ukuran direktori
        /// </summary>
        Task<long> GetDirectorySizeAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Menghapus file secara aman
        /// </summary>
        Task<bool> SafeDeleteFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Menghapus direktori secara aman
        /// </summary>
        Task<bool> SafeDeleteDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Secure delete (overwrite) file
        /// </summary>
        Task<bool> SecureDeleteFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mendapatkan hash file
        /// </summary>
        Task<string> GetFileHashAsync(string filePath, string algorithm = "SHA256", 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cek apakah file sedang digunakan
        /// </summary>
        bool IsFileInUse(string filePath);

        /// <summary>
        /// Mendapatkan informasi file
        /// </summary>
        Task<FileInfo?> GetFileInfoAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mendapatkan daftar temporary directories
        /// </summary>
        List<string> GetTempDirectories();

        /// <summary>
        /// Mendapatkan daftar browser cache directories
        /// </summary>
        List<string> GetBrowserCacheDirectories();

        /// <summary>
        /// Mendapatkan files dalam directory dengan pattern
        /// </summary>
        Task<List<string>> GetFilesAsync(string directoryPath, string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cek apakah path aman untuk dibersihkan
        /// </summary>
        bool IsSafeToClean(string path);
    }
}
