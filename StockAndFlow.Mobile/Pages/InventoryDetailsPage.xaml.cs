using StockAndFlow.Models;

namespace StockAndFlow.Mobile.Pages;

public partial class InventoryDetailsPage : ContentPage
{
	public InventoryDetailsPage(InventoryItem item)
	{
		InitializeComponent();
		BindingContext = item;
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		await Navigation.PopModalAsync();
	}
}
