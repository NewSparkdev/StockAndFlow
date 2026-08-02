using StockAndFlow.Models;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

/// <summary>
/// Print barcode labels for the whole product line at once, with the how-it-works explanation
/// alongside. Generating labels one image at a time made labelling a real product line
/// impractical, and nothing in the app explained the physical workflow.
/// </summary>
public class BarcodeLabelsPage : ContentPage
{
    private readonly InventoryService _inventory;
    private readonly BarcodeService _barcodes = new();

    private readonly VerticalStackLayout _itemList;
    private readonly Label _summary;
    private readonly Stepper _copies;
    private readonly Label _copiesLabel;
    private readonly Button _printBtn;
    private readonly ActivityIndicator _spinner;

    private readonly List<(InventoryItem Item, CheckBox Check)> _rows = new();

    public BarcodeLabelsPage(InventoryService inventory)
    {
        _inventory = inventory;
        Title = "Print Barcode Labels";

        _summary = new Label { FontSize = 13, TextColor = Color.FromArgb("#666") };
        _itemList = new VerticalStackLayout { Spacing = 2 };

        _copiesLabel = new Label { Text = "Copies of each: 1", VerticalOptions = LayoutOptions.Center };
        _copies = new Stepper { Minimum = 1, Maximum = 50, Increment = 1, Value = 1 };
        _copies.ValueChanged += (_, e) => _copiesLabel.Text = $"Copies of each: {(int)e.NewValue}";

        _spinner = new ActivityIndicator { IsRunning = false, IsVisible = false };

        _printBtn = new Button { Text = "🖨  Create label sheet" };
        _printBtn.Clicked += OnPrintClicked;

        var selectAll = new Button { Text = "Select all", BackgroundColor = Color.FromArgb("#666") };
        selectAll.Clicked += (_, _) => SetAll(true);
        var selectNone = new Button { Text = "Clear", BackgroundColor = Color.FromArgb("#666") };
        selectNone.Clicked += (_, _) => SetAll(false);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Pick the products you want labels for, then create a sheet to print.",
                        FontSize = 15
                    },
                    _summary,
                    new Grid
                    {
                        ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
                        ColumnSpacing = 8,
                        Children = { selectAll, selectNone.Column(1) }
                    },
                    new Frame
                    {
                        Padding = 10,
                        CornerRadius = 8,
                        BorderColor = Color.FromArgb("#DDD"),
                        Content = _itemList
                    },
                    new HorizontalStackLayout { Spacing = 12, Children = { _copiesLabel, _copies } },
                    _printBtn,
                    _spinner,
                    new BoxView { HeightRequest = 1, Color = Color.FromArgb("#DDD"), Margin = new Thickness(0, 8) },
                    new Label
                    {
                        Text = AddEditInventoryViewModel.BarcodeHowToText,
                        FontSize = 13,
                        LineHeight = 1.35
                    }
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_rows.Count > 0) return;

        var items = (await _inventory.GetAllItemsAsync()).OrderBy(i => i.Name).ToList();
        var withCode = items.Where(i => !string.IsNullOrWhiteSpace(i.Sku)).ToList();
        var without = items.Count - withCode.Count;

        _summary.Text = without == 0
            ? $"{withCode.Count} product(s) have a code."
            : $"{withCode.Count} product(s) have a code. {without} don't — open an item and tap " +
              "\"Create barcode label\" to give it one.";

        foreach (var item in withCode)
        {
            var check = new CheckBox { IsChecked = true, VerticalOptions = LayoutOptions.Center };
            _rows.Add((item, check));

            _itemList.Add(new Grid
            {
                ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
                Children =
                {
                    check,
                    new VerticalStackLayout
                    {
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Label { Text = item.Name, FontSize = 14 },
                            new Label { Text = item.Sku, FontSize = 11, TextColor = Color.FromArgb("#888") }
                        }
                    }.Column(1)
                }
            });
        }

        if (withCode.Count == 0)
            _printBtn.IsEnabled = false;
    }

    private void SetAll(bool value)
    {
        foreach (var (_, check) in _rows)
            check.IsChecked = value;
    }

    private async void OnPrintClicked(object? sender, EventArgs e)
    {
        var chosen = _rows.Where(r => r.Check.IsChecked).Select(r => r.Item).ToList();
        if (chosen.Count == 0)
        {
            await DisplayAlert("Nothing selected", "Tick at least one product to print labels for.", "OK");
            return;
        }

        try
        {
            _printBtn.IsEnabled = false;
            _spinner.IsVisible = _spinner.IsRunning = true;

            var copies = (int)_copies.Value;
            var requests = chosen.Select(i => new BarcodeService.LabelRequest(i.Sku!.Trim(), i.Name)).ToList();
            var pdf = await Task.Run(() => _barcodes.CreateLabelSheetPdf(requests, copiesEach: copies));

            var file = Path.Combine(FileSystem.CacheDirectory,
                $"barcode_labels_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            await File.WriteAllBytesAsync(file, pdf);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Barcode labels",
                File = new ShareFile(file)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Couldn't create labels", ex.Message, "OK");
        }
        finally
        {
            _printBtn.IsEnabled = true;
            _spinner.IsVisible = _spinner.IsRunning = false;
        }
    }
}

internal static class LayoutExtensions
{
    /// <summary>Fluent Grid.Column assignment, so the layout above reads top-to-bottom.</summary>
    public static T Column<T>(this T view, int column) where T : View
    {
        Grid.SetColumn(view, column);
        return view;
    }
}
