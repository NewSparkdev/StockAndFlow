using Microsoft.Maui.Controls.Shapes;

namespace StockAndFlow.Mobile.Pages;

/// <summary>
/// Shown once on first launch. Four swipeable how-to cards, then routes to the main shell.
/// Writes "onboarding_done" to Preferences before navigating so it never shows again.
/// </summary>
public class OnboardingPage : ContentPage
{
    private sealed record SlideData(string Icon, string Heading, string Body, string[] Steps)
    {
        public SlideData(string Icon, string Heading, string Body)
            : this(Icon, Heading, Body, []) { }
    }

    private static readonly SlideData[] Slides =
    [
        new("logo.png",
            "Welcome to Stock & Flow",
            "Your simple tool for inventory, sales, and business insight — right from your phone."),
        new("tab_inventory.png",
            "Add your inventory",
            "",
            [
                "Tap Inventory in the bottom bar",
                "Tap Add in the top-right corner",
                "Enter product name, price, and quantity",
                "Tap Save — it appears in your list instantly",
            ]),
        new("tab_sales.png",
            "Record a sale",
            "",
            [
                "Tap Sales in the bottom bar",
                "Tap Record in the top-right corner",
                "Select items from your inventory",
                "Tap Complete Sale to log it",
            ]),
        new("tab_reports.png",
            "Reports & expenses",
            "",
            [
                "Tap Expenses → Add to log a business cost",
                "Tap Reports to see revenue and profit",
                "Tap any sale to generate a PDF invoice",
                "Tap Export to share or print a report",
            ]),
    ];

    private readonly IServiceProvider _services;
    private readonly Label[] _dots = new Label[Slides.Length];
    private readonly Button _nextBtn;
    private readonly Button _skipBtn;
    private readonly CarouselView _carousel;
    private int _current;

    public OnboardingPage(IServiceProvider services)
    {
        _services = services;
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#512BD4");

        _carousel = BuildCarousel();
        _nextBtn = new Button
        {
            Text = "Next",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#512BD4"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            CornerRadius = 28,
            HeightRequest = 56,
            Margin = new Thickness(32, 0),
        };
        _skipBtn = new Button
        {
            Text = "Skip",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#CCFFFFFF"),
            FontSize = 14,
            BorderWidth = 0,
        };

        _nextBtn.Clicked += OnNextClicked;
        _skipBtn.Clicked += (_, _) => Finish();

        var dots = BuildDots();
        var btnStack = new VerticalStackLayout
        {
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 48),
            Children = { _nextBtn, _skipBtn }
        };

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            }
        };

        Grid.SetRow(_carousel, 0);
        Grid.SetRow(dots, 1);
        Grid.SetRow(btnStack, 2);

        grid.Add(_carousel);
        grid.Add(dots);
        grid.Add(btnStack);

        Content = grid;
        UpdateState(0);
    }

    // ── carousel ──────────────────────────────────────────────────────────────

    private CarouselView BuildCarousel()
    {
        var cv = new CarouselView
        {
            ItemsSource = Slides,
            Loop = false,
            IsSwipeEnabled = true,
            ItemTemplate = new DataTemplate(() => new SlideView()),
        };
        cv.CurrentItemChanged += (_, e) =>
        {
            var idx = Array.IndexOf(Slides, e.CurrentItem);
            if (idx >= 0) UpdateState(idx);
        };
        return cv;
    }

    // ── slide view ────────────────────────────────────────────────────────────

    // ContentView subclass: OnBindingContextChanged gives direct access to SlideData,
    // avoiding the MAUI reflection issues that break static DataTemplate binding on records.
    private sealed class SlideView : ContentView
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is SlideData slide)
                Content = BuildSlide(slide);
        }

        private static View BuildSlide(SlideData slide)
        {
            var icon = new Image
            {
                Source = slide.Icon,
                WidthRequest = 100,
                HeightRequest = 100,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 0, 28),
            };

            var heading = new Label
            {
                Text = slide.Heading,
                TextColor = Colors.White,
                FontSize = 24,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24),
            };

            View body;
            if (slide.Steps.Length > 0)
            {
                var steps = new VerticalStackLayout { Spacing = 16 };
                for (var i = 0; i < slide.Steps.Length; i++)
                    steps.Add(StepRow(i + 1, slide.Steps[i]));
                body = steps;
            }
            else
            {
                body = new Label
                {
                    Text = slide.Body,
                    TextColor = Color.FromArgb("#DCD4F7"),
                    FontSize = 16,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
            }

            return new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Padding = new Thickness(40, 0),
                Spacing = 0,
                Children = { icon, heading, body }
            };
        }

        private static View StepRow(int num, string text)
        {
            var grid = new Grid
            {
                ColumnSpacing = 14,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                }
            };

            var badge = new Border
            {
                WidthRequest = 32,
                HeightRequest = 32,
                BackgroundColor = Color.FromArgb("#40FFFFFF"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                VerticalOptions = LayoutOptions.Start,
                Content = new Label
                {
                    Text = num.ToString(),
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                }
            };

            var label = new Label
            {
                Text = text,
                TextColor = Colors.White,
                FontSize = 15,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.WordWrap,
            };

            Grid.SetColumn(badge, 0);
            Grid.SetColumn(label, 1);
            grid.Add(badge);
            grid.Add(label);

            return grid;
        }
    }

    // ── dots ──────────────────────────────────────────────────────────────────

    private View BuildDots()
    {
        var row = new HorizontalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 8,
            Margin = new Thickness(0, 28, 0, 20),
        };
        for (var i = 0; i < Slides.Length; i++)
        {
            _dots[i] = new Label { Text = "●", FontSize = 12 };
            row.Add(_dots[i]);
        }
        return row;
    }

    // ── state ─────────────────────────────────────────────────────────────────

    private void OnNextClicked(object? sender, EventArgs e)
    {
        if (_current < Slides.Length - 1)
            _carousel.ScrollTo(_current + 1, animate: true);
        else
            Finish();
    }

    private void UpdateState(int index)
    {
        _current = index;
        var last = index == Slides.Length - 1;
        _nextBtn.Text = last ? "Get Started" : "Next";
        _skipBtn.IsVisible = !last;

        for (var i = 0; i < _dots.Length; i++)
            _dots[i].TextColor = i == index ? Colors.White : Color.FromArgb("#40FFFFFF");
    }

    private void Finish()
    {
        Preferences.Default.Set("onboarding_done", true);
        if (Application.Current?.Windows is { Count: > 0 } windows)
            windows[0].Page = new AppShell(_services);
    }
}
