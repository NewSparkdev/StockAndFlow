using StockAndFlow.Mobile.Controls;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class ReportsPage : ContentView, ISectionView
{
	public ReportsPage(ReportsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		Actions = new[]
		{
			new NavAction("Export/Import", viewModel.ExportImportCommand),
			new NavAction("Refresh", viewModel.RefreshCommand),
		};
	}

	public string SectionId => "reports";
	public string SectionTitle => "Reports";
	public IReadOnlyList<NavAction> Actions { get; }
}
