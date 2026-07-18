using StockAndFlow.Mobile.Controls;
using StockAndFlow.Models;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class SalesPage : ContentView, ISectionView
{
	private readonly SalesViewModel _viewModel;

	public SalesPage(SalesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		_viewModel = viewModel;
		Actions = new[]
		{
			new NavAction("Record", viewModel.RecordSaleCommand),
			new NavAction("Refresh", viewModel.RefreshCommand),
		};
	}

	public string SectionId => "sales";
	public string SectionTitle => "Sales";
	public IReadOnlyList<NavAction> Actions { get; }

	private void Select(object sender)
	{
		if (sender is BindableObject b && b.BindingContext is SaleTransaction item)
			_viewModel.SelectedSale = item;
	}

	private void OnRowTapped(object sender, TappedEventArgs e)
	{
		Select(sender);
		_viewModel.ViewDetailsCommand.Execute(null);
	}

	private void OnDeleteSwipe(object sender, EventArgs e)
	{
		Select(sender);
		_viewModel.DeleteSaleCommand.Execute(null);
	}
}
