using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using StockAndFlow.Models;
using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public class CustomersPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly CustomersViewModel _viewModel;

    public CustomersPage(CustomersViewModel viewModel, IServiceProvider services)
    {
        _services = services;
        _viewModel = viewModel;
        Title = "Customers";
        BindingContext = viewModel;

        viewModel.AddEditRequested += OnAddEditRequested;
        viewModel.DetailRequested += OnDetailRequested;

        Content = BuildContent();
    }

    private View BuildContent()
    {
        var search = new SearchBar
        {
            Placeholder = "Search by name, email, or phone",
            Margin = new Thickness(12, 8),
        };
        search.SetBinding(SearchBar.TextProperty, nameof(CustomersViewModel.SearchText));

        var list = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            Margin = new Thickness(12, 0),
        };
        list.SetBinding(ItemsView.ItemsSourceProperty, nameof(CustomersViewModel.Customers));
        list.ItemTemplate = new DataTemplate(CustomerRowTemplate);

        var empty = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 8,
            Children =
            {
                new Label { Text = "👥", FontSize = 40, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = "No customers yet", FontSize = 16, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = "Tap + to add your first customer.", HorizontalTextAlignment = TextAlignment.Center },
            }
        };
        list.EmptyView = empty;

        var addBtn = new Button
        {
            Text = "+ Add Customer",
            Margin = new Thickness(16, 8, 16, 16),
        };
        addBtn.SetBinding(Button.CommandProperty, nameof(CustomersViewModel.AddCommand));

        return new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
            },
            Children =
            {
                WithRow(search, 0),
                WithRow(list, 1),
                WithRow(addBtn, 2),
            }
        };
    }

    private View CustomerRowTemplate()
    {
        var name = new Label { FontAttributes = FontAttributes.Bold, FontSize = 15 };
        name.SetBinding(Label.TextProperty, nameof(Customer.Name));
        name.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);

        var email = new Label { FontSize = 13, TextColor = Color.FromArgb("#888888") };
        email.SetBinding(Label.TextProperty, nameof(Customer.Email));
        email.SetBinding(IsVisibleProperty, nameof(Customer.HasEmail));

        var phone = new Label { FontSize = 13, TextColor = Color.FromArgb("#888888") };
        phone.SetBinding(Label.TextProperty, nameof(Customer.Phone));
        phone.SetBinding(IsVisibleProperty, nameof(Customer.HasPhone));

        var editBtn = new Button
        {
            Text = "Edit",
            FontSize = 12,
            HeightRequest = 34,
            Padding = new Thickness(12, 0),
            VerticalOptions = LayoutOptions.Center,
        };
        editBtn.SetBinding(Button.CommandProperty,
            new Binding(nameof(CustomersViewModel.EditCommand), source: _viewModel));
        editBtn.SetBinding(Button.CommandParameterProperty, new Binding("."));

        var card = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 10),
            Margin = new Thickness(0, 4),
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children =
                {
                    WithColumn(new VerticalStackLayout { Spacing = 2, Children = { name, email, phone } }, 0),
                    WithColumn(editBtn, 1),
                }
            }
        };

        card.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F5F5F5"), Color.FromArgb("#2A2A2A"));

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command<Customer>(c => _viewModel.SelectCommand.Execute(c)),
            CommandParameter = new Binding("."),
        });

        return card;
    }

    private async void OnAddEditRequested(object? sender, Customer? customer)
    {
        var vm = _services.GetRequiredService<AddEditCustomerViewModel>();
        vm.Load(customer);
        var page = new AddEditCustomerPage(vm);
        await Navigation.PushAsync(page);
    }

    private void OnDetailRequested(object? sender, Customer customer)
    {
        // Tap on a customer row opens edit for now (detail view is Phase 2)
        var vm = _services.GetRequiredService<AddEditCustomerViewModel>();
        vm.Load(customer);
        _ = Navigation.PushAsync(new AddEditCustomerPage(vm));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadAsync();
    }

    private static View WithRow(View v, int row) { Grid.SetRow(v, row); return v; }
    private static View WithColumn(View v, int col) { Grid.SetColumn(v, col); return v; }
}
