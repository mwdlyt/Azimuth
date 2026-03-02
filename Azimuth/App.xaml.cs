using System.Windows;
using System.Windows.Threading;

namespace Azimuth;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch unhandled exceptions on the UI thread
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Catch unhandled exceptions on background threads
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Catch unobserved task exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred. Your work may not have been saved.\n\nPlease restart Azimuth.",
            "Azimuth Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "A fatal error occurred. Azimuth needs to close.\n\nPlease restart the application.",
            "Azimuth Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Prevent unobserved task exceptions from crashing the process
        e.SetObserved();
    }
}
