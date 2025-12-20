using System.Windows;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class SaleDetailsDialog : Window
    {
        public SaleDetailsDialog()
        {
            InitializeComponent();
        }

        public SaleDetailsDialog(SaleDetailsViewModel viewModel) : this()
        {
            DataContext = viewModel;

            // Subscribe to close request
            viewModel.CloseRequested += (sender, result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
