using StockAndFlow.Models;

namespace StockAndFlow.Platform
{
    /// <summary>
    /// Presents modal editor/detail screens for the list ViewModels. The platform owns construction
    /// of the editor ViewModels and their views:
    ///   • WPF creates a dialog Window, sets its DataContext, and calls ShowDialog().
    ///   • MAUI pushes a modal ContentPage via Navigation.PushModalAsync.
    /// This keeps the shared list ViewModels free of any View/Window references.
    /// Each method completes when the editor is dismissed.
    /// </summary>
    public interface IEditorPresenter
    {
        Task ShowAddInventoryAsync();
        Task ShowEditInventoryAsync(InventoryItem item);
        Task ShowInventoryDetailsAsync(InventoryItem item);

        Task ShowRecordSaleAsync();
        Task ShowEditSaleAsync(SaleTransaction sale);
        Task ShowSaleDetailsAsync(SaleTransaction sale);

        Task ShowAddExpenseAsync();
        Task ShowEditExpenseAsync(Expense expense);
        Task ShowExpenseDetailsAsync(Expense expense);

        Task ShowRecordAdjustmentAsync();
        Task ShowAdjustmentDetailsAsync(InventoryAdjustment adjustment);

        /// <summary>Returns true if data was imported and dependent views should refresh.</summary>
        Task<bool> ShowExportImportAsync();
    }
}
