using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SijaTech.Core.Interfaces;
using SijaTech.UI.ViewModels;
using SijaTech.UI.Views;
using System;
using System.Windows;

namespace SijaTech.UI
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Setup Serilog
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Debug()
                    .WriteTo.File("logs/sijatech-.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                Log.Information("Starting Sija Tech application...");

                // Build host
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureServices(ConfigureServices)
                    .Build();

                // Start host
                _host.Start();
                Log.Information("Host started successfully");

                // Show simple window
                var simpleWindow = new Views.SimpleWindow();
                Log.Information("SimpleWindow created successfully");

                simpleWindow.Show();
                Log.Information("SimpleWindow shown successfully");

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application startup failed");
                MessageBox.Show($"Aplikasi gagal dimulai: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register Views
            services.AddSingleton<MainWindow>();
            
            // Register ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<CleanerViewModel>();
            services.AddTransient<SecurityViewModel>();
            services.AddTransient<RegistryViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Register Core Services
            services.AddSingleton<SijaTech.Core.Interfaces.IFileService, SijaTech.Engine.Services.FileService>();
            services.AddSingleton<SijaTech.Core.Interfaces.ICleaningService, SijaTech.Engine.Services.CleaningService>();
            services.AddSingleton<SijaTech.Core.Interfaces.IRegistryService, SijaTech.Engine.Services.RegistryService>();
            services.AddSingleton<SijaTech.Core.Interfaces.IRuleEngine, SijaTech.Security.Engine.RuleEngine>();
            services.AddSingleton<SijaTech.Core.Interfaces.ISignatureDatabase, SijaTech.Security.Database.SignatureDatabase>();
            services.AddSingleton<SijaTech.Core.Interfaces.ISecurityService, SijaTech.Security.Services.SecurityService>();
        }
    }
}
