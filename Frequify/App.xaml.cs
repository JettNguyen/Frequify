using System.Windows;
using Frequify.ViewModels;
using System.IO;
using System.Text;

namespace Frequify
{

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	private MainViewModel? _mainViewModel;
	private static readonly object LogLock = new();

	/// <summary>
	/// Gets the diagnostics log path for startup and runtime exceptions.
	/// </summary>
	public static string LogPath => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"Frequify",
		"logs",
		"startup.log");

	/// <inheritdoc />
	protected override void OnStartup(StartupEventArgs e)
	{
		RegisterExceptionHandlers();
		Log("Application startup begin.");

		try
		{
			base.OnStartup(e);
			_mainViewModel = new MainViewModel();
			var window = new MainWindow(_mainViewModel);
			MainWindow = window;
			window.Show();
			Log("Main window shown successfully.");
		}
		catch (Exception ex)
		{
			LogException("Startup failure", ex);
			MessageBox.Show(
				$"Frequify could not open.\n\n{ex.Message}\n\nSee log:\n{LogPath}",
				"Frequify Startup Error",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			Shutdown(1);
		}
	}

	/// <inheritdoc />
	protected override void OnExit(ExitEventArgs e)
	{
		_mainViewModel?.Dispose();
		Log($"Application exit code: {e.ApplicationExitCode}");
		base.OnExit(e);
	}

	private void RegisterExceptionHandlers()
	{
		DispatcherUnhandledException += (_, args) =>
		{
			LogException("DispatcherUnhandledException", args.Exception);
			args.Handled = false;
		};

		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			if (args.ExceptionObject is Exception ex)
			{
				LogException("AppDomain.UnhandledException", ex);
			}
			else
			{
				Log($"Unhandled exception object: {args.ExceptionObject}");
			}
		};

		TaskScheduler.UnobservedTaskException += (_, args) =>
		{
			LogException("TaskScheduler.UnobservedTaskException", args.Exception);
			args.SetObserved();
		};
	}

	private static void LogException(string context, Exception exception)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {context}");
		sb.AppendLine(exception.ToString());
		sb.AppendLine(new string('-', 80));
		Log(sb.ToString());
	}

	private static void Log(string message)
	{
		try
		{
			lock (LogLock)
			{
				var directory = Path.GetDirectoryName(LogPath);
				if (!string.IsNullOrWhiteSpace(directory))
				{
					Directory.CreateDirectory(directory);
				}

				File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
			}
		}
		catch
		{
		}
	}
}
}

