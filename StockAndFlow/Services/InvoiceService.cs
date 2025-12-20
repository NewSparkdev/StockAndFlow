using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StockAndFlow.Models;

namespace StockAndFlow.Services
{
    public class InvoiceService
    {
        private readonly BusinessSettingsService _settingsService;

        public InvoiceService(BusinessSettingsService settingsService)
        {
            _settingsService = settingsService;

            // Set QuestPDF license (Community license is free for non-commercial use)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Generates a PDF invoice for a sale transaction.
        /// </summary>
        /// <param name="transaction">The sale transaction to generate an invoice for</param>
        /// <param name="outputPath">The path where the PDF will be saved. If null, uses default path.</param>
        /// <returns>The path to the generated PDF file</returns>
        public async Task<string> GenerateInvoiceAsync(SaleTransaction transaction, string? outputPath = null)
        {
            var settings = await _settingsService.GetSettingsAsync();

            // Generate default filename if not provided
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                // Use the user's Downloads folder
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

            // Generate the PDF
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(c => ComposeHeader(c, settings, transaction));
                    page.Content().Element(c => ComposeContent(c, transaction));
                    page.Footer().Element(c => ComposeFooter(c, settings));
                });
            }).GeneratePdf(outputPath);

            return outputPath;
        }

        private void ComposeHeader(IContainer container, BusinessSettings settings, SaleTransaction transaction)
        {
            container.Column(column =>
            {
                // Business info and invoice title
                column.Item().Row(row =>
                {
                    // Left side - Business info
                    row.RelativeItem().Column(leftColumn =>
                    {
                        if (!string.IsNullOrWhiteSpace(settings.BusinessName))
                        {
                            leftColumn.Item().Text(settings.BusinessName)
                                .FontSize(18)
                                .Bold()
                                .FontColor(Colors.Blue.Darken2);
                        }

                        if (!string.IsNullOrWhiteSpace(settings.Address))
                        {
                            leftColumn.Item().PaddingTop(5).Text(settings.Address).FontSize(9);
                        }

                        if (!string.IsNullOrWhiteSpace(settings.City))
                        {
                            leftColumn.Item().Text($"{settings.City}, {settings.State} {settings.ZipCode}").FontSize(9);
                        }

                        if (!string.IsNullOrWhiteSpace(settings.Phone))
                        {
                            leftColumn.Item().PaddingTop(5).Text($"Phone: {settings.Phone}").FontSize(9);
                        }

                        if (!string.IsNullOrWhiteSpace(settings.Email))
                        {
                            leftColumn.Item().Text($"Email: {settings.Email}").FontSize(9);
                        }

                        if (!string.IsNullOrWhiteSpace(settings.Website))
                        {
                            leftColumn.Item().Text($"Web: {settings.Website}").FontSize(9);
                        }
                    });

                    // Right side - Invoice title and number
                    row.RelativeItem().AlignRight().Column(rightColumn =>
                    {
                        rightColumn.Item().Text("INVOICE")
                            .FontSize(28)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        var invoiceNumber = transaction.TransactionId.ToString().Substring(0, 8).ToUpper();
                        rightColumn.Item().PaddingTop(10).Text($"Invoice #: {invoiceNumber}").FontSize(10);
                        rightColumn.Item().Text($"Date: {transaction.SaleDate:MMMM dd, yyyy}").FontSize(10);
                        rightColumn.Item().Text($"Time: {transaction.SaleDate:hh:mm tt}").FontSize(10);
                    });
                });

                // Separator
                column.Item().PaddingVertical(20).LineHorizontal(2).LineColor(Colors.Blue.Darken2);

                // Customer information
                column.Item().Column(customerColumn =>
                {
                    customerColumn.Item().Text("Bill To:").FontSize(12).Bold();

                    if (!string.IsNullOrWhiteSpace(transaction.CustomerName))
                    {
                        customerColumn.Item().PaddingTop(5).Text(transaction.CustomerName).FontSize(11);
                    }
                    else
                    {
                        customerColumn.Item().PaddingTop(5).Text("Walk-in Customer").FontSize(11).Italic();
                    }

                    if (!string.IsNullOrWhiteSpace(transaction.CustomerEmail))
                    {
                        customerColumn.Item().Text(transaction.CustomerEmail).FontSize(10);
                    }
                });

                column.Item().PaddingBottom(20);
            });
        }

        private void ComposeContent(IContainer container, SaleTransaction transaction)
        {
            container.Column(column =>
            {
                // Items table
                column.Item().Table(table =>
                {
                    // Define columns
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // Item name
                        columns.RelativeColumn(1); // Quantity
                        columns.RelativeColumn(1.5f); // Unit Price
                        columns.RelativeColumn(1.5f); // Total
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(8).Text("Item").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(8).AlignCenter().Text("Qty").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(8).AlignRight().Text("Unit Price").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(8).AlignRight().Text("Total").FontColor(Colors.White).Bold();
                    });

                    // Items
                    foreach (var item in transaction.Items.OrderBy(i => i.ItemName))
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(item.ItemName ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignCenter().Text(item.Quantity.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight().Text($"${item.SalePricePerUnit:N2}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight().Text($"${item.Revenue:N2}");
                    }
                });

                // Summary section
                column.Item().PaddingTop(20).AlignRight().Column(summaryColumn =>
                {
                    summaryColumn.Item().Row(row =>
                    {
                        row.AutoItem().Width(150).Text("Subtotal:").FontSize(12);
                        row.AutoItem().Width(100).AlignRight().Text($"${transaction.Revenue:N2}").FontSize(12);
                    });

                    summaryColumn.Item().PaddingTop(10).Row(row =>
                    {
                        row.AutoItem().Width(150).Text("Total:").FontSize(14).Bold();
                        row.AutoItem().Width(100).AlignRight().Text($"${transaction.Revenue:N2}")
                            .FontSize(14)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);
                    });
                });

                // Notes section
                if (!string.IsNullOrWhiteSpace(transaction.Notes))
                {
                    column.Item().PaddingTop(30).Column(notesColumn =>
                    {
                        notesColumn.Item().Text("Notes:").FontSize(11).Bold();
                        notesColumn.Item().PaddingTop(5).Text(transaction.Notes).FontSize(10);
                    });
                }

                // Thank you message
                column.Item().PaddingTop(30).AlignCenter().Text("Thank you for your business!")
                    .FontSize(12)
                    .Italic()
                    .FontColor(Colors.Grey.Darken1);
            });
        }

        private void ComposeFooter(IContainer container, BusinessSettings settings)
        {
            container.AlignCenter().Column(column =>
            {
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().PaddingTop(10).Text(text =>
                {
                    text.Span("Generated on: ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(DateTime.Now.ToString("MMMM dd, yyyy 'at' hh:mm tt")).FontSize(8).FontColor(Colors.Grey.Medium);
                });

                if (!string.IsNullOrWhiteSpace(settings.TaxId))
                {
                    column.Item().Text($"Tax ID: {settings.TaxId}").FontSize(8).FontColor(Colors.Grey.Medium);
                }
            });
        }
    }
}
