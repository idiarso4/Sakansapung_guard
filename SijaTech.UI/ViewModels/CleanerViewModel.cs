using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SijaTech.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SijaTech.UI.ViewModels
{
    public partial class CleanerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "System Cleaner";

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private bool _isCleaning = false;

        [ObservableProperty]
        private int _progressPercentage = 0;

        [ObservableProperty]
        private string _currentOperation = string.Empty;

        [ObservableProperty]
        private CleaningOptions _cleaningOptions = new();

        [ObservableProperty]
        private CleaningResult? _lastScanResult;

        [ObservableProperty]
        private CleaningResult? _lastCleanResult;

        [ObservableProperty]
        private string _totalFilesFound = "0";

        [ObservableProperty]
        private string _totalSizeFound = "0 B";

        [ObservableProperty]
        private string _estimatedTimeToClean = "0 seconds";

        public ObservableCollection<CleaningCategoryViewModel> Categories { get; } = new();

        public CleanerViewModel()
        {
            InitializeCategories();
        }

        private void InitializeCategories()
        {
            Categories.Add(new CleaningCategoryViewModel
            {
                Name = "Temporary Files",
                Description = "Windows temporary files, user temp files",
                IsEnabled = CleaningOptions.CleanTempFiles,
                Icon = "FileTemporary",
                FilesFound = 0,
                SizeFound = 0,
                EstimatedTime = TimeSpan.Zero
            });

            Categories.Add(new CleaningCategoryViewModel
            {
                Name = "Browser Data",
                Description = "Cache, cookies, history dari semua browser",
                IsEnabled = CleaningOptions.CleanBrowserData,
                Icon = "Web",
                FilesFound = 0,
                SizeFound = 0,
                EstimatedTime = TimeSpan.Zero
            });

            Categories.Add(new CleaningCategoryViewModel
            {
                Name = "Recycle Bin",
                Description = "File yang ada di recycle bin",
                IsEnabled = CleaningOptions.CleanRecycleBin,
                Icon = "Delete",
                FilesFound = 0,
                SizeFound = 0,
                EstimatedTime = TimeSpan.Zero
            });

            Categories.Add(new CleaningCategoryViewModel
            {
                Name = "System Logs",
                Description = "Windows event logs dan crash dumps",
                IsEnabled = CleaningOptions.CleanSystemLogs,
                Icon = "FileDocument",
                FilesFound = 0,
                SizeFound = 0,
                EstimatedTime = TimeSpan.Zero
            });

            Categories.Add(new CleaningCategoryViewModel
            {
                Name = "Recent Documents",
                Description = "Daftar dokumen yang baru dibuka",
                IsEnabled = CleaningOptions.CleanRecentDocuments,
                Icon = "History",
                FilesFound = 0,
                SizeFound = 0,
                EstimatedTime = TimeSpan.Zero
            });
        }

        [RelayCommand]
        private async Task ScanAsync()
        {
            try
            {
                IsScanning = true;
                ProgressPercentage = 0;
                CurrentOperation = "Memulai scan...";

                // Reset results
                LastScanResult = null;
                TotalFilesFound = "0";
                TotalSizeFound = "0 B";

                // Simulate scanning each category
                for (int i = 0; i < Categories.Count; i++)
                {
                    var category = Categories[i];
                    if (!category.IsEnabled) continue;

                    CurrentOperation = $"Scanning {category.Name}...";
                    ProgressPercentage = (i * 100) / Categories.Count;

                    // Simulate scan delay
                    await Task.Delay(1000);

                    // Simulate found files
                    var random = new Random();
                    category.FilesFound = random.Next(10, 500);
                    category.SizeFound = random.Next(1024 * 1024, 100 * 1024 * 1024); // 1MB to 100MB
                    category.EstimatedTime = TimeSpan.FromSeconds(category.FilesFound * 0.1);
                }

                ProgressPercentage = 100;
                CurrentOperation = "Scan selesai";

                // Calculate totals
                int totalFiles = 0;
                long totalSize = 0;
                TimeSpan totalTime = TimeSpan.Zero;

                foreach (var category in Categories)
                {
                    if (category.IsEnabled)
                    {
                        totalFiles += category.FilesFound;
                        totalSize += category.SizeFound;
                        totalTime = totalTime.Add(category.EstimatedTime);
                    }
                }

                TotalFilesFound = totalFiles.ToString("N0");
                TotalSizeFound = FormatBytes(totalSize);
                EstimatedTimeToClean = FormatTime(totalTime);

                LastScanResult = new CleaningResult
                {
                    Success = true,
                    Message = "Scan completed successfully",
                    FilesDeleted = totalFiles,
                    BytesFreed = totalSize,
                    Duration = TimeSpan.FromSeconds(5),
                    CompletedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Error: {ex.Message}";
                LastScanResult = new CleaningResult
                {
                    Success = false,
                    Message = ex.Message,
                    CompletedAt = DateTime.Now
                };
            }
            finally
            {
                IsScanning = false;
                ProgressPercentage = 0;
                await Task.Delay(2000);
                CurrentOperation = string.Empty;
            }
        }

        [RelayCommand]
        private async Task CleanAsync()
        {
            if (LastScanResult == null || !LastScanResult.Success)
            {
                CurrentOperation = "Please run scan first";
                return;
            }

            try
            {
                IsCleaning = true;
                ProgressPercentage = 0;
                CurrentOperation = "Memulai pembersihan...";

                // Simulate cleaning each category
                for (int i = 0; i < Categories.Count; i++)
                {
                    var category = Categories[i];
                    if (!category.IsEnabled || category.FilesFound == 0) continue;

                    CurrentOperation = $"Cleaning {category.Name}...";
                    ProgressPercentage = (i * 100) / Categories.Count;

                    // Simulate cleaning delay
                    await Task.Delay(2000);

                    // Reset category after cleaning
                    category.FilesFound = 0;
                    category.SizeFound = 0;
                    category.EstimatedTime = TimeSpan.Zero;
                }

                ProgressPercentage = 100;
                CurrentOperation = "Pembersihan selesai";

                LastCleanResult = new CleaningResult
                {
                    Success = true,
                    Message = "Cleaning completed successfully",
                    FilesDeleted = int.Parse(TotalFilesFound.Replace(",", "")),
                    BytesFreed = ParseSize(TotalSizeFound),
                    Duration = TimeSpan.FromSeconds(10),
                    CompletedAt = DateTime.Now
                };

                // Reset totals
                TotalFilesFound = "0";
                TotalSizeFound = "0 B";
                EstimatedTimeToClean = "0 seconds";
                LastScanResult = null;
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Error: {ex.Message}";
                LastCleanResult = new CleaningResult
                {
                    Success = false,
                    Message = ex.Message,
                    CompletedAt = DateTime.Now
                };
            }
            finally
            {
                IsCleaning = false;
                ProgressPercentage = 0;
                await Task.Delay(2000);
                CurrentOperation = string.Empty;
            }
        }

        [RelayCommand]
        private void ToggleCategory(CleaningCategoryViewModel category)
        {
            category.IsEnabled = !category.IsEnabled;
            
            // Update cleaning options
            switch (category.Name)
            {
                case "Temporary Files":
                    CleaningOptions.CleanTempFiles = category.IsEnabled;
                    break;
                case "Browser Data":
                    CleaningOptions.CleanBrowserData = category.IsEnabled;
                    break;
                case "Recycle Bin":
                    CleaningOptions.CleanRecycleBin = category.IsEnabled;
                    break;
                case "System Logs":
                    CleaningOptions.CleanSystemLogs = category.IsEnabled;
                    break;
                case "Recent Documents":
                    CleaningOptions.CleanRecentDocuments = category.IsEnabled;
                    break;
            }
        }

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

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalSeconds < 60)
                return $"{time.TotalSeconds:F0} seconds";
            else if (time.TotalMinutes < 60)
                return $"{time.TotalMinutes:F1} minutes";
            else
                return $"{time.TotalHours:F1} hours";
        }

        private static long ParseSize(string sizeString)
        {
            // Simple parser for demonstration
            return 1024 * 1024; // 1MB default
        }
    }

    public partial class CleaningCategoryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _icon = "File";

        [ObservableProperty]
        private int _filesFound = 0;

        [ObservableProperty]
        private long _sizeFound = 0;

        [ObservableProperty]
        private TimeSpan _estimatedTime = TimeSpan.Zero;

        public string FormattedSize => FormatBytes(SizeFound);
        public string FormattedTime => FormatTime(EstimatedTime);

        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";
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

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalSeconds < 1) return "< 1 sec";
            if (time.TotalSeconds < 60) return $"{time.TotalSeconds:F0} sec";
            if (time.TotalMinutes < 60) return $"{time.TotalMinutes:F1} min";
            return $"{time.TotalHours:F1} hr";
        }
    }
}
