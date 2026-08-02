using StockAndFlow.Services;

namespace StockAndFlow.Mobile.Pages;

// Barcode scanning via MediaPicker: the user taps the shutter button, the OS
// camera UI opens, they frame the barcode and take the shot, and we decode it
// with ZXing.Net + SkiaSharp (both already in the project).  A live viewfinder
// can be added later without touching any of the callers — same BarcodeDetected
// event contract.
public class BarcodeScanPage : ContentPage
{
    public event EventHandler<string>? BarcodeDetected;

    // Decoding lives in Core's BarcodeService so it is shared with label generation and
    // covered by tests (generated barcodes are decoded back through this exact path).
    private static readonly BarcodeService Decoder = new();

    private readonly ActivityIndicator _spinner;
    private readonly Label _statusLabel;
    private readonly Button _scanBtn;

    public BarcodeScanPage()
    {
        Title = "Scan Barcode";
        NavigationPage.SetHasNavigationBar(this, true);

        _spinner = new ActivityIndicator { IsRunning = false, IsVisible = false, Color = Colors.DodgerBlue };

        _statusLabel = new Label
        {
            Text = "Point your camera at a barcode and take a photo.",
            FontSize = 14,
            TextColor = Color.FromArgb("#888"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(16, 0),
        };

        _scanBtn = new Button
        {
            Text = "📷  Open Camera",
            FontSize = 15,
            HeightRequest = 52,
        };
        _scanBtn.Clicked += OnScanClicked;

        var manualEntry = new Entry
        {
            Placeholder = "Or type / paste a barcode here",
            Keyboard = Keyboard.Plain,
        };

        var useManualBtn = new Button
        {
            Text = "Use this code",
            BackgroundColor = Color.FromArgb("#555"),
        };
        useManualBtn.Clicked += async (_, _) =>
        {
            var val = manualEntry.Text?.Trim();
            if (!string.IsNullOrEmpty(val))
            {
                BarcodeDetected?.Invoke(this, val);
                await Navigation.PopAsync();
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 32),
                Spacing = 16,
                Children =
                {
                    new Label
                    {
                        Text = "Scan a Barcode",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    _statusLabel,
                    _scanBtn,
                    _spinner,
                    new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#DDD"), Margin = new Thickness(0, 8) },
                    new Label
                    {
                        Text = "Manual entry",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#888"),
                        FontAttributes = FontAttributes.Bold,
                    },
                    manualEntry,
                    useManualBtn,
                }
            }
        };
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        SetScanning(true);
        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Photo the barcode"
            });

            if (photo == null)
            {
                SetScanning(false);
                return;
            }

            var barcode = await Task.Run(async () =>
            {
                await using var stream = await photo.OpenReadAsync();
                return Decoder.Decode(stream);
            });

            if (!string.IsNullOrEmpty(barcode))
            {
                BarcodeDetected?.Invoke(this, barcode!);
                await Navigation.PopAsync();
            }
            else
            {
                SetScanning(false);
                SetStatus("No barcode found — try again with the barcode filling the frame.");
            }
        }
        catch (FeatureNotSupportedException)
        {
            SetScanning(false);
            SetStatus("Camera not available on this device.");
        }
        catch (PermissionException)
        {
            SetScanning(false);
            SetStatus("Camera permission was denied. Enable it in device settings.");
        }
        catch (Exception ex)
        {
            SetScanning(false);
            SetStatus($"Scan failed: {ex.Message}");
        }
    }

    private void SetScanning(bool active)
    {
        _scanBtn.IsEnabled = !active;
        _spinner.IsVisible = active;
        _spinner.IsRunning = active;
        if (active) _statusLabel.Text = "Decoding…";
    }

    private void SetStatus(string msg) => _statusLabel.Text = msg;
}
