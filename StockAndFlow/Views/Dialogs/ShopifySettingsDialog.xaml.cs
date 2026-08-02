using System.ComponentModel;
using System.Windows;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class ShopifySettingsDialog : Window
    {
        private bool _syncingToken;

        public ShopifySettingsDialog()
        {
            InitializeComponent();
        }

        public ShopifySettingsDialog(ShopifySettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;

            // PasswordBox has no bindable Password property; sync it manually in both
            // directions (the VM loads the stored token asynchronously after construction).
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            viewModel.CloseRequested += (sender, result) =>
            {
                DialogResult = result;
                Close();
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ShopifySettingsViewModel.AccessToken) || _syncingToken)
                return;

            var vm = (ShopifySettingsViewModel)DataContext;
            if (TokenBox.Password != (vm.AccessToken ?? string.Empty))
            {
                _syncingToken = true;
                TokenBox.Password = vm.AccessToken ?? string.Empty;
                _syncingToken = false;
            }
        }

        private void OnTokenChanged(object sender, RoutedEventArgs e)
        {
            if (_syncingToken || DataContext is not ShopifySettingsViewModel vm)
                return;

            _syncingToken = true;
            vm.AccessToken = TokenBox.Password;
            _syncingToken = false;
        }
    }
}
