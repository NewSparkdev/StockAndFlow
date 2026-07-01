using StockAndFlow.Mobile.Pages;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile;

public partial class MainPage : ContentPage
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	private async void OnSettingsClicked(object? sender, EventArgs e)
	{
		await Navigation.PushModalAsync(new NavigationPage(new SettingsPage()));
	}
}
