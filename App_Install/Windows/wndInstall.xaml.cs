using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace App_Install.Windows
{
    /// <summary>
    /// Interaction logic for wndInstall.xaml. This window serves as the installer
    /// for the ProfMix application, handling the extraction of embedded application files,
    /// creation of shortcuts, and registration of the application within Windows.
    /// </summary>
    public partial class wndInstall : Window
    {
        /// <summary>
        /// Defines the constant name for the application.
        /// </summary>
        private const string APP_NAME = "ProfMix";

        /// <summary>
        /// Defines the constant executable file name for the application.
        /// </summary>
        private const string APP_EXE = "ProfMix.exe";

        /// <summary>
        /// Defines the constant icon file name for the application.
        /// </summary>
        private const string APP_ICON = "ProfMix_Icon.ico";

        /// <summary>
        /// Defines the constant registry key path for application-specific settings.
        /// </summary>
        private const string REGISTRY_KEY = @"SOFTWARE\ProfMix";

        /// <summary>
        /// Defines the embedded resource path for the zipped application deployment files.
        /// </summary>
        private const string DEPLOYMENT_RESOURCE = "App_Install.Resources.deployment.zip";

        /// <summary>
        /// Defines the embedded resource path for the application icon, used during installation.
        /// </summary>
        private const string ICON_RESOURCE = "App_Install.Resources.ProfMix_Icon_Reversed.ico";

        /// <summary>
        /// A flag indicating whether an installation process is currently in progress.
        /// Prevents multiple installations or improper closing during installation.
        /// </summary>
        private bool isInstalling = false;

        /// <summary>
        /// An instance of <see cref="BackgroundWorker"/> used to perform the installation
        /// in a separate thread, keeping the UI responsive.
        /// </summary>
        private BackgroundWorker installWorker;

        /// <summary>
        /// Initializes a new instance of the <see cref="wndInstall"/> class.
        /// This constructor sets up the UI components and prepares the installer for use.
        /// </summary>
        public wndInstall()
        {
            InitializeComponent();
            InitializeInstaller();
        }

        /// <summary>
        /// Extracts the application icon file from the embedded resources and saves it
        /// to the specified installation path. This icon is used for shortcuts and
        /// Windows registry entries.
        /// </summary>
        /// <param name="installPath">The directory where the application will be installed.</param>
        /// <exception cref="Exception">Thrown if the application icon resource cannot be found.</exception>
        private void ExtractIconFile(string installPath)
        {
            // Gets the assembly where the current code is running, which contains the embedded resources.
            var assembly = Assembly.GetExecutingAssembly();

            // Opens a stream to the embedded icon resource.
            using (var resourceStream = assembly.GetManifestResourceStream(ICON_RESOURCE))
            {
                // Checks if the resource stream was successfully opened.
                if (resourceStream == null)
                    throw new Exception("Application icon not found in installer resources.");

                // Constructs the full path for the icon file within the installation directory.
                string iconPath = Path.Combine(installPath, APP_ICON);
                // Creates a new file stream to write the icon data to the specified path.
                using (var fileStream = new FileStream(iconPath, FileMode.Create, FileAccess.Write))
                {
                    // Copies the content from the resource stream to the file stream.
                    resourceStream.CopyTo(fileStream);
                }
            }
        }

        /// <summary>
        /// Initializes the installer's internal state and UI elements.
        /// This includes setting the default installation path based on user privileges
        /// and configuring the <see cref="BackgroundWorker"/> for asynchronous operations.
        /// </summary>
        private void InitializeInstaller()
        {
            // Determines the default installation path.
            string defaultPath;
            if (IsRunningAsAdmin())
            {
                // If running as administrator, suggest installation to Program Files (for all users).
                defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    APP_NAME);
            }
            else
            {
                // If not running as administrator, suggest installation to the user's Local AppData (user-specific).
                defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    APP_NAME);
            }

            // Sets the determined default path to the installation path text box in the UI.
            TxtInstallPath.Text = defaultPath;

            // Initializes the BackgroundWorker instance.
            installWorker = new BackgroundWorker
            {
                // Enables the worker to report progress updates.
                WorkerReportsProgress = true,
                // Enables the worker to be cancelled during its operation.
                WorkerSupportsCancellation = true
            };

            // Subscribes event handlers for the worker's lifecycle events.
            installWorker.DoWork += InstallWorker_DoWork; // The long-running installation logic.
            installWorker.ProgressChanged += InstallWorker_ProgressChanged; // Updates to UI during progress.
            installWorker.RunWorkerCompleted += InstallWorker_RunWorkerCompleted; // Actions after completion (success, error, cancelled).
        }

        #region Event Handlers

        /// <summary>
        /// Event handler for the "Browse" button click.
        /// Opens a <see cref="FolderBrowserDialog"/> to allow the user to select a custom
        /// installation directory. The selected path is then updated in the UI.
        /// </summary>
        /// <param name="sender">The source of the event (BtnBrowsePath).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnBrowsePath_Click(object sender, RoutedEventArgs e)
        {
            // Creates a new FolderBrowserDialog instance.
            using (var dialog = new FolderBrowserDialog())
            {
                // Sets the descriptive text for the dialog window.
                dialog.Description = "Select installation directory for ProfMix";
                // Sets the initial selected path in the dialog to the current text box value.
                dialog.SelectedPath = TxtInstallPath.Text;

                // Displays the dialog and checks if the user clicked OK.
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // If OK is clicked, updates the text box with the chosen path, appending the app name.
                    TxtInstallPath.Text = Path.Combine(dialog.SelectedPath, APP_NAME);
                }
            }
        }

        /// <summary>
        /// Event handler for the "Install" button click.
        /// This method validates the selected installation path, handles existing directories,
        /// checks write permissions, and then initiates the asynchronous installation process.
        /// </summary>
        /// <param name="sender">The source of the event (BtnInstall).</param>
        /// <param name="e">Event arguments.</param>
        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            // Prevents multiple installation attempts if one is already in progress.
            if (isInstalling) return;

            // Retrieves and trims the installation path from the text box.
            string installPath = TxtInstallPath.Text.Trim();
            // Validates that an installation path has been specified.
            if (string.IsNullOrEmpty(installPath))
            {
                MessageBox.Show("Please specify an installation directory.", "Invalid Path",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Aborts the installation process.
            }

            // Checks if the chosen directory already exists and contains files.
            if (Directory.Exists(installPath) && Directory.GetFiles(installPath).Length > 0)
            {
                // Prompts the user for confirmation if the directory is not empty.
                var result = MessageBox.Show(
                    $"The directory '{installPath}' already exists and contains files. Continue anyway?",
                    "Directory Exists",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // If the user chooses not to continue, aborts the installation.
                if (result == MessageBoxResult.No)
                    return;
            }

            // Checks if the application has write permissions to the selected path.
            if (!CanWriteToDirectory(installPath))
            {
                // If permissions are denied, prompts the user to switch to a user-specific directory.
                var result = MessageBox.Show(
                    $"You don't have permission to write to '{installPath}'.\n\nWould you like to install to your user directory instead?",
                    "Permission Denied",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                // If the user agrees to change the path.
                if (result == MessageBoxResult.Yes)
                {
                    // Sets the installation path to the user's Local AppData directory.
                    string userPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        APP_NAME);
                    TxtInstallPath.Text = userPath;

                    // Informs the user about the updated installation path.
                    MessageBox.Show(
                        $"Installation path changed to:\n{userPath}",
                        "Path Updated",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return; // Returns, allowing the user to click "Install" again with the new path.
                }
                else
                {
                    // If the user declines, aborts the installation.
                    return;
                }
            }

            // If all checks pass, starts the installation process asynchronously.
            await StartInstallation();
        }

        /// <summary>
        /// Event handler for the "Cancel" button click.
        /// If an installation is in progress, it prompts the user for confirmation
        /// before attempting to cancel the background worker. Otherwise, it simply closes the window.
        /// </summary>
        /// <param name="sender">The source of the event (BtnCancel).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Checks if an installation is currently active.
            if (isInstalling)
            {
                // Prompts the user to confirm cancellation of the ongoing installation.
                var result = MessageBox.Show(
                    "Installation is in progress. Are you sure you want to cancel?",
                    "Cancel Installation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // If the user confirms cancellation.
                if (result == MessageBoxResult.Yes)
                {
                    // Requests the background worker to cancel its operation.
                    installWorker?.CancelAsync();
                }
            }
            else
            {
                // If no installation is in progress, simply closes the window.
                Close();
            }
        }

        /// <summary>
        /// Event handler for the "Close" button click.
        /// This button is only enabled and functional when no installation is in progress,
        /// allowing the user to exit the installer.
        /// </summary>
        /// <param name="sender">The source of the event (BtnClose).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Allows closing only if no installation is running.
            if (!isInstalling)
                Close();
        }

        #endregion

        #region Installation Logic

        /// <summary>
        /// Initiates the main installation sequence.
        /// This method updates the UI to show the progress section, disables the close button,
        /// and starts the <see cref="BackgroundWorker"/> to perform the actual installation steps.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task StartInstallation()
        {
            // Sets the flag to indicate that installation has started.
            isInstalling = true;

            // Changes the UI visibility to show the progress bar and details.
            ActionButtons.Visibility = Visibility.Collapsed; // Hides the Install/Cancel/Close buttons.
            ProgressSection.Visibility = Visibility.Visible; // Shows the progress bar and status text.

            // Disables the close button to prevent interruption during installation.
            BtnClose.IsEnabled = false;

            // Prepares an InstallationData object with the current UI selections.
            var installData = new InstallationData
            {
                InstallPath = TxtInstallPath.Text.Trim(),
                CreateDesktopShortcut = ChkDesktopShortcut.IsChecked ?? false, // Handles null for IsChecked
                CreateStartMenuShortcut = ChkStartMenuShortcut.IsChecked ?? false
            };

            // Starts the background worker, passing the installation data as an argument.
            installWorker.RunWorkerAsync(installData);
        }

        /// <summary>
        /// The main method executed on the background worker thread.
        /// This method orchestrates the various steps of the application installation,
        /// including directory creation, file extraction, shortcut creation, and
        /// Windows registry registration. It reports progress and handles cancellations.
        /// </summary>
        /// <param name="sender">The source of the event (the <see cref="BackgroundWorker"/>).</param>
        /// <param name="e">Arguments for the background operation, including input data and result.</param>
        private void InstallWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Casts the sender to a BackgroundWorker to access its methods.
            var worker = sender as BackgroundWorker;
            // Retrieves the installation data passed from the main thread.
            var data = e.Argument as InstallationData;

            try
            {
                // Step 1: Create the target installation directory.
                worker?.ReportProgress(10, "Creating installation directory...");
                Directory.CreateDirectory(data.InstallPath);

                // Checks for cancellation request after creating the directory.
                if (worker?.CancellationPending == true)
                {
                    e.Cancel = true; // Sets the cancellation flag.
                    return; // Exits the DoWork method.
                }

                // Step 2: Extract application files from the embedded ZIP resource.
                worker?.ReportProgress(20, "Extracting application files...");
                ExtractDeploymentFiles(data.InstallPath, worker);

                // Step 2.5: Extract the application's icon file.
                worker?.ReportProgress(30, "Installing application icon...");
                ExtractIconFile(data.InstallPath);

                // Checks for cancellation request after icon extraction.
                if (worker?.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }

                // Step 2.7: Copy the installer executable to the install path to serve as an uninstaller.
                worker?.ReportProgress(35, "Installing uninstaller...");
                CopyInstallerAsUninstaller(data.InstallPath);

                // Checks for cancellation request after uninstaller creation.
                if (worker?.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }

                // Step 3: Create desktop and/or Start Menu shortcuts if selected by the user.
                if (data.CreateDesktopShortcut || data.CreateStartMenuShortcut)
                {
                    worker?.ReportProgress(60, "Creating shortcuts...");
                    CreateShortcuts(data);
                }

                // Checks for cancellation request after shortcut creation.
                if (worker?.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }

                // Step 4: Register the application in the Windows Registry for Add/Remove Programs.
                worker?.ReportProgress(80, "Registering application...");
                RegisterApplication(data);

                // Checks for cancellation request after registry registration.
                if (worker?.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }

                // Step 5: Final completion status.
                worker?.ReportProgress(100, "Installation completed successfully!");

                // Sets the result of the background operation to the installation data on success.
                e.Result = data;
            }
            catch (Exception ex)
            {
                // If any exception occurs during the installation, sets the result to the exception,
                // which will be handled in RunWorkerCompleted.
                e.Result = new Exception($"Installation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Copies the currently running installer executable to the specified installation
        /// directory, renaming it to "uninstall.exe". This allows the application to be
        /// uninstalled later by running this copied executable with an uninstall flag.
        /// </summary>
        /// <param name="installPath">The directory where the application is being installed.</param>
        /// <exception cref="Exception">Thrown if the uninstaller creation fails.</exception>
        private void CopyInstallerAsUninstaller(string installPath)
        {
            try
            {
                // Gets the full path of the current executable (the installer itself).
                string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
                // Constructs the desired path for the uninstaller within the installation directory.
                string uninstallerPath = Path.Combine(installPath, "uninstall.exe");

                // Copies the current executable to the uninstaller path. Overwrites if it exists.
                File.Copy(currentExePath, uninstallerPath, true);

                // Sets file attributes (optional) to ensure it's a normal file, not hidden or read-only.
                File.SetAttributes(uninstallerPath, FileAttributes.Normal);
            }
            catch (Exception ex)
            {
                // Wraps and re-throws the exception to be handled by the DoWork method.
                throw new Exception($"Failed to create uninstaller: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extracts all files from the embedded `deployment.zip` resource into the specified
        /// installation directory. It reports progress as files are extracted.
        /// </summary>
        /// <param name="installPath">The target directory for file extraction.</param>
        /// <param name="worker">The <see cref="BackgroundWorker"/> to report progress to.</param>
        /// <exception cref="Exception">Thrown if the deployment ZIP resource cannot be found.</exception>
        private void ExtractDeploymentFiles(string installPath, BackgroundWorker worker)
        {
            // Gets the assembly containing the embedded resources.
            var assembly = Assembly.GetExecutingAssembly();

            // Debugging line (can be removed in release) to list all embedded resources.
            // This is useful if the DEPLOYMENT_RESOURCE name is incorrect.
            var resources = assembly.GetManifestResourceNames();
            string debugList = string.Join("\n", resources); // For error message if resource not found.

            // Opens a stream to the embedded deployment ZIP resource.
            using (var resourceStream = assembly.GetManifestResourceStream(DEPLOYMENT_RESOURCE))
            {
                // Checks if the resource stream was successfully opened.
                if (resourceStream == null)
                {
                    throw new Exception($"Deployment package not found in installer resources.\n\nAvailable resources:\n{debugList}\n\nLooking for: {DEPLOYMENT_RESOURCE}");
                }

                // Creates a ZipArchive to read the contents of the ZIP file from the stream.
                using (var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read))
                {
                    int totalEntries = archive.Entries.Count; // Total number of files/directories in the zip.
                    int currentEntry = 0; // Counter for extracted entries.

                    // Iterates through each entry (file or directory) in the ZIP archive.
                    foreach (var entry in archive.Entries)
                    {
                        // Checks if a cancellation has been requested by the user.
                        if (worker?.CancellationPending == true)
                            return; // Exits the method if cancelled.

                        currentEntry++;
                        // Calculates the progress percentage (from 25% to 60% of total installation).
                        var progress = 25 + (int)((currentEntry / (double)totalEntries) * 35);
                        // Reports the current progress and the name of the file being extracted.
                        worker?.ReportProgress(progress, $"Extracting: {entry.Name}");

                        // Constructs the full destination path for the current entry.
                        string destinationPath = Path.Combine(installPath, entry.FullName);

                        // Extracts and creates the directory structure if the entry is a directory.
                        string directoryPath = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directoryPath))
                            Directory.CreateDirectory(directoryPath);

                        // Extracts the file content if the entry is a file (not a directory).
                        if (!string.IsNullOrEmpty(entry.Name)) // Entries representing directories have empty Name.
                        {
                            // Manually opens the entry stream and writes its content to a new file.
                            // This approach avoids requiring the System.IO.Compression.FileSystem assembly.
                            using (var entryStream = entry.Open())
                            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                            {
                                entryStream.CopyTo(fileStream); // Copies data from zip entry to disk.
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates desktop and/or Start Menu shortcuts for the installed application
        /// based on the user's selections in the installation data.
        /// </summary>
        /// <param name="data">The <see cref="InstallationData"/> containing shortcut preferences and install path.</param>
        private void CreateShortcuts(InstallationData data)
        {
            // Constructs the full path to the application's executable.
            string exePath = Path.Combine(data.InstallPath, APP_EXE);

            // Creates a desktop shortcut if the option is selected.
            if (data.CreateDesktopShortcut)
            {
                // Gets the path to the current user's desktop folder.
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                // Forms the complete path for the shortcut file (.lnk).
                string shortcutPath = Path.Combine(desktopPath, $"{APP_NAME}.lnk");
                // Calls a helper method to create the actual shortcut file.
                CreateShortcut(shortcutPath, exePath, data.InstallPath, "Professional Voice Over Mixer");
            }

            // Creates a Start Menu shortcut if the option is selected.
            if (data.CreateStartMenuShortcut)
            {
                // Gets the path to the current user's Start Menu programs folder.
                string startMenuPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    APP_NAME);
                // Ensures the application's subfolder in the Start Menu exists.
                Directory.CreateDirectory(startMenuPath);

                // Forms the complete path for the Start Menu shortcut file.
                string shortcutPath = Path.Combine(startMenuPath, $"{APP_NAME}.lnk");
                // Calls a helper method to create the actual shortcut file.
                CreateShortcut(shortcutPath, exePath, data.InstallPath, "Professional Voice Over Mixer");
            }
        }

        /// <summary>
        /// Creates a Windows shortcut (.lnk file) at the specified path, pointing to a target executable.
        /// This method utilizes the Windows Script Host Shell for shortcut creation.
        /// Errors during shortcut creation are caught and suppressed, as they are not critical enough
        /// to fail the entire installation.
        /// </summary>
        /// <param name="shortcutPath">The full path where the .lnk file will be created.</param>
        /// <param name="targetPath">The full path to the executable or file the shortcut will open.</param>
        /// <param name="workingDirectory">The working directory for the target application.</param>
        /// <param name="description">A descriptive text for the shortcut.</param>
        private void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
        {
            try
            {
                // Uses reflection to get the WScript.Shell COM object, which is used to create shortcuts.
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                // Creates a shortcut object representing the .lnk file.
                var shortcut = shell.CreateShortcut(shortcutPath);

                // Sets the properties of the shortcut.
                shortcut.TargetPath = targetPath; // The application executable.
                shortcut.WorkingDirectory = workingDirectory; // Where the application will run from.
                shortcut.Description = description; // Text displayed when hovering over the shortcut.
                shortcut.IconLocation = Path.Combine(workingDirectory, APP_ICON) + ",0"; // Path to the icon file and icon index.
                shortcut.Save(); // Saves the shortcut file to disk.
            }
            catch (Exception)
            {
                // Catches any exceptions during shortcut creation (e.g., permissions issues, COM object errors).
                // Errors are intentionally suppressed here as shortcut creation is not critical to the application's core functionality.
            }
        }

        /// <summary>
        /// Registers the application in the Windows Registry, specifically under the
        /// 'Add or Remove Programs' (or 'Programs and Features') list, and stores
        /// custom application data. This allows for proper uninstallation and
        /// system management.
        /// </summary>
        /// <param name="data">The <see cref="InstallationData"/> containing installation details.</param>
        private void RegisterApplication(InstallationData data)
        {
            try
            {
                // Determines whether to register under HKEY_CURRENT_USER or HKEY_LOCAL_MACHINE
                // based on whether it's a user-specific or system-wide installation.
                RegistryKey baseKey = IsUserInstallation(data.InstallPath) ?
                    Registry.CurrentUser : Registry.LocalMachine;

                // Constructs the path to the uninstaller executable.
                string uninstallerPath = Path.Combine(data.InstallPath, "uninstall.exe");

                // Registers custom application data under a specific key (e.g., SOFTWARE\ProfMix).
                using (var key = baseKey.CreateSubKey(REGISTRY_KEY))
                {
                    if (key != null) // Ensures the key was created or opened successfully.
                    {
                        // Stores various installation parameters.
                        key.SetValue("InstallPath", data.InstallPath);
                        key.SetValue("Version", "1.0.0"); // Application version.
                        key.SetValue("InstallDate", DateTime.Now.ToString("yyyy-MM-dd")); // Installation date.
                        key.SetValue("Publisher", "DadOfTheClan"); // Application publisher.
                        key.SetValue("DisplayName", APP_NAME); // Name shown in the registry.
                        key.SetValue("UninstallString", uninstallerPath); // Command to run for uninstallation.
                        key.SetValue("InstallScope", IsUserInstallation(data.InstallPath) ? "User" : "Machine"); // Installation scope.
                    }
                }

                // Registers the application in the Windows "Uninstall" key so it appears in "Programs and Features".
                string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + APP_NAME;
                using (var key = baseKey.CreateSubKey(uninstallKey))
                {
                    if (key != null) // Ensures the key was created or opened successfully.
                    {
                        // Populates values required for display in "Programs and Features".
                        key.SetValue("DisplayName", APP_NAME);
                        key.SetValue("DisplayVersion", "1.0.0");
                        key.SetValue("Publisher", "DadOfTheClan");
                        key.SetValue("InstallLocation", data.InstallPath);
                        key.SetValue("UninstallString", uninstallerPath);
                        key.SetValue("DisplayIcon", Path.Combine(data.InstallPath, APP_ICON));
                        key.SetValue("NoModify", 1); // Prevents "Change" button in Programs and Features.
                        key.SetValue("NoRepair", 1); // Prevents "Repair" button.

                        // Calculates and sets the estimated size of the installed application.
                        long size = GetDirectorySize(data.InstallPath);
                        key.SetValue("EstimatedSize", size / 1024); // Size in KB.

                        // If it's a user-specific installation, mark it as not a system component.
                        if (IsUserInstallation(data.InstallPath))
                        {
                            key.SetValue("SystemComponent", 0); // 0 indicates it's not a system component.
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Catches any exceptions during registry operations.
                // These errors are not critical enough to halt the installation, but may prevent
                // the app from appearing correctly in Programs and Features or being uninstalled easily.
            }
        }

        /// <summary>
        /// This method is currently a placeholder and does not perform any file association.
        /// It's kept for potential future use if ProfMix needs to be associated with specific
        /// audio file types (e.g., .wav, .mp3) for direct opening.
        /// </summary>
        /// <param name="installPath">The application's installation path.</param>
        private void CreateFileAssociations(string installPath)
        {
            // File associations removed - ProfMix is not an audio player.
            // This method is kept for potential future use but currently does nothing.
        }

        /// <summary>
        /// Determines if the given installation path falls within a user-specific directory,
        /// such as Local AppData or the user's profile folder. This helps in deciding
        /// whether to perform a user-level or machine-level registry registration.
        /// </summary>
        /// <param name="installPath">The proposed installation directory path.</param>
        /// <returns>
        /// <c>true</c> if the path is within a user-specific directory; otherwise, <c>false</c>.
        /// </returns>
        private bool IsUserInstallation(string installPath)
        {
            // Gets the paths for common user-specific folders.
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Checks if the install path starts with either of these user-specific paths (case-insensitively).
            return installPath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase) ||
                   installPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the application has write permissions to a given directory.
        /// It attempts to create a temporary directory (if it doesn't exist) and then a test file,
        /// deleting the test file immediately. If any operation fails, it means write access is denied.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to check.</param>
        /// <returns>
        /// <c>true</c> if the application can write to the directory; otherwise, <c>false</c>.
        /// </returns>
        private bool CanWriteToDirectory(string directoryPath)
        {
            try
            {
                // Attempts to create the directory; no-op if it already exists.
                Directory.CreateDirectory(directoryPath);

                // Constructs a path for a temporary test file within the directory.
                string testFile = Path.Combine(directoryPath, "test_write_permission.tmp");
                // Attempts to write text to the test file.
                File.WriteAllText(testFile, "test");
                // Deletes the test file immediately after successful writing.
                File.Delete(testFile);

                return true; // If all operations succeed, write access is granted.
            }
            catch
            {
                return false; // If any exception occurs, write access is denied.
            }
        }

        /// <summary>
        /// Calculates the total size of all files within a specified directory, including
        /// files in any subdirectories. This is used to estimate the installed application's size
        /// for Windows registry entries.
        /// </summary>
        /// <param name="directory">The path to the directory to calculate the size of.</param>
        /// <returns>The total size of the directory's contents in bytes, or 0 if an error occurs.</returns>
        private long GetDirectorySize(string directory)
        {
            try
            {
                // Creates a DirectoryInfo object for the specified directory.
                return new DirectoryInfo(directory)
                    // Gets all files (including those in subdirectories) within the directory.
                    .GetFiles("*", SearchOption.AllDirectories)
                    // Sums the length (size) of each file to get the total size.
                    .Sum(file => file.Length);
            }
            catch
            {
                // Returns 0 if there's an error (e.g., permissions issue accessing a file/directory).
                return 0;
            }
        }

        #endregion

        #region Background Worker Events

        /// <summary>
        /// Event handler for the <see cref="BackgroundWorker.ProgressChanged"/> event.
        /// This method updates the UI elements (progress bar, percentage text, and status message)
        /// on the main UI thread to reflect the current progress of the installation.
        /// </summary>
        /// <param name="sender">The source of the event (the <see cref="BackgroundWorker"/>).</param>
        /// <param name="e">Arguments containing progress percentage and user state (message).</param>
        private void InstallWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Uses Dispatcher.Invoke to ensure UI updates happen on the main UI thread,
            // as this event is raised from a background thread.
            Dispatcher.Invoke(() =>
            {
                // Updates the value of the progress bar.
                InstallProgress.Value = e.ProgressPercentage;
                // Updates the text displaying the percentage.
                ProgressPercentage.Text = $"{e.ProgressPercentage}%";

                // If a status message is provided, updates the detailed progress text.
                if (e.UserState is string message)
                {
                    ProgressDetails.Text = message;
                }
            });
        }

        /// <summary>
        /// Event handler for the <see cref="BackgroundWorker.RunWorkerCompleted"/> event.
        /// This method is called when the background installation process finishes (successfully,
        /// with an error, or due to cancellation). It updates the UI to reflect the final
        /// installation status and offers post-installation options.
        /// </summary>
        /// <param name="sender">The source of the event (the <see cref="BackgroundWorker"/>).</param>
        /// <param name="e">Arguments containing information about the completion status (cancelled, error, result).</param>
        private void InstallWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Ensures UI updates happen on the main UI thread.
            Dispatcher.Invoke(() =>
            {
                // Resets the installation flag.
                isInstalling = false;
                // Re-enables the close button.
                BtnClose.IsEnabled = true;

                // Checks if the installation was cancelled.
                if (e.Cancelled)
                {
                    // Updates UI to show cancellation status.
                    ProgressTitle.Text = "Installation Cancelled";
                    ProgressDetails.Text = "The installation was cancelled by the user.";
                    ProgressPercentage.Text = "Cancelled";

                    // Hides the progress section and shows the initial action buttons again.
                    ProgressSection.Visibility = Visibility.Collapsed;
                    ActionButtons.Visibility = Visibility.Visible;
                }
                // Checks if an error occurred during installation.
                else if (e.Error != null || e.Result is Exception)
                {
                    // Retrieves the exception that occurred.
                    var error = e.Error ?? (Exception)e.Result;
                    // Updates UI to show error status.
                    ProgressTitle.Text = "Installation Failed";
                    ProgressDetails.Text = error.Message;
                    ProgressPercentage.Text = "Error";

                    // Displays a detailed error message box to the user.
                    MessageBox.Show(
                        $"Installation failed:\n\n{error.Message}",
                        "Installation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                // If installation completed successfully.
                else if (e.Result is InstallationData data)
                {
                    // Updates UI to show success status.
                    ProgressTitle.Text = "Installation Complete! 🎉";
                    ProgressDetails.Text = $"ProfMix has been successfully installed to {data.InstallPath}";
                    ProgressPercentage.Text = "100%";

                    // Presents post-installation options (e.g., launch application).
                    ShowCompletionOptions(data);
                }
            });
        }

        /// <summary>
        /// Displays a message box to the user after a successful installation,
        /// offering the option to launch the newly installed ProfMix application.
        /// If the user chooses to launch, it attempts to start the executable.
        /// </summary>
        /// <param name="data">The <see cref="InstallationData"/> containing the installation path.</param>
        private void ShowCompletionOptions(InstallationData data)
        {
            // Prompts the user to ask if they want to launch ProfMix now.
            var result = MessageBox.Show(
                "ProfMix has been installed successfully!\n\nWould you like to launch ProfMix now?",
                "Installation Complete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            // If the user chooses to launch the application.
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Constructs the full path to the installed application's executable.
                    string exePath = Path.Combine(data.InstallPath, APP_EXE);
                    // Checks if the executable file actually exists.
                    if (File.Exists(exePath))
                    {
                        // Starts a new process for the application executable.
                        Process.Start(exePath);
                    }
                }
                catch (Exception ex)
                {
                    // Catches and displays any errors that occur during the attempt to launch the application.
                    MessageBox.Show(
                        $"Could not launch ProfMix: {ex.Message}",
                        "Launch Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            // Closes the installer window regardless of whether the application was launched.
            Close();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Checks if the current application process is running with administrator privileges.
        /// This is crucial for determining where the application can be installed (e.g., Program Files vs. AppData).
        /// </summary>
        /// <returns>
        /// <c>true</c> if the application is running as administrator; otherwise, <c>false</c>.
        /// </returns>
        private bool IsRunningAsAdmin()
        {
            try
            {
                // Gets the current Windows user identity.
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                // Creates a WindowsPrincipal object based on the identity, allowing role checks.
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                // Checks if the current principal is in the Administrator role.
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                // Returns false if there's any error during the privilege check.
                return false;
            }
        }

        /// <summary>
        /// Attempts to restart the current installer application with administrator privileges
        /// using the "runas" verb. If successful, the current application instance shuts down.
        /// This is typically used when the installer detects it needs elevated permissions.
        /// </summary>
        private void RestartAsAdmin()
        {
            try
            {
                // Configures the ProcessStartInfo for the restart.
                var startInfo = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName, // Path to the current executable.
                    UseShellExecute = true, // Must be true for "runas" verb.
                    Verb = "runas" // Requests elevation to administrator privileges.
                };

                // Starts the new process. This will trigger a UAC prompt if needed.
                Process.Start(startInfo);
                // Shuts down the current application instance.
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                // Catches and displays an error if the restart with admin privileges fails.
                MessageBox.Show(
                    $"Could not restart with administrator privileges: {ex.Message}",
                    "Elevation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Overrides the default behavior for handling left mouse button down events.
        /// This allows the user to drag and move the installer window by clicking and
        /// dragging anywhere on the window, unless an installation is in progress.
        /// </summary>
        /// <param name="e">Event arguments containing mouse button information.</param>
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e); // Calls the base implementation first.
            // Allows window dragging only if an installation is not currently active.
            if (!isInstalling)
                DragMove(); // Initiates the window drag operation.
        }

        /// <summary>
        /// Overrides the default behavior for handling the window closing event.
        /// If an installation is in progress, it prompts the user for confirmation
        /// before allowing the window to close and attempts to cancel the background worker.
        /// </summary>
        /// <param name="e">Event arguments indicating whether the close can be cancelled.</param>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Checks if an installation is currently active.
            if (isInstalling)
            {
                // Prompts the user to confirm exiting while installation is in progress.
                var result = MessageBox.Show(
                    "Installation is in progress. Are you sure you want to exit?",
                    "Installation In Progress",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                // If the user chooses not to exit, cancels the closing event.
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // If the user confirms exiting, attempts to cancel the background worker.
                installWorker?.CancelAsync();
            }

            base.OnClosing(e); // Calls the base implementation to finalize the closing process.
        }

        #endregion
    }

    /// <summary>
    /// A simple data transfer class used to encapsulate and pass installation
    /// configuration settings between the UI thread and the background worker.
    /// </summary>
    public class InstallationData
    {
        /// <summary>
        /// Gets or sets the full path where the application will be installed.
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a desktop shortcut should be created.
        /// </summary>
        public bool CreateDesktopShortcut { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a Start Menu shortcut should be created.
        /// </summary>
        public bool CreateStartMenuShortcut { get; set; }
    }
}