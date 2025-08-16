using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using SijaTech.Core.Utilities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Engine.Services
{
    /// <summary>
    /// Implementation of file operations service
    /// </summary>
    public class FileService : IFileService
    {
        private readonly ILogger<FileService> _logger;

        public FileService(ILogger<FileService> logger)
        {
            _logger = logger;
        }

        public async Task<long> GetDirectorySizeAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting directory size for: {Path}", path);
                
                var size = await FileHelper.GetDirectorySizeAsync(path, cancellationToken);
                
                _logger.LogDebug("Directory {Path} size: {Size} bytes", path, size);
                return size;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting directory size for: {Path}", path);
                return 0;
            }
        }

        public async Task<bool> SafeDeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Safe deleting file: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
                    return true; // Consider it successful if file doesn't exist
                }

                // Check if file is safe to delete
                if (!FileHelper.IsSafeToDelete(filePath))
                {
                    _logger.LogWarning("File is not safe to delete: {FilePath}", filePath);
                    return false;
                }

                // Check if file is in use
                if (FileHelper.IsFileInUse(filePath))
                {
                    _logger.LogWarning("File is in use, cannot delete: {FilePath}", filePath);
                    return false;
                }

                var result = await FileHelper.SafeDeleteFileAsync(filePath, maxRetries: 3, cancellationToken);
                
                if (result)
                {
                    _logger.LogInformation("Successfully deleted file: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("Failed to delete file: {FilePath}", filePath);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> SafeDeleteDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Safe deleting directory: {DirectoryPath}", directoryPath);

                if (!Directory.Exists(directoryPath))
                {
                    _logger.LogWarning("Directory not found for deletion: {DirectoryPath}", directoryPath);
                    return true; // Consider it successful if directory doesn't exist
                }

                // Check if directory is safe to delete
                if (!FileHelper.IsSafeToDelete(directoryPath))
                {
                    _logger.LogWarning("Directory is not safe to delete: {DirectoryPath}", directoryPath);
                    return false;
                }

                await Task.Run(() =>
                {
                    try
                    {
                        // Delete all files in directory first
                        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            try
                            {
                                File.SetAttributes(file, FileAttributes.Normal);
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete file in directory: {File}", file);
                            }
                        }

                        // Delete the directory
                        Directory.Delete(directoryPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting directory contents: {DirectoryPath}", directoryPath);
                        throw;
                    }
                }, cancellationToken);

                _logger.LogInformation("Successfully deleted directory: {DirectoryPath}", directoryPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting directory: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public async Task<bool> SecureDeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Secure deleting file: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for secure deletion: {FilePath}", filePath);
                    return true;
                }

                // Check if file is safe to delete
                if (!FileHelper.IsSafeToDelete(filePath))
                {
                    _logger.LogWarning("File is not safe to secure delete: {FilePath}", filePath);
                    return false;
                }

                var result = await FileHelper.SecureDeleteFileAsync(filePath, passes: 3, cancellationToken);
                
                if (result)
                {
                    _logger.LogInformation("Successfully secure deleted file: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("Failed to secure delete file: {FilePath}", filePath);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error secure deleting file: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<string> GetFileHashAsync(string filePath, string algorithm = "SHA256", CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting {Algorithm} hash for file: {FilePath}", algorithm, filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for hashing: {FilePath}", filePath);
                    return string.Empty;
                }

                var hash = await FileHelper.GetFileHashAsync(filePath, algorithm, cancellationToken);
                
                _logger.LogDebug("File {FilePath} {Algorithm} hash: {Hash}", filePath, algorithm, hash);
                return hash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file hash: {FilePath}", filePath);
                return string.Empty;
            }
        }

        public bool IsFileInUse(string filePath)
        {
            try
            {
                var inUse = FileHelper.IsFileInUse(filePath);
                _logger.LogDebug("File {FilePath} in use: {InUse}", filePath, inUse);
                return inUse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file is in use: {FilePath}", filePath);
                return true; // Assume in use if we can't check
            }
        }

        public async Task<FileInfo?> GetFileInfoAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting file info for: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for info: {FilePath}", filePath);
                    return null;
                }

                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new FileInfo(filePath);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file info: {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Get common temporary directories for cleaning
        /// </summary>
        public List<string> GetTempDirectories()
        {
            try
            {
                var tempDirs = FileHelper.GetCommonTempDirectories();
                _logger.LogDebug("Found {Count} temporary directories", tempDirs.Count);
                return tempDirs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting temp directories");
                return new List<string>();
            }
        }

        /// <summary>
        /// Get browser cache directories
        /// </summary>
        public List<string> GetBrowserCacheDirectories()
        {
            try
            {
                var cacheDirs = new List<string>();
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                // Chrome
                var chromeCache = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache");
                if (Directory.Exists(chromeCache))
                    cacheDirs.Add(chromeCache);

                // Firefox
                var firefoxProfiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox\Profiles");
                if (Directory.Exists(firefoxProfiles))
                {
                    foreach (var profile in Directory.GetDirectories(firefoxProfiles))
                    {
                        var cacheDir = Path.Combine(profile, "cache2");
                        if (Directory.Exists(cacheDir))
                            cacheDirs.Add(cacheDir);
                    }
                }

                // Edge
                var edgeCache = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache");
                if (Directory.Exists(edgeCache))
                    cacheDirs.Add(edgeCache);

                // Internet Explorer
                var ieCache = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
                if (Directory.Exists(ieCache))
                    cacheDirs.Add(ieCache);

                _logger.LogDebug("Found {Count} browser cache directories", cacheDirs.Count);
                return cacheDirs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting browser cache directories");
                return new List<string>();
            }
        }

        /// <summary>
        /// Get files in directory with pattern matching
        /// </summary>
        public async Task<List<string>> GetFilesAsync(string directoryPath, string searchPattern = "*", 
            SearchOption searchOption = SearchOption.TopDirectoryOnly, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return new List<string>();

                return await Task.Run(() =>
                {
                    var files = new List<string>();
                    
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(directoryPath, searchPattern, searchOption))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            files.Add(file);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _logger.LogWarning("Access denied to directory: {DirectoryPath}", directoryPath);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        _logger.LogWarning("Directory not found: {DirectoryPath}", directoryPath);
                    }

                    return files;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files from directory: {DirectoryPath}", directoryPath);
                return new List<string>();
            }
        }

        /// <summary>
        /// Check if path is safe to clean
        /// </summary>
        public bool IsSafeToClean(string path)
        {
            return FileHelper.IsSafeToDelete(path);
        }
    }
}
