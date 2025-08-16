using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SijaTech.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SijaTech.UI.ViewModels
{
    public partial class RegistryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "Registry Cleaner";

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private bool _isCleaning = false;

        [ObservableProperty]
        private bool _isBackingUp = false;

        [ObservableProperty]
        private int _progressPercentage = 0;

        [ObservableProperty]
        private string _currentOperation = string.Empty;

        [ObservableProperty]
        private int _invalidEntriesFound = 0;

        [ObservableProperty]
        private int _entriesCleaned = 0;

        [ObservableProperty]
        private DateTime? _lastScanTime;

        [ObservableProperty]
        private DateTime? _lastCleanTime;

        [ObservableProperty]
        private DateTime? _lastBackupTime;

        [ObservableProperty]
        private bool _autoBackupEnabled = true;

        public ObservableCollection<RegistryIssueViewModel> RegistryIssues { get; } = new();
        public ObservableCollection<RegistryBackupViewModel> Backups { get; } = new();

        public RegistryViewModel()
        {
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            // Sample registry issues
            RegistryIssues.Add(new RegistryIssueViewModel
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs",
                ValueName = "file1.txt",
                IssueType = "Missing File Reference",
                Description = "References a file that no longer exists",
                IsSelected = true,
                Severity = "Low"
            });

            RegistryIssues.Add(new RegistryIssueViewModel
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache",
                ValueName = "C:\\Program Files\\OldApp\\app.exe.ApplicationCompany",
                IssueType = "Orphaned Entry",
                Description = "Application no longer installed",
                IsSelected = true,
                Severity = "Medium"
            });

            RegistryIssues.Add(new RegistryIssueViewModel
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
                ValueName = "a",
                IssueType = "Invalid Path",
                Description = "Points to non-existent executable",
                IsSelected = false,
                Severity = "Low"
            });

            InvalidEntriesFound = RegistryIssues.Count;

            // Sample backups
            Backups.Add(new RegistryBackupViewModel
            {
                Id = 1,
                Name = "Auto Backup - Before Clean",
                FilePath = @"C:\ProgramData\SijaTech\Backups\registry_backup_20241201_143022.reg",
                CreatedDate = DateTime.Now.AddHours(-2),
                FileSize = 2048576, // 2MB
                IsAutomatic = true,
                CanRestore = true
            });

            Backups.Add(new RegistryBackupViewModel
            {
                Id = 2,
                Name = "Manual Backup - System Clean",
                FilePath = @"C:\ProgramData\SijaTech\Backups\registry_backup_20241130_090000.reg",
                CreatedDate = DateTime.Now.AddDays(-1),
                FileSize = 1536000, // 1.5MB
                IsAutomatic = false,
                CanRestore = true
            });

            LastBackupTime = Backups.Count > 0 ? Backups[0].CreatedDate : null;
        }

        [RelayCommand]
        private async Task ScanRegistryAsync()
        {
            try
            {
                IsScanning = true;
                ProgressPercentage = 0;
                CurrentOperation = "Starting registry scan...";

                // Clear previous results
                RegistryIssues.Clear();
                InvalidEntriesFound = 0;

                // Simulate scanning different registry areas
                var scanAreas = new[]
                {
                    "Recent Documents",
                    "Run MRU",
                    "Typed Paths",
                    "File Extensions",
                    "MUI Cache",
                    "Application Paths",
                    "Shared DLLs",
                    "Uninstall Entries"
                };

                for (int i = 0; i < scanAreas.Length; i++)
                {
                    CurrentOperation = $"Scanning {scanAreas[i]}...";
                    ProgressPercentage = (i * 100) / scanAreas.Length;

                    await Task.Delay(800);

                    // Simulate finding issues
                    var random = new Random();
                    int issuesFound = random.Next(0, 5);

                    for (int j = 0; j < issuesFound; j++)
                    {
                        RegistryIssues.Add(new RegistryIssueViewModel
                        {
                            KeyPath = $@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\{scanAreas[i]}",
                            ValueName = $"entry_{j + 1}",
                            IssueType = GetRandomIssueType(),
                            Description = GetRandomDescription(),
                            IsSelected = true,
                            Severity = GetRandomSeverity()
                        });
                    }
                }

                ProgressPercentage = 100;
                InvalidEntriesFound = RegistryIssues.Count;
                LastScanTime = DateTime.Now;
                CurrentOperation = $"Scan completed. Found {InvalidEntriesFound} issues.";

                await Task.Delay(3000);
                CurrentOperation = string.Empty;
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Scan error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                ProgressPercentage = 0;
            }
        }

        [RelayCommand]
        private async Task CleanRegistryAsync()
        {
            if (RegistryIssues.Count == 0)
            {
                CurrentOperation = "No issues to clean. Please run scan first.";
                await Task.Delay(3000);
                CurrentOperation = string.Empty;
                return;
            }

            try
            {
                // Auto backup if enabled
                if (AutoBackupEnabled)
                {
                    await CreateBackupAsync(true);
                }

                IsCleaning = true;
                ProgressPercentage = 0;
                CurrentOperation = "Starting registry cleanup...";

                var selectedIssues = new List<RegistryIssueViewModel>();
                foreach (var issue in RegistryIssues)
                {
                    if (issue.IsSelected)
                        selectedIssues.Add(issue);
                }

                if (selectedIssues.Count == 0)
                {
                    CurrentOperation = "No issues selected for cleaning.";
                    await Task.Delay(3000);
                    CurrentOperation = string.Empty;
                    return;
                }

                // Simulate cleaning selected issues
                for (int i = 0; i < selectedIssues.Count; i++)
                {
                    var issue = selectedIssues[i];
                    CurrentOperation = $"Cleaning {issue.KeyPath}...";
                    ProgressPercentage = (i * 100) / selectedIssues.Count;

                    await Task.Delay(500);

                    // Remove from collection
                    RegistryIssues.Remove(issue);
                }

                ProgressPercentage = 100;
                EntriesCleaned = selectedIssues.Count;
                InvalidEntriesFound = RegistryIssues.Count;
                LastCleanTime = DateTime.Now;
                CurrentOperation = $"Cleanup completed. Cleaned {EntriesCleaned} entries.";

                await Task.Delay(3000);
                CurrentOperation = string.Empty;
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Cleanup error: {ex.Message}";
            }
            finally
            {
                IsCleaning = false;
                ProgressPercentage = 0;
            }
        }

        [RelayCommand]
        private async Task CreateBackupAsync(bool isAutomatic = false)
        {
            try
            {
                IsBackingUp = true;
                CurrentOperation = "Creating registry backup...";

                await Task.Delay(2000);

                var backup = new RegistryBackupViewModel
                {
                    Id = Backups.Count + 1,
                    Name = isAutomatic ? $"Auto Backup - {DateTime.Now:yyyy-MM-dd HH:mm}" : $"Manual Backup - {DateTime.Now:yyyy-MM-dd HH:mm}",
                    FilePath = $@"C:\ProgramData\SijaTech\Backups\registry_backup_{DateTime.Now:yyyyMMdd_HHmmss}.reg",
                    CreatedDate = DateTime.Now,
                    FileSize = new Random().Next(1024000, 3072000), // 1-3MB
                    IsAutomatic = isAutomatic,
                    CanRestore = true
                };

                Backups.Insert(0, backup);
                LastBackupTime = backup.CreatedDate;

                CurrentOperation = "Registry backup created successfully.";
                await Task.Delay(2000);
                CurrentOperation = string.Empty;
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Backup error: {ex.Message}";
            }
            finally
            {
                IsBackingUp = false;
            }
        }

        [RelayCommand]
        private async Task RestoreBackupAsync(RegistryBackupViewModel backup)
        {
            try
            {
                CurrentOperation = $"Restoring backup {backup.Name}...";
                await Task.Delay(3000);

                CurrentOperation = "Registry restored successfully. Please restart the application.";
                await Task.Delay(3000);
                CurrentOperation = string.Empty;
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Restore error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void DeleteBackup(RegistryBackupViewModel backup)
        {
            Backups.Remove(backup);
            CurrentOperation = $"Backup {backup.Name} deleted.";
        }

        [RelayCommand]
        private void SelectAllIssues()
        {
            foreach (var issue in RegistryIssues)
            {
                issue.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllIssues()
        {
            foreach (var issue in RegistryIssues)
            {
                issue.IsSelected = false;
            }
        }

        [RelayCommand]
        private void ToggleIssueSelection(RegistryIssueViewModel issue)
        {
            issue.IsSelected = !issue.IsSelected;
        }

        private static string GetRandomIssueType()
        {
            var types = new[] { "Missing File Reference", "Orphaned Entry", "Invalid Path", "Broken Link", "Obsolete Entry" };
            return types[new Random().Next(types.Length)];
        }

        private static string GetRandomDescription()
        {
            var descriptions = new[]
            {
                "References a file that no longer exists",
                "Application no longer installed",
                "Points to non-existent executable",
                "Broken file association",
                "Outdated system reference"
            };
            return descriptions[new Random().Next(descriptions.Length)];
        }

        private static string GetRandomSeverity()
        {
            var severities = new[] { "Low", "Medium", "High" };
            return severities[new Random().Next(severities.Length)];
        }
    }

    public partial class RegistryIssueViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _keyPath = string.Empty;

        [ObservableProperty]
        private string _valueName = string.Empty;

        [ObservableProperty]
        private string _issueType = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _severity = "Low";

        public string SeverityColor => Severity switch
        {
            "High" => "#F44336",
            "Medium" => "#FF9800",
            "Low" => "#4CAF50",
            _ => "#757575"
        };

        public string SeverityIcon => Severity switch
        {
            "High" => "AlertCircle",
            "Medium" => "Alert",
            "Low" => "Information",
            _ => "Help"
        };
    }

    public partial class RegistryBackupViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private DateTime _createdDate = DateTime.Now;

        [ObservableProperty]
        private long _fileSize;

        [ObservableProperty]
        private bool _isAutomatic;

        [ObservableProperty]
        private bool _canRestore = true;

        public string FormattedSize
        {
            get
            {
                string[] suffixes = { "B", "KB", "MB", "GB" };
                int counter = 0;
                decimal number = FileSize;
                while (Math.Round(number / 1024) >= 1)
                {
                    number /= 1024;
                    counter++;
                }
                return $"{number:n1} {suffixes[counter]}";
            }
        }

        public string BackupTypeIcon => IsAutomatic ? "Autorenew" : "AccountBox";
        public string BackupTypeText => IsAutomatic ? "Automatic" : "Manual";
    }
}
