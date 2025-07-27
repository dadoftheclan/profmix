using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace App_Install.Windows
{
    /// <summary>
    /// Interaction logic for wndUninstall.xaml. This window provides the user interface
    /// and logic for uninstalling the ProfMix application from the system. It detects
    /// existing installation details and allows the user to choose which components
    /// (files, shortcuts, registry entries, user profiles) to remove.
    /// </summary>
    public partial class wndUninstall : Window
    {
        /// <summary>
        /// Defines the constant name for the application being uninstalled.
        /// </summary>
        private const string APP_NAME = "ProfMix";

        /// <summary>
        /// Defines the constant executable file name for the application.
        /// Used to detect if the application is running.
        /// </summary>
        private const string APP_EXE = "ProfMix.exe";

        /// <summary>
        /// Defines the custom registry key path where ProfMix installation information is stored.
        /// </summary>
        private const string REGISTRY_KEY = @"SOFTWARE\ProfMix";

        /// <summary>
        /// Defines the standard Windows "Uninstall" registry key path for ProfMix.
        /// This is where the application registers itself to appear in "Programs and Features."
        /// </summary>
        private const string UNINSTALL_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ProfMix";

        /// <summary>
        /// A flag indicating whether an uninstallation process is currently active.
        /// Prevents multiple uninstallation attempts or improper window closing during the process.
        /// </summary>
        private bool isUninstalling = false;

        /// <summary>
        /// An instance of <see cref="BackgroundWorker"/> used to perform the uninstallation
        /// operations in a separate thread, keeping the UI responsive.
        /// </summary>
        private BackgroundWorker uninstallWorker;

        /// <summary>
        /// Stores the detected installation path of ProfMix.
        /// </summary>
        private string installPath;

        /// <summary>
        /// Stores the detected version of the ProfMix installation.
        /// </summary>
        private string version;

        /// <summary>
        /// Stores the detected installation date of ProfMix.
        /// </summary>
        private string installDate;

        /// <summary>
        /// Initializes a new instance of the <see cref="wndUninstall"/> class.
        /// This constructor sets up the UI components and prepares the uninstaller
        /// by initializing the background worker and loading existing installation information.
        /// </summary>
        public wndUninstall()
        {
            InitializeComponent();
            InitializeUninstaller();
        }

        /// <summary>
        /// Initializes the uninstaller's components and triggers the loading of existing
        /// ProfMix installation information from the system.
        /// </summary>
        private async void InitializeUninstaller()
        {
            // Initializes the BackgroundWorker for asynchronous uninstallation.
            uninstallWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,     // Allows reporting progress updates to the UI.
                WorkerSupportsCancellation = true // Enables cancelling the uninstallation process.
            };

            // Subscribes event handlers for the worker's lifecycle events.
            uninstallWorker.DoWork += UninstallWorker_DoWork;           // The main uninstallation logic.
            uninstallWorker.ProgressChanged += UninstallWorker_ProgressChanged; // Updates UI during progress.
            uninstallWorker.RunWorkerCompleted += UninstallWorker_RunWorkerCompleted; // Actions after completion (success, error, cancelled).

            // Asynchronously loads installation details from the system.
            await LoadInstallationInfo();
        }

        /// <summary>
        /// Asynchronously loads information about the currently installed ProfMix application.
        /// It first checks common registry locations (LocalMachine and CurrentUser) for installation
        /// paths, versions, and dates. If registry information is incomplete or unavailable,
        /// it falls back to scanning common file system paths.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task LoadInstallationInfo()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Defines the base registry hives to search for installation data.
                    RegistryKey[] baseKeys = { Registry.LocalMachine, Registry.CurrentUser };

                    // Loops through both registry hives to find installation information.
                    foreach (var baseKey in baseKeys)
                    {
                        // Tries to open the custom ProfMix registry key first.
                        using (var key = baseKey.OpenSubKey(REGISTRY_KEY))
                        {
                            if (key != null) // If the custom key exists.
                            {
                                // Retrieves installation path, version, and date from the registry.
                                installPath = key.GetValue("InstallPath") as string;
                                version = key.GetValue("Version") as string;
                                installDate = key.GetValue("InstallDate") as string;

                                // If a valid path is found and it exists on disk, we've found our info.
                                if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                                {
                                    return; // Exit as valid installation info has been found.
                                }
                            }
                        }

                        // If the main registry key didn't provide complete info, try the standard Uninstall key.
                        using (var key = baseKey.OpenSubKey(UNINSTALL_KEY))
                        {
                            if (key != null) // If the uninstall entry exists.
                            {
                                // Retrieves installation location and display version.
                                installPath = key.GetValue("InstallLocation") as string;
                                version = key.GetValue("DisplayVersion") as string;
                                // Note: InstallDate is often not stored in the standard Uninstall key.

                                // If a valid path is found and it exists on disk, we've found our info.
                                if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                                {
                                    return; // Exit as valid installation info has been found.
                                }
                            }
                        }
                    }

                    // If registry search fails to find a valid installation path, fall back to file system scan.
                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                    {
                        installPath = FindInstallationByFileSystem(); // Attempts to find it by looking for files.
                        if (!string.IsNullOrEmpty(installPath))
                        {
                            // If found by file system, try to get the version from the executable itself.
                            string exePath = Path.Combine(installPath, APP_EXE);
                            if (File.Exists(exePath))
                            {
                                try
                                {
                                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                                    version = versionInfo.ProductVersion ?? "Unknown";
                                }
                                catch
                                {
                                    version = "Unknown"; // Set to unknown if version extraction fails.
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Catches any errors during detection and sets all info to "Unknown".
                    installPath = "Unknown";
                    version = "Unknown";
                    installDate = "Unknown";
                }

                // Ensures all info fields have a value, even if "Unknown".
                if (string.IsNullOrEmpty(installPath)) installPath = "Unknown";
                if (string.IsNullOrEmpty(version)) version = "Unknown";
                if (string.IsNullOrEmpty(installDate)) installDate = "Unknown";
            });

            // Updates the UI elements on the main thread after information loading is complete.
            Dispatcher.Invoke(() =>
            {
                TxtInstallPath.Text = installPath;
                TxtVersion.Text = version;
                TxtInstallDate.Text = installDate;

                // Asynchronously calculates and displays the installation size.
                if (Directory.Exists(installPath))
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            long sizeBytes = GetDirectorySize(installPath);
                            double sizeMB = sizeBytes / (1024.0 * 1024.0); // Convert bytes to megabytes.

                            Dispatcher.Invoke(() =>
                            {
                                TxtSize.Text = $"{sizeMB:F1} MB"; // Display size formatted to one decimal place.
                            });
                        }
                        catch
                        {
                            Dispatcher.Invoke(() =>
                            {
                                TxtSize.Text = "Unknown"; // Display "Unknown" if size calculation fails.
                            });
                        }
                    });
                }
                else
                {
                    TxtSize.Text = "Directory not found"; // Indicate if the install directory isn't there.
                }
            });
        }

        /// <summary>
        /// Searches common file system paths to find the ProfMix installation directory
        /// by looking for the main application executable.
        /// </summary>
        /// <returns>
        /// The full path to the ProfMix installation directory if found; otherwise, <c>null</c>.
        /// </returns>
        private string FindInstallationByFileSystem()
        {
            // Defines a list of standard and common paths where ProfMix might be installed.
            string[] possiblePaths = {
                // System-wide installation locations
                @"C:\Program Files\ProfMix", // Hardcoded path, might be default for some installs.
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ProfMix"),      // 64-bit Program Files.
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ProfMix"),   // 32-bit Program Files (on 64-bit OS).
                
                // User-specific installation locations
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProfMix"), // User's local application data.
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProfMix"),       // User's roaming application data.
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ProfMix"),         // User's general profile folder.
                
                // Common portable or desktop installation locations
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ProfMix"),    // User's Desktop.
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ProfMix") // User's Documents folder.
            };

            // Iterates through each potential path.
            foreach (var path in possiblePaths)
            {
                // Constructs the full path to the application's executable within the current potential directory.
                string exePath = Path.Combine(path, APP_EXE);
                // Checks if the executable file actually exists at this location.
                if (File.Exists(exePath))
                {
                    return path; // Returns the directory path if the executable is found.
                }
            }

            return null; // Returns null if the executable is not found in any of the checked paths.
        }

        /// <summary>
        /// Calculates the total size of all files within a specified directory, including
        /// files in any subdirectories. This is used to display the disk space occupied
        /// by the installed application.
        /// </summary>
        /// <param name="directory">The path to the directory for which to calculate the size.</param>
        /// <returns>The total size of the directory's contents in bytes, or 0 if an error occurs.</returns>
        private long GetDirectorySize(string directory)
        {
            try
            {
                // Creates a DirectoryInfo object to interact with the directory.
                return new DirectoryInfo(directory)
                    // Gets all files within the directory and its subdirectories.
                    .GetFiles("*", SearchOption.AllDirectories)
                    // Sums the length (size) of each file to get the total.
                    .Sum(file => file.Length);
            }
            catch
            {
                // Returns 0 if an error occurs during file enumeration (e.g., permission denied).
                return 0;
            }
        }

        #region Event Handlers

        /// <summary>
        /// Event handler for the "Uninstall" button click.
        /// This method initiates the uninstallation process after confirming with the user
        /// and ensuring the application (ProfMix) is not currently running.
        /// </summary>
        /// <param name="sender">The source of the event (BtnUninstall).</param>
        /// <param name="e">Event arguments.</param>
        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            // Prevents multiple uninstallation attempts.
            if (isUninstalling) return;

            // Displays a confirmation dialog to the user before starting the uninstallation.
            var result = MessageBox.Show(
                "Are you sure you want to uninstall ProfMix?\n\nThis action cannot be undone.",
                "Confirm Uninstallation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // If the user does not confirm, aborts the operation.
            if (result != MessageBoxResult.Yes)
                return;

            // Checks if the ProfMix application process is currently running.
            // Replaces ".exe" with an empty string to get the process name (e.g., "ProfMix").
            if (IsProcessRunning(APP_EXE.Replace(".exe", "")))
            {
                // Prompts the user to close the running application.
                var closeResult = MessageBox.Show(
                    "ProfMix is currently running. It must be closed before uninstalling.\n\nWould you like to close it now?",
                    "Application Running",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // If the user agrees to close the application.
                if (closeResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Gets all running processes with the ProfMix name.
                        var processes = Process.GetProcessesByName(APP_EXE.Replace(".exe", ""));
                        foreach (var process in processes)
                        {
                            process.CloseMainWindow(); // Tries to gracefully close the application's main window.
                            if (!process.WaitForExit(5000)) // Waits up to 5 seconds for the process to exit.
                            {
                                process.Kill(); // If it doesn't close gracefully, force kills the process.
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Displays an error if closing the application fails.
                        MessageBox.Show(
                            $"Could not close ProfMix: {ex.Message}\n\nPlease close it manually and try again.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return; // Aborts uninstallation if the app cannot be closed.
                    }
                }
                else
                {
                    return; // Aborts uninstallation if the user declines to close the app.
                }
            }

            // If all checks pass and the app is not running, starts the asynchronous uninstallation.
            await StartUninstallation();
        }

        /// <summary>
        /// Event handler for the "Cancel" button click.
        /// If an uninstallation is in progress, it prompts the user for confirmation
        /// before attempting to cancel the background worker. Otherwise, it simply closes the window.
        /// </summary>
        /// <param name="sender">The source of the event (BtnCancel).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Checks if an uninstallation is currently active.
            if (isUninstalling)
            {
                // Prompts the user to confirm cancellation of the ongoing uninstallation.
                var result = MessageBox.Show(
                    "Uninstallation is in progress. Are you sure you want to cancel?",
                    "Cancel Uninstallation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // If the user confirms cancellation.
                if (result == MessageBoxResult.Yes)
                {
                    uninstallWorker?.CancelAsync(); // Requests the background worker to cancel.
                }
            }
            else
            {
                Close(); // If not uninstalling, simply closes the window.
            }
        }

        /// <summary>
        /// Event handler for the "Close" button click.
        /// This button is typically enabled only when uninstallation is not in progress,
        /// allowing the user to exit the uninstaller.
        /// </summary>
        /// <param name="sender">The source of the event (BtnClose).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Allows closing only if uninstallation is not active.
            if (!isUninstalling)
                Close();
        }

        #endregion

        #region Uninstallation Logic

        /// <summary>
        /// Initiates the main uninstallation sequence.
        /// This method updates the UI to show the progress section, disables the close button,
        /// gathers selected uninstallation options, and starts the <see cref="BackgroundWorker"/>
        /// to perform the actual removal steps.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task StartUninstallation()
        {
            // Sets the flag to indicate that uninstallation has started.
            isUninstalling = true;

            // Changes the UI visibility to hide information/options and show progress.
            InfoPanel.Visibility = Visibility.Collapsed;
            OptionsPanel.Visibility = Visibility.Collapsed;
            ActionButtons.Visibility = Visibility.Collapsed;
            ProgressSection.Visibility = Visibility.Visible;

            // Disables the close button to prevent interruption.
            BtnClose.IsEnabled = false;

            // Gathers the uninstallation options selected by the user.
            var uninstallData = new UninstallationData
            {
                InstallPath = installPath,
                RemoveShortcuts = ChkRemoveShortcuts.IsChecked ?? false, // Handle null for IsChecked.
                RemoveRegistry = ChkRemoveRegistry.IsChecked ?? false,
                KeepProfiles = ChkKeepProfiles.IsChecked ?? false
            };

            // Starts the background worker, passing the uninstallation data.
            uninstallWorker.RunWorkerAsync(uninstallData);
        }

        /// <summary>
        /// The main method executed on the background worker thread for uninstallation.
        /// This method orchestrates the removal of various ProfMix components based on
        /// user selections, including shortcuts, registry entries, application files,
        /// and cleaning up empty directories. It reports progress and handles cancellations.
        /// </summary>
        /// <param name="sender">The source of the event (the <see cref="BackgroundWorker"/>).</param>
        /// <param name="e">Arguments for the background operation, including input data and result.</param>
        private void UninstallWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Casts the sender to a BackgroundWorker to access its methods.
            var worker = sender as BackgroundWorker;
            // Retrieves the uninstallation data passed from the main thread.
            var data = e.Argument as UninstallationData;

            try
            {
                // Step 1: Remove desktop and Start Menu shortcuts if selected.
                if (data.RemoveShortcuts)
                {
                    worker?.ReportProgress(15, "Removing shortcuts...");
                    RemoveShortcuts();
                }

                // Checks for cancellation request after shortcut removal.
                if (worker?.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }

                // Step 2: Remove file associations and registry entries if selected.
                if (data.RemoveRegistry)
                {
                    worker?.ReportProgress(30, "Removing file associations...");
                    RemoveFileAssociations(); // Cleans up file type associations.

                    worker?.ReportProgress(45, "Cleaning registry entries...");
                    RemoveRegistryEntries(); // Removes custom and uninstall registry entries.
                }

                // Checks for cancellation request after registry cleanup.
                if (worker?.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }

                // Step 3: Remove the main application files.
                worker?.ReportProgress(60, "Removing application files...");
                RemoveApplicationFiles(data, worker);

                // Checks for cancellation request after file removal.
                if (worker?.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }

                // Step 4: Clean up any empty directories left behind.
                worker?.ReportProgress(90, "Cleaning up directories...");
                CleanupDirectories(data);

                // Step 5: Final completion status.
                worker?.ReportProgress(100, "Uninstallation completed successfully!");

                // Sets the result of the background operation to the uninstallation data on success.
                e.Result = data;
            }
            catch (Exception ex)
            {
                // If any exception occurs during uninstallation, sets the result to the exception.
                e.Result = new Exception($"Uninstallation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Removes the desktop shortcut and Start Menu folder/shortcuts associated with ProfMix.
        /// Errors during shortcut removal are caught and suppressed, as they are not critical
        /// enough to halt the entire uninstallation.
        /// </summary>
        private void RemoveShortcuts()
        {
            try
            {
                // Path to the desktop shortcut.
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string desktopShortcut = Path.Combine(desktopPath, $"{APP_NAME}.lnk");
                // Deletes the desktop shortcut if it exists.
                if (File.Exists(desktopShortcut))
                {
                    File.Delete(desktopShortcut);
                }

                // Path to the Start Menu folder for ProfMix.
                string startMenuPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    APP_NAME);
                // Deletes the entire Start Menu directory recursively if it exists.
                if (Directory.Exists(startMenuPath))
                {
                    Directory.Delete(startMenuPath, true); // `true` for recursive deletion.
                }
            }
            catch (Exception)
            {
                // Catches and ignores errors during shortcut removal.
            }
        }

        /// <summary>
        /// Removes file associations for audio file types (e.g., .wav, .mp3) that ProfMix
        /// might have registered. It attempts to clean up ProgIDs and reset default
        /// associations if they pointed to ProfMix.
        /// </summary>
        private void RemoveFileAssociations()
        {
            try
            {
                // Registry hives to check for file associations: system-wide and user-specific.
                RegistryKey[] baseKeys = { Registry.ClassesRoot, Registry.CurrentUser };

                foreach (var baseKey in baseKeys)
                {
                    RegistryKey classesKey = baseKey; // Default to base key for Registry.ClassesRoot.

                    // For CurrentUser, we need to navigate to the "SOFTWARE\Classes" subkey.
                    if (baseKey == Registry.CurrentUser)
                    {
                        try
                        {
                            classesKey = baseKey.OpenSubKey(@"SOFTWARE\Classes", true); // Open writable.
                            if (classesKey == null) continue; // Skip if classes key cannot be opened.
                        }
                        catch
                        {
                            continue; // Continue to next base key if access is denied.
                        }
                    }

                    // Define the file extensions that ProfMix might have associated.
                    string[] extensions = { ".wav", ".mp3" };

                    foreach (string ext in extensions)
                    {
                        // Constructs the Programmatic ID (ProgID) that ProfMix would have registered.
                        string progId = $"ProfMix{ext.Substring(1).ToUpper()}File"; // e.g., "ProfMixWAVFile".

                        try
                        {
                            // Deletes the entire ProgID key and its subkeys.
                            classesKey.DeleteSubKeyTree(progId);
                        }
                        catch (Exception)
                        {
                            // ProgID might not exist or access denied, ignore.
                        }

                        // Resets the default association for the file extension if it pointed to ProfMix.
                        try
                        {
                            using (var extKey = classesKey.OpenSubKey(ext, true)) // Open writable.
                            {
                                if (extKey != null)
                                {
                                    var currentValue = extKey.GetValue("") as string; // Get default value.
                                    if (currentValue == progId) // If it points to our ProgID.
                                    {
                                        extKey.SetValue("", ""); // Clear the association.
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Extension key access failed, ignore.
                        }
                    }

                    // If we opened a separate classes key for CurrentUser, dispose it.
                    if (baseKey == Registry.CurrentUser && classesKey != baseKey)
                    {
                        classesKey?.Dispose();
                    }
                }
            }
            catch (Exception)
            {
                // Catches and ignores any top-level exceptions during file association removal.
            }
        }

        /// <summary>
        /// Removes the custom registry entries and the uninstallation entry
        /// for ProfMix from the Windows Registry.
        /// It tries to remove from both <c>HKEY_LOCAL_MACHINE</c> and <c>HKEY_CURRENT_USER</c> hives.
        /// </summary>
        private void RemoveRegistryEntries()
        {
            try
            {
                // Defines the base registry hives to attempt removal from.
                RegistryKey[] baseKeys = { Registry.LocalMachine, Registry.CurrentUser };

                foreach (var baseKey in baseKeys)
                {
                    // Attempts to delete the main custom ProfMix registry key.
                    try
                    {
                        baseKey.DeleteSubKeyTree(REGISTRY_KEY);
                    }
                    catch (Exception)
                    {
                        // Key might not exist or access denied, ignore.
                    }

                    // Attempts to delete the standard "Uninstall" registry key for ProfMix.
                    try
                    {
                        string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ProfMix";
                        baseKey.DeleteSubKeyTree(uninstallKeyPath);
                    }
                    catch (Exception)
                    {
                        // Key might not exist or access denied, ignore.
                    }

                    // Additionally, remove file association entries from the user's registry (SOFTWARE\Classes).
                    if (baseKey == Registry.CurrentUser)
                    {
                        try
                        {
                            string classesKeyPath = @"SOFTWARE\Classes";
                            using (var classes = baseKey.OpenSubKey(classesKeyPath, true)) // Open writable.
                            {
                                if (classes != null)
                                {
                                    // Remove specific ProgIDs related to ProfMix.
                                    try { classes.DeleteSubKeyTree("ProfMixWAVFile"); } catch { }
                                    try { classes.DeleteSubKeyTree("ProfMixMP3File"); } catch { }

                                    // Reset default associations for extensions if they point to ProfMix.
                                    string[] extensions = { ".wav", ".mp3" };
                                    foreach (string ext in extensions)
                                    {
                                        try
                                        {
                                            using (var extKey = classes.OpenSubKey(ext, true))
                                            {
                                                if (extKey != null)
                                                {
                                                    var currentValue = extKey.GetValue("") as string;
                                                    // Check if the association starts with "ProfMix" to identify our entries.
                                                    if (currentValue?.StartsWith("ProfMix") == true)
                                                    {
                                                        extKey.SetValue("", ""); // Clear the association.
                                                    }
                                                }
                                            }
                                        }
                                        catch { } // Ignore individual extension key errors.
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // User classes cleanup failed, ignore.
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Catches and ignores any top-level exceptions during registry cleanup.
            }
        }

        /// <summary>
        /// Removes application files from the installation directory.
        /// It iterates through all files and attempts to delete them, respecting
        /// the user's choice to keep profile-related files. It reports progress
        /// through the <see cref="BackgroundWorker"/>.
        /// </summary>
        /// <param name="data">The <see cref="UninstallationData"/> containing uninstallation options.</param>
        /// <param name="worker">The <see cref="BackgroundWorker"/> to report progress to.</param>
        private void RemoveApplicationFiles(UninstallationData data, BackgroundWorker worker)
        {
            // If the installation path is invalid or doesn't exist, there's nothing to remove.
            if (string.IsNullOrEmpty(data.InstallPath) || !Directory.Exists(data.InstallPath))
                return;

            try
            {
                // Gets all files (including those in subdirectories) from the installation path.
                var files = Directory.GetFiles(data.InstallPath, "*", SearchOption.AllDirectories);
                int totalFiles = files.Length;
                int currentFile = 0;

                foreach (string file in files)
                {
                    // Checks if cancellation has been requested.
                    if (worker?.CancellationPending == true)
                        return;

                    currentFile++;
                    // Calculates progress percentage (from 60% to 85% of total uninstallation).
                    var progress = 60 + (int)((currentFile / (double)totalFiles) * 25);
                    // Reports the current progress and the name of the file being removed.
                    worker?.ReportProgress(progress, $"Removing: {Path.GetFileName(file)}");

                    try
                    {
                        // If the user chose to keep profiles and the current file is a profile file, skip deletion.
                        if (data.KeepProfiles && IsUserProfileFile(file))
                            continue;

                        File.SetAttributes(file, FileAttributes.Normal); // Removes read-only or other attributes.
                        File.Delete(file); // Deletes the file.
                    }
                    catch (Exception)
                    {
                        // If an individual file deletion fails (e.g., in use), continue with other files.
                    }
                }
            }
            catch (Exception)
            {
                // Catches and ignores any top-level exceptions during file enumeration or initial removal.
            }
        }

        /// <summary>
        /// Determines if a given file path corresponds to a user profile-related file
        /// that should be potentially preserved during uninstallation if the user opts to keep profiles.
        /// </summary>
        /// <param name="filePath">The full path to the file.</param>
        /// <returns>
        /// <c>true</c> if the file is identified as a user profile file; otherwise, <c>false</c>.
        /// </returns>
        private bool IsUserProfileFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath).ToLower(); // Get file name and convert to lowercase for comparison.
            // List of file names considered to be user profile/settings data.
            string[] profileFiles = { "profiles.json", "application.log", "crash.log", "settings.xml" };

            // Checks if the file name contains any of the predefined profile file names.
            return profileFiles.Any(pf => fileName.Contains(pf.ToLower()));
        }

        /// <summary>
        /// Cleans up empty directories remaining after application files have been removed.
        /// It prioritizes removing deepest subdirectories first to avoid "directory not empty" errors.
        /// </summary>
        /// <param name="data">The <see cref="UninstallationData"/> containing the installation path.</param>
        private void CleanupDirectories(UninstallationData data)
        {
            // If the installation path is invalid or doesn't exist, there's nothing to clean up.
            if (string.IsNullOrEmpty(data.InstallPath) || !Directory.Exists(data.InstallPath))
                return;

            try
            {
                // Gets all subdirectories and orders them by length in descending order
                // (deepest subdirectories first) to ensure parent directories can be deleted.
                var subdirectories = Directory.GetDirectories(data.InstallPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length);

                foreach (string dir in subdirectories)
                {
                    try
                    {
                        // Deletes a directory only if it's empty (contains no files or subdirectories).
                        if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                        {
                            Directory.Delete(dir);
                        }
                    }
                    catch (Exception)
                    {
                        // If a directory deletion fails (e.g., still in use), continue to others.
                    }
                }

                // Finally, try to remove the main installation directory itself if it's empty.
                if (Directory.GetFiles(data.InstallPath).Length == 0 &&
                    Directory.GetDirectories(data.InstallPath).Length == 0)
                {
                    Directory.Delete(data.InstallPath);
                }
            }
            catch (Exception)
            {
                // Catches and ignores any top-level exceptions during directory cleanup.
            }
        }

        /// <summary>
        /// Checks if a process with a given name is currently running on the system.
        /// This is used to ensure ProfMix is closed before uninstallation.
        /// </summary>
        /// <param name="processName">The name of the process to check (e.g., "ProfMix").</param>
        /// <returns>
        /// <c>true</c> if at least one instance of the specified process is running; otherwise, <c>false</c>.
        /// </returns>
        private bool IsProcessRunning(string processName)
        {
            try
            {
                // Gets all processes with the specified name.
                var processes = Process.GetProcessesByName(processName);
                // Returns true if any processes were found.
                return processes.Length > 0;
            }
            catch
            {
                // Returns false if there's an error during process enumeration.
                return false;
            }
        }

        #endregion

        #region Background Worker Events

        /// <summary>
        /// Event handler for the <see cref="BackgroundWorker.ProgressChanged"/> event.
        /// This method updates the UI elements (progress bar, percentage text, and status message)
        /// on the main UI thread to show the current progress of the uninstallation.
        /// </summary>
        /// <param name="sender">The source of the event (the <see cref="BackgroundWorker"/>).</param>
        /// <param name="e">Arguments containing progress percentage and user state (message).</param>
        private void UninstallWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Uses Dispatcher.Invoke to ensure UI updates are performed on the main UI thread.
            Dispatcher.Invoke(() =>
            {
                UninstallProgress.Value = e.ProgressPercentage;          // Updates the progress bar value.
                ProgressPercentage.Text = $"{e.ProgressPercentage}%";    // Updates the percentage text.

                // If a status message is provided, updates the detailed progress text.
                if (e.UserState is string message)
                {
                    ProgressDetails.Text = message;
                }
            });
        }

        /// <summary>
        /// Event handler for the <see cref="BackgroundWorker.RunWorkerCompleted"/> event.
        /// This method is called when the background uninstallation process finishes (successfully,
        /// with an error, or due to cancellation). It updates the UI to reflect the final
        /// uninstallation status and displays a completion message.
        /// </summary>
        /// <param name="sender">The source of the event (the <see cref="BackgroundWorker"/>).</param>
        /// <param name="e">Arguments containing information about the completion status (cancelled, error, result).</param>
        private void UninstallWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Ensures UI updates happen on the main UI thread.
            Dispatcher.Invoke(() =>
            {
                isUninstalling = false;     // Resets the uninstallation flag.
                BtnClose.IsEnabled = true;  // Re-enables the close button.

                // Checks if the uninstallation was cancelled by the user.
                if (e.Cancelled)
                {
                    // Updates UI to show cancellation status.
                    ProgressTitle.Text = "Uninstallation Cancelled";
                    ProgressDetails.Text = "The uninstallation was cancelled by the user.";
                    ProgressPercentage.Text = "Cancelled";

                    // Restores the initial UI panels (info, options, action buttons).
                    ProgressSection.Visibility = Visibility.Collapsed;
                    InfoPanel.Visibility = Visibility.Visible;
                    OptionsPanel.Visibility = Visibility.Visible;
                    ActionButtons.Visibility = Visibility.Visible;
                }
                // Checks if an error occurred during uninstallation.
                else if (e.Error != null || e.Result is Exception)
                {
                    var error = e.Error ?? (Exception)e.Result; // Retrieves the exception.
                    // Updates UI to show error status.
                    ProgressTitle.Text = "Uninstallation Failed";
                    ProgressDetails.Text = error.Message;
                    ProgressPercentage.Text = "Error";

                    // Displays a detailed error message box to the user.
                    MessageBox.Show(
                        $"Uninstallation failed:\n\n{error.Message}",
                        "Uninstallation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                // If uninstallation completed successfully.
                else if (e.Result is UninstallationData data)
                {
                    // Updates UI to show success status.
                    ProgressTitle.Text = "Uninstallation Complete! ✅";
                    ProgressDetails.Text = "ProfMix has been successfully removed from your computer.";
                    ProgressPercentage.Text = "100%";

                    // Displays a final completion message, potentially indicating if profiles were kept.
                    ShowCompletionMessage(data);
                }
            });
        }

        /// <summary>
        /// Displays a final message to the user after uninstallation,
        /// informing them of the successful removal and whether user profiles were preserved.
        /// It then closes the uninstaller window.
        /// </summary>
        /// <param name="data">The <see cref="UninstallationData"/> containing details about the uninstallation (e.g., if profiles were kept).</param>
        private void ShowCompletionMessage(UninstallationData data)
        {
            string message = "ProfMix has been successfully uninstalled.";

            // Appends a message if user profiles were intentionally kept.
            if (data.KeepProfiles)
            {
                message += "\n\nYour user profiles and settings have been preserved.";
            }

            // Displays the final message box.
            MessageBox.Show(
                message,
                "Uninstallation Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Close(); // Closes the uninstaller window.
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Overrides the default behavior for the left mouse button down event on the window.
        /// This allows the user to drag and move the uninstaller window by clicking and
        /// dragging anywhere on the window, as long as uninstallation is not in progress.
        /// </summary>
        /// <param name="e">The <see cref="System.Windows.Input.MouseButtonEventArgs"/> that contains the event data.</param>
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e); // Calls the base class implementation first.
            // Allows window dragging only if uninstallation is not currently active.
            if (!isUninstalling)
                DragMove(); // Initiates the window drag operation.
        }

        /// <summary>
        /// Overrides the default behavior for the window closing event.
        /// If an uninstallation is in progress, it prompts the user for confirmation
        /// before allowing the window to close and attempts to cancel the background worker.
        /// </summary>
        /// <param name="e">Event arguments indicating whether the close operation can be cancelled.</param>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Checks if uninstallation is currently in progress.
            if (isUninstalling)
            {
                // Prompts the user to confirm exiting while uninstallation is active.
                var result = MessageBox.Show(
                    "Uninstallation is in progress. Are you sure you want to exit?",
                    "Uninstallation In Progress",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                // If the user chooses not to exit, cancels the closing event.
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // If the user confirms exiting, attempts to cancel the background uninstallation worker.
                uninstallWorker?.CancelAsync();
            }

            base.OnClosing(e); // Calls the base class's OnClosing method to ensure proper WPF window closing behavior.
        }

        #endregion
    }

    /// <summary>
    /// A simple data class used to pass uninstallation configuration settings
    /// and options between the UI thread and the background worker.
    /// </summary>
    public class UninstallationData
    {
        /// <summary>
        /// Gets or sets the full path from which the application is being uninstalled.
        /// </summary>
        public string InstallPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether desktop and Start Menu shortcuts should be removed.
        /// </summary>
        public bool RemoveShortcuts { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether registry entries (custom and uninstall)
        /// and file associations should be removed.
        /// </summary>
        public bool RemoveRegistry { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether user profile data and settings should be preserved.
        /// </summary>
        public bool KeepProfiles { get; set; }
    }
}