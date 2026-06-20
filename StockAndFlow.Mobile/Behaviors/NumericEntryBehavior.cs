using System.Globalization;
using Microsoft.Maui.Controls;

namespace StockAndFlow.Mobile.Behaviors;

/// <summary>
/// Restricts an Entry to valid numeric input, reverting any keystroke/paste that would make the text
/// non-numeric (letters, stray separators, etc.). This keeps the bound decimal/int property in sync
/// and avoids the silent "invalid input dropped / stale value retained" problem, using the current
/// culture's decimal separator. Partial states like "" , "-" and "1." are allowed while typing.
/// </summary>
public class NumericEntryBehavior : Behavior<Entry>
{
	public static readonly BindableProperty AllowDecimalProperty =
		BindableProperty.Create(nameof(AllowDecimal), typeof(bool), typeof(NumericEntryBehavior), true);

	public static readonly BindableProperty AllowNegativeProperty =
		BindableProperty.Create(nameof(AllowNegative), typeof(bool), typeof(NumericEntryBehavior), false);

	public bool AllowDecimal
	{
		get => (bool)GetValue(AllowDecimalProperty);
		set => SetValue(AllowDecimalProperty, value);
	}

	public bool AllowNegative
	{
		get => (bool)GetValue(AllowNegativeProperty);
		set => SetValue(AllowNegativeProperty, value);
	}

	protected override void OnAttachedTo(Entry entry)
	{
		entry.TextChanged += OnTextChanged;
		base.OnAttachedTo(entry);
	}

	protected override void OnDetachingFrom(Entry entry)
	{
		entry.TextChanged -= OnTextChanged;
		base.OnDetachingFrom(entry);
	}

	private void OnTextChanged(object? sender, TextChangedEventArgs e)
	{
		if (sender is not Entry entry)
			return;

		var text = e.NewTextValue;
		if (string.IsNullOrEmpty(text))
			return; // empty is allowed (treated as 0 by the binding)

		if (!IsValidPartial(text))
		{
			entry.Text = e.OldTextValue;
		}
	}

	private bool IsValidPartial(string text)
	{
		var sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

		int i = 0;
		if (AllowNegative && text.StartsWith("-", StringComparison.Ordinal))
			i = 1;

		var separatorSeen = false;
		for (; i < text.Length; i++)
		{
			if (char.IsDigit(text[i]))
				continue;

			if (AllowDecimal && !separatorSeen &&
				text.AsSpan(i).StartsWith(sep))
			{
				separatorSeen = true;
				i += sep.Length - 1;
				continue;
			}

			return false;
		}

		return true;
	}
}
