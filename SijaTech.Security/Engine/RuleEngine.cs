using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using SijaTech.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SijaTech.Security.Engine
{
    /// <summary>
    /// Rule engine terinspirasi dari Snort IDS/IPS
    /// Mendukung syntax seperti: alert file any any -> any any (msg:"Threat Detected"; content:"pattern"; sid:1001;)
    /// </summary>
    public class RuleEngine : IRuleEngine
    {
        private readonly ILogger<RuleEngine> _logger;
        private readonly List<SecurityRule> _loadedRules = new();
        private readonly object _rulesLock = new();

        // Regex pattern untuk parsing Snort-like rules
        private static readonly Regex RulePattern = new(
            @"^(?<action>\w+)\s+(?<protocol>\w+)\s+(?<src_ip>\S+)\s+(?<src_port>\S+)\s+->\s+(?<dst_ip>\S+)\s+(?<dst_port>\S+)\s+\((?<options>.*)\)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OptionPattern = new(
            @"(?<key>\w+):\s*""?(?<value>[^"";]+)""?;?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public RuleEngine(ILogger<RuleEngine> logger)
        {
            _logger = logger;
        }

        public async Task LoadRulesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Loading security rules...");

                lock (_rulesLock)
                {
                    _loadedRules.Clear();
                    
                    // Load default rules
                    LoadDefaultRules();
                }

                // In real implementation, load from database
                await Task.Delay(100, cancellationToken);

                _logger.LogInformation("Loaded {Count} security rules", _loadedRules.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading security rules");
                throw;
            }
        }

        public SecurityRule? ParseRule(string ruleText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ruleText))
                    return null;

                _logger.LogDebug("Parsing rule: {RuleText}", ruleText);

                var match = RulePattern.Match(ruleText.Trim());
                if (!match.Success)
                {
                    _logger.LogWarning("Invalid rule syntax: {RuleText}", ruleText);
                    return null;
                }

                var rule = new SecurityRule
                {
                    RuleText = ruleText,
                    CreatedDate = DateTime.Now
                };

                // Parse action
                var action = match.Groups["action"].Value.ToLowerInvariant();
                rule.Action = action switch
                {
                    "alert" => SecurityAction.Alert,
                    "log" => SecurityAction.Log,
                    "quarantine" => SecurityAction.Quarantine,
                    "delete" => SecurityAction.Delete,
                    "block" => SecurityAction.Block,
                    _ => SecurityAction.Alert
                };

                // Parse protocol/type
                var protocol = match.Groups["protocol"].Value.ToLowerInvariant();
                rule.Type = protocol switch
                {
                    "file" => RuleType.File,
                    "process" => RuleType.Process,
                    "registry" => RuleType.Registry,
                    "network" => RuleType.Network,
                    _ => RuleType.File
                };

                // Parse options
                var optionsText = match.Groups["options"].Value;
                ParseRuleOptions(rule, optionsText);

                _logger.LogDebug("Successfully parsed rule: SID={Sid}, Action={Action}, Type={Type}", 
                    rule.Sid, rule.Action, rule.Type);

                return rule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing rule: {RuleText}", ruleText);
                return null;
            }
        }

        public async Task<List<SecurityRule>> EvaluateFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var triggeredRules = new List<SecurityRule>();

            try
            {
                _logger.LogDebug("Evaluating file against rules: {FilePath}", filePath);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for evaluation: {FilePath}", filePath);
                    return triggeredRules;
                }

                List<SecurityRule> rulesToEvaluate;
                lock (_rulesLock)
                {
                    rulesToEvaluate = _loadedRules.Where(r => r.Enabled && r.Type == RuleType.File).ToList();
                }

                foreach (var rule in rulesToEvaluate)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await EvaluateRuleAgainstFileAsync(rule, filePath, cancellationToken))
                    {
                        triggeredRules.Add(rule);
                        _logger.LogInformation("Rule triggered: SID={Sid}, File={FilePath}", rule.Sid, filePath);
                    }
                }

                _logger.LogDebug("File evaluation completed. {Count} rules triggered for {FilePath}", 
                    triggeredRules.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating file: {FilePath}", filePath);
            }

            return triggeredRules;
        }

        public async Task<bool> AddRuleAsync(SecurityRule rule, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Adding new security rule: SID={Sid}", rule.Sid);

                // Validate rule
                if (rule.Sid <= 0)
                {
                    _logger.LogWarning("Invalid SID for rule: {Sid}", rule.Sid);
                    return false;
                }

                lock (_rulesLock)
                {
                    // Check for duplicate SID
                    if (_loadedRules.Any(r => r.Sid == rule.Sid))
                    {
                        _logger.LogWarning("Rule with SID {Sid} already exists", rule.Sid);
                        return false;
                    }

                    _loadedRules.Add(rule);
                }

                // In real implementation, save to database
                await Task.Delay(10, cancellationToken);

                _logger.LogInformation("Successfully added rule: SID={Sid}", rule.Sid);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding rule: SID={Sid}", rule.Sid);
                return false;
            }
        }

        public async Task<bool> UpdateRuleAsync(SecurityRule rule, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating security rule: SID={Sid}", rule.Sid);

                lock (_rulesLock)
                {
                    var existingRule = _loadedRules.FirstOrDefault(r => r.Sid == rule.Sid);
                    if (existingRule == null)
                    {
                        _logger.LogWarning("Rule not found for update: SID={Sid}", rule.Sid);
                        return false;
                    }

                    // Update rule properties
                    existingRule.RuleText = rule.RuleText;
                    existingRule.Message = rule.Message;
                    existingRule.Content = rule.Content;
                    existingRule.Hash = rule.Hash;
                    existingRule.Behavior = rule.Behavior;
                    existingRule.Action = rule.Action;
                    existingRule.Type = rule.Type;
                    existingRule.Enabled = rule.Enabled;
                    existingRule.Revision = existingRule.Revision + 1;
                }

                // In real implementation, update in database
                await Task.Delay(10, cancellationToken);

                _logger.LogInformation("Successfully updated rule: SID={Sid}", rule.Sid);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rule: SID={Sid}", rule.Sid);
                return false;
            }
        }

        public async Task<bool> DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Deleting security rule: SID={Sid}", ruleId);

                lock (_rulesLock)
                {
                    var rule = _loadedRules.FirstOrDefault(r => r.Sid == ruleId);
                    if (rule == null)
                    {
                        _logger.LogWarning("Rule not found for deletion: SID={Sid}", ruleId);
                        return false;
                    }

                    _loadedRules.Remove(rule);
                }

                // In real implementation, delete from database
                await Task.Delay(10, cancellationToken);

                _logger.LogInformation("Successfully deleted rule: SID={Sid}", ruleId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rule: SID={Sid}", ruleId);
                return false;
            }
        }

        public async Task<List<SecurityRule>> GetAllRulesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(10, cancellationToken);

                lock (_rulesLock)
                {
                    return new List<SecurityRule>(_loadedRules);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all rules");
                return new List<SecurityRule>();
            }
        }

        private void ParseRuleOptions(SecurityRule rule, string optionsText)
        {
            var matches = OptionPattern.Matches(optionsText);
            
            foreach (Match match in matches)
            {
                var key = match.Groups["key"].Value.ToLowerInvariant();
                var value = match.Groups["value"].Value;

                switch (key)
                {
                    case "msg":
                        rule.Message = value;
                        break;
                    case "content":
                        rule.Content = value;
                        break;
                    case "hash":
                        rule.Hash = value;
                        break;
                    case "behavior":
                        rule.Behavior = value;
                        break;
                    case "sid":
                        if (int.TryParse(value, out var sid))
                            rule.Sid = sid;
                        break;
                    case "rev":
                        if (int.TryParse(value, out var rev))
                            rule.Revision = rev;
                        break;
                    case "action":
                        rule.Action = value.ToLowerInvariant() switch
                        {
                            "quarantine" => SecurityAction.Quarantine,
                            "delete" => SecurityAction.Delete,
                            "block" => SecurityAction.Block,
                            "log" => SecurityAction.Log,
                            _ => SecurityAction.Alert
                        };
                        break;
                }
            }
        }

        private async Task<bool> EvaluateRuleAgainstFileAsync(SecurityRule rule, string filePath, CancellationToken cancellationToken)
        {
            try
            {
                // Check filename patterns
                if (!string.IsNullOrEmpty(rule.Content))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (rule.Content.Contains("*") || rule.Content.Contains("?"))
                    {
                        // Wildcard matching
                        var pattern = "^" + Regex.Escape(rule.Content).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
                        if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                            return true;
                    }
                    else if (fileName.Contains(rule.Content, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                // Check file hash if specified
                if (!string.IsNullOrEmpty(rule.Hash))
                {
                    var hashParts = rule.Hash.Split(':');
                    if (hashParts.Length == 2)
                    {
                        var algorithm = hashParts[0].ToUpperInvariant();
                        var expectedHash = hashParts[1].ToLowerInvariant();
                        
                        // In real implementation, calculate file hash
                        // For now, simulate hash check
                        await Task.Delay(10, cancellationToken);
                        
                        // Simulate hash match (for demo purposes)
                        if (expectedHash.Length > 10) // Basic validation
                        {
                            return false; // No match in simulation
                        }
                    }
                }

                // Check behavior patterns
                if (!string.IsNullOrEmpty(rule.Behavior))
                {
                    var behaviors = rule.Behavior.Split(',');
                    foreach (var behavior in behaviors)
                    {
                        var behaviorTrimmed = behavior.Trim();
                        
                        // Simulate behavior analysis
                        if (await CheckBehaviorPatternAsync(filePath, behaviorTrimmed, cancellationToken))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating rule {Sid} against file {FilePath}", rule.Sid, filePath);
                return false;
            }
        }

        private async Task<bool> CheckBehaviorPatternAsync(string filePath, string behavior, CancellationToken cancellationToken)
        {
            // Simulate behavior pattern checking
            await Task.Delay(5, cancellationToken);
            
            return behavior.ToLowerInvariant() switch
            {
                "network_connect" => false, // Simulate no network behavior
                "registry_modify" => false, // Simulate no registry modification
                "file_encrypt" => false,    // Simulate no file encryption
                "mass_delete" => false,     // Simulate no mass deletion
                _ => false
            };
        }

        private void LoadDefaultRules()
        {
            // Load some default security rules
            var defaultRules = new[]
            {
                "alert file any any -> any any (msg:\"Potential Malware - Suspicious Extension\"; content:\"*.exe.tmp\"; action:quarantine; sid:1001; rev:1;)",
                "alert file any any -> any any (msg:\"Ransomware - Encrypted File Extension\"; content:\"*.crypted\"; action:quarantine; sid:1002; rev:1;)",
                "alert file any any -> any any (msg:\"Suspicious Executable in Temp\"; content:\"temp\"; behavior:\"network_connect\"; action:alert; sid:1003; rev:1;)",
                "alert process any any -> any any (msg:\"Mass File Encryption Behavior\"; behavior:\"file_encrypt,mass_delete\"; action:quarantine; sid:1004; rev:1;)",
                "alert file any any -> any any (msg:\"Known Malware Hash\"; hash:\"md5:d41d8cd98f00b204e9800998ecf8427e\"; action:delete; sid:1005; rev:1;)"
            };

            foreach (var ruleText in defaultRules)
            {
                var rule = ParseRule(ruleText);
                if (rule != null)
                {
                    _loadedRules.Add(rule);
                }
            }

            _logger.LogInformation("Loaded {Count} default security rules", _loadedRules.Count);
        }

        /// <summary>
        /// Get rules by type
        /// </summary>
        public List<SecurityRule> GetRulesByType(RuleType type)
        {
            lock (_rulesLock)
            {
                return _loadedRules.Where(r => r.Type == type && r.Enabled).ToList();
            }
        }

        /// <summary>
        /// Get rule by SID
        /// </summary>
        public SecurityRule? GetRuleBySid(int sid)
        {
            lock (_rulesLock)
            {
                return _loadedRules.FirstOrDefault(r => r.Sid == sid);
            }
        }

        /// <summary>
        /// Enable/disable rule
        /// </summary>
        public bool ToggleRule(int sid, bool enabled)
        {
            lock (_rulesLock)
            {
                var rule = _loadedRules.FirstOrDefault(r => r.Sid == sid);
                if (rule != null)
                {
                    rule.Enabled = enabled;
                    _logger.LogInformation("Rule {Sid} {Status}", sid, enabled ? "enabled" : "disabled");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get rule statistics
        /// </summary>
        public (int total, int enabled, int disabled) GetRuleStatistics()
        {
            lock (_rulesLock)
            {
                var total = _loadedRules.Count;
                var enabled = _loadedRules.Count(r => r.Enabled);
                var disabled = total - enabled;
                
                return (total, enabled, disabled);
            }
        }
    }
}
