using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using StockAndFlow.Mobile.Pages;
using StockAndFlow.Models;
using StockAndFlow.Platform;
using StockAndFlow.Services;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Platform;

/// <summary>
/// MAUI implementation of <see cref="IEditorPresenter"/>. Builds editor ViewModels from DI
/// (mirroring WpfEditorPresenter) and pushes the matching ContentPage modally. Each editor page
/// pops itself when its ViewModel raises CloseRequested.
/// </summary>
public sealed class MauiEditorPresenter : IEditorPresenter
{
	private readonly IServiceProvider _services;

	public MauiEditorPresenter(IServiceProvider services)
	{
		_services = services;
	}

	private static INavigation Nav =>
		Shell.Current?.Navigation
		?? Application.Current?.Windows[0].Page?.Navigation
		?? throw new InvalidOperationException("No navigation available.");

	private T Create<T>(params object[] args) => ActivatorUtilities.CreateInstance<T>(_services, args);

	private Task PushAsync(Page page) =>
		MainThread.InvokeOnMainThreadAsync(() => Nav.PushModalAsync(new NavigationPage(page)));

	public Task ShowAddInventoryAsync() =>
		PushAsync(new AddEditInventoryPage(Create<AddEditInventoryViewModel>()));

	public Task ShowEditInventoryAsync(InventoryItem item) =>
		PushAsync(new AddEditInventoryPage(Create<AddEditInventoryViewModel>(item)));

	public Task ShowInventoryDetailsAsync(InventoryItem item) =>
		PushAsync(new InventoryDetailsPage(
			item,
			this,
			_services.GetRequiredService<BomService>(),
			_services.GetRequiredService<InventoryService>()));

	public Task ShowAddExpenseAsync() =>
		PushAsync(new AddEditExpensePage(Create<AddEditExpenseViewModel>()));

	public Task ShowEditExpenseAsync(Expense expense)
	{
		var vm = Create<AddEditExpenseViewModel>();
		vm.LoadExpense(expense);
		return PushAsync(new AddEditExpensePage(vm));
	}

	public Task ShowExpenseDetailsAsync(Expense expense) =>
		PushAsync(new ExpenseDetailsPage(Create<ExpenseDetailsViewModel>(expense)));

	public Task ShowRecordAdjustmentAsync() =>
		PushAsync(new RecordAdjustmentPage(Create<RecordAdjustmentViewModel>()));

	public Task ShowAdjustmentDetailsAsync(InventoryAdjustment adjustment) =>
		PushAsync(new AdjustmentDetailsPage(Create<AdjustmentDetailsViewModel>(adjustment)));

	public Task ShowRecordSaleAsync() =>
		PushAsync(new RecordSalePage(Create<RecordSaleViewModel>()));

	public Task ShowEditSaleAsync(SaleTransaction sale) =>
		PushAsync(new EditSalePage(Create<EditSaleViewModel>(sale)));

	public Task ShowSaleDetailsAsync(SaleTransaction sale) =>
		PushAsync(new SaleDetailsPage(Create<SaleDetailsViewModel>(sale)));

	public async Task<bool> ShowExportImportAsync()
	{
		await PushAsync(new ExportImportPage(_services.GetRequiredService<ExportImportService>()));
		// Imports raise service-change events that dependent views react to; no explicit refresh signal needed.
		return false;
	}
}
