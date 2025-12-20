using System.Windows;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class AddEditExpenseDialog : Window
    {
        public AddEditExpenseDialog()
        {
            InitializeComponent();
        }

        public AddEditExpenseDialog(AddEditExpenseViewModel viewModel) : this()
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
