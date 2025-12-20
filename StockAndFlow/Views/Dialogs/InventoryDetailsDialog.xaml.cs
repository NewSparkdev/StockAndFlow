using System.Windows;

namespace StockAndFlow.Views.Dialogs
{
    public partial class InventoryDetailsDialog : Window
    {
        public InventoryDetailsDialog()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
