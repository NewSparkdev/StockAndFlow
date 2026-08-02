using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace StockAndFlow.Services
{
    /// <summary>Symbologies this app can *generate*. Decoding accepts far more (see <see cref="ReadableFormats"/>).</summary>
    public enum BarcodeSymbology
    {
        /// <summary>Variable-length alphanumeric. The right default for a maker's own SKUs.</summary>
        Code128,

        /// <summary>2D; holds more text and scans well from phone cameras.</summary>
        QrCode
    }

    /// <summary>
    /// Creates barcode label images for inventory items and decodes barcodes out of photos.
    ///
    /// Only CODE_128 and QR are generated on purpose: EAN-13/UPC-A digits are issued from a
    /// GS1 company prefix, so inventing them would collide with real products and be rejected
    /// by retailers. Those formats are still *read*, since customers' existing stock has them.
    /// </summary>
    public class BarcodeService
    {
        /// <summary>Ambiguous glyphs (0/O, 1/I/L) are excluded so a human can retype a SKU from a label.</summary>
        private const string SkuAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        private static readonly BarcodeFormat[] ReadableFormats =
        {
            BarcodeFormat.QR_CODE, BarcodeFormat.CODE_128, BarcodeFormat.CODE_39,
            BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.UPC_A, BarcodeFormat.UPC_E,
            BarcodeFormat.ITF, BarcodeFormat.DATA_MATRIX, BarcodeFormat.PDF_417, BarcodeFormat.AZTEC
        };

        /// <summary>
        /// A short, unique, human-retypable code such as "SF-K7Q2M9". Not derived from the item
        /// name: names change and duplicate, and a barcode must stay stable and unique.
        /// </summary>
        /// <param name="existingCodes">
        /// Codes already in use. Supplying them guarantees no clash — two items sharing a code
        /// makes scanning ambiguous, so the scanner adds whichever item it happens to find first.
        /// </param>
        public string GenerateSku(IEnumerable<string?>? existingCodes = null, int length = 6)
        {
            if (length < 4) length = 4;

            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existingCodes != null)
            {
                foreach (var code in existingCodes)
                {
                    if (!string.IsNullOrWhiteSpace(code))
                        taken.Add(code!.Trim());
                }
            }

            for (int attempt = 0; attempt < 50; attempt++)
            {
                var chars = new char[length];
                for (int i = 0; i < length; i++)
                    chars[i] = SkuAlphabet[RandomNumberGenerator.GetInt32(SkuAlphabet.Length)];

                var candidate = $"SF-{new string(chars)}";
                if (!taken.Contains(candidate))
                    return candidate;
            }

            // 50 collisions at 31^6 combinations means the pool really is crowded; widen it
            // rather than hand back a duplicate.
            return GenerateSku(existingCodes, length + 2);
        }

        /// <summary>Whether <paramref name="value"/> can be encoded in the given symbology.</summary>
        public bool CanEncode(string? value, BarcodeSymbology symbology = BarcodeSymbology.Code128)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // CODE_128 covers ASCII 0-127 only; a name with an accent or emoji must go to QR.
            if (symbology == BarcodeSymbology.Code128)
            {
                foreach (var c in value)
                {
                    if (c > 127)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Renders a print-ready PNG label: the barcode plus the code in human-readable text
        /// underneath, so it can still be typed in if a scan fails.
        /// </summary>
        /// <param name="value">The code to encode (usually the item's SKU).</param>
        /// <param name="symbology">CODE_128 (default) or QR.</param>
        /// <param name="caption">Optional line above the code — typically the product name.</param>
        /// <param name="scale">Pixel size of one barcode module; higher = sharper print.</param>
        public byte[] CreateLabelPng(
            string value,
            BarcodeSymbology symbology = BarcodeSymbology.Code128,
            string? caption = null,
            int scale = 4)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Barcode value cannot be empty.", nameof(value));
            if (!CanEncode(value, symbology))
                throw new ArgumentException(
                    $"'{value}' cannot be encoded as {symbology}. CODE_128 supports ASCII characters only — use a QR code instead.",
                    nameof(value));
            if (scale < 1) scale = 1;

            var matrix = Encode(value, symbology);
            return Render(matrix, value, caption, scale, symbology);
        }

        private static BitMatrix Encode(string value, BarcodeSymbology symbology)
        {
            if (symbology == BarcodeSymbology.QrCode)
            {
                var qrWriter = new BarcodeWriterGeneric
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new ZXing.QrCode.QrCodeEncodingOptions
                    {
                        Width = 0,
                        Height = 0,
                        Margin = 1,
                        PureBarcode = true,
                        // Without this ZXing encodes Latin-1 and any accent or dash in a
                        // product name comes back mangled.
                        CharacterSet = "UTF-8",
                        // Level M tolerates ~15% damage — labels get scuffed and smudged.
                        ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                    }
                };
                return qrWriter.Encode(value);
            }

            var writer = new BarcodeWriterGeneric
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    // Modest intrinsic size; the real scaling happens when we draw.
                    Width = 1,
                    Height = 1,
                    Margin = 10,   // 1D symbols need a wide quiet zone to scan
                    PureBarcode = true
                }
            };
            return writer.Encode(value);
        }

        private static byte[] Render(BitMatrix matrix, string value, string? caption, int scale, BarcodeSymbology symbology)
        {
            // 1D symbols encode no height of their own — give them a printable bar height.
            int barsHeight = symbology == BarcodeSymbology.QrCode ? matrix.Height * scale : 34 * scale;
            int barsWidth = matrix.Width * scale;

            const float captionSize = 13f;
            const float valueSize = 15f;
            int captionHeight = string.IsNullOrWhiteSpace(caption) ? 0 : (int)(captionSize * 1.6f);
            int valueHeight = (int)(valueSize * 1.7f);
            int pad = 8 * Math.Max(1, scale / 2);

            int width = barsWidth + pad * 2;
            int height = captionHeight + barsHeight + valueHeight + pad * 2;

            using var surface = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(surface))
            {
                canvas.Clear(SKColors.White);

                using var black = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Fill };
                float y = pad;

                if (captionHeight > 0)
                {
                    using var captionPaint = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = captionSize,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center,
                        Typeface = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default
                    };
                    canvas.DrawText(Truncate(caption!, 40), width / 2f, y + captionSize, captionPaint);
                    y += captionHeight;
                }

                // Modules are drawn as exact rectangles with antialiasing off; blurred edges
                // are a common reason a printed barcode won't scan.
                for (int mx = 0; mx < matrix.Width; mx++)
                {
                    if (symbology == BarcodeSymbology.QrCode)
                    {
                        for (int my = 0; my < matrix.Height; my++)
                        {
                            if (matrix[mx, my])
                                canvas.DrawRect(pad + mx * scale, y + my * scale, scale, scale, black);
                        }
                    }
                    else if (matrix[mx, 0])
                    {
                        canvas.DrawRect(pad + mx * scale, y, scale, barsHeight, black);
                    }
                }
                y += barsHeight;

                using var valuePaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = valueSize,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center,
                    Typeface = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default
                };
                canvas.DrawText(value, width / 2f, y + valueSize + 2, valuePaint);
            }

            using var image = SKImage.FromBitmap(surface);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static string Truncate(string text, int max)
            => text.Length <= max ? text : text.Substring(0, max - 1) + "…";

        /// <summary>
        /// Decodes a barcode from a photo. Returns null when nothing is found.
        /// </summary>
        public string? Decode(Stream imageStream)
        {
            using var decoded = SKBitmap.Decode(imageStream);
            return decoded == null ? null : Decode(decoded);
        }

        /// <summary>Decodes a barcode from an already-loaded bitmap.</summary>
        public string? Decode(SKBitmap bitmap)
        {
            if (bitmap.Width <= 0 || bitmap.Height <= 0)
                return null;

            // SKBitmap.Decode returns whatever colour type the source used (RGBA on some
            // platforms, BGRA on others) and rows can be padded. Copying into a known
            // BGRA8888 buffer makes the pixel layout we hand ZXing platform-independent —
            // assuming the source format is how scanning silently fails on one OS only.
            using var normalized = new SKBitmap(new SKImageInfo(
                bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            if (!bitmap.CopyTo(normalized, SKColorType.Bgra8888))
                return null;

            var source = new RGBLuminanceSource(
                normalized.Bytes, normalized.Width, normalized.Height,
                RGBLuminanceSource.BitmapFormat.BGRA32);

            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true,   // labels photographed on dark surfaces
                    PossibleFormats = new List<BarcodeFormat>(ReadableFormats)
                }
            };

            return reader.Decode(source)?.Text;
        }
    }
}
