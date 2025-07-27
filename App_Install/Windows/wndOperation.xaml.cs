using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace App_Install.Windows
{
    /// <summary>
    /// Interaction logic for wndOperation.xaml. This window serves as the initial entry point
    /// for the ProfMix installer. Its primary function is to detect whether ProfMix is
    /// already installed on the system and, based on that detection, present the user with
    /// appropriate options such as installing, updating, launching, or uninstalling the application.
    /// </summary>
    public partial class wndOperation : Window
    {
        /// <summary>
        /// Defines the standard registry key path where ProfMix installation information is stored.
        /// </summary>
        private const string PROFMIX_REGISTRY_KEY = @"SOFTWARE\ProfMix";

        /// <summary>
        /// Defines the executable file name for the ProfMix application.
        /// </summary>
        private const string PROFMIX_EXE_NAME = "ProfMix.exe";

        /// <summary>
        /// Defines a common default installation directory for ProfMix.
        /// This is used as one of the paths to check for existing installations.
        /// </summary>
        private const string PROFMIX_INSTALL_DIR = @"C:\Program Files\ProfMix";

        /// <summary>
        /// Stores the file path of the detected ProfMix installation, if found.
        /// This path is used for launching or uninstalling the application.
        /// </summary>
        private string detectedInstallPath;

        /// <summary>
        /// Stores the version of the detected ProfMix installation, if found.
        /// This helps in determining if an update is available or simply to inform the user.
        /// </summary>
        private Version detectedVersion;

        /// <summary>
        /// A boolean flag indicating whether ProfMix is detected as installed on the system.
        /// </summary>
        private bool isInstalled;

        /// <summary>
        /// Initializes a new instance of the <see cref="wndOperation"/> class.
        /// This constructor sets up the UI components and subscribes to the window's Loaded event
        /// to initiate the installation detection process once the window is ready.
        /// </summary>
        public wndOperation()
        {
            InitializeComponent();
            // Subscribes the OnWindowLoaded method to be called when the window is fully loaded.
            Loaded += OnWindowLoaded;
        }

        /// <summary>
        /// Event handler for when the window has fully loaded.
        /// This method introduces a brief visual pause before initiating the asynchronous
        /// detection of the ProfMix installation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(1000); // Pauses execution for 1 second, providing a smooth visual transition.
            await DetectProfMixInstallation(); // Starts the asynchronous detection process.
        }

        /// <summary>
        /// Asynchronously detects whether ProfMix is already installed on the system.
        /// It checks both Windows Registry entries and common file system locations.
        /// Based on the detection result, it updates the UI to show relevant status and action buttons.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous detection operation.</returns>
        private async Task DetectProfMixInstallation()
        {
            try
            {
                // Updates the UI to show that the detection process has started.
                StatusText.Text = "Scanning system for ProfMix...";
                StatusDetails.Text = "Checking registry and file system...";

                await Task.Delay(1500); // Simulates the time taken for system scanning.

                // Attempts to find installation information in the Windows Registry.
                bool registryFound = CheckRegistry();

                // Attempts to find installation files in common file system locations.
                bool fileSystemFound = CheckFileSystem();

                // If either registry entries or files are found, ProfMix is considered installed.
                if (registryFound || fileSystemFound)
                {
                    isInstalled = true;
                    await ShowInstalledStatus(); // Updates UI to show installed state and options.
                }
                else
                {
                    isInstalled = false;
                    await ShowNotInstalledStatus(); // Updates UI to show not installed state and options.
                }
            }
            catch (Exception ex)
            {
                // Catches any errors during the detection process and updates the UI with an error message.
                StatusText.Text = "Detection Error";
                StatusDetails.Text = $"Error during detection: {ex.Message}";
                ShowInstallOption(); // As a fallback, always offer the install option if detection fails.
            }
        }

        /// <summary>
        /// Checks the Windows Registry for existing ProfMix installation entries.
        /// It looks in both <c>HKEY_LOCAL_MACHINE</c> (for all users) and
        /// <c>HKEY_CURRENT_USER</c> (for current user) hives for custom application data
        /// and entries in the "Uninstall" key.
        /// </summary>
        /// <returns>
        /// <c>true</c> if a valid ProfMix installation is detected via the registry;
        /// otherwise, <c>false</c>.
        /// </returns>
        private bool CheckRegistry()
        {
            try
            {
                // Defines the base registry hives to search: system-wide and user-specific.
                RegistryKey[] baseKeys = { Registry.LocalMachine, Registry.CurrentUser };

                // Iterates through each base key.
                foreach (var baseKey in baseKeys)
                {
                    // Attempts to open the custom ProfMix registry key.
                    using (var key = baseKey.OpenSubKey(PROFMIX_REGISTRY_KEY))
                    {
                        if (key != null) // If the custom key exists.
                        {
                            // Retrieves the installation path and version string.
                            detectedInstallPath = key.GetValue("InstallPath") as string;
                            var versionString = key.GetValue("Version") as string;

                            // Attempts to parse the version string and validates the install path.
                            if (Version.TryParse(versionString, out detectedVersion) &&
                                !string.IsNullOrEmpty(detectedInstallPath) &&
                                Directory.Exists(detectedInstallPath))
                            {
                                return true; // Found a valid installation.
                            }
                        }
                    }

                    // Also checks the standard "Uninstall" registry key where applications register themselves.
                    string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + "ProfMix";
                    using (var key = baseKey.OpenSubKey(uninstallKey))
                    {
                        if (key != null) // If the uninstall entry exists.
                        {
                            // Retrieves the installation location and display version.
                            detectedInstallPath = key.GetValue("InstallLocation") as string;
                            var versionString = key.GetValue("DisplayVersion") as string;

                            // Attempts to parse the version and validates the install path.
                            if (Version.TryParse(versionString, out detectedVersion) &&
                                !string.IsNullOrEmpty(detectedInstallPath) &&
                                Directory.Exists(detectedInstallPath))
                            {
                                return true; // Found a valid installation via uninstall entry.
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Catches any exceptions that occur during registry access (e.g., permission issues).
                // These errors are typically suppressed as file system checks can still provide detection.
            }

            return false; // No installation detected in the registry.
        }

        /// <summary>
        /// Checks common file system directories for the presence of the ProfMix executable.
        /// This serves as a fallback detection method if registry checks are inconclusive or fail.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the ProfMix executable is found in a common installation path;
        /// otherwise, <c>false</c>.
        /// </returns>
        private bool CheckFileSystem()
        {
            // Defines a list of common potential installation paths.
            string[] possiblePaths = {
                // System-wide Program Files paths
                PROFMIX_INSTALL_DIR,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ProfMix"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ProfMix"),
                
                // User-specific application data paths
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProfMix"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProfMix"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ProfMix"),
                
                // Portable installation in common user locations
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ProfMix"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ProfMix")
            };

            // Iterates through each possible path.
            foreach (var path in possiblePaths)
            {
                // Constructs the full path to the ProfMix executable.
                string exePath = Path.Combine(path, PROFMIX_EXE_NAME);
                // Checks if the executable file exists at the current path.
                if (File.Exists(exePath))
                {
                    detectedInstallPath = path; // Stores the detected installation path.

                    // Attempts to retrieve the product version from the executable's file properties.
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                        if (Version.TryParse(versionInfo.ProductVersion, out detectedVersion))
                        {
                            return true; // Returns true if version is successfully parsed.
                        }
                    }
                    catch (Exception)
                    {
                        // Continues even if version detection fails, as the presence of the EXE confirms installation.
                    }

                    return true; // Returns true if the executable is found, even if version is unknown.
                }
            }

            return false; // No installation found in any of the checked file system paths.
        }

        /// <summary>
        /// Updates the UI to reflect that ProfMix is detected as installed.
        /// It displays the detected version and installation path and makes relevant action buttons
        /// (Launch, Update, Uninstall) visible to the user.
        /// </summary>
        /// <returns>A <see cref="Task"/> for asynchronous operation.</returns>
        private async Task ShowInstalledStatus()
        {
            // Updates the main status text.
            StatusText.Text = "ProfMix is installed! 🎉";

            // Formats the version text; "Version unknown" if not detected.
            string versionText = detectedVersion != null ?
                $"Version {detectedVersion}" : "Version unknown";

            // Updates the detailed status text with version and path.
            StatusDetails.Text = $"{versionText}\nInstalled at: {detectedInstallPath}";

            await Task.Delay(1000); // Pauses for a brief moment for visual emphasis.

            // Makes the action buttons for an installed application visible.
            BtnLaunch.Visibility = Visibility.Visible;
            BtnUpdate.Visibility = Visibility.Visible;
            BtnUninstall.Visibility = Visibility.Visible;
            ActionPanel.Visibility = Visibility.Visible; // Ensures the panel containing these buttons is visible.
        }

        /// <summary>
        /// Updates the UI to reflect that ProfMix is not detected as installed.
        /// It informs the user and prepares the UI to offer the installation option.
        /// </summary>
        /// <returns>A <see cref="Task"/> for asynchronous operation.</returns>
        private async Task ShowNotInstalledStatus()
        {
            // Updates the main status text.
            StatusText.Text = "ProfMix not found";
            // Updates the detailed status text.
            StatusDetails.Text = "ProfMix is not currently installed on this system.\nReady to install the latest version!";

            await Task.Delay(1000); // Pauses for a brief moment.

            ShowInstallOption(); // Makes the "Install" button visible.
        }

        /// <summary>
        /// Makes the "Install" button visible, preparing the UI for a new installation.
        /// </summary>
        private void ShowInstallOption()
        {
            BtnInstall.Visibility = Visibility.Visible; // Shows the "Install" button.
            ActionPanel.Visibility = Visibility.Visible; // Ensures the action panel is visible.
        }

        #region Button Event Handlers

        /// <summary>
        /// Event handler for the "Install" button click.
        /// This opens a new <see cref="wndInstall"/> window, which handles the actual
        /// installation process, and then closes the current operation window.
        /// </summary>
        /// <param name="sender">The source of the event (BtnInstall).</param>
        /// <param name="e">Event arguments.</param>
        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            // Creates a new instance of the dedicated installer window.
            var installWindow = new wndInstall();
            installWindow.Show(); // Displays the installer window.
            Close(); // Closes the current operation window.
        }

        /// <summary>
        /// Event handler for the "Update" button click.
        /// It prompts the user for confirmation before proceeding to open the installer window,
        /// which would then handle updating the existing ProfMix installation.
        /// </summary>
        /// <param name="sender">The source of the event (BtnUpdate).</param>
        /// <param name="e">Event arguments.</param>
        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            // Asks the user for confirmation before proceeding with the update.
            var result = MessageBox.Show(
                "This will update your existing ProfMix installation. Continue?",
                "Update ProfMix",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            // If the user confirms to proceed with the update.
            if (result == MessageBoxResult.Yes)
            {
                // Creates and displays a new instance of the installer window.
                // In a more complex scenario, you might pass a parameter to 'wndInstall'
                // to indicate it's an update operation and potentially pre-fill paths.
                var installWindow = new wndInstall();
                installWindow.Show();
                Close(); // Closes the current operation window.
            }
        }

        /// <summary>
        /// Event handler for the "Launch" button click.
        /// This attempts to start the detected ProfMix application executable.
        /// If successful, it closes the current installer window. If the executable is not found
        /// or an error occurs during launch, it displays an error message to the user.
        /// </summary>
        /// <param name="sender">The source of the event (BtnLaunch).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Constructs the full path to the detected ProfMix executable.
                string exePath = Path.Combine(detectedInstallPath, PROFMIX_EXE_NAME);
                // Checks if the executable file exists at the path.
                if (File.Exists(exePath))
                {
                    Process.Start(exePath); // Starts the application as a new process.
                    Close(); // Closes the installer window after launching the application.
                }
                else
                {
                    // Informs the user if the executable cannot be found, suggesting a corrupted installation.
                    MessageBox.Show(
                        "ProfMix executable not found. The installation may be corrupted.",
                        "Launch Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                // Catches and displays any general errors that occur during the launch attempt.
                MessageBox.Show(
                    $"Error launching ProfMix: {ex.Message}",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Event handler for the "Uninstall" button click.
        /// It prompts the user for confirmation before opening a new <see cref="wndUninstall"/> window,
        /// which is responsible for guiding the user through the uninstallation process.
        /// </summary>
        /// <param name="sender">The source of the event (BtnUninstall).</param>
        /// <param name="e">Event arguments.</param>
        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            // Asks the user for confirmation before initiating uninstallation.
            var result = MessageBox.Show(
                "This will open the ProfMix uninstaller. Continue?",
                "Uninstall ProfMix",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // If the user confirms to proceed with uninstallation.
            if (result == MessageBoxResult.Yes)
            {
                // Creates a new instance of the dedicated uninstaller window.
                var uninstallWindow = new wndUninstall();
                uninstallWindow.Show(); // Displays the uninstaller window.
                Close(); // Closes the current operation window.
            }
        }

        /// <summary>
        /// Event handler for the "Close" button click.
        /// This method simply closes the current operation window.
        /// </summary>
        /// <param name="sender">The source of the event (BtnClose).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close(); // Closes the window.
        }

        #endregion

        #region Window Management

        /// <summary>
        /// Overrides the default behavior for the left mouse button down event on the window.
        /// This allows the user to drag and move the window by clicking and holding
        /// anywhere within its client area.
        /// </summary>
        /// <param name="e">The <see cref="System.Windows.Input.MouseButtonEventArgs"/> that contains the event data.</param>
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e); // Calls the base class implementation.
            DragMove(); // Enables dragging of the window.
        }

        #endregion
    }
}