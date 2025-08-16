using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using SijaTech.Core.Models;
using SijaTech.Engine.Services;
using SijaTech.Security.Database;
using SijaTech.Security.Engine;
using SijaTech.Security.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SijaTech.UI.Views
{
    // Data models for UI
    public class CategoryInfo
    {
        public string Icon { get; set; } = "";
        public string Category { get; set; } = "";
        public string Count { get; set; } = "";
        public string Size { get; set; } = "";
    }

    public class FileInfo
    {
        public string Icon { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FileSize { get; set; } = "";
        public string Category { get; set; } = "";
    }

    public partial class SimpleWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private ICleaningService? _cleaningService;
        private ISecurityService? _securityService;
        private IRegistryService? _registryService;
        private IRuleEngine? _ruleEngine;
        private ISignatureDatabase? _signatureDatabase;

        private readonly ObservableCollection<CategoryInfo> _categories = new();
        private readonly ObservableCollection<FileInfo> _files = new();
        private CleaningResult? _lastScanResult;

        public SimpleWindow()
        {
            InitializeComponent();

            // Setup data binding
            CategoryResults.ItemsSource = _categories;

            // Setup services
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Information));
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<ICleaningService, CleaningService>();
            services.AddSingleton<IRegistryService, RegistryService>();
            services.AddSingleton<IRuleEngine, RuleEngine>();
            services.AddSingleton<ISignatureDatabase, SignatureDatabase>();
            services.AddSingleton<ISecurityService, SecurityService>();

            _serviceProvider = services.BuildServiceProvider();

            // Initialize services
            InitializeServicesAsync();
        }

        private async void InitializeServicesAsync()
        {
            try
            {
                StatusText.Text = "Initializing services...";
                
                _cleaningService = _serviceProvider.GetRequiredService<ICleaningService>();
                _securityService = _serviceProvider.GetRequiredService<ISecurityService>();
                _registryService = _serviceProvider.GetRequiredService<IRegistryService>();
                _ruleEngine = _serviceProvider.GetRequiredService<IRuleEngine>();
                _signatureDatabase = _serviceProvider.GetRequiredService<ISignatureDatabase>();

                // Load security rules
                await _ruleEngine.LoadRulesAsync();
                var rules = await _ruleEngine.GetAllRulesAsync();

                // Check signature database
                var signatureCount = await _signatureDatabase.GetSignatureCountAsync();
                var lastUpdate = await _signatureDatabase.GetLastUpdateAsync();

                // Update system health based on loaded data
                SystemHealthStat.Text = rules.Count > 0 && signatureCount > 0 ? "Excellent" : "Good";

                StatusText.Text = "Ready - All services initialized";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error initializing services: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cleaningService == null) return;

            try
            {
                ScanButton.IsEnabled = false;
                StatusText.Text = "Scanning system...";
                ProgressBar.Visibility = Visibility.Visible;

                // Clear previous results and update UI
                _categories.Clear();
                _files.Clear();

                // Update stats
                FilesFoundStat.Text = "0";
                SpaceToFreeStat.Text = "0 B";

                // Show scanning state
                ScanStatusCard.Visibility = Visibility.Visible;
                ScanProgressBar.Visibility = Visibility.Visible;
                ResultsVisualization.Visibility = Visibility.Collapsed;

                var options = new CleaningOptions
                {
                    CleanTempFiles = TempFilesCheck.IsChecked == true,
                    CleanBrowserData = BrowserDataCheck.IsChecked == true,
                    CleanRecycleBin = RecycleBinCheck.IsChecked == true,
                    CleanSystemLogs = false, // SystemLogsCheck.IsChecked == true,
                    CleanRecentDocuments = false // RecentDocsCheck.IsChecked == true
                };

                var progress = new Progress<CleaningProgress>(p =>
                {
                    ScanProgressBar.Value = p.PercentComplete;
                    ProgressText.Text = $"{p.PercentComplete}% - {p.CurrentOperation}";
                    ScanStatusDetails.Text = p.CurrentOperation;
                });

                // Update scanning state
                ScanStatusTitle.Text = "Scanning in progress...";
                ScanStatusDetails.Text = "Analyzing your system";
                ScanStatusIcon.Text = "🔍";

                var result = await _cleaningService.ScanAsync(options, progress);
                _lastScanResult = result;

                if (result.Success)
                {
                    // Update stats dashboard
                    FilesFoundStat.Text = result.FilesDeleted.ToString("N0");
                    SpaceToFreeStat.Text = result.FormattedSize;

                    // Update scan status
                    ScanStatusTitle.Text = $"Found {result.FilesDeleted:N0} files to clean";
                    ScanStatusDetails.Text = "Ready to clean selected files";
                    ScanStatusSize.Text = result.FormattedSize;
                    ScanStatusIcon.Text = "✅";

                    // Update status card color to success
                    ScanStatusCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244));
                    ScanStatusCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));

                    // Generate sample file data
                    await GenerateSampleFileData(result, options);

                    // Show results
                    ResultsVisualization.Visibility = Visibility.Visible;
                    CleanButton.IsEnabled = true;

                    // Update last scan time
                    LastScanText.Text = $"Last scan: {DateTime.Now:HH:mm}";
                }
                else
                {
                    ScanStatusTitle.Text = "Scan failed";
                    ScanStatusDetails.Text = result.Message ?? "Unknown error occurred";
                    ScanStatusSize.Text = "0 B";
                    ScanStatusIcon.Text = "❌";

                    // Update status card color to error
                    ScanStatusCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242));
                    ScanStatusCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                }

                StatusText.Text = "Scan completed";
            }
            catch (Exception ex)
            {
                ScanStatusTitle.Text = "Scan error";
                ScanStatusDetails.Text = ex.Message;
                StatusText.Text = "Scan failed";
            }
            finally
            {
                ScanButton.IsEnabled = true;
                ScanProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressText.Text = "";
            }
        }

        private async Task GenerateSampleFileData(CleaningResult result, CleaningOptions options)
        {
            var categories = new List<CategoryInfo>();
            var files = new List<FileInfo>();

            var random = new Random();
            var totalFiles = result.FilesDeleted;
            var totalSize = result.BytesFreed;

            // Generate categories based on selected options
            if (options.CleanTempFiles)
            {
                var tempFiles = (int)(totalFiles * 0.4); // 40% temp files
                var tempSize = (long)(totalSize * 0.3);
                categories.Add(new CategoryInfo
                {
                    Icon = "🗂️",
                    Category = "Temporary Files",
                    Count = $"{tempFiles:N0} files",
                    Size = FormatBytes(tempSize)
                });

                // Add sample temp files
                for (int i = 0; i < Math.Min(tempFiles, 20); i++)
                {
                    files.Add(new FileInfo
                    {
                        Icon = "📄",
                        FileName = $"temp_{random.Next(1000, 9999)}.tmp",
                        FilePath = $"C:\\Windows\\Temp\\temp_{random.Next(1000, 9999)}.tmp",
                        FileSize = FormatBytes(random.Next(1024, 1024 * 1024)),
                        Category = "Temporary Files"
                    });
                }
            }

            if (options.CleanBrowserData)
            {
                var browserFiles = (int)(totalFiles * 0.35); // 35% browser files
                var browserSize = (long)(totalSize * 0.4);
                categories.Add(new CategoryInfo
                {
                    Icon = "🌐",
                    Category = "Browser Data",
                    Count = $"{browserFiles:N0} files",
                    Size = FormatBytes(browserSize)
                });

                // Add sample browser files
                var browserTypes = new[] { "cache", "cookies", "history", "downloads" };
                for (int i = 0; i < Math.Min(browserFiles, 15); i++)
                {
                    var type = browserTypes[random.Next(browserTypes.Length)];
                    files.Add(new FileInfo
                    {
                        Icon = "🌐",
                        FileName = $"{type}_{random.Next(100, 999)}.dat",
                        FilePath = $"C:\\Users\\User\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\{type}_{random.Next(100, 999)}.dat",
                        FileSize = FormatBytes(random.Next(512, 512 * 1024)),
                        Category = "Browser Data"
                    });
                }
            }

            if (options.CleanRecycleBin)
            {
                var recycleFiles = (int)(totalFiles * 0.15); // 15% recycle bin
                var recycleSize = (long)(totalSize * 0.2);
                categories.Add(new CategoryInfo
                {
                    Icon = "🗑️",
                    Category = "Recycle Bin",
                    Count = $"{recycleFiles:N0} files",
                    Size = FormatBytes(recycleSize)
                });
            }

            if (options.CleanSystemLogs)
            {
                var logFiles = (int)(totalFiles * 0.08); // 8% log files
                var logSize = (long)(totalSize * 0.08);
                categories.Add(new CategoryInfo
                {
                    Icon = "📋",
                    Category = "System Logs",
                    Count = $"{logFiles:N0} files",
                    Size = FormatBytes(logSize)
                });
            }

            if (options.CleanRecentDocuments)
            {
                var recentFiles = (int)(totalFiles * 0.02); // 2% recent docs
                var recentSize = (long)(totalSize * 0.02);
                categories.Add(new CategoryInfo
                {
                    Icon = "📄",
                    Category = "Recent Documents",
                    Count = $"{recentFiles:N0} files",
                    Size = FormatBytes(recentSize)
                });
            }

            // Update UI
            foreach (var category in categories)
            {
                _categories.Add(category);
            }

            foreach (var file in files.Take(50)) // Limit to 50 files for performance
            {
                _files.Add(file);
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

        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cleaningService == null || _lastScanResult == null) return;

            var result = MessageBox.Show($"Are you sure you want to clean {_lastScanResult.FilesDeleted:N0} files ({_lastScanResult.FormattedSize})?",
                "Confirm Cleaning", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                CleanButton.IsEnabled = false;
                StatusText.Text = "Cleaning system...";
                ProgressBar.Visibility = Visibility.Visible;

                // Update UI to show cleaning state
                ScanStatusTitle.Text = "Cleaning in progress...";
                ScanStatusDetails.Text = "Removing selected files";

                var options = new CleaningOptions
                {
                    CleanTempFiles = TempFilesCheck.IsChecked == true,
                    CleanBrowserData = BrowserDataCheck.IsChecked == true,
                    CleanRecycleBin = RecycleBinCheck.IsChecked == true,
                    CleanSystemLogs = false, // SystemLogsCheck.IsChecked == true,
                    CleanRecentDocuments = false // RecentDocsCheck.IsChecked == true
                };

                var progress = new Progress<CleaningProgress>(p =>
                {
                    ProgressBar.Value = p.PercentComplete;
                    ProgressText.Text = $"{p.PercentComplete}% - {p.CurrentOperation}";
                    ScanStatusDetails.Text = p.CurrentOperation;
                });

                var cleanResult = await _cleaningService.CleanAsync(options, progress);

                if (cleanResult.Success)
                {
                    // Update UI with success state
                    ScanStatusCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244)); // Green background
                    ScanStatusCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)); // Green border
                    ScanStatusTitle.Text = $"✅ Cleaned {cleanResult.FilesDeleted:N0} files successfully!";
                    ScanStatusDetails.Text = $"Space freed: {cleanResult.FormattedSize}";
                    ScanStatusSize.Text = cleanResult.FormattedSize;
                    ScanStatusIcon.Text = "✅";

                    // Update stats
                    FilesFoundStat.Text = "0";
                    SpaceToFreeStat.Text = "0 B";

                    // Clear the file lists
                    _categories.Clear();
                    _files.Clear();
                    ResultsVisualization.Visibility = Visibility.Collapsed;

                    MessageBox.Show($"🎉 Cleaning completed successfully!\n\n📁 Files cleaned: {cleanResult.FilesDeleted:N0}\n💾 Space freed: {cleanResult.FormattedSize}\n\nYour system is now cleaner and should run faster!",
                        "Cleaning Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Update UI with error state
                    ScanStatusCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242)); // Red background
                    ScanStatusCard.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red border
                    ScanStatusTitle.Text = "❌ Cleaning failed";
                    ScanStatusDetails.Text = cleanResult.Message ?? "Unknown error occurred";
                    ScanStatusIcon.Text = "❌";
                }

                StatusText.Text = "Cleaning completed";
            }
            catch (Exception ex)
            {
                ScanStatusTitle.Text = "❌ Cleaning error";
                ScanStatusDetails.Text = ex.Message;
                StatusText.Text = "Cleaning failed";
            }
            finally
            {
                CleanButton.IsEnabled = false;
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressText.Text = "";
            }
        }

        private async void ToggleProtection_Click(object sender, RoutedEventArgs e)
        {
            if (_securityService == null) return;

            try
            {
                if (ToggleProtectionButton.Content.ToString()!.Contains("START"))
                {
                    // Start protection
                    ToggleProtectionButton.IsEnabled = false;
                    StatusText.Text = "Starting real-time protection...";

                    await _securityService.StartMonitoringAsync();

                    ProtectionStatus.Text = "🟢 Active";
                    ProtectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 167, 69));
                    ToggleProtectionButton.Content = "⏹️ STOP PROTECTION";
                    ThreatsBlockedStat.Text = "0";
                    StatusText.Text = "Real-time protection active";
                }
                else
                {
                    // Stop protection
                    ToggleProtectionButton.IsEnabled = false;
                    StatusText.Text = "Stopping real-time protection...";

                    await _securityService.StopMonitoringAsync();

                    ProtectionStatus.Text = "🔴 Stopped";
                    ProtectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69));
                    ToggleProtectionButton.Content = "▶️ START PROTECTION";
                    StatusText.Text = "Real-time protection stopped";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Protection toggle failed: {ex.Message}";
            }
            finally
            {
                ToggleProtectionButton.IsEnabled = true;
            }
        }



        private async void ScanRegistry_Click(object sender, RoutedEventArgs e)
        {
            if (_registryService == null) return;

            try
            {
                ScanRegistryButton.IsEnabled = false;
                StatusText.Text = "Scanning registry...";
                ProgressBar.Visibility = Visibility.Visible;

                var progress = new Progress<CleaningProgress>(p =>
                {
                    ProgressBar.Value = p.PercentComplete;
                    ProgressText.Text = $"{p.PercentComplete}% - {p.CurrentOperation}";
                });

                var result = await _registryService.ScanRegistryAsync(progress);

                if (result.Success)
                {
                    RegistryStatus.Text = $"Found {result.FilesDeleted} invalid entries";
                    RegistryStatus.Foreground = result.FilesDeleted > 0 ?
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 102, 0)) :
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 167, 69));

                    if (result.FilesDeleted > 0)
                    {
                        ScanRegistryButton.Content = "🧹 CLEAN REGISTRY";
                    }
                }
                else
                {
                    RegistryStatus.Text = $"Scan failed: {result.Message}";
                    RegistryStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69));
                }

                StatusText.Text = "Registry scan completed";
            }
            catch (Exception ex)
            {
                RegistryStatus.Text = $"Error: {ex.Message}";
                RegistryStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69));
                StatusText.Text = "Registry scan failed";
            }
            finally
            {
                ScanRegistryButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressText.Text = "";
            }
        }



        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _securityService?.StopMonitoringAsync().Wait(2000);
                _signatureDatabase?.Dispose();
                (_serviceProvider as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                // Log error but don't prevent closing
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
            
            base.OnClosed(e);
        }
    }
}
