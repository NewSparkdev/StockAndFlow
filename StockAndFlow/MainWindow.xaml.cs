using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StockAndFlow;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles mouse wheel scrolling in the Reports tab to prevent child controls
    /// (DataGrids, Charts) from capturing scroll events
    /// </summary>
    private void ReportsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // Calculate new scroll offset
            double newOffset = scrollViewer.VerticalOffset - (e.Delta / 3.0);

            // Clamp to valid range
            newOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, newOffset));

            // Apply scroll
            scrollViewer.ScrollToVerticalOffset(newOffset);

            // Mark event as handled to prevent child controls from capturing it
            e.Handled = true;
        }
    }
}