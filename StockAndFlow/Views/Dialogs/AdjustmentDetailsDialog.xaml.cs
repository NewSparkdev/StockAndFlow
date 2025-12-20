using System.Windows;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class AdjustmentDetailsDialog : Window
    {
        public AdjustmentDetailsDialog()
        {
            InitializeComponent();
        }

        public AdjustmentDetailsDialog(AdjustmentDetailsViewModel viewModel) : this()
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
