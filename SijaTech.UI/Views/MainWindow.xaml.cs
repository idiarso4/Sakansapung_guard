using Microsoft.Extensions.DependencyInjection;
using SijaTech.UI.ViewModels;
using System;
using System.Windows;

namespace SijaTech.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Create a simple ViewModel for testing
                DataContext = new MainViewModel(Microsoft.Extensions.Logging.Abstractions.NullLogger<MainViewModel>.Instance);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing MainWindow: {ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        public MainWindow(MainViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
