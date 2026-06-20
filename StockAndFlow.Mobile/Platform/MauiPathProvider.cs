using System.IO;
using StockAndFlow.Platform;

namespace StockAndFlow.Mobile.Platform;

/// <summary>
/// MAUI storage locations, all under the per-app sandboxed <see cref="FileSystem.AppDataDirectory"/>.
/// </summary>
public sealed class MauiPathProvider : IPathProvider
{
    public string DataDirectory { get; }
    public string ImagesDirectory { get; }
    public string LogsDirectory { get; }

    public MauiPathProvider()
    {
        var root = FileSystem.AppDataDirectory;
        DataDirectory = Path.Combine(root, "Data");
        ImagesDirectory = Path.Combine(root, "Images");
        LogsDirectory = Path.Combine(root, "Logs");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ImagesDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
