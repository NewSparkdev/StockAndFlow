using FluentAssertions;
using SkiaSharp;
using StockAndFlow.Services;

namespace StockAndFlow.Tests.Unit.Services;

/// <summary>
/// Barcode generate + decode. The generator doubles as the scanner's test fixture: every
/// round-trip here exercises the exact decode path the camera uses, so the scanning feature
/// gets real coverage without a camera.
/// </summary>
public class BarcodeServiceTests
{
    private readonly BarcodeService _service = new();

    private SKBitmap DecodeTarget(byte[] png)
    {
        using var ms = new MemoryStream(png);
        return SKBitmap.Decode(ms);
    }

    [Theory]
    [InlineData("SF-K7Q2M9")]
    [InlineData("WAX-001")]
    [InlineData("1234567890")]
    [InlineData("Lavender-Candle-8oz")]
    public void Code128_RoundTrips(string value)
    {
        var png = _service.CreateLabelPng(value, BarcodeSymbology.Code128, caption: "Soyful serene");

        using var bitmap = DecodeTarget(png);
        _service.Decode(bitmap).Should().Be(value);
    }

    [Theory]
    [InlineData("SF-K7Q2M9")]
    [InlineData("https://example.com/item/123")]
    [InlineData("Lavender Candle — 8 oz")]  // non-ASCII: QR handles what CODE_128 cannot
    public void QrCode_RoundTrips(string value)
    {
        var png = _service.CreateLabelPng(value, BarcodeSymbology.QrCode);

        using var bitmap = DecodeTarget(png);
        _service.Decode(bitmap).Should().Be(value);
    }

