using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using SijaTech.Core.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Security.Database
{
    /// <summary>
    /// SQLite-based signature database untuk malware detection
    /// </summary>
    public class SignatureDatabase : ISignatureDatabase, IDisposable
    {
        private readonly ILogger<SignatureDatabase> _logger;
        private readonly string _databasePath;
        private SQLiteConnection? _connection;
        private readonly object _connectionLock = new();

        public SignatureDatabase(ILogger<SignatureDatabase> logger, string? databasePath = null)
        {
            _logger = logger;
            _databasePath = databasePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SijaTech", "Security", "signatures.db");
            
            InitializeDatabaseAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_databasePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create connection
                var connectionString = $"Data Source={_databasePath};Version=3;";
                _connection = new SQLiteConnection(connectionString);
                await _connection.OpenAsync();

                // Create tables
                await CreateTablesAsync();
                
                // Load initial signatures
                await LoadInitialSignaturesAsync();

                _logger.LogInformation("Signature database initialized at: {DatabasePath}", _databasePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize signature database");
                throw;
            }
        }

        private async Task CreateTablesAsync()
        {
            var createSignaturesTable = @"
                CREATE TABLE IF NOT EXISTS malware_signatures (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    hash_md5 TEXT,
                    hash_sha1 TEXT,
                    hash_sha256 TEXT,
                    malware_family TEXT NOT NULL,
                    severity INTEGER NOT NULL,
                    description TEXT,
                    created_date TEXT NOT NULL,
                    updated_date TEXT NOT NULL,
                    UNIQUE(hash_md5, hash_sha1, hash_sha256)
                );";

            var createSecurityRulesTable = @"
                CREATE TABLE IF NOT EXISTS security_rules (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sid INTEGER UNIQUE NOT NULL,
                    rule_text TEXT NOT NULL,
                    rule_type TEXT NOT NULL,
                    message TEXT,
                    content TEXT,
                    hash_value TEXT,
                    behavior TEXT,
                    action TEXT NOT NULL,
                    revision INTEGER DEFAULT 1,
                    enabled BOOLEAN DEFAULT 1,
                    created_date TEXT NOT NULL,
                    last_triggered TEXT,
                    trigger_count INTEGER DEFAULT 0
                );";

            var createQuarantineTable = @"
                CREATE TABLE IF NOT EXISTS quarantine_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    original_path TEXT NOT NULL,
                    quarantine_path TEXT NOT NULL,
                    threat_name TEXT NOT NULL,
                    threat_type TEXT NOT NULL,
                    file_hash TEXT,
                    file_size INTEGER,
                    rule_triggered TEXT,
                    quarantine_date TEXT NOT NULL,
                    can_restore BOOLEAN DEFAULT 1
                );";

            var createSecurityEventsTable = @"
                CREATE TABLE IF NOT EXISTS security_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    message TEXT NOT NULL,
                    details TEXT,
                    file_path TEXT,
                    severity INTEGER NOT NULL
                );";

            var createIndexes = @"
                CREATE INDEX IF NOT EXISTS idx_signatures_md5 ON malware_signatures(hash_md5);
                CREATE INDEX IF NOT EXISTS idx_signatures_sha1 ON malware_signatures(hash_sha1);
                CREATE INDEX IF NOT EXISTS idx_signatures_sha256 ON malware_signatures(hash_sha256);
                CREATE INDEX IF NOT EXISTS idx_signatures_family ON malware_signatures(malware_family);
                CREATE INDEX IF NOT EXISTS idx_rules_sid ON security_rules(sid);
                CREATE INDEX IF NOT EXISTS idx_rules_enabled ON security_rules(enabled);
                CREATE INDEX IF NOT EXISTS idx_quarantine_date ON quarantine_log(quarantine_date);
                CREATE INDEX IF NOT EXISTS idx_events_timestamp ON security_events(timestamp);
                CREATE INDEX IF NOT EXISTS idx_events_type ON security_events(event_type);";

            using var command = new SQLiteCommand(_connection);
            
            command.CommandText = createSignaturesTable;
            await command.ExecuteNonQueryAsync();
            
            command.CommandText = createSecurityRulesTable;
            await command.ExecuteNonQueryAsync();
            
            command.CommandText = createQuarantineTable;
            await command.ExecuteNonQueryAsync();
            
            command.CommandText = createSecurityEventsTable;
            await command.ExecuteNonQueryAsync();
            
            command.CommandText = createIndexes;
            await command.ExecuteNonQueryAsync();

            _logger.LogDebug("Database tables created successfully");
        }

        public async Task<MalwareSignature?> CheckHashAsync(string hash, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(hash))
                    return null;

                lock (_connectionLock)
                {
                    if (_connection == null)
                        return null;
                }

                var sql = @"
                    SELECT id, hash_md5, hash_sha1, hash_sha256, malware_family, severity, description, created_date, updated_date
                    FROM malware_signatures 
                    WHERE hash_md5 = @hash OR hash_sha1 = @hash OR hash_sha256 = @hash
                    LIMIT 1";

                using var command = new SQLiteCommand(sql, _connection);
                command.Parameters.AddWithValue("@hash", hash.ToLowerInvariant());

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var signature = new MalwareSignature
                    {
                        Id = reader.GetInt32(0),
                        HashMd5 = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        HashSha1 = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        HashSha256 = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        MalwareFamily = reader.GetString(4),
                        Severity = (ThreatSeverity)reader.GetInt32(5),
                        Description = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        CreatedDate = DateTime.Parse(reader.GetString(7)),
                        UpdatedDate = DateTime.Parse(reader.GetString(8))
                    };

                    _logger.LogInformation("Malware signature found for hash {Hash}: {Family}", hash, signature.MalwareFamily);
                    return signature;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking hash in signature database: {Hash}", hash);
                return null;
            }
        }

        public async Task<bool> AddSignatureAsync(MalwareSignature signature, CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_connectionLock)
                {
                    if (_connection == null)
                        return false;
                }

                var sql = @"
                    INSERT OR REPLACE INTO malware_signatures 
                    (hash_md5, hash_sha1, hash_sha256, malware_family, severity, description, created_date, updated_date)
                    VALUES (@hash_md5, @hash_sha1, @hash_sha256, @malware_family, @severity, @description, @created_date, @updated_date)";

                using var command = new SQLiteCommand(sql, _connection);
                command.Parameters.AddWithValue("@hash_md5", signature.HashMd5?.ToLowerInvariant() ?? string.Empty);
                command.Parameters.AddWithValue("@hash_sha1", signature.HashSha1?.ToLowerInvariant() ?? string.Empty);
                command.Parameters.AddWithValue("@hash_sha256", signature.HashSha256?.ToLowerInvariant() ?? string.Empty);
                command.Parameters.AddWithValue("@malware_family", signature.MalwareFamily);
                command.Parameters.AddWithValue("@severity", (int)signature.Severity);
                command.Parameters.AddWithValue("@description", signature.Description);
                command.Parameters.AddWithValue("@created_date", signature.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@updated_date", signature.UpdatedDate.ToString("yyyy-MM-dd HH:mm:ss"));

                var result = await command.ExecuteNonQueryAsync(cancellationToken);
                
                if (result > 0)
                {
                    _logger.LogInformation("Added malware signature: {Family}", signature.MalwareFamily);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding signature to database: {Family}", signature.MalwareFamily);
                return false;
            }
        }

        public async Task<bool> UpdateSignaturesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting signature database update...");

                // In real implementation, this would download from cloud threat intelligence feeds
                // For now, we'll simulate by adding some sample signatures
                
                var sampleSignatures = new[]
                {
                    new MalwareSignature
                    {
                        HashMd5 = "5d41402abc4b2a76b9719d911017c592",
                        HashSha1 = "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d",
                        HashSha256 = "2cf24dba4f21d4288094c8b0f5b6dc0b80e70ebd95c5b0f42d3c94fc53d6e61a",
                        MalwareFamily = "Trojan.Generic.Sample",
                        Severity = ThreatSeverity.High,
                        Description = "Sample trojan signature for testing",
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now
                    },
                    new MalwareSignature
                    {
                        HashMd5 = "098f6bcd4621d373cade4e832627b4f6",
                        HashSha1 = "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3",
                        HashSha256 = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
                        MalwareFamily = "Adware.BrowserHijacker",
                        Severity = ThreatSeverity.Medium,
                        Description = "Browser hijacker adware signature",
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now
                    },
                    new MalwareSignature
                    {
                        HashMd5 = "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8",
                        HashSha1 = "b1d5781111d84f7b3fe45a0852e59758cd7a87e5",
                        HashSha256 = "50d858e0985ecc7f60418aaf0cc5ab587f42c2570a884095a9e8ccacd0f6545c",
                        MalwareFamily = "Ransomware.Cryptolocker",
                        Severity = ThreatSeverity.Critical,
                        Description = "Cryptolocker ransomware variant",
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now
                    }
                };

                foreach (var signature in sampleSignatures)
                {
                    await AddSignatureAsync(signature, cancellationToken);
                }

                // Update last update timestamp
                await SetLastUpdateAsync(DateTime.Now, cancellationToken);

                _logger.LogInformation("Signature database update completed. Added {Count} signatures", sampleSignatures.Length);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating signature database");
                return false;
            }
        }

        public async Task<int> GetSignatureCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_connectionLock)
                {
                    if (_connection == null)
                        return 0;
                }

                var sql = "SELECT COUNT(*) FROM malware_signatures";
                using var command = new SQLiteCommand(sql, _connection);
                
                var result = await command.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting signature count");
                return 0;
            }
        }

        public async Task<DateTime?> GetLastUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_connectionLock)
                {
                    if (_connection == null)
                        return null;
                }

                // Check if metadata table exists, create if not
                await EnsureMetadataTableAsync();

                var sql = "SELECT value FROM metadata WHERE key = 'last_update' LIMIT 1";
                using var command = new SQLiteCommand(sql, _connection);
                
                var result = await command.ExecuteScalarAsync(cancellationToken);
                if (result != null && DateTime.TryParse(result.ToString(), out var lastUpdate))
                {
                    return lastUpdate;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last update timestamp");
                return null;
            }
        }

        private async Task SetLastUpdateAsync(DateTime timestamp, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureMetadataTableAsync();

                var sql = @"
                    INSERT OR REPLACE INTO metadata (key, value) 
                    VALUES ('last_update', @timestamp)";

                using var command = new SQLiteCommand(sql, _connection);
                command.Parameters.AddWithValue("@timestamp", timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting last update timestamp");
            }
        }

        private async Task EnsureMetadataTableAsync()
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS metadata (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                )";

            using var command = new SQLiteCommand(sql, _connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task LoadInitialSignaturesAsync()
        {
            try
            {
                var count = await GetSignatureCountAsync();
                if (count == 0)
                {
                    _logger.LogInformation("Loading initial signature database...");
                    await UpdateSignaturesAsync();
                }
                else
                {
                    _logger.LogInformation("Signature database already contains {Count} signatures", count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading initial signatures");
            }
        }

        /// <summary>
        /// Get signatures by malware family
        /// </summary>
        public async Task<List<MalwareSignature>> GetSignaturesByFamilyAsync(string family, CancellationToken cancellationToken = default)
        {
            var signatures = new List<MalwareSignature>();

            try
            {
                lock (_connectionLock)
                {
                    if (_connection == null)
                        return signatures;
                }

                var sql = @"
                    SELECT id, hash_md5, hash_sha1, hash_sha256, malware_family, severity, description, created_date, updated_date
                    FROM malware_signatures 
                    WHERE malware_family LIKE @family
                    ORDER BY created_date DESC";

                using var command = new SQLiteCommand(sql, _connection);
                command.Parameters.AddWithValue("@family", $"%{family}%");

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    signatures.Add(new MalwareSignature
                    {
                        Id = reader.GetInt32(0),
                        HashMd5 = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        HashSha1 = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        HashSha256 = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        MalwareFamily = reader.GetString(4),
                        Severity = (ThreatSeverity)reader.GetInt32(5),
                        Description = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        CreatedDate = DateTime.Parse(reader.GetString(7)),
                        UpdatedDate = DateTime.Parse(reader.GetString(8))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting signatures by family: {Family}", family);
            }

            return signatures;
        }

        /// <summary>
        /// Get signatures by severity
        /// </summary>
        public async Task<List<MalwareSignature>> GetSignaturesBySeverityAsync(ThreatSeverity severity, CancellationToken cancellationToken = default)
        {
            var signatures = new List<MalwareSignature>();

            try
            {
                lock (_connectionLock)
                {
                    if (_connection == null)
                        return signatures;
                }

                var sql = @"
                    SELECT id, hash_md5, hash_sha1, hash_sha256, malware_family, severity, description, created_date, updated_date
                    FROM malware_signatures 
                    WHERE severity = @severity
                    ORDER BY created_date DESC";

                using var command = new SQLiteCommand(sql, _connection);
                command.Parameters.AddWithValue("@severity", (int)severity);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    signatures.Add(new MalwareSignature
                    {
                        Id = reader.GetInt32(0),
                        HashMd5 = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        HashSha1 = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        HashSha256 = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        MalwareFamily = reader.GetString(4),
                        Severity = (ThreatSeverity)reader.GetInt32(5),
                        Description = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        CreatedDate = DateTime.Parse(reader.GetString(7)),
                        UpdatedDate = DateTime.Parse(reader.GetString(8))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting signatures by severity: {Severity}", severity);
            }

            return signatures;
        }

        /// <summary>
        /// Delete signature by ID
        /// </summary>
        public async Task<bool> DeleteSignatureAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_connectionLock)
                {
                    if (_connection == null)
                        return false;
                }

                var sql = "DELETE FROM malware_signatures WHERE id = @id";
                using var command = new SQLiteCommand(sql, _connection);
                command.Parameters.AddWithValue("@id", id);

                var result = await command.ExecuteNonQueryAsync(cancellationToken);
                
                if (result > 0)
                {
                    _logger.LogInformation("Deleted signature with ID: {Id}", id);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting signature: {Id}", id);
                return false;
            }
        }

        /// <summary>
        /// Get database statistics
        /// </summary>
        public async Task<(int totalSignatures, int criticalThreats, int highThreats, int mediumThreats, int lowThreats)> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                lock (_connectionLock)
                {
                    if (_connection == null)
                        return (0, 0, 0, 0, 0);
                }

                var sql = @"
                    SELECT 
                        COUNT(*) as total,
                        SUM(CASE WHEN severity = 4 THEN 1 ELSE 0 END) as critical,
                        SUM(CASE WHEN severity = 3 THEN 1 ELSE 0 END) as high,
                        SUM(CASE WHEN severity = 2 THEN 1 ELSE 0 END) as medium,
                        SUM(CASE WHEN severity = 1 THEN 1 ELSE 0 END) as low
                    FROM malware_signatures";

                using var command = new SQLiteCommand(sql, _connection);
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    return (
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2),
                        reader.GetInt32(3),
                        reader.GetInt32(4)
                    );
                }

                return (0, 0, 0, 0, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database statistics");
                return (0, 0, 0, 0, 0);
            }
        }

        public void Dispose()
        {
            lock (_connectionLock)
            {
                _connection?.Close();
                _connection?.Dispose();
                _connection = null;
            }
        }
    }
}
