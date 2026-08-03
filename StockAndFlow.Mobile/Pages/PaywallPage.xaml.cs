using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class PaywallPage : ContentPage
{
	private readonly PaywallViewModel _viewModel;

	public PaywallPage(PaywallViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;

		viewModel.CloseRequested += async (_, _) =>
		{
			if (Navigation.ModalStack.Count > 0)
				await Navigation.PopModalAsync();
			else
				await Navigation.PopAsync();
		};
	}

	// Plan cards are tapped rather than bound to a command per row, so the selection can be
	// read straight off the tapped card's binding context.
	private void OnPlanTapped(object? sender, TappedEventArgs e)
	{
		if (sender is Border { BindingContext: ProPlan plan })
			_viewModel.SelectedPlan = plan;
	}
}
