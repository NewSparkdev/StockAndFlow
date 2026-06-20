using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Serilog;
using StockAndFlow.Commands;

namespace StockAndFlow.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Logger instance for this ViewModel. Automatically uses the derived class name.
        /// </summary>
        protected ILogger Log { get; }

        private bool _disposed;
        private PropertyInfo[]? _commandProperties;

        protected ViewModelBase()
        {
            // Create a logger context with the ViewModel's type name
            Log = Serilog.Log.ForContext(GetType());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Re-evaluate every command's CanExecute when state changes. This replaces WPF's
            // CommandManager.RequerySuggested (which the portable RelayCommand no longer uses) so
            // that command-bound controls (e.g. Save buttons gated on IsValid) enable/disable
            // correctly on both WPF and MAUI.
            RaiseCommandsCanExecuteChanged();
        }

        private void RaiseCommandsCanExecuteChanged()
        {
            // Properties are typically declared as ICommand; select those and narrow on the
            // runtime value below (an IRelayCommand can be re-queried, a plain ICommand cannot).
            _commandProperties ??= GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(System.Windows.Input.ICommand).IsAssignableFrom(p.PropertyType))
                .ToArray();

            foreach (var prop in _commandProperties)
            {
                if (prop.GetValue(this) is IRelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Helper method to log and handle exceptions in ViewModels.
        /// </summary>
        protected void LogError(Exception ex, string message, params object[] propertyValues)
        {
            Log.Error(ex, message, propertyValues);
        }

        /// <summary>
        /// Helper method to log warnings in ViewModels.
        /// </summary>
        protected void LogWarning(string message, params object[] propertyValues)
        {
            Log.Warning(message, propertyValues);
        }

        /// <summary>
        /// Helper method to log information in ViewModels.
        /// </summary>
        protected void LogInformation(string message, params object[] propertyValues)
        {
            Log.Information(message, propertyValues);
        }

        /// <summary>
        /// Override this method in derived classes to clean up resources, unsubscribe from events, etc.
        /// Always call base.Dispose() at the end of your override.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    Log.Debug("{ViewModelType} disposing", GetType().Name);
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
