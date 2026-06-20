namespace StockAndFlow.Platform
{
    /// <summary>
    /// Platform-agnostic image picking. The picked file is copied into the app's image storage
    /// (see <see cref="IPathProvider.ImagesDirectory"/>) and the stored path is returned, because
    /// picker-supplied paths are ephemeral on mobile platforms.
    /// WPF implements with OpenFileDialog; MAUI with MediaPicker/FilePicker.
    /// </summary>
    public interface IFilePickerService
    {
        /// <summary>
        /// Prompts the user to pick an image, stores a copy in app image storage, and returns the
        /// stored absolute path. Returns null if the user cancels.
        /// </summary>
        /// <param name="title">Picker dialog title.</param>
        /// <param name="fileNamePrefix">Optional prefix for the stored file name (e.g. "logo_").</param>
        Task<string?> PickAndStoreImageAsync(string title, string fileNamePrefix = "");
    }
}
