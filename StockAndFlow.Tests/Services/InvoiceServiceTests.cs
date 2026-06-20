using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
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
