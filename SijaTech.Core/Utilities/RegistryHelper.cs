using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Core.Utilities
{
    /// <summary>
    /// Helper utilities untuk registry operations
    /// </summary>
    public static class RegistryHelper
    {
        /// <summary>
        /// Registry keys yang aman untuk dibersihkan
        /// </summary>
        public static readonly Dictionary<string, string> SafeCleanupKeys = new()
        {
            { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs", "Recent Documents" },
            { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU", "Run MRU" },
            { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths", "Typed Paths" },
            { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery", "Search History" },
            { @"HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\TypedURLs", "IE Typed URLs" },
            { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit\Favorites", "RegEdit Favorites" }
        };

        /// <summary>
        /// Registry keys yang TIDAK BOLEH disentuh
        /// </summary>
        public static readonly HashSet<string> CriticalKeys = new()
        {
            @"HKEY_LOCAL_MACHINE\SYSTEM",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"HKEY_LOCAL_MACHINE\HARDWARE",
            @"HKEY_LOCAL_MACHINE\SAM",
            @"HKEY_LOCAL_MACHINE\SECURITY"
        };

        /// <summary>
        /// Cek apakah registry key aman untuk dihapus
        /// </summary>
        public static bool IsSafeToDelete(string keyPath)
        {
            if (string.IsNullOrEmpty(keyPath))
                return false;

            var normalizedPath = keyPath.ToUpperInvariant();

            // Cek critical keys
            foreach (var criticalKey in CriticalKeys)
            {
                if (normalizedPath.StartsWith(criticalKey.ToUpperInvariant()))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Backup registry key ke file
        /// </summary>
        public static async Task<bool> BackupRegistryKeyAsync(string keyPath, string backupFilePath, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Run(() =>
                {
                    var (hive, subKey) = ParseRegistryPath(keyPath);
                    if (hive == null || string.IsNullOrEmpty(subKey))
                        throw new ArgumentException($"Invalid registry path: {keyPath}");

                    using var key = hive.OpenSubKey(subKey, false);
                    if (key == null)
                        throw new ArgumentException($"Registry key not found: {keyPath}");

                    // Export using reg.exe command
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "reg.exe",
                            Arguments = $"export \"{keyPath}\" \"{backupFilePath}\" /y",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new InvalidOperationException($"Registry backup failed with exit code: {process.ExitCode}");

                }, cancellationToken);

                return File.Exists(backupFilePath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Restore registry dari backup file
        /// </summary>
        public static async Task<bool> RestoreRegistryFromBackupAsync(string backupFilePath, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                    return false;

                await Task.Run(() =>
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "reg.exe",
                            Arguments = $"import \"{backupFilePath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new InvalidOperationException($"Registry restore failed with exit code: {process.ExitCode}");

                }, cancellationToken);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Parse registry path menjadi hive dan subkey
        /// </summary>
        public static (RegistryKey? hive, string subKey) ParseRegistryPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return (null, string.Empty);

            var parts = fullPath.Split('\\', 2);
            if (parts.Length < 2)
                return (null, string.Empty);

            var hiveName = parts[0].ToUpperInvariant();
            var subKey = parts[1];

            var hive = hiveName switch
            {
                "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
                "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
                "HKEY_USERS" or "HKU" => Registry.Users,
                "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
                _ => null
            };

            return (hive, subKey);
        }

        /// <summary>
        /// Scan registry untuk invalid entries
        /// </summary>
        public static async Task<List<string>> ScanInvalidEntriesAsync(
            IProgress<(int processed, string current)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var invalidEntries = new List<string>();
            int processed = 0;

            await Task.Run(() =>
            {
                // Scan common locations for invalid entries
                var locationsToScan = new[]
                {
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
                    @"HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts"
                };

                foreach (var location in locationsToScan)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        progress?.Report((processed++, location));
                        var invalid = ScanRegistryLocation(location);
                        invalidEntries.AddRange(invalid);
                    }
                    catch (Exception)
                    {
                        // Skip locations we can't access
                    }
                }
            }, cancellationToken);

            return invalidEntries;
        }

        /// <summary>
        /// Scan specific registry location untuk invalid entries
        /// </summary>
        private static List<string> ScanRegistryLocation(string keyPath)
        {
            var invalidEntries = new List<string>();

            try
            {
                var (hive, subKey) = ParseRegistryPath(keyPath);
                if (hive == null)
                    return invalidEntries;

                using var key = hive.OpenSubKey(subKey, false);
                if (key == null)
                    return invalidEntries;

                // Check value entries
                foreach (var valueName in key.GetValueNames())
                {
                    try
                    {
                        var value = key.GetValue(valueName);
                        if (value is string stringValue && !string.IsNullOrEmpty(stringValue))
                        {
                            // Check if referenced file/path exists
                            if (IsFilePath(stringValue) && !File.Exists(stringValue) && !Directory.Exists(stringValue))
                            {
                                invalidEntries.Add($"{keyPath}\\{valueName}");
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Skip values we can't read
                    }
                }

                // Check subkeys
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKeyObj = key.OpenSubKey(subKeyName, false);
                        if (subKeyObj == null)
                        {
                            invalidEntries.Add($"{keyPath}\\{subKeyName}");
                        }
                    }
                    catch (Exception)
                    {
                        invalidEntries.Add($"{keyPath}\\{subKeyName}");
                    }
                }
            }
            catch (Exception)
            {
                // Skip if we can't access the key
            }

            return invalidEntries;
        }

        /// <summary>
        /// Cek apakah string adalah file path
        /// </summary>
        private static bool IsFilePath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            try
            {
                // Simple heuristic: contains drive letter or UNC path
                return value.Length > 3 && 
                       (value[1] == ':' || value.StartsWith(@"\\")) &&
                       (value.Contains('\\') || value.Contains('/'));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Delete registry value safely
        /// </summary>
        public static bool SafeDeleteValue(string keyPath, string valueName)
        {
            if (!IsSafeToDelete(keyPath))
                return false;

            try
            {
                var (hive, subKey) = ParseRegistryPath(keyPath);
                if (hive == null)
                    return false;

                using var key = hive.OpenSubKey(subKey, true);
                if (key == null)
                    return false;

                key.DeleteValue(valueName, false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Delete registry key safely
        /// </summary>
        public static bool SafeDeleteKey(string keyPath)
        {
            if (!IsSafeToDelete(keyPath))
                return false;

            try
            {
                var (hive, subKey) = ParseRegistryPath(keyPath);
                if (hive == null)
                    return false;

                var lastBackslash = subKey.LastIndexOf('\\');
                if (lastBackslash == -1)
                    return false;

                var parentPath = subKey.Substring(0, lastBackslash);
                var keyName = subKey.Substring(lastBackslash + 1);

                using var parentKey = hive.OpenSubKey(parentPath, true);
                if (parentKey == null)
                    return false;

                parentKey.DeleteSubKeyTree(keyName, false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
