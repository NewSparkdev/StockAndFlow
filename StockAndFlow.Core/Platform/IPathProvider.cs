namespace StockAndFlow.Platform
{
    /// <summary>
    /// Resolves writable storage locations per platform. WPF uses folders next to the executable;
    /// MAUI uses <c>FileSystem.AppDataDirectory</c>.
    /// </summary>
    public interface IPathProvider
    {
        /// <summary>Directory holding the SQLite database and settings.json.</summary>
        string DataDirectory { get; }

        /// <summary>Directory holding product/logo/receipt images.</summary>
        string ImagesDirectory { get; }

        /// <summary>Directory holding rolling log files.</summary>
        string LogsDirectory { get; }
    }
}
