using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SijaTech.Core.Models;
using SijaTech.Security.Engine;
using System;
using System.Threading.Tasks;
using Xunit;

namespace SijaTech.Tests.Security
{
    public class RuleEngineTests
    {
        private readonly Mock<ILogger<RuleEngine>> _mockLogger;
        private readonly RuleEngine _ruleEngine;

        public RuleEngineTests()
        {
            _mockLogger = new Mock<ILogger<RuleEngine>>();
            _ruleEngine = new RuleEngine(_mockLogger.Object);
        }

        [Fact]
        public void ParseRule_ValidAlertRule_ShouldParseCorrectly()
        {
            // Arrange
            var ruleText = "alert file any any -> any any (msg:\"Test Malware\"; content:\"*.virus\"; sid:1001; rev:1;)";

            // Act
            var rule = _ruleEngine.ParseRule(ruleText);

            // Assert
            rule.Should().NotBeNull();
            rule!.Action.Should().Be(SecurityAction.Alert);
            rule.Type.Should().Be(RuleType.File);
            rule.Message.Should().Be("Test Malware");
            rule.Content.Should().Be("*.virus");
            rule.Sid.Should().Be(1001);
            rule.Revision.Should().Be(1);
        }

        [Fact]
        public void ParseRule_ValidQuarantineRule_ShouldParseCorrectly()
        {
            // Arrange
            var ruleText = "alert file any any -> any any (msg:\"Ransomware Detected\"; content:\"*.crypted\"; action:quarantine; sid:2001;)";

            // Act
            var rule = _ruleEngine.ParseRule(ruleText);

            // Assert
            rule.Should().NotBeNull();
            rule!.Action.Should().Be(SecurityAction.Quarantine);
            rule.Message.Should().Be("Ransomware Detected");
            rule.Content.Should().Be("*.crypted");
            rule.Sid.Should().Be(2001);
        }

        [Fact]
        public void ParseRule_ValidHashRule_ShouldParseCorrectly()
        {
            // Arrange
            var ruleText = "alert file any any -> any any (msg:\"Known Malware Hash\"; hash:\"md5:d41d8cd98f00b204e9800998ecf8427e\"; action:delete; sid:3001;)";

            // Act
            var rule = _ruleEngine.ParseRule(ruleText);

            // Assert
            rule.Should().NotBeNull();
            rule!.Action.Should().Be(SecurityAction.Delete);
            rule.Hash.Should().Be("md5:d41d8cd98f00b204e9800998ecf8427e");
            rule.Sid.Should().Be(3001);
        }

