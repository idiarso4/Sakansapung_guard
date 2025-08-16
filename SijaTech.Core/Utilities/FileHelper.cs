using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Core.Utilities
{
    /// <summary>
    /// Helper utilities untuk file operations
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// Format bytes ke human-readable string
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
                if (counter >= suffixes.Length - 1) break;
            }
            
            return $"{number:n1} {suffixes[counter]}";
        }

        /// <summary>
        /// Mendapatkan hash file dengan algoritma tertentu
        /// </summary>
        public static async Task<string> GetFileHashAsync(string filePath, string algorithm = "SHA256", 
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 
                bufferSize: 4096, useAsync: true);
            
            using HashAlgorithm hashAlgorithm = algorithm.ToUpperInvariant() switch
            {
                "MD5" => MD5.Create(),
                "SHA1" => SHA1.Create(),
                "SHA256" => SHA256.Create(),
                "SHA512" => SHA512.Create(),
                _ => throw new ArgumentException($"Unsupported hash algorithm: {algorithm}")
            };

            var hashBytes = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Cek apakah file sedang digunakan oleh process lain
        /// </summary>
        public static bool IsFileInUse(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        /// <summary>
        /// Mendapatkan ukuran direktori secara rekursif
        /// </summary>
        public static async Task<long> GetDirectorySizeAsync(string directoryPath, 
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directoryPath))
                return 0;

            long totalSize = 0;
            var directoryInfo = new DirectoryInfo(directoryPath);

            await Task.Run(() =>
            {
                try
                {
                    foreach (var file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            totalSize += file.Length;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip files we can't access
                        }
                        catch (FileNotFoundException)
                        {
                            // Skip files that were deleted during enumeration
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip directories we can't access
                }
            }, cancellationToken);

            return totalSize;
        }

        /// <summary>
        /// Secure delete file dengan overwrite
        /// </summary>
        public static async Task<bool> SecureDeleteFileAsync(string filePath, int passes = 3, 
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileSize = fileInfo.Length;

                // Overwrite file multiple times
                for (int pass = 0; pass < passes; pass++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
                    
                    // Fill with random data
                    var buffer = new byte[4096];
                    var random = new Random();
                    
                    for (long written = 0; written < fileSize; written += buffer.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        random.NextBytes(buffer);
                        var bytesToWrite = (int)Math.Min(buffer.Length, fileSize - written);
                        await stream.WriteAsync(buffer.AsMemory(0, bytesToWrite), cancellationToken);
                    }
                    
                    await stream.FlushAsync(cancellationToken);
                }

                // Finally delete the file
                File.Delete(filePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Safe delete dengan retry mechanism
        /// </summary>
        public static async Task<bool> SafeDeleteFileAsync(string filePath, int maxRetries = 3, 
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                return true;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Try to delete
                    File.Delete(filePath);
                    return true;
                }
                catch (IOException) when (attempt < maxRetries - 1)
                {
                    // File might be in use, wait and retry
                    await Task.Delay(1000, cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                    // Try to remove read-only attribute
                    try
                    {
                        File.SetAttributes(filePath, FileAttributes.Normal);
                        File.Delete(filePath);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Mendapatkan temporary directories yang umum
        /// </summary>
        public static List<string> GetCommonTempDirectories()
        {
            var tempDirs = new List<string>();

            // Windows temp directories
            var windowsTemp = Environment.GetEnvironmentVariable("TEMP");
            if (!string.IsNullOrEmpty(windowsTemp) && Directory.Exists(windowsTemp))
                tempDirs.Add(windowsTemp);

            var windowsTmp = Environment.GetEnvironmentVariable("TMP");
            if (!string.IsNullOrEmpty(windowsTmp) && Directory.Exists(windowsTmp))
                tempDirs.Add(windowsTmp);

            // System temp
            var systemTemp = Path.GetTempPath();
            if (Directory.Exists(systemTemp))
                tempDirs.Add(systemTemp);

            // Windows specific temp locations
            var commonTempPaths = new[]
            {
                @"C:\Windows\Temp",
                @"C:\Windows\Prefetch",
                @"C:\Windows\SoftwareDistribution\Download",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Recent))
            };

            foreach (var path in commonTempPaths)
            {
                if (Directory.Exists(path) && !tempDirs.Contains(path))
                    tempDirs.Add(path);
            }

            return tempDirs;
        }

        /// <summary>
        /// Cek apakah path aman untuk dihapus
        /// </summary>
        public static bool IsSafeToDelete(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var normalizedPath = Path.GetFullPath(path).ToLowerInvariant();

            // Blacklist critical system paths
            var criticalPaths = new[]
            {
                @"c:\windows\system32",
                @"c:\windows\syswow64",
                @"c:\program files",
                @"c:\program files (x86)",
                @"c:\users\default",
                @"c:\programdata\microsoft\windows\start menu",
                @"c:\windows\boot",
                @"c:\windows\fonts"
            };

            foreach (var criticalPath in criticalPaths)
            {
                if (normalizedPath.StartsWith(criticalPath))
                    return false;
            }

            return true;
        }
    }
}
