using System.Threading.Tasks;
using System.Windows;
using StockAndFlow.Platform;

namespace StockAndFlow.Wpf.Platform
{
    /// <summary>
    /// WPF implementation of <see cref="IDialogService"/> using MessageBox, marshalled to the UI thread.
    /// </summary>
    public sealed class WpfDialogService : IDialogService
    {
        public Task ShowAlertAsync(string title, string message, string buttonText = "OK")
        {
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No")
        {
            var result = Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No));
            return Task.FromResult(result == MessageBoxResult.Yes);
        }
    }
}