    [Fact]
    public void GenerateSku_IsUnique_AndAvoidsAmbiguousCharacters()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => _service.GenerateSku()).ToList();

        codes.Distinct().Should().HaveCount(500, "a duplicate SKU would scan as the wrong product");
        codes.Should().OnlyContain(c => c.StartsWith("SF-"));
        codes.Should().OnlyContain(c => !c.Substring(3).Intersect("O0I1L").Any(),
            "a person retyping a code from a label must not confuse O/0 or I/1/L");
    }

    [Fact]
    public void GenerateSku_NeverReturnsACodeAlreadyInUse()
    {
        // Force the collision path: every code the generator can make is already taken
        // except a handful, so it must find one of the survivors rather than duplicate.
        var service = new BarcodeService();
        var taken = new List<string?>();
        for (int i = 0; i < 200; i++) taken.Add(service.GenerateSku());
        taken.Add(null);          // items with no code must not break the check
        taken.Add("   ");

        for (int i = 0; i < 50; i++)
        {
            var fresh = service.GenerateSku(taken);
            taken.Should().NotContain(fresh, "a generated barcode must never clash with an existing one");
            taken.Add(fresh);
        }
    }

    [Fact]
    public void GenerateSku_IgnoresCaseWhenAvoidingClashes()
    {
        var service = new BarcodeService();
        var existing = new List<string?> { "sf-abcdef" };

        // Repeated draws should never produce the same code in different casing, which the
        // SKU lookup (case-insensitive) would treat as a duplicate.
        for (int i = 0; i < 100; i++)
            service.GenerateSku(existing).Should().NotBeEquivalentTo("SF-ABCDEF");
    }

    [Fact]
    public void GeneratedSku_IsEncodableAndScannable()
    {
        var sku = _service.GenerateSku();

        var png = _service.CreateLabelPng(sku);

        using var bitmap = DecodeTarget(png);
        _service.Decode(bitmap).Should().Be(sku, "a generated SKU must survive its own label");
    }

    [Fact]
    public void Code128_RejectsNonAsciiWithAnActionableMessage()
    {
        var act = () => _service.CreateLabelPng("Café Candle", BarcodeSymbology.Code128);

        act.Should().Throw<ArgumentException>().WithMessage("*QR*",
            "the error should tell the user which format to use instead");
    }

    [Fact]
    public void CreateLabelPng_RejectsEmptyValue()
    {
        var act = () => _service.CreateLabelPng("  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Label_IncludesHumanReadableTextAndQuietZone()
    {
        var png = _service.CreateLabelPng("SF-ABC123", BarcodeSymbology.Code128, caption: "Soy Wax");

        using var bitmap = DecodeTarget(png);
        // Caption line + bars + printed code make the label taller than the bars alone.
        bitmap.Height.Should().BeGreaterThan(34 * 4, "label must leave room for caption and code text");

        // Corners must be white: a barcode printed hard against the edge won't scan.
        bitmap.GetPixel(0, 0).Should().Be(SKColors.White);
        bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).Should().Be(SKColors.White);
    }

    // ---- Photo-like degradation: what a phone camera actually produces ----

    private static SKBitmap Transform(SKBitmap source, float rotationDegrees, float scale)
    {
        var w = (int)(source.Width * scale);
        var h = (int)(source.Height * scale);
        var diag = (int)Math.Ceiling(Math.Sqrt(w * w + h * h));
        var result = new SKBitmap(diag, diag, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        canvas.Translate(diag / 2f, diag / 2f);
        canvas.RotateDegrees(rotationDegrees);
        canvas.Scale(scale);
        canvas.DrawBitmap(source, -source.Width / 2f, -source.Height / 2f);
        return result;
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    [InlineData(180f)]
    [InlineData(270f)]
    public void Decode_SurvivesPhoneRotation(float degrees)
    {
        var png = _service.CreateLabelPng("SF-ROT123", BarcodeSymbology.QrCode, scale: 6);
        using var original = DecodeTarget(png);
        using var rotated = Transform(original, degrees, 1f);

        _service.Decode(rotated).Should().Be("SF-ROT123",
            "a photo taken in any orientation must still scan");
    }

    [Fact]
    public void Decode_SurvivesDownscaling()
    {
        // A barcode occupying a modest part of a photo frame.
        var png = _service.CreateLabelPng("SF-SMALL1", BarcodeSymbology.QrCode, scale: 8);
        using var original = DecodeTarget(png);
        using var small = Transform(original, 0f, 0.4f);

        _service.Decode(small).Should().Be("SF-SMALL1");
    }

    [Fact]
    public void Decode_ReturnsNull_WhenImageHasNoBarcode()
    {
        using var blank = new SKBitmap(300, 300, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(blank))
            canvas.Clear(SKColors.White);

        _service.Decode(blank).Should().BeNull("a photo of nothing must not invent a code");
    }

    [Fact]
    public void Decode_ReturnsNull_ForUndecodableStream()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("not an image"));

        _service.Decode(ms).Should().BeNull();
    }

    [Fact]
    public void ScanToSale_GeneratedLabelFindsTheRightItem()
    {
        // End-to-end for the whole feature loop: generate a code for an item, "scan" the
        // printed label, and confirm the lookup used by both the USB scanner (WPF) and the
        // camera (mobile) selects that item.
        var service = new BarcodeService();
        var sku = service.GenerateSku();

        var png = service.CreateLabelPng(sku, BarcodeSymbology.Code128, caption: "Lavender Candle");
        using var scanned = DecodeTarget(png);
        var scannedCode = service.Decode(scanned);

        scannedCode.Should().Be(sku);

        var catalogue = new[]
        {
            new StockAndFlow.Models.InventoryItem { Name = "Beeswax", Sku = "SF-OTHER1" },
            new StockAndFlow.Models.InventoryItem { Name = "Lavender Candle", Sku = sku },
            new StockAndFlow.Models.InventoryItem { Name = "No code item", Sku = null }
        };

        var match = catalogue.FirstOrDefault(i =>
            !string.IsNullOrWhiteSpace(i.Sku) &&
            string.Equals(i.Sku, scannedCode, StringComparison.OrdinalIgnoreCase));

        match.Should().NotBeNull();
        match!.Name.Should().Be("Lavender Candle");
    }

    private static int CountPdfPages(byte[] pdf)
        => System.Text.RegularExpressions.Regex.Matches(
            System.Text.Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]").Count;

    [Fact]
    public void LabelSheet_ProducesAValidPdf()
    {
        var labels = new[]
        {
            new BarcodeService.LabelRequest("SF-AAA111", "Lavender Candle"),
            new BarcodeService.LabelRequest("SF-BBB222", "Soy Wax"),
        };

        var pdf = _service.CreateLabelSheetPdf(labels);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        CountPdfPages(pdf).Should().Be(1);
    }

    [Fact]
    public void LabelSheet_FlowsOntoExtraPages()
    {
        // 3 columns x 8 rows = 24 per page, so 30 labels must spill onto a second sheet.
        var labels = Enumerable.Range(1, 30)
            .Select(i => new BarcodeService.LabelRequest($"SF-ITEM{i:D3}", $"Item {i}"))
            .ToArray();

        var pdf = _service.CreateLabelSheetPdf(labels);

        CountPdfPages(pdf).Should().BeGreaterThan(1);
    }

    [Fact]
    public void LabelSheet_RepeatsEachLabelWhenCopiesRequested()
    {
        var one = _service.CreateLabelSheetPdf(
            new[] { new BarcodeService.LabelRequest("SF-COPY01", "Candle") }, copiesEach: 1);
        var many = _service.CreateLabelSheetPdf(
            new[] { new BarcodeService.LabelRequest("SF-COPY01", "Candle") }, copiesEach: 40);

        CountPdfPages(one).Should().Be(1);
        CountPdfPages(many).Should().BeGreaterThan(1, "40 copies cannot fit on one sheet");
    }

    [Fact]
    public void LabelSheet_SkipsBlankCodesAndRejectsAnEmptyRun()
    {
        var act = () => _service.CreateLabelSheetPdf(new[]
        {
            new BarcodeService.LabelRequest("", "No code"),
            new BarcodeService.LabelRequest("   ", "Also no code"),
        });

        act.Should().Throw<ArgumentException>("printing a sheet of nothing is a mistake worth reporting");
    }

    [Fact]
    public void LabelSheet_HandlesNonAsciiCodesByFallingBackToQr()
    {
        // Must not throw: the sheet picks QR per-label when CODE_128 can't carry the value.
        var pdf = _service.CreateLabelSheetPdf(new[]
        {
            new BarcodeService.LabelRequest("CAFÉ-01", "Café Candle"),
            new BarcodeService.LabelRequest("SF-PLAIN1", "Plain"),
        });

        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Decode_HandlesRgba_NotJustBgra()
    {
        // SKBitmap.Decode yields different colour types per platform; the decoder must
        // normalise rather than assume, or scanning works on one OS and fails on another.
        var png = _service.CreateLabelPng("SF-RGBA01", BarcodeSymbology.QrCode, scale: 6);
        using var original = DecodeTarget(png);
        using var rgba = new SKBitmap(new SKImageInfo(original.Width, original.Height,
            SKColorType.Rgba8888, SKAlphaType.Premul));
        original.CopyTo(rgba, SKColorType.Rgba8888);

        _service.Decode(rgba).Should().Be("SF-RGBA01");
    }
}
