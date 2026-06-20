using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class RecordSaleDialog : Window
    {
        private RecordSaleViewModel? _viewModel;

        public RecordSaleDialog()
        {
            InitializeComponent();
        }

        public RecordSaleDialog(RecordSaleViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            // Subscribe to close request
            viewModel.CloseRequested += (sender, result) =>
            {
                DialogResult = result;
                Close();
            };

            // Subscribe to sale completed event to offer invoice generation
            viewModel.SaleCompleted += async (sender, transaction) =>
            {
                await HandleSaleCompletedAsync(transaction);
            };
        }

        private async System.Threading.Tasks.Task HandleSaleCompletedAsync(SaleTransaction transaction)
        {
            // Ask user if they want to generate an invoice
            var result = MessageBox.Show(
                "Sale recorded successfully!\n\nWould you like to generate a PDF invoice for this sale?",
                "Generate Invoice?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (result == MessageBoxResult.Yes)
            {
                // Check if business settings are configured
                if (_viewModel != null)
                {
                    var isConfigured = await _viewModel.IsBusinessConfiguredAsync();
                    var settingsService = ((App)Application.Current).GetBusinessSettingsService();
                    var shouldEditSettings = false;

                    if (!isConfigured)
                    {
                        var configResult = MessageBox.Show(
                            "You need to configure your business information before generating invoices.\n\nWould you like to do that now?",
                            "Business Information Required",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information,
                            MessageBoxResult.Yes);

                        if (configResult == MessageBoxResult.Yes)
                        {
                            shouldEditSettings = true;
                        }
                        else
                        {
                            // User doesn't want to configure now, just close
                            DialogResult = true;
                            Close();
                            return;
                        }
                    }
                    else
                    {
                        // Business settings exist, ask if they want to use current or edit
                        var settings = await settingsService.GetSettingsAsync();
                        var businessInfo = $"Business Name: {settings.BusinessName ?? "Not set"}\n" +
                                         $"Address: {settings.Address ?? "Not set"}\n" +
                                         $"City: {settings.City ?? "Not set"}, {settings.State ?? "Not set"} {settings.ZipCode ?? "Not set"}\n" +
                                         $"Phone: {settings.Phone ?? "Not set"}\n" +
                                         $"Email: {settings.Email ?? "Not set"}";

                        var infoChoice = MessageBox.Show(
                            $"Current Business Information:\n\n{businessInfo}\n\n" +
                            "Would you like to use this information?\n\n" +
                            "• Click 'Yes' to use current information\n" +
                            "• Click 'No' to edit the information\n" +
                            "• Click 'Cancel' to skip invoice generation",
                            "Business Information",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question,
                            MessageBoxResult.Yes);

                        if (infoChoice == MessageBoxResult.Cancel)
                        {
                            // User cancelled, just close
                            DialogResult = true;
                            Close();
                            return;
                        }
                        else if (infoChoice == MessageBoxResult.No)
                        {
                            // User wants to edit
                            shouldEditSettings = true;
                        }
                        // If Yes, continue with current settings (shouldEditSettings stays false)
                    }

                    // Show business settings dialog if needed
                    if (shouldEditSettings)
                    {
                        var settingsViewModel = App.Services.GetRequiredService<BusinessSettingsViewModel>();
                        var settingsDialog = new BusinessSettingsDialog(settingsViewModel);
                        var settingsResult = settingsDialog.ShowDialog();

                        if (settingsResult != true)
                        {
                            // User cancelled business settings, close the record sale dialog
                            DialogResult = true;
                            Close();
                            return;
                        }
                    }

                    // Generate the invoice
                    var invoicePath = await _viewModel.GenerateInvoiceAsync(transaction);

                    if (invoicePath != null)
                    {
                        var openResult = MessageBox.Show(
                            $"Invoice generated successfully!\n\nLocation: {invoicePath}\n\nWould you like to open it now?",
                            "Invoice Generated",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information,
                            MessageBoxResult.Yes);

                        if (openResult == MessageBoxResult.Yes)
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = invoicePath,
                                    UseShellExecute = true
                                });
                            }
                            catch (System.Exception ex)
                            {
                                MessageBox.Show(
                                    $"Could not open the invoice file:\n\n{ex.Message}",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to generate invoice. Please check the application logs for details.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }

            // Close the dialog
            DialogResult = true;
            Close();
        }
    }
}
