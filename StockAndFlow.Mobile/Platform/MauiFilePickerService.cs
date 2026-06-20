using System;
using System.IO;
using System.Threading.Tasks;
using StockAndFlow.Platform;

namespace StockAndFlow.Mobile.Platform;

/// <summary>
/// MAUI implementation of <see cref="IFilePickerService"/>. Uses the photo picker and copies the
/// chosen image into the app's Images directory (picker results are temporary cache files).
/// </summary>
public sealed class MauiFilePickerService : IFilePickerService
{
    private readonly IPathProvider _pathProvider;

    public MauiFilePickerService(IPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task<string?> PickAndStoreImageAsync(string title, string fileNamePrefix = "")
    {
        FileResult? picked;
        try
        {
            picked = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = title });
        }
        catch (FeatureNotSupportedException)
        {
            // Fall back to the generic file picker (e.g. on devices without a camera/gallery picker).
            picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = title,
                FileTypes = FilePickerFileType.Images
            });
        }

        if (picked == null)
            return null;

        Directory.CreateDirectory(_pathProvider.ImagesDirectory);
        var fileName = $"{fileNamePrefix}{Guid.NewGuid()}{Path.GetExtension(picked.FileName)}";
        var destPath = Path.Combine(_pathProvider.ImagesDirectory, fileName);

        await using var source = await picked.OpenReadAsync();
        await using var dest = File.Create(destPath);
        await source.CopyToAsync(dest);

        return destPath;
    }
}
