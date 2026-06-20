using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class RecordAdjustmentPage : ContentPage
{
	private readonly RecordAdjustmentViewModel _viewModel;

	public RecordAdjustmentPage(RecordAdjustmentViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
		viewModel.CloseRequested += async (_, _) => await Navigation.PopModalAsync();
	}

	// The reason list holds AdjustmentReasonItem wrappers; map the picked one onto the VM's enum.
	private void OnReasonSelected(object? sender, EventArgs e)
	{
		if (ReasonPicker.SelectedItem is AdjustmentReasonItem item)
		{
			_viewModel.SelectedReason = item.Reason;
		}
	}
}
