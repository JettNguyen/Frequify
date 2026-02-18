using System.Windows.Input;

namespace Frequify.Infrastructure;

/// <summary>
/// Defines an asynchronous command with optional execution guard.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    /// <summary>
    /// Initializes a new asynchronous command instance.
    /// </summary>
    /// <param name="execute">Asynchronous command action.</param>
    /// <param name="canExecute">Optional execution predicate.</param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    /// <inheritdoc/>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            await _execute().ConfigureAwait(true);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Triggers a can-execute re-evaluation.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
