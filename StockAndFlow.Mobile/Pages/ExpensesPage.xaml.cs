using StockAndFlow.Mobile.Controls;
using StockAndFlow.Models;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public partial class ExpensesPage : ContentView, ISectionView
{
	private readonly ExpensesViewModel _viewModel;

	public ExpensesPage(ExpensesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		_viewModel = viewModel;
		Actions = new[]
		{
			new NavAction("Add", viewModel.AddExpenseCommand),
			new NavAction("Refresh", viewModel.RefreshCommand),
		};
	}

	public string SectionId => "expenses";
	public string SectionTitle => "Expenses";
	public IReadOnlyList<NavAction> Actions { get; }

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
