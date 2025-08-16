using System;
using System.Collections.Generic;

namespace SijaTech.Core.Models
{
    /// <summary>
    /// Security rule terinspirasi dari Snort syntax
    /// </summary>
    public class SecurityRule
    {
        public int Id { get; set; }
        public string RuleText { get; set; } = string.Empty;
        public RuleType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string Behavior { get; set; } = string.Empty;
        public SecurityAction Action { get; set; }
        public int Sid { get; set; }
        public int Revision { get; set; } = 1;
        public bool Enabled { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastTriggered { get; set; }
    }

    public enum RuleType
    {
        File,
        Process,
        Registry,
        Network
    }

    public enum SecurityAction
    {
        Alert,
        Log,
        Quarantine,
        Delete,
        Block
    }

    /// <summary>
    /// Malware signature untuk deteksi
    /// </summary>
    public class MalwareSignature
    {
        public int Id { get; set; }
        public string HashMd5 { get; set; } = string.Empty;
        public string HashSha1 { get; set; } = string.Empty;
        public string HashSha256 { get; set; } = string.Empty;
        public string MalwareFamily { get; set; } = string.Empty;
        public ThreatSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime UpdatedDate { get; set; } = DateTime.Now;
    }

    public enum ThreatSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Hasil deteksi threat
    /// </summary>
    public class ThreatDetection
    {
        public string FilePath { get; set; } = string.Empty;
        public string ThreatName { get; set; } = string.Empty;
        public ThreatType Type { get; set; }
        public ThreatSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public SecurityRule? TriggeredRule { get; set; }
        public MalwareSignature? MatchedSignature { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
        public SecurityAction ActionTaken { get; set; }
        public string FileHash { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public enum ThreatType
    {
        Malware,
        Virus,
        Trojan,
        Adware,
        Spyware,
        Ransomware,
        Suspicious,
        Unknown
    }

    /// <summary>
    /// Item yang dikarantina
    /// </summary>
    public class QuarantineItem
    {
        public int Id { get; set; }
        public string OriginalPath { get; set; } = string.Empty;
        public string QuarantinePath { get; set; } = string.Empty;
        public string ThreatName { get; set; } = string.Empty;
        public ThreatType ThreatType { get; set; }
        public DateTime QuarantineDate { get; set; } = DateTime.Now;
        public string FileHash { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string RuleTriggered { get; set; } = string.Empty;
        public bool CanRestore { get; set; } = true;
    }

    /// <summary>
    /// Behavioral pattern untuk heuristic analysis
    /// </summary>
    public class BehaviorPattern
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Indicators { get; set; } = new();
        public int Weight { get; set; }
        public ThreatSeverity Severity { get; set; }
    }

    /// <summary>
    /// Security event log
    /// </summary>
    public class SecurityEvent
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public SecurityEventType EventType { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public ThreatSeverity Severity { get; set; }
    }

    public enum SecurityEventType
    {
        ThreatDetected,
        FileQuarantined,
        RuleTriggered,
        ScanCompleted,
        DatabaseUpdated,
        SystemError
    }
}
