using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using SijaTech.Core.Models;
using SijaTech.Security.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Security.Services
{
    /// <summary>
    /// Main security service yang mengintegrasikan semua komponen security
    /// </summary>
    public class SecurityService : ISecurityService, IDisposable
    {
        private readonly ILogger<SecurityService> _logger;
        private readonly IRuleEngine _ruleEngine;
        private readonly ISignatureDatabase _signatureDatabase;
        private readonly IFileService _fileService;
        private readonly RealTimeMonitor _realTimeMonitor;
        
        private readonly List<QuarantineItem> _quarantineItems = new();
        private readonly object _quarantineLock = new();

        public SecurityService(
            ILogger<SecurityService> logger,
            IRuleEngine ruleEngine,
            ISignatureDatabase signatureDatabase,
            IFileService fileService)
        {
            _logger = logger;
            _ruleEngine = ruleEngine;
            _signatureDatabase = signatureDatabase;
            _fileService = fileService;
            
            _realTimeMonitor = new RealTimeMonitor(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<RealTimeMonitor>.Instance,
                ruleEngine,
                signatureDatabase,
                fileService);

            // Subscribe to real-time monitor events
            _realTimeMonitor.ThreatDetected += OnThreatDetected;
            _realTimeMonitor.FileQuarantined += OnFileQuarantined;
            _realTimeMonitor.SecurityEventOccurred += OnSecurityEventOccurred;

            _logger.LogInformation("Security service initialized");
        }

        public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting security monitoring...");
                
                await _realTimeMonitor.StartMonitoringAsync(cancellationToken);
                
                _logger.LogInformation("Security monitoring started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start security monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            try
            {
                _logger.LogInformation("Stopping security monitoring...");
                
                await _realTimeMonitor.StopMonitoringAsync();
                
                _logger.LogInformation("Security monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping security monitoring");
                throw;
            }
        }

        public async Task<ThreatDetection?> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Scanning file: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for scanning: {FilePath}", filePath);
                    return null;
                }

                // Check against signature database first
                var fileHash = await _fileService.GetFileHashAsync(filePath, "SHA256", cancellationToken);
                if (!string.IsNullOrEmpty(fileHash))
                {
                    var signature = await _signatureDatabase.CheckHashAsync(fileHash, cancellationToken);
                    if (signature != null)
                    {
                        var threat = new ThreatDetection
                        {
                            FilePath = filePath,
                            ThreatName = signature.MalwareFamily,
                            Type = ThreatType.Malware,
                            Severity = signature.Severity,
                            Description = signature.Description,
                            MatchedSignature = signature,
                            DetectedAt = DateTime.Now,
                            ActionTaken = SecurityAction.Alert,
                            FileHash = fileHash
                        };

                        _logger.LogWarning("Known malware detected: {ThreatName} - {FilePath}", signature.MalwareFamily, filePath);
                        return threat;
                    }
                }

                // Check against security rules
                var triggeredRules = await _ruleEngine.EvaluateFileAsync(filePath, cancellationToken);
                if (triggeredRules.Count > 0)
                {
                    var rule = triggeredRules[0]; // Use first triggered rule
                    var threat = new ThreatDetection
                    {
                        FilePath = filePath,
                        ThreatName = rule.Message,
                        Type = ThreatType.Suspicious,
                        Severity = ThreatSeverity.Medium,
                        Description = $"Rule triggered: {rule.Message}",
                        TriggeredRule = rule,
                        DetectedAt = DateTime.Now,
                        ActionTaken = rule.Action,
                        FileHash = fileHash
                    };

                    _logger.LogWarning("Suspicious file detected by rule {Sid}: {FilePath}", rule.Sid, filePath);
                    return threat;
                }

                _logger.LogDebug("File scan completed, no threats detected: {FilePath}", filePath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning file: {FilePath}", filePath);
                return null;
            }
        }

        public async Task<List<ThreatDetection>> ScanDirectoryAsync(string directoryPath, 
            IProgress<CleaningProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var threats = new List<ThreatDetection>();

            try
            {
                _logger.LogInformation("Starting directory scan: {DirectoryPath}", directoryPath);

                if (!Directory.Exists(directoryPath))
                {
                    _logger.LogWarning("Directory not found for scanning: {DirectoryPath}", directoryPath);
                    return threats;
                }

                var files = await _fileService.GetFilesAsync(directoryPath, "*", SearchOption.AllDirectories, cancellationToken);
                var totalFiles = files.Count;
                var processedFiles = 0;

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var threat = await ScanFileAsync(file, cancellationToken);
                        if (threat != null)
                        {
                            threats.Add(threat);
                        }

                        processedFiles++;
                        var percentComplete = totalFiles > 0 ? (processedFiles * 100) / totalFiles : 100;
                        
                        progress?.Report(new CleaningProgress
                        {
                            PercentComplete = percentComplete,
                            CurrentOperation = $"Scanning {Path.GetFileName(file)}...",
                            CurrentFile = file,
                            FilesProcessed = processedFiles
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error scanning file: {File}", file);
                    }
                }

                _logger.LogInformation("Directory scan completed. Found {ThreatCount} threats in {FileCount} files", 
                    threats.Count, totalFiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning directory: {DirectoryPath}", directoryPath);
            }

            return threats;
        }

        public async Task<bool> QuarantineFileAsync(string filePath, ThreatDetection threat, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Quarantining file: {FilePath} - {ThreatName}", filePath, threat.ThreatName);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for quarantine: {FilePath}", filePath);
                    return false;
                }

                // Create quarantine directory
                var quarantineDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SijaTech", "Quarantine");

                if (!Directory.Exists(quarantineDir))
                    Directory.CreateDirectory(quarantineDir);

                // Generate unique quarantine filename
                var quarantineFileName = $"{Guid.NewGuid()}.quarantine";
                var quarantinePath = Path.Combine(quarantineDir, quarantineFileName);

                // Move file to quarantine (in real implementation)
                // For now, we'll simulate by copying and then deleting
                File.Copy(filePath, quarantinePath, true);
                await _fileService.SafeDeleteFileAsync(filePath, cancellationToken);

                // Create quarantine item record
                var quarantineItem = new QuarantineItem
                {
                    OriginalPath = filePath,
                    QuarantinePath = quarantinePath,
                    ThreatName = threat.ThreatName,
                    ThreatType = threat.Type,
                    QuarantineDate = DateTime.Now,
                    FileHash = threat.FileHash,
                    FileSize = new FileInfo(quarantinePath).Length,
                    RuleTriggered = threat.TriggeredRule?.RuleText ?? string.Empty,
                    CanRestore = true
                };

                lock (_quarantineLock)
                {
                    quarantineItem.Id = _quarantineItems.Count + 1;
                    _quarantineItems.Add(quarantineItem);
                }

                _logger.LogInformation("File successfully quarantined: {OriginalPath} -> {QuarantinePath}", 
                    filePath, quarantinePath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error quarantining file: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> RestoreFromQuarantineAsync(int quarantineId, CancellationToken cancellationToken = default)
        {
            try
            {
                QuarantineItem? item;
                lock (_quarantineLock)
                {
                    item = _quarantineItems.Find(q => q.Id == quarantineId);
                }

                if (item == null)
                {
                    _logger.LogWarning("Quarantine item not found: {QuarantineId}", quarantineId);
                    return false;
                }

                if (!item.CanRestore)
                {
                    _logger.LogWarning("Quarantine item cannot be restored: {QuarantineId}", quarantineId);
                    return false;
                }

                _logger.LogInformation("Restoring file from quarantine: {QuarantinePath} -> {OriginalPath}", 
                    item.QuarantinePath, item.OriginalPath);

                if (!File.Exists(item.QuarantinePath))
                {
                    _logger.LogError("Quarantined file not found: {QuarantinePath}", item.QuarantinePath);
                    return false;
                }

                // Ensure original directory exists
                var originalDir = Path.GetDirectoryName(item.OriginalPath);
                if (!string.IsNullOrEmpty(originalDir) && !Directory.Exists(originalDir))
                {
                    Directory.CreateDirectory(originalDir);
                }

                // Restore file
                File.Move(item.QuarantinePath, item.OriginalPath);

                // Remove from quarantine list
                lock (_quarantineLock)
                {
                    _quarantineItems.Remove(item);
                }

                _logger.LogInformation("File successfully restored from quarantine: {OriginalPath}", item.OriginalPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring file from quarantine: {QuarantineId}", quarantineId);
                return false;
            }
        }

        public async Task<bool> DeleteFromQuarantineAsync(int quarantineId, CancellationToken cancellationToken = default)
        {
            try
            {
                QuarantineItem? item;
                lock (_quarantineLock)
                {
                    item = _quarantineItems.Find(q => q.Id == quarantineId);
                }

                if (item == null)
                {
                    _logger.LogWarning("Quarantine item not found: {QuarantineId}", quarantineId);
                    return false;
                }

                _logger.LogInformation("Permanently deleting quarantined file: {QuarantinePath}", item.QuarantinePath);

                if (File.Exists(item.QuarantinePath))
                {
                    await _fileService.SecureDeleteFileAsync(item.QuarantinePath, cancellationToken);
                }

                // Remove from quarantine list
                lock (_quarantineLock)
                {
                    _quarantineItems.Remove(item);
                }

                _logger.LogInformation("Quarantined file permanently deleted: {ThreatName}", item.ThreatName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting quarantined file: {QuarantineId}", quarantineId);
                return false;
            }
        }

        public Task<List<QuarantineItem>> GetQuarantineItemsAsync(CancellationToken cancellationToken = default)
        {
            lock (_quarantineLock)
            {
                return Task.FromResult(new List<QuarantineItem>(_quarantineItems));
            }
        }

        // Event handlers
        private void OnThreatDetected(object? sender, ThreatDetection threat)
        {
            _logger.LogWarning("Real-time threat detected: {ThreatName} - {FilePath}", threat.ThreatName, threat.FilePath);
            
            // In real implementation, you might want to:
            // - Send notifications to UI
            // - Update threat statistics
            // - Trigger additional security actions
        }

        private void OnFileQuarantined(object? sender, string filePath)
        {
            _logger.LogInformation("File quarantined by real-time monitor: {FilePath}", filePath);
        }

        private void OnSecurityEventOccurred(object? sender, SecurityEvent securityEvent)
        {
            _logger.LogInformation("Security event: {EventType} - {Message}", securityEvent.EventType, securityEvent.Message);
            
            // In real implementation, save to database
        }

        // Properties
        public bool IsMonitoring => _realTimeMonitor.IsMonitoring;
        public int QueuedEventsCount => _realTimeMonitor.QueuedEventsCount;

        // Additional methods
        public async Task<(int totalSignatures, DateTime? lastUpdate)> GetSignatureDatabaseInfoAsync(CancellationToken cancellationToken = default)
        {
            var count = await _signatureDatabase.GetSignatureCountAsync(cancellationToken);
            var lastUpdate = await _signatureDatabase.GetLastUpdateAsync(cancellationToken);
            return (count, lastUpdate);
        }

        public async Task<bool> UpdateSignatureDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating signature database...");
                var result = await _signatureDatabase.UpdateSignaturesAsync(cancellationToken);
                
                if (result)
                {
                    _logger.LogInformation("Signature database updated successfully");
                }
                else
                {
                    _logger.LogWarning("Signature database update failed");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating signature database");
                return false;
            }
        }

        public void AddMonitoredPath(string path)
        {
            _realTimeMonitor.AddMonitoredPath(path);
        }

        public void RemoveMonitoredPath(string path)
        {
            _realTimeMonitor.RemoveMonitoredPath(path);
        }

        public void Dispose()
        {
            try
            {
                StopMonitoringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping monitoring during dispose");
            }

            _realTimeMonitor?.Dispose();
            _signatureDatabase?.Dispose();
        }
    }
}
