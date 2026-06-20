using System;
using System.IO;
using StockAndFlow.Platform;

namespace StockAndFlow.Wpf.Platform
{
    /// <summary>
    /// WPF storage locations: folders alongside the executable, matching the app's historical layout.
    /// </summary>
    public sealed class WpfPathProvider : IPathProvider
    {
        public string DataDirectory { get; }
        public string ImagesDirectory { get; }
        public string LogsDirectory { get; }

        public WpfPathProvider()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            ImagesDirectory = Path.Combine(baseDir, "Images");
            LogsDirectory = Path.Combine(baseDir, "Logs");
        }
    }
}
