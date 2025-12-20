using System.Windows;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Views.Dialogs
{
    public partial class ExpenseDetailsDialog : Window
    {
        public ExpenseDetailsDialog()
        {
            InitializeComponent();
        }

        public ExpenseDetailsDialog(ExpenseDetailsViewModel viewModel) : this()
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
