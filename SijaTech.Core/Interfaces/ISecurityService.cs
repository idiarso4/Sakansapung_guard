using SijaTech.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Core.Interfaces
{
    /// <summary>
    /// Interface untuk security service (terinspirasi Snort)
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Memulai real-time monitoring
        /// </summary>
        Task StartMonitoringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Menghentikan real-time monitoring
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Scan file untuk threat
        /// </summary>
        Task<ThreatDetection?> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Scan direktori untuk threat
        /// </summary>
        Task<List<ThreatDetection>> ScanDirectoryAsync(string directoryPath, 
            IProgress<CleaningProgress>? progress = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Karantina file yang terdeteksi sebagai threat
        /// </summary>
        Task<bool> QuarantineFileAsync(string filePath, ThreatDetection threat, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Restore file dari karantina
        /// </summary>
        Task<bool> RestoreFromQuarantineAsync(int quarantineId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Hapus item dari karantina
        /// </summary>
        Task<bool> DeleteFromQuarantineAsync(int quarantineId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Mendapatkan daftar item karantina
        /// </summary>
        Task<List<QuarantineItem>> GetQuarantineItemsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface untuk rule engine (seperti Snort)
    /// </summary>
    public interface IRuleEngine
    {
        /// <summary>
        /// Load rules dari database
        /// </summary>
        Task LoadRulesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Parse rule text menjadi SecurityRule object
        /// </summary>
        SecurityRule? ParseRule(string ruleText);

        /// <summary>
        /// Evaluate file terhadap semua rules
        /// </summary>
        Task<List<SecurityRule>> EvaluateFileAsync(string filePath, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tambah rule baru
        /// </summary>
        Task<bool> AddRuleAsync(SecurityRule rule, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update rule
        /// </summary>
        Task<bool> UpdateRuleAsync(SecurityRule rule, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hapus rule
        /// </summary>
        Task<bool> DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mendapatkan semua rules
        /// </summary>
        Task<List<SecurityRule>> GetAllRulesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface untuk signature database
    /// </summary>
    public interface ISignatureDatabase : IDisposable
    {
        /// <summary>
        /// Cek apakah hash file ada di database malware
        /// </summary>
        Task<MalwareSignature?> CheckHashAsync(string hash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tambah signature baru
        /// </summary>
        Task<bool> AddSignatureAsync(MalwareSignature signature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update signature database dari cloud
        /// </summary>
        Task<bool> UpdateSignaturesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Mendapatkan jumlah signatures
        /// </summary>
        Task<int> GetSignatureCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Mendapatkan tanggal update terakhir
        /// </summary>
        Task<DateTime?> GetLastUpdateAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface untuk behavior analysis
    /// </summary>
    public interface IBehaviorAnalyzer
    {
        /// <summary>
        /// Analisis behavior file
        /// </summary>
        Task<List<BehaviorPattern>> AnalyzeFileAsync(string filePath, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Analisis behavior process
        /// </summary>
        Task<List<BehaviorPattern>> AnalyzeProcessAsync(int processId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Hitung threat score berdasarkan behavior patterns
        /// </summary>
        int CalculateThreatScore(List<BehaviorPattern> patterns);

        /// <summary>
        /// Load behavior patterns dari database
        /// </summary>
        Task LoadPatternsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface untuk security logging
    /// </summary>
    public interface ISecurityLogger
    {
        /// <summary>
        /// Log security event
        /// </summary>
        Task LogEventAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mendapatkan security events
        /// </summary>
        Task<List<SecurityEvent>> GetEventsAsync(DateTime? fromDate = null, DateTime? toDate = null, 
            SecurityEventType? eventType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hapus old events
        /// </summary>
        Task<int> CleanupOldEventsAsync(int daysToKeep, CancellationToken cancellationToken = default);
    }
}
