using System.Windows;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class AddEditInventoryDialog : Window
    {
        public AddEditInventoryDialog()
        {
            InitializeComponent();
        }

        public AddEditInventoryDialog(AddEditInventoryViewModel viewModel) : this()
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
