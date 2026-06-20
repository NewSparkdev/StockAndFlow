using Microsoft.Extensions.DependencyInjection;
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
		var services = IPlatformApplication.Current!.Services;
		var vm = services.GetRequiredService<BusinessSettingsViewModel>();
		await Navigation.PushModalAsync(new NavigationPage(new BusinessSettingsPage(vm)));
	}
}
