using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using SkiaSharp;
using StockAndFlow.Models;
using StockAndFlow.Services;

namespace StockAndFlow.Tests.Services
{
    /// <summary>
    /// Verifies the SkiaSharp-based invoice renderer on the desktop runtime. The same renderer is
    /// exercised on Android via the emulator; this guards the cross-platform code path and ensures
    /// libSkiaSharp can produce a valid, non-empty PDF on Windows.
    /// </summary>
    public class InvoiceServiceTests
    {
        private static InvoiceService CreateService(BusinessSettings settings)
        {
            var dataService = Substitute.For<IDataService>();
            dataService.GetAllAsync<BusinessSettings>()
                .Returns(Task.FromResult(new List<BusinessSettings> { settings }));
            return new InvoiceService(new BusinessSettingsService(dataService));
        }

        private static SaleTransaction SampleTransaction() => new()
        {
            TransactionId = Guid.NewGuid(),
            SaleDate = new DateTime(2026, 6, 20, 15, 13, 0),
            CustomerName = "Jane Doe",
            CustomerEmail = "jane@example.com",
            Notes = "Thanks for shopping with us!",
            Items = new List<Sale>
            {
                new() { ItemName = "Candle", Quantity = 2, SalePricePerUnit = 20m, CostPerUnit = 8m, TaxRate = 7.5m, TaxAmount = 3m },
                new() { ItemName = "Wick Trimmer", Quantity = 1, SalePricePerUnit = 12m, CostPerUnit = 5m, TaxRate = 7.5m, TaxAmount = 0.9m }
            }
        };

        [Fact]
        public async Task GenerateInvoiceAsync_WritesValidPdf()
        {
            var settings = new BusinessSettings
            {
                BusinessName = "Candle Co",
                Address = "123 Main St",
                City = "Springfield",
                State = "IL",
                ZipCode = "62704",
                Phone = "555-0100",
                Email = "hello@candle.co",
                Website = "candle.co",
                TaxId = "12-3456789"
            };
            var service = CreateService(settings);
            var outputPath = Path.Combine(Path.GetTempPath(), $"InvoiceTest_{Guid.NewGuid():N}.pdf");

            try
            {
                var result = await service.GenerateInvoiceAsync(SampleTransaction(), outputPath);

                result.Should().Be(outputPath);
                File.Exists(outputPath).Should().BeTrue();

                var bytes = await File.ReadAllBytesAsync(outputPath);
                bytes.Length.Should().BeGreaterThan(1000, "a rendered invoice should be a non-trivial PDF");
                System.Text.Encoding.ASCII.GetString(bytes, 0, 5)
                    .Should().Be("%PDF-", "the output must be a valid PDF file");
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        /// <summary>Writes a small solid-color PNG to temp and returns its path.</summary>
        private static string CreateTempLogo()
        {
            var path = Path.Combine(Path.GetTempPath(), $"logo_{Guid.NewGuid():N}.png");
            using var bmp = new SKBitmap(200, 80);
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(new SKColor(0x51, 0x2B, 0xD4));
            }
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            using var fs = File.OpenWrite(path);
            data.SaveTo(fs);
            return path;
        }

        [Fact]
        public async Task GenerateInvoiceAsync_EmbedsLogo_WhenEnabled()
        {
            var logoPath = CreateTempLogo();
            var withLogoPdf = Path.Combine(Path.GetTempPath(), $"InvoiceTest_{Guid.NewGuid():N}.pdf");
            var withoutLogoPdf = Path.Combine(Path.GetTempPath(), $"InvoiceTest_{Guid.NewGuid():N}.pdf");

            try
            {
                var settings = new BusinessSettings { BusinessName = "Candle Co", LogoPath = logoPath };

                settings.ShowLogoOnInvoice = true;
                await CreateService(settings).GenerateInvoiceAsync(SampleTransaction(), withLogoPdf);

                settings.ShowLogoOnInvoice = false;
                await CreateService(settings).GenerateInvoiceAsync(SampleTransaction(), withoutLogoPdf);

                new FileInfo(withLogoPdf).Length.Should().BeGreaterThan(
                    new FileInfo(withoutLogoPdf).Length,
                    "the enabled logo must actually be embedded in the PDF");
            }
            finally
            {
                foreach (var f in new[] { logoPath, withLogoPdf, withoutLogoPdf })
                    if (File.Exists(f)) File.Delete(f);
            }
        }

        [Fact]
        public async Task GenerateInvoiceAsync_MissingLogoFile_StillRenders()
        {
            var settings = new BusinessSettings
            {
                BusinessName = "Candle Co",
                LogoPath = Path.Combine(Path.GetTempPath(), "does_not_exist_logo.png"),
                ShowLogoOnInvoice = true
            };
            var outputPath = Path.Combine(Path.GetTempPath(), $"InvoiceTest_{Guid.NewGuid():N}.pdf");

            try
            {
                await CreateService(settings).GenerateInvoiceAsync(SampleTransaction(), outputPath);
                new FileInfo(outputPath).Length.Should().BeGreaterThan(1000,
                    "a missing logo file must never break invoice generation");
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        [Fact]
        public async Task GenerateInvoiceAsync_AllDisplayTogglesOff_StillRendersValidPdf()
        {
            var settings = new BusinessSettings
            {
                BusinessName = "Candle Co",
                Phone = "555-0100",
                Email = "hello@candle.co",
                Website = "candle.co",
                TaxId = "12-3456789",
                ShowLogoOnInvoice = false,
                ShowPhoneOnInvoice = false,
                ShowEmailOnInvoice = false,
                ShowWebsiteOnInvoice = false,
                ShowTaxIdOnInvoice = false
            };
            var outputPath = Path.Combine(Path.GetTempPath(), $"InvoiceTest_{Guid.NewGuid():N}.pdf");

            try
            {
                await CreateService(settings).GenerateInvoiceAsync(SampleTransaction(), outputPath);

                var bytes = await File.ReadAllBytesAsync(outputPath);
                System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        [Fact]
        public async Task GenerateInvoiceAsync_HandlesWalkInCustomerAndNoTax()
        {
            var settings = new BusinessSettings { BusinessName = "Candle Co" };
            var service = CreateService(settings);
            var outputPath = Path.Combine(Path.GetTempPath(), $"InvoiceTest_{Guid.NewGuid():N}.pdf");

            var transaction = new SaleTransaction
            {
                TransactionId = Guid.NewGuid(),
                SaleDate = DateTime.Now,
                Items = new List<Sale>
                {
                    new() { ItemName = "Candle", Quantity = 1, SalePricePerUnit = 20m, CostPerUnit = 8m }
                }
            };

            try
            {
                var result = await service.GenerateInvoiceAsync(transaction, outputPath);
                File.Exists(result).Should().BeTrue();
                new FileInfo(result).Length.Should().BeGreaterThan(1000);
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }
    }
}
