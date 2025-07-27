using System;
using System.Diagnostics; // For Process.Start to open files.
using System.IO; // For File.Exists to check log file presence.
using System.Windows; // Core WPF classes like Window, MessageBox, Clipboard, Application.
using System.Windows.Input; // Not directly used in the provided snippet, but often for keyboard/mouse input.

namespace App_Mix.Windows
{
    /// <summary>
    /// Interaction logic for wndException.xaml. This window is designed to display
    /// details of unhandled exceptions or critical errors that occur within the application.
    /// It provides options to copy error details, open the crash log file, restart the application, or exit.
    /// </summary>
    public partial class wndException : Window
    {
        /// <summary>
        /// Stores the detailed error message or exception string to be displayed.
        /// </summary>
        private string crashDetails;

        /// <summary>
        /// Stores the file path to the detailed crash log, allowing the user to open it.
        /// </summary>
        private string crashLogPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="wndException"/> class.
        /// </summary>
        /// <param name="details">A string containing the detailed exception information (e.g., stack trace).</param>
        /// <param name="crashLog">Optional: The file path to the crash log, if available.</param>
        public wndException(string details, string crashLog = null)
        {
            InitializeComponent(); // Initializes the WPF UI components.

            crashDetails = details; // Assigns the provided error details.
            crashLogPath = crashLog; // Assigns the provided crash log path.

            // Sets the text of the TextBlock control (TxtErrorDetails) to display the crash details.
            TxtErrorDetails.Text = crashDetails;
        }

        /// <summary>
        /// Event handler for the "Copy Details" button click.
        /// Copies the full crash details to the system clipboard.
        /// </summary>
        /// <param name="sender">The source of the event (BtnCopy).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(crashDetails); // Puts the crash details string onto the clipboard.
            // Informs the user that the details have been copied.
            MessageBox.Show("Error details copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Event handler for the "Open Log" button click.
        /// Attempts to open the crash log file using Notepad (or the default text editor).
        /// Displays a warning if the log file path is invalid or the file does not exist.
        /// </summary>
        /// <param name="sender">The source of the event (BtnOpenLog).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            // Check if the crash log path is valid and the file exists.
            if (!string.IsNullOrEmpty(crashLogPath) && File.Exists(crashLogPath))
            {
                // Use Process.Start to open the file with the default associated application (e.g., Notepad).
                Process.Start("notepad.exe", crashLogPath);
            }
            else
            {
                // Inform the user if the crash log file cannot be found.
                MessageBox.Show("Crash log not found.", "Missing File", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Event handler for the "Restart Application" button click.
        /// Attempts to restart the current application instance.
        /// Displays an error message if the restart fails.
        /// Finally, shuts down the current application instance.
        /// </summary>
        /// <param name="sender">The source of the event (BtnRestart).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Starts a new process using the executable path of the current application.
                Process.Start(Application.ResourceAssembly.Location);
            }
            catch (Exception ex)
            {
                // Display an error if the application fails to restart.
                MessageBox.Show($"Failed to restart application: {ex.Message}", "Restart Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // Shuts down the current application instance, regardless of restart success.
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Event handler for the "Exit Application" button click.
        /// Shuts down the entire application.
        /// </summary>
        /// <param name="sender">The source of the event (BtnExit).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown(); // Terminates the application.
        }
    }
}
