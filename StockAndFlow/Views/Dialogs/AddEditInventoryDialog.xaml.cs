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

            viewModel.CloseRequested += (sender, result) =>
            {
                DialogResult = result;
                Close();
            };

            Loaded += async (_, _) => await viewModel.InitializeAsync();
        }
    }
}
