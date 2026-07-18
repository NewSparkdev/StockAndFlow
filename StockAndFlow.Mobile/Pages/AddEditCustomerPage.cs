using StockAndFlow.ViewModels;

namespace StockAndFlow.Mobile.Pages;

public class AddEditCustomerPage : ContentPage
{
    public AddEditCustomerPage(AddEditCustomerViewModel viewModel)
    {
        BindingContext = viewModel;
        viewModel.CloseRequested += async (_, _) => await Navigation.PopAsync();

        var title = new Label { FontSize = 20, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 0, 0, 16) };
        title.SetBinding(Label.TextProperty, nameof(AddEditCustomerViewModel.DialogTitle));

        var nameEntry = new Entry { Placeholder = "Full name *" };
        nameEntry.SetBinding(Entry.TextProperty, nameof(AddEditCustomerViewModel.Name));

        var emailEntry = new Entry { Placeholder = "Email address", Keyboard = Keyboard.Email };
        emailEntry.SetBinding(Entry.TextProperty, nameof(AddEditCustomerViewModel.Email));

        var phoneEntry = new Entry { Placeholder = "Phone number", Keyboard = Keyboard.Telephone };
        phoneEntry.SetBinding(Entry.TextProperty, nameof(AddEditCustomerViewModel.Phone));

        var addressEntry = new Entry { Placeholder = "Address (optional)" };
        addressEntry.SetBinding(Entry.TextProperty, nameof(AddEditCustomerViewModel.Address));

        var notesEditor = new Editor { Placeholder = "Notes (optional)", HeightRequest = 80 };
        notesEditor.SetBinding(Editor.TextProperty, nameof(AddEditCustomerViewModel.Notes));

        var saveBtn = new Button { Text = "Save" };
        saveBtn.SetBinding(Button.CommandProperty, nameof(AddEditCustomerViewModel.SaveCommand));

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.SetBinding(Button.CommandProperty, nameof(AddEditCustomerViewModel.CancelCommand));
        cancelBtn.Style = (Style)Application.Current!.Resources["SecondaryButton"];

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    title,
                    new Label { Text = "Name *" },
                    nameEntry,
                    new Label { Text = "Email" },
                    emailEntry,
                    new Label { Text = "Phone" },
                    phoneEntry,
                    new Label { Text = "Address" },
                    addressEntry,
                    new Label { Text = "Notes" },
                    notesEditor,
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Star },
                        },
                        ColumnSpacing = 12,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children = { cancelBtn, saveBtn }
                    }
                }
            }
        };

        Grid.SetColumn(cancelBtn, 0);
        Grid.SetColumn(saveBtn, 1);
    }
}
