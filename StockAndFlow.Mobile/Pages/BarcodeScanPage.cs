namespace StockAndFlow.Mobile.Pages;

// Camera-based barcode scanning requires a net10.0-compatible ZXing package.
// Until one ships, this page lets the user type or paste the barcode value so
// all downstream plumbing (SKU lookup, SelectItemBySku) is fully functional.
public class BarcodeScanPage : ContentPage
{
    public event EventHandler<string>? BarcodeDetected;

    public BarcodeScanPage()
    {
        Title = "Enter Barcode";
        NavigationPage.SetHasNavigationBar(this, true);

        var entry = new Entry
        {
            Placeholder = "Type or paste barcode / SKU",
            Keyboard = Keyboard.Plain,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var useBtn = new Button { Text = "Use this code" };
        useBtn.Clicked += async (_, _) =>
        {
            var val = entry.Text?.Trim();
            if (!string.IsNullOrEmpty(val))
            {
                BarcodeDetected?.Invoke(this, val);
                await Navigation.PopAsync();
            }
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#888"),
        };
        cancelBtn.Clicked += async (_, _) => await Navigation.PopAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 32),
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "📷  Camera scanning coming soon",
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                    },
                    new Label
                    {
                        Text = "For now, type or paste the barcode or SKU value below.",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#888"),
                    },
                    entry,
                    useBtn,
                    cancelBtn,
                }
            }
        };
    }
}
