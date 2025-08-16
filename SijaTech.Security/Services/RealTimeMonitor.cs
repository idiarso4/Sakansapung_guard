using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using SijaTech.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Security.Services
{
    /// <summary>
    /// Real-time file system monitoring service terinspirasi dari Snort network monitoring
    /// </summary>
    public class RealTimeMonitor : IDisposable
    {
        private readonly ILogger<RealTimeMonitor> _logger;
        private readonly IRuleEngine _ruleEngine;
        private readonly ISignatureDatabase _signatureDatabase;
        private readonly IFileService _fileService;
        
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly ConcurrentQueue<FileSystemEventArgs> _eventQueue = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly SemaphoreSlim _processingLock = new(1, 1);
        
        private Task? _processingTask;
        private bool _isMonitoring = false;
        private readonly object _monitoringLock = new();

        // Events
        public event EventHandler<ThreatDetection>? ThreatDetected;
        public event EventHandler<string>? FileQuarantined;
        public event EventHandler<SecurityEvent>? SecurityEventOccurred;

        // Configuration
        private readonly List<string> _monitoredPaths = new();
        private readonly HashSet<string> _excludedExtensions = new();
        private readonly HashSet<string> _excludedPaths = new();
        
        public RealTimeMonitor(
            ILogger<RealTimeMonitor> logger,
            IRuleEngine ruleEngine,
            ISignatureDatabase signatureDatabase,
            IFileService fileService)
        {
            _logger = logger;
            _ruleEngine = ruleEngine;
            _signatureDatabase = signatureDatabase;
            _fileService = fileService;
            
            InitializeDefaultConfiguration();
        }

        private void InitializeDefaultConfiguration()
        {
            // Default monitored paths
            _monitoredPaths.AddRange(new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads",
                Path.GetTempPath(),
                @"C:\Windows\Temp"
            });

            // Excluded file extensions (to reduce noise)
            _excludedExtensions.UnionWith(new[]
            {
                ".tmp", ".log", ".cache", ".bak", ".old", ".swp", ".lock"
            });

            // Excluded paths (system critical)
            _excludedPaths.UnionWith(new[]
            {
                @"C:\Windows\System32",
                @"C:\Windows\SysWOW64",
                @"C:\Program Files\Windows Defender",
                @"C:\ProgramData\Microsoft\Windows Defender"
            });
        }

        public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_monitoringLock)
                {
                    if (_isMonitoring)
                    {
                        _logger.LogWarning("Real-time monitoring is already active");
                        return;
                    }
                    _isMonitoring = true;
                }

                _logger.LogInformation("Starting real-time file system monitoring...");

                // Load security rules
                await _ruleEngine.LoadRulesAsync(cancellationToken);

                // Setup file system watchers
                SetupFileSystemWatchers();

                // Start processing task
                _processingTask = ProcessEventsAsync(_cancellationTokenSource.Token);

                await LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.SystemError, // Using available enum value
                    Message = "Real-time monitoring started",
                    Details = $"Monitoring {_monitoredPaths.Count} paths",
                    Severity = ThreatSeverity.Low
                });

                _logger.LogInformation("Real-time monitoring started successfully. Monitoring {Count} paths", _monitoredPaths.Count);
            }
            catch (Exception ex)
            {
                lock (_monitoringLock)
                {
                    _isMonitoring = false;
                }
                
                _logger.LogError(ex, "Failed to start real-time monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            try
            {
                lock (_monitoringLock)
                {
                    if (!_isMonitoring)
                    {
                        _logger.LogWarning("Real-time monitoring is not active");
                        return;
                    }
                    _isMonitoring = false;
                }

                _logger.LogInformation("Stopping real-time file system monitoring...");

                // Stop file system watchers
                foreach (var watcher in _watchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                _watchers.Clear();

                // Cancel processing task
                _cancellationTokenSource.Cancel();
                
                if (_processingTask != null)
                {
                    await _processingTask;
                }

                await LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.SystemError, // Using available enum value
                    Message = "Real-time monitoring stopped",
                    Severity = ThreatSeverity.Low
                });

                _logger.LogInformation("Real-time monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping real-time monitoring");
            }
        }

        private void SetupFileSystemWatchers()
        {
            foreach (var path in _monitoredPaths)
            {
                try
                {
                    if (!Directory.Exists(path))
                    {
                        _logger.LogWarning("Monitored path does not exist: {Path}", path);
                        continue;
                    }

                    var watcher = new FileSystemWatcher(path)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                        Filter = "*.*"
                    };

                    watcher.Created += OnFileSystemEvent;
                    watcher.Changed += OnFileSystemEvent;
                    watcher.Renamed += OnFileRenamed;
                    watcher.Error += OnWatcherError;

                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);

                    _logger.LogDebug("File system watcher setup for: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to setup watcher for path: {Path}", path);
                }
            }
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Quick filtering to reduce noise
                if (ShouldIgnoreFile(e.FullPath))
                    return;

                _eventQueue.Enqueue(e);
                _logger.LogTrace("File system event queued: {EventType} - {Path}", e.ChangeType, e.FullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file system event: {Path}", e.FullPath);
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                // Check both old and new names
                if (!ShouldIgnoreFile(e.FullPath) || !ShouldIgnoreFile(e.OldFullPath))
                {
                    _eventQueue.Enqueue(e);
                    _logger.LogTrace("File rename event queued: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file rename event: {Path}", e.FullPath);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "File system watcher error occurred");
        }

        private bool ShouldIgnoreFile(string filePath)
        {
            try
            {
                // Check excluded paths
                foreach (var excludedPath in _excludedPaths)
                {
                    if (filePath.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // Check excluded extensions
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (_excludedExtensions.Contains(extension))
                    return true;

                // Ignore very small files (likely not malware)
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length < 1024) // Less than 1KB
                        return true;
                }

                return false;
            }
            catch
            {
                return true; // Ignore files we can't access
            }
        }

        private async Task ProcessEventsAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Event processing task started");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_eventQueue.TryDequeue(out var fileEvent))
                    {
                        await ProcessFileEventAsync(fileEvent, cancellationToken);
                    }
                    else
                    {
                        // No events to process, wait a bit
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Event processing task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event processing task");
            }

            _logger.LogDebug("Event processing task completed");
        }

        private async Task ProcessFileEventAsync(FileSystemEventArgs fileEvent, CancellationToken cancellationToken)
        {
            try
            {
                await _processingLock.WaitAsync(cancellationToken);

                _logger.LogDebug("Processing file event: {EventType} - {Path}", fileEvent.ChangeType, fileEvent.FullPath);

                // Wait a bit for file to be fully written
                await Task.Delay(500, cancellationToken);

                if (!File.Exists(fileEvent.FullPath))
                {
                    _logger.LogTrace("File no longer exists: {Path}", fileEvent.FullPath);
                    return;
                }

                // Check if file is still being written
                if (_fileService.IsFileInUse(fileEvent.FullPath))
                {
                    _logger.LogTrace("File is in use, skipping: {Path}", fileEvent.FullPath);
                    return;
                }

                // Evaluate against security rules
                var triggeredRules = await _ruleEngine.EvaluateFileAsync(fileEvent.FullPath, cancellationToken);
                
                if (triggeredRules.Count > 0)
                {
                    await HandleTriggeredRulesAsync(fileEvent.FullPath, triggeredRules, cancellationToken);
                }

                // Check against signature database
                await CheckFileSignatureAsync(fileEvent.FullPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file event: {Path}", fileEvent.FullPath);
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private async Task HandleTriggeredRulesAsync(string filePath, List<SecurityRule> triggeredRules, CancellationToken cancellationToken)
        {
            foreach (var rule in triggeredRules)
            {
                try
                {
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
                        FileHash = await _fileService.GetFileHashAsync(filePath, "SHA256", cancellationToken)
                    };

                    // Execute action based on rule
                    switch (rule.Action)
                    {
                        case SecurityAction.Quarantine:
                            await QuarantineFileAsync(filePath, threat, cancellationToken);
                            break;
                        case SecurityAction.Delete:
                            await DeleteThreatFileAsync(filePath, threat, cancellationToken);
                            break;
                        case SecurityAction.Block:
                            // In real implementation, block file access
                            _logger.LogWarning("File access blocked: {Path}", filePath);
                            break;
                        case SecurityAction.Alert:
                        case SecurityAction.Log:
                        default:
                            // Just log and alert
                            break;
                    }

                    // Raise threat detected event
                    ThreatDetected?.Invoke(this, threat);

                    // Log security event
                    await LogSecurityEventAsync(new SecurityEvent
                    {
                        EventType = SecurityEventType.ThreatDetected,
                        Message = $"Threat detected: {rule.Message}",
                        Details = $"File: {filePath}, Rule SID: {rule.Sid}",
                        FilePath = filePath,
                        Severity = ThreatSeverity.Medium
                    });

                    _logger.LogWarning("Threat detected by rule {Sid}: {Path} - {Message}", rule.Sid, filePath, rule.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling triggered rule {Sid} for file: {Path}", rule.Sid, filePath);
                }
            }
        }

        private async Task CheckFileSignatureAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                var fileHash = await _fileService.GetFileHashAsync(filePath, "SHA256", cancellationToken);
                if (string.IsNullOrEmpty(fileHash))
                    return;

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
                        ActionTaken = SecurityAction.Quarantine,
                        FileHash = fileHash
                    };

                    // Auto-quarantine known malware
                    await QuarantineFileAsync(filePath, threat, cancellationToken);

                    // Raise threat detected event
                    ThreatDetected?.Invoke(this, threat);

                    // Log security event
                    await LogSecurityEventAsync(new SecurityEvent
                    {
                        EventType = SecurityEventType.ThreatDetected,
                        Message = $"Known malware detected: {signature.MalwareFamily}",
                        Details = $"File: {filePath}, Hash: {fileHash}",
                        FilePath = filePath,
                        Severity = signature.Severity
                    });

                    _logger.LogCritical("Known malware detected: {Family} - {Path}", signature.MalwareFamily, filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file signature: {Path}", filePath);
            }
        }

        private async Task QuarantineFileAsync(string filePath, ThreatDetection threat, CancellationToken cancellationToken)
        {
            try
            {
                // In real implementation, move file to quarantine directory
                var quarantineDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SijaTech", "Quarantine");

                if (!Directory.Exists(quarantineDir))
                    Directory.CreateDirectory(quarantineDir);

                var quarantinePath = Path.Combine(quarantineDir, $"{Guid.NewGuid()}.quarantine");
                
                // Simulate quarantine (in real implementation, move the file)
                _logger.LogInformation("File quarantined: {OriginalPath} -> {QuarantinePath}", filePath, quarantinePath);

                FileQuarantined?.Invoke(this, filePath);

                await LogSecurityEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.FileQuarantined,
                    Message = $"File quarantined: {threat.ThreatName}",
                    Details = $"Original: {filePath}, Quarantine: {quarantinePath}",
                    FilePath = filePath,
                    Severity = threat.Severity
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error quarantining file: {Path}", filePath);
            }
        }

        private async Task DeleteThreatFileAsync(string filePath, ThreatDetection threat, CancellationToken cancellationToken)
        {
            try
            {
                if (await _fileService.SafeDeleteFileAsync(filePath, cancellationToken))
                {
                    _logger.LogWarning("Threat file deleted: {Path} - {ThreatName}", filePath, threat.ThreatName);

                    await LogSecurityEventAsync(new SecurityEvent
                    {
                        EventType = SecurityEventType.ThreatDetected, // Using available enum value
                        Message = $"Threat file deleted: {threat.ThreatName}",
                        Details = $"File: {filePath}",
                        FilePath = filePath,
                        Severity = threat.Severity
                    });
                }
                else
                {
                    _logger.LogError("Failed to delete threat file: {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting threat file: {Path}", filePath);
            }
        }

        private async Task LogSecurityEventAsync(SecurityEvent securityEvent)
        {
            try
            {
                SecurityEventOccurred?.Invoke(this, securityEvent);
                
                // In real implementation, save to database
                await Task.Delay(1);
                
                _logger.LogDebug("Security event logged: {EventType} - {Message}", securityEvent.EventType, securityEvent.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event");
            }
        }

        public bool IsMonitoring
        {
            get
            {
                lock (_monitoringLock)
                {
                    return _isMonitoring;
                }
            }
        }

        public int QueuedEventsCount => _eventQueue.Count;

        public void AddMonitoredPath(string path)
        {
            if (!_monitoredPaths.Contains(path))
            {
                _monitoredPaths.Add(path);
                _logger.LogInformation("Added monitored path: {Path}", path);
            }
        }

        public void RemoveMonitoredPath(string path)
        {
            if (_monitoredPaths.Remove(path))
            {
                _logger.LogInformation("Removed monitored path: {Path}", path);
            }
        }

        public void Dispose()
        {
            try
            {
                StopMonitoringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time monitor");
            }

            _cancellationTokenSource?.Dispose();
            _processingLock?.Dispose();

            foreach (var watcher in _watchers)
            {
                watcher?.Dispose();
            }
            _watchers.Clear();
        }
    }
}
