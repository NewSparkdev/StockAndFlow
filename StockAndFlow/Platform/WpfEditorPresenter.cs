using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;
using StockAndFlow.Views.Dialogs;

namespace StockAndFlow.Wpf.Platform
{
    /// <summary>
    /// WPF implementation of <see cref="IEditorPresenter"/>. Builds editor ViewModels (resolving their
    /// dependencies from the DI container) and shows the corresponding modal dialog Windows.
    /// </summary>
    public sealed class WpfEditorPresenter : IEditorPresenter
    {
        private readonly IServiceProvider _services;

        public WpfEditorPresenter(IServiceProvider services)
        {
            _services = services;
        }

        private T Create<T>(params object[] args) => ActivatorUtilities.CreateInstance<T>(_services, args)!;

        private Task ShowModal(Func<Window> createWindow)
        {
            Application.Current.Dispatcher.Invoke(() => createWindow().ShowDialog());
            return Task.CompletedTask;
        }

        public Task ShowAddInventoryAsync() =>
            ShowModal(() => new AddEditInventoryDialog(Create<AddEditInventoryViewModel>()));

        public Task ShowEditInventoryAsync(InventoryItem item) =>
            ShowModal(() => new AddEditInventoryDialog(Create<AddEditInventoryViewModel>(item)));

        public Task ShowInventoryDetailsAsync(InventoryItem item) =>
            ShowModal(() => new InventoryDetailsDialog { DataContext = item });

        public Task ShowRecordSaleAsync() =>
            ShowModal(() => new RecordSaleDialog(Create<RecordSaleViewModel>()));

        public Task ShowEditSaleAsync(SaleTransaction sale) => Task.CompletedTask; // WPF edit-sale UI not yet implemented

        public Task ShowSaleDetailsAsync(SaleTransaction sale) =>
            ShowModal(() => new SaleDetailsDialog(Create<SaleDetailsViewModel>(sale)));

        public Task ShowAddExpenseAsync() =>
            ShowModal(() => new AddEditExpenseDialog(Create<AddEditExpenseViewModel>()));

        public Task ShowEditExpenseAsync(Expense expense) =>
            ShowModal(() =>
            {
                var vm = Create<AddEditExpenseViewModel>();
                vm.LoadExpense(expense);
                return new AddEditExpenseDialog(vm);
            });

        public Task ShowExpenseDetailsAsync(Expense expense) =>
            ShowModal(() => new ExpenseDetailsDialog(Create<ExpenseDetailsViewModel>(expense)));

        public Task ShowRecordAdjustmentAsync() =>
            ShowModal(() => new RecordAdjustmentDialog(Create<RecordAdjustmentViewModel>()));

        public Task ShowAdjustmentDetailsAsync(InventoryAdjustment adjustment) =>
            ShowModal(() => new AdjustmentDetailsDialog(Create<AdjustmentDetailsViewModel>(adjustment)));

        public Task<bool> ShowExportImportAsync()
        {
            var service = _services.GetRequiredService<ExportImportService>();
            var result = Application.Current.Dispatcher.Invoke(() =>
                new ExportImportDialog(service).ShowDialog());
            return Task.FromResult(result == true);
        }
    }
}
