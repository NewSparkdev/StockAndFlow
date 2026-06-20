using StockAndFlow.Models;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class ExpensesPage : ContentPage
{
	private readonly ExpensesViewModel _viewModel;

	public ExpensesPage(ExpensesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		_viewModel = viewModel;
	}

	private void Select(object sender)
	{
		if (sender is BindableObject b && b.BindingContext is Expense item)
			_viewModel.SelectedExpense = item;
	}

	private void OnRowTapped(object sender, TappedEventArgs e)
	{
		Select(sender);
		_viewModel.ViewDetailsCommand.Execute(null);
	}

	private void OnEditSwipe(object sender, EventArgs e)
	{
		Select(sender);
		_viewModel.EditExpenseCommand.Execute(null);
	}

	private void OnDeleteSwipe(object sender, EventArgs e)
	{
		Select(sender);
		_viewModel.DeleteExpenseCommand.Execute(null);
	}
}