        [Fact]
        public void ParseRule_ValidBehaviorRule_ShouldParseCorrectly()
        {
            // Arrange
            var ruleText = "alert process any any -> any any (msg:\"Suspicious Behavior\"; behavior:\"file_encrypt,mass_delete\"; sid:4001;)";

            // Act
            var rule = _ruleEngine.ParseRule(ruleText);

            // Assert
            rule.Should().NotBeNull();
            rule!.Type.Should().Be(RuleType.Process);
            rule.Behavior.Should().Be("file_encrypt,mass_delete");
            rule.Sid.Should().Be(4001);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid rule syntax")]
        [InlineData("alert file")]
        [InlineData("not a rule at all")]
        public void ParseRule_InvalidSyntax_ShouldReturnNull(string invalidRule)
        {
            // Act
            var rule = _ruleEngine.ParseRule(invalidRule);

            // Assert
            rule.Should().BeNull();
        }

        [Fact]
        public async Task LoadRulesAsync_ShouldLoadDefaultRules()
        {
            // Act
            await _ruleEngine.LoadRulesAsync();
            var rules = await _ruleEngine.GetAllRulesAsync();

            // Assert
            rules.Should().NotBeEmpty();
            rules.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task AddRuleAsync_ValidRule_ShouldAddSuccessfully()
        {
            // Arrange
            await _ruleEngine.LoadRulesAsync();
            var rule = new SecurityRule
            {
                Sid = 9999,
                RuleText = "alert file any any -> any any (msg:\"Test Rule\"; sid:9999;)",
                Message = "Test Rule",
                Action = SecurityAction.Alert,
                Type = RuleType.File,
                Enabled = true
            };

            // Act
            var result = await _ruleEngine.AddRuleAsync(rule);

            // Assert
            result.Should().BeTrue();
            
            var addedRule = _ruleEngine.GetRuleBySid(9999);
            addedRule.Should().NotBeNull();
            addedRule!.Message.Should().Be("Test Rule");
        }

        [Fact]
        public async Task AddRuleAsync_DuplicateSid_ShouldFail()
        {
            // Arrange
            await _ruleEngine.LoadRulesAsync();
            var rule1 = new SecurityRule
            {
                Sid = 8888,
                RuleText = "alert file any any -> any any (msg:\"First Rule\"; sid:8888;)",
                Message = "First Rule",
                Action = SecurityAction.Alert,
                Type = RuleType.File
            };
            var rule2 = new SecurityRule
            {
                Sid = 8888, // Same SID
                RuleText = "alert file any any -> any any (msg:\"Second Rule\"; sid:8888;)",
                Message = "Second Rule",
                Action = SecurityAction.Alert,
                Type = RuleType.File
            };

            // Act
            var result1 = await _ruleEngine.AddRuleAsync(rule1);
            var result2 = await _ruleEngine.AddRuleAsync(rule2);

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateRuleAsync_ExistingRule_ShouldUpdateSuccessfully()
        {
            // Arrange
            await _ruleEngine.LoadRulesAsync();
            var originalRule = new SecurityRule
            {
                Sid = 7777,
                RuleText = "alert file any any -> any any (msg:\"Original Rule\"; sid:7777;)",
                Message = "Original Rule",
                Action = SecurityAction.Alert,
                Type = RuleType.File
            };
            await _ruleEngine.AddRuleAsync(originalRule);

            var updatedRule = new SecurityRule
            {
                Sid = 7777,
                RuleText = "alert file any any -> any any (msg:\"Updated Rule\"; sid:7777;)",
                Message = "Updated Rule",
                Action = SecurityAction.Quarantine,
                Type = RuleType.File
            };

            // Act
            var result = await _ruleEngine.UpdateRuleAsync(updatedRule);

            // Assert
            result.Should().BeTrue();
            
            var rule = _ruleEngine.GetRuleBySid(7777);
            rule.Should().NotBeNull();
            rule!.Message.Should().Be("Updated Rule");
            rule.Action.Should().Be(SecurityAction.Quarantine);
            rule.Revision.Should().Be(2); // Should increment revision
        }

        [Fact]
        public async Task DeleteRuleAsync_ExistingRule_ShouldDeleteSuccessfully()
        {
            // Arrange
            await _ruleEngine.LoadRulesAsync();
            var rule = new SecurityRule
            {
                Sid = 6666,
                RuleText = "alert file any any -> any any (msg:\"To Be Deleted\"; sid:6666;)",
                Message = "To Be Deleted",
                Action = SecurityAction.Alert,
                Type = RuleType.File
            };
            await _ruleEngine.AddRuleAsync(rule);

            // Act
            var result = await _ruleEngine.DeleteRuleAsync(6666);

            // Assert
            result.Should().BeTrue();
            
            var deletedRule = _ruleEngine.GetRuleBySid(6666);
            deletedRule.Should().BeNull();
        }

        [Fact]
        public void ToggleRule_ExistingRule_ShouldToggleEnabled()
        {
            // Arrange
            _ruleEngine.LoadRulesAsync().Wait();
            var rules = _ruleEngine.GetAllRulesAsync().Result;
            var firstRule = rules[0];
            var originalState = firstRule.Enabled;

            // Act
            var result = _ruleEngine.ToggleRule(firstRule.Sid, !originalState);

            // Assert
            result.Should().BeTrue();
            
            var toggledRule = _ruleEngine.GetRuleBySid(firstRule.Sid);
            toggledRule.Should().NotBeNull();
            toggledRule!.Enabled.Should().Be(!originalState);
        }

        [Fact]
        public void GetRuleStatistics_ShouldReturnCorrectCounts()
        {
            // Arrange
            _ruleEngine.LoadRulesAsync().Wait();

            // Act
            var (total, enabled, disabled) = _ruleEngine.GetRuleStatistics();

            // Assert
            total.Should().BeGreaterThan(0);
            enabled.Should().BeGreaterThan(0);
            (enabled + disabled).Should().Be(total);
        }

        [Fact]
        public void GetRulesByType_FileType_ShouldReturnOnlyFileRules()
        {
            // Arrange
            _ruleEngine.LoadRulesAsync().Wait();

            // Act
            var fileRules = _ruleEngine.GetRulesByType(RuleType.File);

            // Assert
            fileRules.Should().NotBeEmpty();
            fileRules.Should().OnlyContain(r => r.Type == RuleType.File);
        }

        [Fact]
        public async Task EvaluateFileAsync_NonExistentFile_ShouldReturnEmptyList()
        {
            // Arrange
            await _ruleEngine.LoadRulesAsync();
            var nonExistentFile = @"C:\NonExistent\File.exe";

            // Act
            var triggeredRules = await _ruleEngine.EvaluateFileAsync(nonExistentFile);

            // Assert
            triggeredRules.Should().BeEmpty();
        }

        [Theory]
        [InlineData("alert")]
        [InlineData("log")]
        [InlineData("quarantine")]
        [InlineData("delete")]
        [InlineData("block")]
        public void ParseRule_DifferentActions_ShouldParseCorrectly(string action)
        {
            // Arrange
            var ruleText = $"alert file any any -> any any (msg:\"Test {action}\"; action:{action}; sid:5001;)";

            // Act
            var rule = _ruleEngine.ParseRule(ruleText);

            // Assert
            rule.Should().NotBeNull();
            
            var expectedAction = action switch
            {
                "alert" => SecurityAction.Alert,
                "log" => SecurityAction.Log,
                "quarantine" => SecurityAction.Quarantine,
                "delete" => SecurityAction.Delete,
                "block" => SecurityAction.Block,
                _ => SecurityAction.Alert
            };
            
            rule!.Action.Should().Be(expectedAction);
        }

        [Theory]
        [InlineData("file")]
        [InlineData("process")]
        [InlineData("registry")]
        [InlineData("network")]
        public void ParseRule_DifferentTypes_ShouldParseCorrectly(string type)
        {
            // Arrange
            var ruleText = $"alert {type} any any -> any any (msg:\"Test {type}\"; sid:5002;)";

            // Act
            var rule = _ruleEngine.ParseRule(ruleText);

            // Assert
            rule.Should().NotBeNull();
            
            var expectedType = type switch
            {
                "file" => RuleType.File,
                "process" => RuleType.Process,
                "registry" => RuleType.Registry,
                "network" => RuleType.Network,
                _ => RuleType.File
            };
            
            rule!.Type.Should().Be(expectedType);
        }
    }
}
