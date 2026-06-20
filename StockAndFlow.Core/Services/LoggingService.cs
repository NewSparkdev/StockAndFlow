using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace StockAndFlow.Services
{
    /// <summary>
    /// Centralized logging configuration and management using Serilog.
    /// Provides structured logging with file rotation and multiple log levels.
    /// </summary>
    public static class LoggingService
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// Initializes the logging system with file and debug output.
        /// </summary>
        /// <param name="logDirectory">Directory for log files. Defaults to "Logs" in app directory.</param>
        public static void Initialize(string? logDirectory = null)
        {
            if (_isInitialized)
                return;

            try
            {
                // Determine log directory
                if (string.IsNullOrEmpty(logDirectory))
                {
                    logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                }

                // Ensure log directory exists
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Configure Serilog
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .Enrich.WithThreadId()
                    .WriteTo.Debug(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: Path.Combine(logDirectory, "stockandflow-.log"),
                        rollingInterval: RollingInterval.Day,
                        fileSizeLimitBytes: 10_485_760, // 10 MB
                        retainedFileCountLimit: 30,      // Keep 30 days
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                        shared: true)
                    .CreateLogger();

                _isInitialized = true;

                Log.Information("========================================");
                Log.Information("Stock & Flow Application Started");
                Log.Information("Version: {Version}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
                Log.Information("Log Directory: {LogDirectory}", logDirectory);
                Log.Information("========================================");
            }
            catch (Exception ex)
            {
                // Fallback to console if logging setup fails
                Console.WriteLine($"Failed to initialize logging: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Closes and flushes the logging system. Call this on application shutdown.
        /// </summary>
        public static void Shutdown()
        {
            if (_isInitialized)
            {
                Log.Information("========================================");
                Log.Information("Stock & Flow Application Shutting Down");
                Log.Information("========================================");
                Log.CloseAndFlush();
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Gets the current logger instance. Initialize() must be called first.
        /// </summary>
        public static ILogger Logger
        {
            get
            {
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("LoggingService has not been initialized. Call Initialize() first.");
                }
                return Log.Logger;
            }
        }
    }
}
