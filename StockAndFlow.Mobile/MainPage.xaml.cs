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

	private static async void OpenSettings() =>
		await Shell.Current.Navigation.PushModalAsync(new NavigationPage(new SettingsPage()));
}
