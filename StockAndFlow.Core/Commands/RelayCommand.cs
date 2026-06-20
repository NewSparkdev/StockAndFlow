using System;
using System.Windows.Input;

namespace StockAndFlow.Commands
{
    /// <summary>
    /// An ICommand whose CanExecute state can be re-evaluated on demand.
    /// <see cref="ViewModelBase"/> calls this for every command when a property changes,
    /// replacing WPF's CommandManager.RequerySuggested in a cross-platform way.
    /// </summary>
    public interface IRelayCommand : ICommand
    {
        void RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Portable ICommand. Unlike the previous WPF-only version it does not use CommandManager;
    /// call <see cref="RaiseCanExecuteChanged"/> to re-query CanExecute. Works on WPF and MAUI.
    /// </summary>
    public class RelayCommand : IRelayCommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class RelayCommand<T> : IRelayCommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute((T?)parameter);
        }

        public void Execute(object? parameter)
        {
            _execute((T?)parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
