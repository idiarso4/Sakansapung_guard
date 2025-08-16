using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SijaTech.Core.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SijaTech.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILogger<MainViewModel> _logger;
        // private readonly ICleaningService _cleaningService;
        // private readonly ISecurityService _securityService;

        [ObservableProperty]
        private string _title = "Sija Tech System Cleaner";

        [ObservableProperty]
        private string _statusMessage = "Siap untuk membersihkan sistem Anda";

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private bool _isCleaning = false;

        [ObservableProperty]
        private int _progressPercentage = 0;

        [ObservableProperty]
        private string _currentOperation = string.Empty;

        [ObservableProperty]
        private object? _currentView;

        // Navigation ViewModels
        [ObservableProperty]
        private CleanerViewModel? _cleanerViewModel;

        [ObservableProperty]
        private SecurityViewModel? _securityViewModel;

        [ObservableProperty]
        private RegistryViewModel? _registryViewModel;

        [ObservableProperty]
        private SettingsViewModel? _settingsViewModel;

        public MainViewModel(ILogger<MainViewModel> logger)
        {
            _logger = logger;
            
            // Initialize ViewModels
            CleanerViewModel = new CleanerViewModel();
            SecurityViewModel = new SecurityViewModel();
            RegistryViewModel = new RegistryViewModel();
            SettingsViewModel = new SettingsViewModel();

            // Set default view
            CurrentView = CleanerViewModel;

            _logger.LogInformation("MainViewModel initialized");
        }

        [RelayCommand]
        private void NavigateToHome()
        {
            CurrentView = CleanerViewModel;
            _logger.LogDebug("Navigated to Home/Cleaner view");
        }

        [RelayCommand]
        private void NavigateToSecurity()
        {
            CurrentView = SecurityViewModel;
            _logger.LogDebug("Navigated to Security view");
        }

        [RelayCommand]
        private void NavigateToRegistry()
        {
            CurrentView = RegistryViewModel;
            _logger.LogDebug("Navigated to Registry view");
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            CurrentView = SettingsViewModel;
            _logger.LogDebug("Navigated to Settings view");
        }

        [RelayCommand]
        private async Task QuickScanAsync()
        {
            try
            {
                IsScanning = true;
                StatusMessage = "Melakukan quick scan...";
                ProgressPercentage = 0;

                // Simulate scan progress
                for (int i = 0; i <= 100; i += 10)
                {
                    ProgressPercentage = i;
                    CurrentOperation = $"Scanning... {i}%";
                    await Task.Delay(200);
                }

                StatusMessage = "Quick scan selesai";
                _logger.LogInformation("Quick scan completed");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during scan: {ex.Message}";
                _logger.LogError(ex, "Error during quick scan");
            }
            finally
            {
                IsScanning = false;
                ProgressPercentage = 0;
                CurrentOperation = string.Empty;
            }
        }

        [RelayCommand]
        private async Task QuickCleanAsync()
        {
            try
            {
                IsCleaning = true;
                StatusMessage = "Melakukan quick clean...";
                ProgressPercentage = 0;

                // Simulate cleaning progress
                for (int i = 0; i <= 100; i += 5)
                {
                    ProgressPercentage = i;
                    CurrentOperation = $"Cleaning... {i}%";
                    await Task.Delay(100);
                }

                StatusMessage = "Quick clean selesai";
                _logger.LogInformation("Quick clean completed");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during cleaning: {ex.Message}";
                _logger.LogError(ex, "Error during quick clean");
            }
            finally
            {
                IsCleaning = false;
                ProgressPercentage = 0;
                CurrentOperation = string.Empty;
            }
        }

        [RelayCommand]
        private void ShowAbout()
        {
            StatusMessage = "Sija Tech System Cleaner v1.0 - Advanced System Cleaner with Real-time Security";
            _logger.LogDebug("About information displayed");
        }

        [RelayCommand]
        private void Exit()
        {
            _logger.LogInformation("Application exit requested");
            System.Windows.Application.Current.Shutdown();
        }
    }
}
