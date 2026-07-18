namespace StockAndFlow.Mobile.Pages;

/// <summary>
/// Shown once on first launch. Four swipeable feature cards, then routes to the main shell.
/// Writes "onboarding_done" to Preferences before navigating so it never shows again.
/// </summary>
public class OnboardingPage : ContentPage
{
    private sealed record SlideData(string Icon, string Heading, string Body);

    private static readonly SlideData[] Slides =
    [
        new("logo.png",
            "Welcome to Stock & Flow",
            "The simple way to manage inventory, record sales, and understand your business — right from your phone."),
        new("tab_inventory.png",
            "Track your inventory",
            "Add products, monitor quantities, and get instant alerts when stock runs low so you never miss a sale."),
        new("tab_sales.png",
            "Record sales & expenses",
            "Log every transaction in seconds. Watch your revenue, costs, and profit update in real time."),
        new("tab_reports.png",
            "Reports & invoices",
            "Generate profit & loss reports and send professional PDF invoices to customers — all from one place."),
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
        Grid.SetRow(BuildDots(), 1);

        var btnStack = new VerticalStackLayout
        {
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 48),
            Children = { _nextBtn, _skipBtn }
        };
        Grid.SetRow(btnStack, 2);

        grid.Add(_carousel);
        grid.Add(BuildDots());
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
            ItemTemplate = new DataTemplate(SlideTemplate),
        };
        cv.CurrentItemChanged += (_, e) =>
        {
            var idx = Array.IndexOf(Slides, e.CurrentItem);
            if (idx >= 0) UpdateState(idx);
        };
        return cv;
    }

    private static View SlideTemplate()
    {
        var icon = new Image
        {
            WidthRequest = 110,
            HeightRequest = 110,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 40),
        };
        icon.SetBinding(Image.SourceProperty, nameof(SlideData.Icon));

        var heading = new Label
        {
            TextColor = Colors.White,
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
        };
        heading.SetBinding(Label.TextProperty, nameof(SlideData.Heading));

        var body = new Label
        {
            TextColor = Color.FromArgb("#DCD4F7"),
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        body.SetBinding(Label.TextProperty, nameof(SlideData.Body));

        return new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(44, 0),
            Spacing = 0,
            Children = { icon, heading, body }
        };
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
