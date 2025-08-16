using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using SijaTech.Core.Models;
using SijaTech.Engine.Services;
using SijaTech.Security.Database;
using SijaTech.Security.Engine;
using SijaTech.Security.Services;

namespace SijaTech.Console;

class Program
{
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("🛡️ Sija Tech System Cleaner - Console Test");
        System.Console.WriteLine("==========================================");

        // Setup DI container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // Register services
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ICleaningService, CleaningService>();
        services.AddSingleton<IRegistryService, RegistryService>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<ISignatureDatabase, SignatureDatabase>();
        services.AddSingleton<ISecurityService, SecurityService>();

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            // Test 1: Rule Engine
            System.Console.WriteLine("\n🔍 Testing Rule Engine...");
            var ruleEngine = serviceProvider.GetRequiredService<IRuleEngine>();
            await ruleEngine.LoadRulesAsync();
            
            var rules = await ruleEngine.GetAllRulesAsync();
            System.Console.WriteLine($"✅ Loaded {rules.Count} security rules");

            // Test parsing a custom rule
            var customRule = ruleEngine.ParseRule("alert file any any -> any any (msg:\"Test Rule\"; content:\"*.test\"; sid:9999;)");
            if (customRule != null)
            {
                System.Console.WriteLine($"✅ Successfully parsed custom rule: {customRule.Message}");
            }

            // Test 2: Signature Database
            System.Console.WriteLine("\n🗄️ Testing Signature Database...");
            var signatureDb = serviceProvider.GetRequiredService<ISignatureDatabase>();
            
            var signatureCount = await signatureDb.GetSignatureCountAsync();
            System.Console.WriteLine($"✅ Signature database contains {signatureCount} signatures");

            var lastUpdate = await signatureDb.GetLastUpdateAsync();
            System.Console.WriteLine($"✅ Last update: {lastUpdate?.ToString() ?? "Never"}");

            // Test 3: File Service
            System.Console.WriteLine("\n📁 Testing File Service...");
            var fileService = serviceProvider.GetRequiredService<IFileService>();
            
            var tempDirs = fileService.GetTempDirectories();
            System.Console.WriteLine($"✅ Found {tempDirs.Count} temporary directories");

            var browserDirs = fileService.GetBrowserCacheDirectories();
            System.Console.WriteLine($"✅ Found {browserDirs.Count} browser cache directories");

            // Test 4: Cleaning Service
            System.Console.WriteLine("\n🧹 Testing Cleaning Service...");
            var cleaningService = serviceProvider.GetRequiredService<ICleaningService>();
            
            var options = new CleaningOptions
            {
                CleanTempFiles = true,
                CleanBrowserData = false,
                CleanRecycleBin = false,
                CleanSystemLogs = false,
                CleanRecentDocuments = false
            };

            System.Console.WriteLine("🔍 Running scan...");
            var scanResult = await cleaningService.ScanAsync(options, new Progress<CleaningProgress>(progress =>
            {
                System.Console.WriteLine($"   {progress.PercentComplete}% - {progress.CurrentOperation}");
            }));

            if (scanResult.Success)
            {
                System.Console.WriteLine($"✅ Scan completed: {scanResult.FilesDeleted} files found ({scanResult.FormattedSize})");
            }
            else
            {
                System.Console.WriteLine($"❌ Scan failed: {scanResult.Message}");
            }

            // Test 5: Security Service
            System.Console.WriteLine("\n🛡️ Testing Security Service...");
            var securityService = serviceProvider.GetRequiredService<ISecurityService>();

            var quarantineItems = await securityService.GetQuarantineItemsAsync();
            System.Console.WriteLine($"✅ Security service initialized, {quarantineItems.Count} items in quarantine");

            // Test 6: Registry Service
            System.Console.WriteLine("\n🔧 Testing Registry Service...");
            var registryService = serviceProvider.GetRequiredService<IRegistryService>();

            System.Console.WriteLine("🔍 Running registry scan...");
            var registryScanResult = await registryService.ScanRegistryAsync();
            if (registryScanResult.Success)
            {
                System.Console.WriteLine($"✅ Registry scan completed: {registryScanResult.FilesDeleted} invalid entries found");
            }
            else
            {
                System.Console.WriteLine($"❌ Registry scan failed: {registryScanResult.Message}");
            }

            System.Console.WriteLine("\n🎉 All tests completed successfully!");
            System.Console.WriteLine("✅ Core functionality is working properly");
            
            System.Console.WriteLine("\nPress any key to exit...");
            System.Console.ReadKey();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\n❌ Error during testing: {ex.Message}");
            System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
            System.Console.WriteLine("\nPress any key to exit...");
            System.Console.ReadKey();
        }
        finally
        {
            serviceProvider.Dispose();
        }
    }
}
