using System.Windows.Input;
using StockAndFlow.Mobile.Controls;
using StockAndFlow.Mobile.Pages;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile;

public partial class MainPage : ContentView, ISectionView
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		Actions = new[] { new NavAction("Settings", new Command(OpenSettings)) };
	}

	public string SectionId => "dashboard";
	public string SectionTitle => "Stock & Flow";
	public IReadOnlyList<NavAction> Actions { get; }

	private static async void OpenSettings()
	{
		try
		{
			var nav = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
			if (nav != null)
				await nav.PushModalAsync(new NavigationPage(new SettingsPage()));
		}
		catch (Exception ex)
		{
			if (Application.Current?.MainPage != null)
				await Application.Current.MainPage.DisplayAlert("Settings error", ex.Message, "OK");
		}
	}
}
