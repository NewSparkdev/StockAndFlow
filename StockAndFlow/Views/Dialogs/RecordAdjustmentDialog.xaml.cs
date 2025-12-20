using System.Windows;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class RecordAdjustmentDialog : Window
    {
        public RecordAdjustmentDialog()
        {
            InitializeComponent();
        }

        public RecordAdjustmentDialog(RecordAdjustmentViewModel viewModel) : this()
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
