using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using Serilog;
using StockAndFlow.Platform;

namespace StockAndFlow.Wpf.Platform
{
    /// <summary>
    /// WPF implementation of <see cref="IFilePickerService"/> using OpenFileDialog. The chosen image
    /// is copied into the app's Images directory (mirroring the previous in-ViewModel behavior).
    /// </summary>
    public sealed class WpfFilePickerService : IFilePickerService
    {
        private readonly IPathProvider _pathProvider;

        public WpfFilePickerService(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        public Task<string?> PickAndStoreImageAsync(string title, string fileNamePrefix = "")
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
            {
                return Task.FromResult<string?>(null);
            }

            try
            {
                Directory.CreateDirectory(_pathProvider.ImagesDirectory);

                var fileName = $"{fileNamePrefix}{Guid.NewGuid()}{Path.GetExtension(dialog.FileName)}";
                var destPath = Path.Combine(_pathProvider.ImagesDirectory, fileName);

                File.Copy(dialog.FileName, destPath, true);
                return Task.FromResult<string?>(destPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy picked image into Images folder; using original path");
                return Task.FromResult<string?>(dialog.FileName);
            }
        }
    }
}
