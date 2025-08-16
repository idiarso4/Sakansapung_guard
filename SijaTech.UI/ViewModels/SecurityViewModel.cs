using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SijaTech.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SijaTech.UI.ViewModels
{
    public partial class SecurityViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "Security Monitor";

        [ObservableProperty]
        private bool _isMonitoring = false;

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private int _progressPercentage = 0;

        [ObservableProperty]
        private string _currentOperation = string.Empty;

        [ObservableProperty]
        private string _monitoringStatus = "Stopped";

        [ObservableProperty]
        private int _threatsDetected = 0;

        [ObservableProperty]
        private int _filesQuarantined = 0;

        [ObservableProperty]
        private int _rulesActive = 0;

        [ObservableProperty]
        private DateTime? _lastScanTime;

        [ObservableProperty]
        private DateTime? _lastThreatTime;

        public ObservableCollection<ThreatDetectionViewModel> RecentThreats { get; } = new();
        public ObservableCollection<SecurityRuleViewModel> SecurityRules { get; } = new();
        public ObservableCollection<QuarantineItemViewModel> QuarantineItems { get; } = new();

        public SecurityViewModel()
        {
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            // Sample security rules (Snort-inspired)
            SecurityRules.Add(new SecurityRuleViewModel
            {
                Id = 1001,
                Name = "Malware Detection",
                RuleText = "alert file any any -> any any (msg:\"Potential Malware\"; content:\"malicious_pattern\"; sid:1001;)",
                IsEnabled = true,
                LastTriggered = DateTime.Now.AddHours(-2),
                TriggerCount = 5
            });

            SecurityRules.Add(new SecurityRuleViewModel
            {
                Id = 1002,
                Name = "Suspicious Behavior",
                RuleText = "alert process any any -> any any (msg:\"Mass File Encryption\"; behavior:\"file_encrypt,mass_delete\"; sid:1002;)",
                IsEnabled = true,
                LastTriggered = null,
                TriggerCount = 0
            });

            SecurityRules.Add(new SecurityRuleViewModel
            {
                Id = 2001,
                Name = "Block Crypted Files",
                RuleText = "alert file any any -> any any (msg:\"Ransomware Extension\"; filename:\"*.crypted\"; action:quarantine; sid:2001;)",
                IsEnabled = true,
                LastTriggered = DateTime.Now.AddDays(-1),
                TriggerCount = 2
            });

            RulesActive = SecurityRules.Count;

            // Sample recent threats
            RecentThreats.Add(new ThreatDetectionViewModel
            {
                ThreatName = "Trojan.Generic.KD.12345",
                FilePath = @"C:\Users\User\Downloads\suspicious.exe",
                Severity = ThreatSeverity.High,
                DetectedAt = DateTime.Now.AddMinutes(-30),
                ActionTaken = SecurityAction.Quarantine,
                Status = "Quarantined"
            });

            RecentThreats.Add(new ThreatDetectionViewModel
            {
                ThreatName = "Adware.BrowserHijacker",
                FilePath = @"C:\Program Files\SuspiciousApp\malware.dll",
                Severity = ThreatSeverity.Medium,
                DetectedAt = DateTime.Now.AddHours(-2),
                ActionTaken = SecurityAction.Delete,
                Status = "Deleted"
            });

            ThreatsDetected = RecentThreats.Count;
            LastThreatTime = RecentThreats.Count > 0 ? RecentThreats[0].DetectedAt : null;

            // Sample quarantine items
            QuarantineItems.Add(new QuarantineItemViewModel
            {
                Id = 1,
                OriginalPath = @"C:\Users\User\Downloads\suspicious.exe",
                ThreatName = "Trojan.Generic.KD.12345",
                QuarantineDate = DateTime.Now.AddMinutes(-30),
                FileSize = 2048576, // 2MB
                CanRestore = true
            });

            FilesQuarantined = QuarantineItems.Count;
        }

        [RelayCommand]
        private async Task StartMonitoringAsync()
        {
            try
            {
                IsMonitoring = true;
                MonitoringStatus = "Starting...";
                CurrentOperation = "Initializing real-time monitoring...";

                // Simulate startup delay
                await Task.Delay(2000);

                MonitoringStatus = "Active";
                CurrentOperation = "Real-time monitoring active";

                // Simulate clearing operation message after delay
                await Task.Delay(3000);
                CurrentOperation = string.Empty;
            }
            catch (Exception ex)
            {
                MonitoringStatus = "Error";
                CurrentOperation = $"Failed to start monitoring: {ex.Message}";
                IsMonitoring = false;
            }
        }

        [RelayCommand]
        private async Task StopMonitoringAsync()
        {
            try
            {
                MonitoringStatus = "Stopping...";
                CurrentOperation = "Stopping real-time monitoring...";

                // Simulate shutdown delay
                await Task.Delay(1000);

                IsMonitoring = false;
                MonitoringStatus = "Stopped";
                CurrentOperation = "Monitoring stopped";

                await Task.Delay(2000);
                CurrentOperation = string.Empty;
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Error stopping monitoring: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ScanSystemAsync()
        {
            try
            {
                IsScanning = true;
                ProgressPercentage = 0;
                CurrentOperation = "Starting system scan...";

                // Simulate scanning progress
                for (int i = 0; i <= 100; i += 5)
                {
                    ProgressPercentage = i;
                    CurrentOperation = $"Scanning system... {i}%";
                    await Task.Delay(200);
                }

                LastScanTime = DateTime.Now;
                CurrentOperation = "System scan completed";

                // Simulate finding a threat
                var random = new Random();
                if (random.Next(0, 3) == 0) // 33% chance of finding threat
                {
                    var newThreat = new ThreatDetectionViewModel
                    {
                        ThreatName = "Suspicious.File.Detected",
                        FilePath = @"C:\Temp\suspicious_file.tmp",
                        Severity = ThreatSeverity.Medium,
                        DetectedAt = DateTime.Now,
                        ActionTaken = SecurityAction.Quarantine,
                        Status = "Quarantined"
                    };

                    RecentThreats.Insert(0, newThreat);
                    ThreatsDetected++;
                    LastThreatTime = newThreat.DetectedAt;

                    CurrentOperation = "Threat detected and quarantined";
                }
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Scan error: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                ProgressPercentage = 0;
                await Task.Delay(3000);
                CurrentOperation = string.Empty;
            }
        }

        [RelayCommand]
        private void AddCustomRule()
        {
            // This would open a dialog to add custom security rule
            CurrentOperation = "Add custom rule dialog would open here";
        }

        [RelayCommand]
        private void EditRule(SecurityRuleViewModel rule)
        {
            CurrentOperation = $"Edit rule {rule.Name} dialog would open here";
        }

        [RelayCommand]
        private void ToggleRule(SecurityRuleViewModel rule)
        {
            rule.IsEnabled = !rule.IsEnabled;
            CurrentOperation = $"Rule {rule.Name} {(rule.IsEnabled ? "enabled" : "disabled")}";
        }

        [RelayCommand]
        private async Task RestoreFromQuarantineAsync(QuarantineItemViewModel item)
        {
            try
            {
                CurrentOperation = $"Restoring {item.ThreatName}...";
                await Task.Delay(1000);

                QuarantineItems.Remove(item);
                FilesQuarantined--;

                CurrentOperation = "File restored successfully";
                await Task.Delay(2000);
                CurrentOperation = string.Empty;
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Restore failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteFromQuarantineAsync(QuarantineItemViewModel item)
        {
            try
            {
                CurrentOperation = $"Permanently deleting {item.ThreatName}...";
                await Task.Delay(1000);

                QuarantineItems.Remove(item);
                FilesQuarantined--;

                CurrentOperation = "File permanently deleted";
                await Task.Delay(2000);
                CurrentOperation = string.Empty;
            }
            catch (Exception ex)
            {
                CurrentOperation = $"Delete failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewThreatDetails(ThreatDetectionViewModel threat)
        {
            CurrentOperation = $"Threat details for {threat.ThreatName} would be shown here";
        }
    }

    public partial class ThreatDetectionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _threatName = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private ThreatSeverity _severity = ThreatSeverity.Low;

        [ObservableProperty]
        private DateTime _detectedAt = DateTime.Now;

        [ObservableProperty]
        private SecurityAction _actionTaken = SecurityAction.Alert;

        [ObservableProperty]
        private string _status = string.Empty;

        public string SeverityColor => Severity switch
        {
            ThreatSeverity.Critical => "#F44336",
            ThreatSeverity.High => "#FF9800",
            ThreatSeverity.Medium => "#FFC107",
            ThreatSeverity.Low => "#4CAF50",
            _ => "#757575"
        };

        public string SeverityIcon => Severity switch
        {
            ThreatSeverity.Critical => "AlertCircle",
            ThreatSeverity.High => "Alert",
            ThreatSeverity.Medium => "AlertTriangle",
            ThreatSeverity.Low => "Information",
            _ => "Help"
        };
    }

    public partial class SecurityRuleViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _ruleText = string.Empty;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private DateTime? _lastTriggered;

        [ObservableProperty]
        private int _triggerCount;

        public string StatusColor => IsEnabled ? "#4CAF50" : "#757575";
        public string StatusText => IsEnabled ? "Active" : "Disabled";
    }

    public partial class QuarantineItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _originalPath = string.Empty;

        [ObservableProperty]
        private string _threatName = string.Empty;

        [ObservableProperty]
        private DateTime _quarantineDate = DateTime.Now;

        [ObservableProperty]
        private long _fileSize;

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
    }
}
