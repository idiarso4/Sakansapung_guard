using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using SijaTech.Core.Models;
using SijaTech.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Engine.Services
{
    /// <summary>
    /// Implementation of registry operations service
    /// </summary>
    public class RegistryService : IRegistryService
    {
        private readonly ILogger<RegistryService> _logger;
        private readonly string _backupDirectory;

        public RegistryService(ILogger<RegistryService> logger)
        {
            _logger = logger;
            _backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SijaTech", "Backups");

            // Ensure backup directory exists
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }
        }

        public async Task<CleaningResult> ScanRegistryAsync(IProgress<CleaningProgress>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            var result = new CleaningResult { Success = true, Message = "Registry scan completed" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting registry scan...");

                progress?.Report(new CleaningProgress 
                { 
                    PercentComplete = 0, 
                    CurrentOperation = "Initializing registry scan..." 
                });

                var invalidEntries = await RegistryHelper.ScanInvalidEntriesAsync(
                    new Progress<(int processed, string current)>(p => 
                    {
                        progress?.Report(new CleaningProgress
                        {
                            PercentComplete = Math.Min(90, p.processed * 10), // Cap at 90% during scan
                            CurrentOperation = $"Scanning {p.current}...",
                            FilesProcessed = p.processed
                        });
                    }), 
                    cancellationToken);

                progress?.Report(new CleaningProgress 
                { 
                    PercentComplete = 100, 
                    CurrentOperation = "Registry scan completed" 
                });

                result.FilesDeleted = invalidEntries.Count;
                result.DeletedFiles = invalidEntries;
                result.Duration = stopwatch.Elapsed;

                _logger.LogInformation("Registry scan completed. Found {Count} invalid entries", invalidEntries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registry scan");
                result.Success = false;
                result.Message = ex.Message;
                result.Errors.Add(ex.Message);
            }
            finally
            {
                stopwatch.Stop();
            }

            return result;
        }

        public async Task<CleaningResult> CleanRegistryAsync(IProgress<CleaningProgress>? progress = null, 
            CancellationToken cancellationToken = default)
        {
            var result = new CleaningResult { Success = true, Message = "Registry cleaning completed" };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting registry cleaning...");

                // First, create a backup
                progress?.Report(new CleaningProgress 
                { 
                    PercentComplete = 5, 
                    CurrentOperation = "Creating registry backup..." 
                });

                var backupPath = Path.Combine(_backupDirectory, $"registry_backup_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
                var backupSuccess = await BackupRegistryAsync(backupPath, cancellationToken);

                if (!backupSuccess)
                {
                    _logger.LogWarning("Registry backup failed, proceeding with caution");
                }

                // Scan for invalid entries
                progress?.Report(new CleaningProgress 
                { 
                    PercentComplete = 15, 
                    CurrentOperation = "Scanning for invalid entries..." 
                });

                var invalidEntries = await RegistryHelper.ScanInvalidEntriesAsync(
                    new Progress<(int processed, string current)>(p => 
                    {
                        var scanProgress = 15 + (p.processed * 30 / Math.Max(1, p.processed + 10)); // 15-45%
                        progress?.Report(new CleaningProgress
                        {
                            PercentComplete = scanProgress,
                            CurrentOperation = $"Scanning {p.current}...",
                            FilesProcessed = p.processed
                        });
                    }), 
                    cancellationToken);

                if (invalidEntries.Count == 0)
                {
                    progress?.Report(new CleaningProgress 
                    { 
                        PercentComplete = 100, 
                        CurrentOperation = "No invalid entries found" 
                    });

                    result.Message = "No invalid registry entries found";
                    result.Duration = stopwatch.Elapsed;
                    return result;
                }

                // Clean invalid entries
                var cleanedEntries = new List<string>();
                var errors = new List<string>();

                for (int i = 0; i < invalidEntries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entry = invalidEntries[i];
                    var progressPercent = 50 + (i * 45 / invalidEntries.Count); // 50-95%

                    progress?.Report(new CleaningProgress
                    {
                        PercentComplete = progressPercent,
                        CurrentOperation = $"Cleaning registry entry {i + 1} of {invalidEntries.Count}...",
                        CurrentFile = entry,
                        FilesProcessed = i + 1
                    });

                    try
                    {
                        if (await CleanRegistryEntryAsync(entry, cancellationToken))
                        {
                            cleanedEntries.Add(entry);
                            _logger.LogDebug("Cleaned registry entry: {Entry}", entry);
                        }
                        else
                        {
                            errors.Add($"Failed to clean: {entry}");
                            _logger.LogWarning("Failed to clean registry entry: {Entry}", entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Error cleaning {entry}: {ex.Message}";
                        errors.Add(errorMsg);
                        _logger.LogError(ex, "Error cleaning registry entry: {Entry}", entry);
                    }

                    // Small delay to prevent overwhelming the system
                    await Task.Delay(10, cancellationToken);
                }

                progress?.Report(new CleaningProgress 
                { 
                    PercentComplete = 100, 
                    CurrentOperation = "Registry cleaning completed" 
                });

                result.FilesDeleted = cleanedEntries.Count;
                result.DeletedFiles = cleanedEntries;
                result.Errors = errors;
                result.Duration = stopwatch.Elapsed;

                if (errors.Count > 0)
                {
                    result.Success = false;
                    result.Message = $"Registry cleaning completed with {errors.Count} errors";
                }

                _logger.LogInformation("Registry cleaning completed. Cleaned {CleanedCount} entries, {ErrorCount} errors", 
                    cleanedEntries.Count, errors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registry cleaning");
                result.Success = false;
                result.Message = ex.Message;
                result.Errors.Add(ex.Message);
            }
            finally
            {
                stopwatch.Stop();
            }

            return result;
        }

        public async Task<bool> BackupRegistryAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating registry backup: {BackupPath}", backupPath);

                // Ensure backup directory exists
                var backupDir = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Backup common registry keys that we might clean
                var keysToBackup = new[]
                {
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths",
                    @"HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache"
                };

                var allBackupsSuccessful = true;

                foreach (var keyPath in keysToBackup)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var keyBackupPath = backupPath.Replace(".reg", $"_{keyPath.Replace("\\", "_").Replace(":", "")}.reg");
                        var success = await RegistryHelper.BackupRegistryKeyAsync(keyPath, keyBackupPath, cancellationToken);
                        
                        if (!success)
                        {
                            _logger.LogWarning("Failed to backup registry key: {KeyPath}", keyPath);
                            allBackupsSuccessful = false;
                        }
                        else
                        {
                            _logger.LogDebug("Successfully backed up registry key: {KeyPath}", keyPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error backing up registry key: {KeyPath}", keyPath);
                        allBackupsSuccessful = false;
                    }
                }

                if (allBackupsSuccessful)
                {
                    _logger.LogInformation("Registry backup completed successfully: {BackupPath}", backupPath);
                }
                else
                {
                    _logger.LogWarning("Registry backup completed with some failures: {BackupPath}", backupPath);
                }

                return allBackupsSuccessful;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating registry backup: {BackupPath}", backupPath);
                return false;
            }
        }

        public async Task<bool> RestoreRegistryAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Restoring registry from backup: {BackupPath}", backupPath);

                if (!File.Exists(backupPath))
                {
                    _logger.LogError("Backup file not found: {BackupPath}", backupPath);
                    return false;
                }

                var success = await RegistryHelper.RestoreRegistryFromBackupAsync(backupPath, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Registry restored successfully from: {BackupPath}", backupPath);
                }
                else
                {
                    _logger.LogError("Failed to restore registry from: {BackupPath}", backupPath);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring registry from backup: {BackupPath}", backupPath);
                return false;
            }
        }

        private async Task<bool> CleanRegistryEntryAsync(string entryPath, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(1, cancellationToken); // Simulate async operation

                // Parse the entry path to determine if it's a key or value
                var parts = entryPath.Split('\\');
                if (parts.Length < 2)
                    return false;

                // Check if this is a safe entry to delete
                if (!RegistryHelper.IsSafeToDelete(entryPath))
                {
                    _logger.LogWarning("Entry is not safe to delete: {EntryPath}", entryPath);
                    return false;
                }

                // Determine if this is a value or key deletion
                var lastPart = parts[^1];
                if (lastPart.Contains("=") || entryPath.Contains("\\\\")) // Likely a value
                {
                    var keyPath = string.Join("\\", parts[..^1]);
                    var valueName = lastPart;
                    
                    return RegistryHelper.SafeDeleteValue(keyPath, valueName);
                }
                else // Likely a key
                {
                    return RegistryHelper.SafeDeleteKey(entryPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning registry entry: {EntryPath}", entryPath);
                return false;
            }
        }

        /// <summary>
        /// Get list of available registry backups
        /// </summary>
        public async Task<List<(string path, DateTime created, long size)>> GetAvailableBackupsAsync(CancellationToken cancellationToken = default)
        {
            var backups = new List<(string path, DateTime created, long size)>();

            try
            {
                await Task.Delay(1, cancellationToken);

                if (!Directory.Exists(_backupDirectory))
                    return backups;

                var backupFiles = Directory.GetFiles(_backupDirectory, "*.reg");
                
                foreach (var file in backupFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        backups.Add((file, fileInfo.CreationTime, fileInfo.Length));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error getting backup file info: {File}", file);
                    }
                }

                // Sort by creation time, newest first
                backups.Sort((a, b) => b.created.CompareTo(a.created));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available backups");
            }

            return backups;
        }

        /// <summary>
        /// Delete old backup files
        /// </summary>
        public async Task<int> CleanupOldBackupsAsync(int daysToKeep = 30, CancellationToken cancellationToken = default)
        {
            var deletedCount = 0;

            try
            {
                _logger.LogInformation("Cleaning up old registry backups older than {Days} days", daysToKeep);

                await Task.Delay(1, cancellationToken);

                if (!Directory.Exists(_backupDirectory))
                    return 0;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var backupFiles = Directory.GetFiles(_backupDirectory, "*.reg");

                foreach (var file in backupFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate)
                        {
                            File.Delete(file);
                            deletedCount++;
                            _logger.LogDebug("Deleted old backup: {File}", file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deleting old backup: {File}", file);
                    }
                }

                _logger.LogInformation("Cleaned up {Count} old registry backups", deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old backups");
            }

            return deletedCount;
        }

        /// <summary>
        /// Get registry cleaning statistics
        /// </summary>
        public async Task<(int totalScanned, int invalidFound, int cleaned, int errors)> GetCleaningStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // In real implementation, this would come from a database or log analysis
                await Task.Delay(10, cancellationToken);

                // Simulate statistics
                return (1250, 45, 42, 3);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cleaning statistics");
                return (0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Validate registry backup file
        /// </summary>
        public async Task<bool> ValidateBackupAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(1, cancellationToken);

                if (!File.Exists(backupPath))
                    return false;

                // Basic validation - check if file is a valid registry export
                var content = await File.ReadAllTextAsync(backupPath, cancellationToken);
                
                return content.StartsWith("Windows Registry Editor") || 
                       content.StartsWith("REGEDIT4");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating backup: {BackupPath}", backupPath);
                return false;
            }
        }
    }
}
