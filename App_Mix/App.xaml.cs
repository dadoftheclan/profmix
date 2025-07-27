using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; // For Keyboard and KeyEventArgs.
using System.Windows.Threading; // For Dispatcher and DispatcherUnhandledExceptionEventArgs.
using App_Mix.Systems; // Assuming sysState is defined here for exception handling.
using App_Mix.Windows; // Assuming wndSplash and wndMix are defined here.

namespace App_Mix
{
    /// <summary>
    /// Represents the main application class for the App_Mix application.
    /// This class inherits from <see cref="Application"/> and is responsible for:
    /// 1. Centralized exception handling across UI, non-UI, and asynchronous tasks.
    /// 2. Managing the application's startup sequence, including displaying a splash screen.
    /// 3. Registering global event handlers (e.g., for keyboard shortcuts).
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// This constructor sets up global exception handlers to catch unhandled exceptions
        /// from various parts of the application, ensuring a more robust error reporting mechanism.
        /// </summary>
        public App()
        {
            // Subscribes to the event for unhandled exceptions occurring on the UI thread.
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Subscribes to the event for unhandled exceptions occurring in any AppDomain.
            // This catches exceptions from non-UI threads and general application errors.
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledDomainException;

            // Subscribes to the event for unobserved exceptions from tasks.
            // This is crucial for catching exceptions in asynchronous operations that are not awaited.
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        /// <summary>
        /// Overrides the <see cref="Application.OnStartup"/> method.
        /// This method is called when the application starts up. It registers a global
        /// keyboard event listener for debugging purposes.
        /// </summary>
        /// <param name="e">Event arguments containing startup information.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e); // Call the base class's OnStartup method.

            // Registers a global preview key-down event handler for all Window instances.
            // This allows the application to intercept keyboard events before they are handled by specific controls.
            EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnGlobalKeyDown));
        }

        /// <summary>
        /// Event handler for the application's Startup event (defined in XAML).
        /// This asynchronous method manages the application's initial startup sequence:
        /// displaying a splash screen, waiting for a short period, closing the splash screen,
        /// and then opening the main application window. It includes error handling for the startup process.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Set the shutdown mode to explicit shutdown. This prevents the application from
                // shutting down automatically when the splash screen closes.
                this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Create and display the splash screen window.
                var splash = new Windows.wndSplash();
                splash.Show();

                // Introduce a delay to allow the splash screen to be visible for a moment.
                await Task.Delay(11000); // 2-second delay.

                splash.Close(); // Close the splash screen.

                // Create and set the main application window.
                var mainWindow = new Windows.wndMix();
                this.MainWindow = mainWindow; // Assign the main window.
                mainWindow.Show(); // Display the main window.

                // Change the shutdown mode back to closing when the main window closes.
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                // If any exception occurs during the startup sequence, handle it as a critical exception.
                Systems.sysState.HandleCriticalException("Startup failure", ex);
                Shutdown(); // Shut down the application gracefully after handling the error.
            }
        }

        /// <summary>
        /// Global keyboard event handler for debugging purposes.
        /// If the user presses CTRL + SHIFT + 9, it simulates an unhandled UI exception
        /// to test the application's error handling mechanism.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing key press details.</param>
        private void OnGlobalKeyDown(object sender, KeyEventArgs e)
        {
            // Check for the specific key combination: Ctrl + Shift + 9.
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) &&
                e.Key == Key.D9)
            {
                // Use Dispatcher.BeginInvoke to throw the exception on the UI thread.
                // This ensures it's caught by OnDispatcherUnhandledException.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    throw new InvalidOperationException("Simulated crash via CTRL + SHIFT + 9");
                }));
                e.Handled = true; // Mark the event as handled to prevent further processing.
            }
        }

        /// <summary>
        /// Event handler for unhandled exceptions that occur in any AppDomain.
        /// This catches exceptions from background threads or general application logic not tied to the UI dispatcher.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing the exception object.</param>
        private void OnUnhandledDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            // Cast the exception object to an Exception type, or create a generic one if casting fails.
            var ex = e.ExceptionObject as Exception ?? new Exception("Unknown domain exception");
            // Delegate the exception handling to the centralized sysState system.
            Systems.sysState.HandleCriticalException("Unhandled domain exception", ex);
            // Exit the environment with a non-zero exit code to indicate an error.
            Environment.Exit(1);
        }

        /// <summary>
        /// Event handler for unobserved exceptions from asynchronous tasks.
        /// If a Task throws an exception and that exception is not 'awaited' or handled,
        /// it becomes an unobserved exception. This handler catches such exceptions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing the unobserved exception.</param>
        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Delegate the exception handling to the centralized sysState system.
            Systems.sysState.HandleCriticalException("Unobserved task exception", e.Exception);
            // Mark the exception as observed to prevent the application from crashing.
            e.SetObserved();
        }

        /// <summary>
        /// Event handler for unhandled exceptions that occur on the UI thread.
        /// This is the primary catch-all for exceptions originating from UI interactions or UI-bound logic.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments containing the unhandled exception and a flag to mark it as handled.</param>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Delegate the exception handling to the centralized sysState system.
            Systems.sysState.HandleCriticalException("Unhandled UI exception", e.Exception);
            e.Handled = true; // Mark the exception as handled to prevent the application from terminating immediately.
            Shutdown(); // Gracefully shut down the application after handling the error.
        }
    }
}
