using System;
using System.Globalization;
using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace StockAndFlow.Mobile.Converters;

/// <summary>
/// Resolves a stored image path to a file in the app's current Images directory.
///
/// Image paths are persisted (sometimes as absolute paths from the desktop app or a previous install
/// whose sandbox container differs). On mobile the app-data container can change across reinstalls, so
/// we re-base the stored value to the *current* Images directory by file name. Falls back to the raw
/// value if a re-based file isn't found.
/// </summary>
public sealed class ImagePathConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var stored = value as string;
		if (string.IsNullOrWhiteSpace(stored))
			return null;

		try
		{
			var fileName = Path.GetFileName(stored);
			if (!string.IsNullOrEmpty(fileName))
			{
				var rebased = Path.Combine(FileSystem.AppDataDirectory, "Images", fileName);
				if (File.Exists(rebased))
					return rebased;
			}
		}
		catch
		{
			// fall through to the raw value
		}

		return File.Exists(stored) ? stored : null;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
