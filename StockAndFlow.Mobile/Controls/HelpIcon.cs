using Microsoft.Maui.Controls.Shapes;

namespace StockAndFlow.Mobile.Controls;

/// <summary>
/// A small circled "?" that shows a plain-language explanation of the field next to it when tapped.
/// </summary>
public class HelpIcon : Border
{
	public static readonly BindableProperty HelpTitleProperty =
		BindableProperty.Create(nameof(HelpTitle), typeof(string), typeof(HelpIcon), string.Empty);

	public static readonly BindableProperty HelpTextProperty =
		BindableProperty.Create(nameof(HelpText), typeof(string), typeof(HelpIcon), string.Empty);

	public string HelpTitle
	{
		get => (string)GetValue(HelpTitleProperty);
		set => SetValue(HelpTitleProperty, value);
	}

	public string HelpText
	{
		get => (string)GetValue(HelpTextProperty);
		set => SetValue(HelpTextProperty, value);
	}

	public HelpIcon()
	{
		WidthRequest = 22;
		HeightRequest = 22;
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(11) };
		VerticalOptions = LayoutOptions.Center;
		Padding = 0;
		this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#512BD4"), Color.FromArgb("#ac99ea"));

		var questionMark = new Label
		{
			Text = "?",
			FontSize = 13,
			FontAttributes = FontAttributes.Bold,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center,
			InputTransparent = true,
		};
		questionMark.SetAppThemeColor(Label.TextColorProperty, Colors.White, Color.FromArgb("#242424"));
		Content = questionMark;

		SemanticProperties.SetDescription(this, "Help");
		SemanticProperties.SetHint(this, "Explains what this field means");

		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) =>
		{
			var page = FindParentPage();
			if (page is not null)
				await page.DisplayAlert(HelpTitle, HelpText, "Got it");
		};
		GestureRecognizers.Add(tap);
	}

	private Page? FindParentPage()
	{
		Element? current = Parent;
		while (current is not null && current is not Page)
			current = current.Parent;
		return current as Page;
	}
}
