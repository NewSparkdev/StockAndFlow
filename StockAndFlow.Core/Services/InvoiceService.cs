using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    /// <summary>
    /// Generates PDF invoices using SkiaSharp's <see cref="SKDocument"/> PDF backend.
    ///
    /// We render with SkiaSharp (not QuestPDF) on purpose: QuestPDF ships its own native Skia
    /// build (libQuestPdfSkia) that depends on libstdc++ and fails to load on Android/iOS, so it
    /// is desktop/server-only. SkiaSharp's libSkiaSharp is already present and proven on every head
    /// in this solution (it renders the LiveCharts charts), so the same invoice code path works on
    /// Windows, Android, and iOS.
    /// </summary>
    public class InvoiceService
    {
        private readonly BusinessSettingsService _settingsService;
        private readonly Platform.IPathProvider? _pathProvider;

        // US Letter at 72 dpi (PDF points).
        private const float PageWidth = 612f;
        private const float PageHeight = 792f;
        private const float Margin = 40f;
        private const float ContentLeft = Margin;
        private const float ContentRight = PageWidth - Margin;
        private const float ContentWidth = ContentRight - ContentLeft;
        private const float RowHeight = 26f;

        // Palette mirrors the previous QuestPDF design.
        private static readonly SKColor Brand = new(0x15, 0x65, 0xC0);      // Blue Darken-2
        private static readonly SKColor Ink = new(0x21, 0x21, 0x21);
        private static readonly SKColor GreyDark = new(0x75, 0x75, 0x75);
        private static readonly SKColor GreyMedium = new(0x9E, 0x9E, 0x9E);
        private static readonly SKColor GreyLight = new(0xEE, 0xEE, 0xEE);
        private static readonly SKColor White = SKColors.White;

        public InvoiceService(BusinessSettingsService settingsService, Platform.IPathProvider? pathProvider = null)
        {
            _settingsService = settingsService;
            _pathProvider = pathProvider;
        }

        /// <summary>
        /// Generates a PDF invoice for a sale transaction.
        /// </summary>
        /// <param name="transaction">The sale transaction to generate an invoice for.</param>
        /// <param name="outputPath">
        /// Where the PDF is written. If null, a file is created in the user's Downloads folder
        /// (desktop). Mobile callers pass a sandbox-writable path (e.g. the app cache directory).
        /// </param>
        /// <returns>The path to the generated PDF file.</returns>
        public async Task<string> GenerateInvoiceAsync(SaleTransaction transaction, string? outputPath = null)
        {
            var settings = await _settingsService.GetSettingsAsync();

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                var downloadsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");

                if (!Directory.Exists(downloadsFolder))
                {
                    Directory.CreateDirectory(downloadsFolder);
                }

                var invoiceNumber = transaction.TransactionId.ToString().Substring(0, 8).ToUpper();
                var fileName = $"Invoice_{invoiceNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                outputPath = Path.Combine(downloadsFolder, fileName);
            }

            // Skia drawing is synchronous; keep it off the UI thread.
            await Task.Run(() => Render(transaction, settings, outputPath!));

            return outputPath!;
        }

        private void Render(SaleTransaction transaction, BusinessSettings settings, string outputPath)
        {
            using var regular = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                                ?? SKTypeface.Default;
            using var bold = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                             ?? regular;
            using var italic = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic)
                               ?? regular;

            using var stream = new SKFileWStream(outputPath);
            using var document = SKDocument.CreatePdf(stream);

            var ctx = new RenderContext(document, regular, bold, italic);
            ctx.BeginPage();

            DrawHeader(ctx, settings, transaction);
            DrawItemsTable(ctx, transaction);
            DrawTotals(ctx, transaction);
            DrawNotes(ctx, transaction);
            DrawThankYou(ctx);
            DrawFooter(ctx, settings);

            ctx.EndPage();
            document.Close();
        }

        /// <summary>
        /// Finds a readable logo file for the invoice. Stored paths can be absolute paths from a
        /// previous install (mobile app sandboxes move), so fall back to re-basing the file name
        /// onto the current Images directory — same strategy as the app's ImagePathConverter.
        /// </summary>
        private string? ResolveLogoPath(BusinessSettings settings)
        {
            var stored = settings.LogoPath;
            if (string.IsNullOrWhiteSpace(stored))
                return null;
            if (File.Exists(stored))
                return stored;

            var fileName = Path.GetFileName(stored);
            if (_pathProvider != null && !string.IsNullOrEmpty(fileName))
            {
                var rebased = Path.Combine(_pathProvider.ImagesDirectory, fileName);
                if (File.Exists(rebased))
                    return rebased;
            }
            return null;
        }

        private void DrawHeader(RenderContext ctx, BusinessSettings settings, SaleTransaction transaction)
        {
            float topY = Margin + 16f;

            // Left column: business identity.
            float leftY = topY;

            // Business logo above the name, scaled into a bounded box. A logo that fails to
            // decode is simply skipped — an invoice must never fail because of a bad image.
            if (settings.ShowLogoOnInvoice)
            {
                var logoPath = ResolveLogoPath(settings);
                if (logoPath != null)
                {
                    try
                    {
                        using var logo = SKBitmap.Decode(logoPath);
                        if (logo != null && logo.Width > 0 && logo.Height > 0)
                        {
                            const float maxLogoWidth = 160f;
                            const float maxLogoHeight = 56f;
                            var scale = Math.Min(Math.Min(maxLogoWidth / logo.Width, maxLogoHeight / logo.Height), 1f);
                            var w = logo.Width * scale;
                            var h = logo.Height * scale;
                            var dest = new SKRect(ContentLeft, Margin, ContentLeft + w, Margin + h);
                            ctx.Canvas.DrawBitmap(logo, dest);
                            leftY = Margin + h + 20f;
                        }
                    }
                    catch
                    {
                        // Undecodable image — render the text-only header.
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.BusinessName))
            {
                ctx.Text(settings.BusinessName!, ContentLeft, leftY, 18, ctx.Bold, Brand);
                leftY += 22f;
            }
            if (!string.IsNullOrWhiteSpace(settings.Address))
            {
                ctx.Text(settings.Address!, ContentLeft, leftY, 9, ctx.Regular, Ink);
                leftY += 12f;
            }
            if (!string.IsNullOrWhiteSpace(settings.City))
            {
                ctx.Text($"{settings.City}, {settings.State} {settings.ZipCode}", ContentLeft, leftY, 9, ctx.Regular, Ink);
                leftY += 12f;
            }
            if (settings.ShowPhoneOnInvoice && !string.IsNullOrWhiteSpace(settings.Phone))
            {
                leftY += 3f;
                ctx.Text($"Phone: {settings.Phone}", ContentLeft, leftY, 9, ctx.Regular, Ink);
                leftY += 12f;
            }
            if (settings.ShowEmailOnInvoice && !string.IsNullOrWhiteSpace(settings.Email))
            {
                ctx.Text($"Email: {settings.Email}", ContentLeft, leftY, 9, ctx.Regular, Ink);
                leftY += 12f;
            }
            if (settings.ShowWebsiteOnInvoice && !string.IsNullOrWhiteSpace(settings.Website))
            {
                ctx.Text($"Web: {settings.Website}", ContentLeft, leftY, 9, ctx.Regular, Ink);
                leftY += 12f;
            }

            // Right column: invoice title + meta, right-aligned.
            float rightY = topY;
            ctx.Text("INVOICE", ContentRight, rightY, 28, ctx.Bold, Brand, SKTextAlign.Right);
            rightY += 26f;
            var invoiceNumber = transaction.TransactionId.ToString().Substring(0, 8).ToUpper();
            ctx.Text($"Invoice #: {invoiceNumber}", ContentRight, rightY, 10, ctx.Regular, Ink, SKTextAlign.Right);
            rightY += 14f;
            ctx.Text($"Date: {transaction.SaleDate:MMMM dd, yyyy}", ContentRight, rightY, 10, ctx.Regular, Ink, SKTextAlign.Right);
            rightY += 14f;
            ctx.Text($"Time: {transaction.SaleDate:hh:mm tt}", ContentRight, rightY, 10, ctx.Regular, Ink, SKTextAlign.Right);
            rightY += 14f;

            float y = Math.Max(leftY, rightY) + 12f;

            // Brand separator.
            ctx.Line(ContentLeft, y, ContentRight, y, 2, Brand);
            y += 18f;

            // Bill To.
            ctx.Text("Bill To:", ContentLeft, y, 12, ctx.Bold, Ink);
            y += 16f;
            if (!string.IsNullOrWhiteSpace(transaction.CustomerName))
            {
                ctx.Text(transaction.CustomerName!, ContentLeft, y, 11, ctx.Regular, Ink);
            }
            else
            {
                ctx.Text("Walk-in Customer", ContentLeft, y, 11, ctx.Italic, Ink);
            }
            y += 14f;
            if (!string.IsNullOrWhiteSpace(transaction.CustomerEmail))
            {
                ctx.Text(transaction.CustomerEmail!, ContentLeft, y, 10, ctx.Regular, Ink);
                y += 14f;
            }

            ctx.Y = y + 16f;
        }

        // Column geometry (relative widths 3 : 1 : 1.5 : 1.5 over the content width).
        private const float ColItemX = ContentLeft;                       // left edge, item name
        private static readonly float ColQtyEnd = ContentLeft + ContentWidth * (3f + 1f) / 7f;
        private static readonly float ColUnitEnd = ContentLeft + ContentWidth * (3f + 1f + 1.5f) / 7f;
        private static readonly float ColTotalEnd = ContentRight;
        private static readonly float ColQtyCenter = (ContentLeft + ContentWidth * 3f / 7f + ColQtyEnd) / 2f;
        private const float CellPad = 8f;

        private void DrawItemsTable(RenderContext ctx, SaleTransaction transaction)
        {
            DrawTableHeader(ctx);

            foreach (var item in transaction.Items.OrderBy(i => i.ItemName))
            {
                if (ctx.Y + RowHeight > PageHeight - Margin - 40f)
                {
                    ctx.EndPage();
                    ctx.BeginPage();
                    ctx.Y = Margin + 16f;
                    DrawTableHeader(ctx);
                }

                float baseline = ctx.Y + RowHeight - CellPad;
                ctx.Text(item.ItemName ?? string.Empty, ColItemX + CellPad, baseline, 11, ctx.Regular, Ink);
                ctx.Text(item.Quantity.ToString(), ColQtyCenter, baseline, 11, ctx.Regular, Ink, SKTextAlign.Center);
                ctx.Text($"${item.SalePricePerUnit:N2}", ColUnitEnd - CellPad, baseline, 11, ctx.Regular, Ink, SKTextAlign.Right);
                ctx.Text($"${item.Revenue:N2}", ColTotalEnd - CellPad, baseline, 11, ctx.Regular, Ink, SKTextAlign.Right);

                ctx.Y += RowHeight;
                ctx.Line(ContentLeft, ctx.Y, ContentRight, ctx.Y, 1, GreyLight);
            }
        }

        private void DrawTableHeader(RenderContext ctx)
        {
            ctx.Rect(ContentLeft, ctx.Y, ContentWidth, RowHeight, Brand);
            float baseline = ctx.Y + RowHeight - CellPad;
            ctx.Text("Item", ColItemX + CellPad, baseline, 11, ctx.Bold, White);
            ctx.Text("Qty", ColQtyCenter, baseline, 11, ctx.Bold, White, SKTextAlign.Center);
            ctx.Text("Unit Price", ColUnitEnd - CellPad, baseline, 11, ctx.Bold, White, SKTextAlign.Right);
            ctx.Text("Total", ColTotalEnd - CellPad, baseline, 11, ctx.Bold, White, SKTextAlign.Right);
            ctx.Y += RowHeight;
        }

        private void DrawTotals(RenderContext ctx, SaleTransaction transaction)
        {
            var subtotal = transaction.Items.Sum(i => i.Subtotal);
            var tax = transaction.Items.Sum(i => i.TaxAmount);
            var total = transaction.Revenue; // subtotal + tax

            const float labelRight = ContentRight - 120f;
            float y = ctx.Y + 24f;

            ctx.Text("Subtotal:", labelRight, y, 12, ctx.Regular, Ink, SKTextAlign.Right);
            ctx.Text($"${subtotal:N2}", ContentRight, y, 12, ctx.Regular, Ink, SKTextAlign.Right);
            y += 18f;

            if (tax > 0)
            {
                ctx.Text("Tax:", labelRight, y, 12, ctx.Regular, Ink, SKTextAlign.Right);
                ctx.Text($"${tax:N2}", ContentRight, y, 12, ctx.Regular, Ink, SKTextAlign.Right);
                y += 18f;
            }

            ctx.Text("Total:", labelRight, y, 14, ctx.Bold, Ink, SKTextAlign.Right);
            ctx.Text($"${total:N2}", ContentRight, y, 14, ctx.Bold, Brand, SKTextAlign.Right);

            ctx.Y = y + 8f;
        }

        private void DrawNotes(RenderContext ctx, SaleTransaction transaction)
        {
            if (string.IsNullOrWhiteSpace(transaction.Notes))
                return;

            float y = ctx.Y + 28f;
            ctx.Text("Notes:", ContentLeft, y, 11, ctx.Bold, Ink);
            y += 14f;
            foreach (var line in WrapText(ctx, transaction.Notes!, ctx.Regular, 10, ContentWidth))
            {
                ctx.Text(line, ContentLeft, y, 10, ctx.Regular, Ink);
                y += 13f;
            }
            ctx.Y = y;
        }

        private void DrawThankYou(RenderContext ctx)
        {
            float y = ctx.Y + 30f;
            ctx.Text("Thank you for your business!", PageWidth / 2f, y, 12, ctx.Italic, GreyDark, SKTextAlign.Center);
            ctx.Y = y;
        }

        private void DrawFooter(RenderContext ctx, BusinessSettings settings)
        {
            float y = PageHeight - Margin - 28f;
            ctx.Line(ContentLeft, y, ContentRight, y, 1, GreyLight);
            y += 12f;
            ctx.Text($"Generated on: {DateTime.Now:MMMM dd, yyyy 'at' hh:mm tt}", PageWidth / 2f, y, 8, ctx.Regular, GreyMedium, SKTextAlign.Center);
            y += 11f;
            if (settings.ShowTaxIdOnInvoice && !string.IsNullOrWhiteSpace(settings.TaxId))
            {
                ctx.Text($"Tax ID: {settings.TaxId}", PageWidth / 2f, y, 8, ctx.Regular, GreyMedium, SKTextAlign.Center);
            }
        }

        private static IEnumerable<string> WrapText(RenderContext ctx, string text, SKTypeface typeface, float size, float maxWidth)
        {
            using var paint = new SKPaint { Typeface = typeface, TextSize = size, IsAntialias = true };
            foreach (var rawLine in text.Replace("\r", string.Empty).Split('\n'))
            {
                var words = rawLine.Split(' ');
                var current = string.Empty;
                foreach (var word in words)
                {
                    var candidate = current.Length == 0 ? word : current + " " + word;
                    if (paint.MeasureText(candidate) > maxWidth && current.Length > 0)
                    {
                        yield return current;
                        current = word;
                    }
                    else
                    {
                        current = candidate;
                    }
                }
                yield return current;
            }
        }

        /// <summary>Tracks the active PDF page/canvas and the running vertical cursor.</summary>
        private sealed class RenderContext
        {
            private readonly SKDocument _document;
            public SKCanvas Canvas { get; private set; } = null!;
            public SKTypeface Regular { get; }
            public SKTypeface Bold { get; }
            public SKTypeface Italic { get; }
            public float Y { get; set; }

            public RenderContext(SKDocument document, SKTypeface regular, SKTypeface bold, SKTypeface italic)
            {
                _document = document;
                Regular = regular;
                Bold = bold;
                Italic = italic;
            }

            public void BeginPage()
            {
                Canvas = _document.BeginPage(PageWidth, PageHeight);
                Y = Margin;
            }

            public void EndPage() => _document.EndPage();

            public void Text(string text, float x, float baseline, float size, SKTypeface typeface, SKColor color,
                SKTextAlign align = SKTextAlign.Left)
            {
                using var paint = new SKPaint
                {
                    Typeface = typeface,
                    TextSize = size,
                    Color = color,
                    IsAntialias = true,
                    TextAlign = align
                };
                Canvas.DrawText(text, x, baseline, paint);
            }

            public void Line(float x1, float y1, float x2, float y2, float thickness, SKColor color)
            {
                using var paint = new SKPaint
                {
                    Color = color,
                    StrokeWidth = thickness,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                };
                Canvas.DrawLine(x1, y1, x2, y2, paint);
            }

            public void Rect(float x, float y, float width, float height, SKColor color)
            {
                using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
                Canvas.DrawRect(x, y, width, height, paint);
            }
        }
    }
}
