using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json; // Used for serializing and deserializing profile data.
using App_Mix.Models; // Contains the mdlProfile data model.
using App_Mix.Windows; // Contains window types like wndException for error display.
using System.Windows; // Required for Application.Current and Dispatcher for UI thread operations.

namespace App_Mix.Systems
{
    /// <summary>
    /// Provides a static, centralized system for managing the application's state,
    /// particularly focusing on audio profiles. This includes loading, saving,
    /// adding, updating, and removing profiles, as well as managing the current active profile.
    /// It also handles application-level logging and critical error reporting.
    /// </summary>
    public static class sysState
    {
        /// <summary>
        /// Defines the base directory where application data, including profiles and settings, is stored.
        /// This path is typically in the user's application data folder for proper isolation.
        /// </summary>
        private static readonly string ProfilesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AudioMixerPro",
            "Profiles");

        /// <summary>
        /// The full file path for storing the collection of audio profiles in JSON format.
        /// </summary>
        private static readonly string ProfilesFile = Path.Combine(ProfilesDirectory, "profiles.json");

        /// <summary>
        /// The full file path for storing application settings, such as the ID of the last active profile.
        /// </summary>
        private static readonly string SettingsFile = Path.Combine(ProfilesDirectory, "settings.json");

        /// <summary>
        /// The full file path for the application's general log file.
        /// </summary>
        private static readonly string LogFile = Path.Combine(ProfilesDirectory, "application.log");

        /// <summary>
        /// The full file path for storing detailed crash reports.
        /// </summary>
        private static readonly string CrashLogFile = Path.Combine(ProfilesDirectory, "crash.log");

        /// <summary>
        /// An observable collection of <see cref="mdlProfile"/> objects. This collection
        /// holds all the audio profiles managed by the application. Using ObservableCollection
        /// allows UI elements bound to this collection to automatically update when profiles are added or removed.
        /// </summary>
        private static ObservableCollection<mdlProfile> _profiles;

        /// <summary>
        /// Stores the currently active audio profile. This profile is used for mixing operations.
        /// </summary>
        private static mdlProfile _currentProfile;

        /// <summary>
        /// A flag indicating whether the <see cref="sysState"/> system has been initialized.
        /// This prevents redundant initialization calls.
        /// </summary>
        private static bool _isInitialized = false;

        /// <summary>
        /// Event fired when the <see cref="CurrentProfile"/> property changes.
        /// Subscribers (e.g., UI components) can react to changes in the active profile.
        /// </summary>
        public static event EventHandler<mdlProfile> CurrentProfileChanged;

        /// <summary>
        /// Event fired when the collection of <see cref="Profiles"/> changes (e.g., a profile is added, updated, or removed).
        /// Subscribers can refresh their display of the profile list.
        /// </summary>
        public static event EventHandler ProfilesChanged;

        /// <summary>
        /// Event fired when a critical error occurs within the sysState system.
        /// Subscribers can display error messages or perform additional logging.
        /// </summary>
        public static event EventHandler<string> ErrorOccurred;

        /// <summary>
        /// Gets the observable collection of all audio profiles.
        /// If the system is not yet initialized, it triggers initialization first.
        /// </summary>
        public static ObservableCollection<mdlProfile> Profiles
        {
            get
            {
                if (!_isInitialized)
                    Initialize();
                return _profiles;
            }
        }

        /// <summary>
        /// Gets or sets the currently active audio profile.
        /// When set, it triggers the <see cref="CurrentProfileChanged"/> event and saves the new setting.
        /// Includes error handling for robustness.
        /// </summary>
        public static mdlProfile CurrentProfile
        {
            get
            {
                if (!_isInitialized)
                    Initialize();
                return _currentProfile;
            }
            set
            {
                try
                {
                    LogInfo($"Changing current profile from '{_currentProfile?.Name}' to '{value?.Name}'");

                    // Only update if the new profile is different from the current one.
                    if (_currentProfile != value)
                    {
                        _currentProfile = value;
                        // Invoke the event to notify subscribers of the change.
                        CurrentProfileChanged?.Invoke(null, _currentProfile);
                        // Save the updated current profile setting to disk.
                        SaveSettings();

                        LogInfo($"Current profile changed successfully to '{_currentProfile?.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    // Handle and log any exceptions during profile change, then re-throw.
                    HandleCriticalException("Failed to change current profile", ex);
                    throw; // Re-throw to propagate the exception.
                }
            }
        }

        /// <summary>
        /// Initializes the <see cref="sysState"/> system. This method is idempotent,
        /// meaning it runs its setup logic only once. It creates necessary directories,
        /// loads profiles and settings, and sets up logging. Includes robust error handling
        /// with a fallback to default profiles and a crash log mechanism.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return; // Prevent multiple initializations.

            try
            {
                LogInfo("=== Starting sysState Initialization ===");
                LogSystemInfo(); // Log system environment details.

                // Ensure the profiles directory exists.
                Directory.CreateDirectory(ProfilesDirectory);
                // Load existing profiles from file.
                LoadProfiles();
                // Load application settings (e.g., last active profile).
                LoadSettings();

                _isInitialized = true; // Mark as initialized.
                LogInfo("=== sysState Initialization Complete ===");
            }
            catch (Exception ex)
            {
                // If primary initialization fails, log the error.
                LogError($"Critical error during initialization: {ex}");

                try
                {
                    // Attempt a fallback to a default state (create default profiles).
                    LogInfo("Attempting fallback to default state...");
                    _profiles = new ObservableCollection<mdlProfile>(); // Initialize an empty collection.
                    CreateDefaultProfiles(); // Populate with default profiles.
                    _currentProfile = _profiles.FirstOrDefault(); // Set the first default as current.
                    _isInitialized = true; // Mark as initialized even after fallback.
                    LogInfo("Fallback to default state successful");
                }
                catch (Exception fallbackEx)
                {
                    // If even the fallback fails, it's a critical unrecoverable error.
                    var finalEx = new Exception("Both primary and fallback initialization failed", fallbackEx);
                    WriteCrashLog(finalEx); // Write a detailed crash log.
                    ShowExceptionWindow(finalEx); // Display a critical error window to the user.
                    // Throw a new exception to indicate complete system initialization failure.
                    throw new InvalidOperationException("System initialization failed completely", fallbackEx);
                }
            }
        }

        /// <summary>
        /// Adds a new audio profile to the collection.
        /// It validates the profile, ensures its name is unique (by appending a counter if needed),
        /// and then saves the updated profile list to disk.
        /// </summary>
        /// <param name="profile">The <see cref="mdlProfile"/> object to add.</param>
        /// <exception cref="ArgumentNullException">Thrown if the profile is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if profile validation fails or a name conflict occurs.</exception>
        public static void AddProfile(mdlProfile profile)
        {
            try
            {
                if (!_isInitialized) Initialize(); // Ensure system is initialized.

                if (profile == null)
                    throw new ArgumentNullException(nameof(profile));

                // Validate the profile's data.
                var errors = profile.Validate();
                if (errors.Any())
                    throw new InvalidOperationException($"Profile validation failed: {string.Join(", ", errors)}");

                int counter = 1;
                string originalName = profile.Name;
                // Ensure the profile name is unique by appending a counter if a duplicate exists.
                while (_profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    profile.Name = $"{originalName} ({counter++})";
                }

                _profiles.Add(profile); // Add the profile to the observable collection.
                SaveProfiles(); // Persist the updated profile list to file.
                ProfilesChanged?.Invoke(null, EventArgs.Empty); // Notify subscribers of the change.
            }
            catch (Exception ex)
            {
                HandleCriticalException("Failed to add profile", ex);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing audio profile in the collection.
        /// It validates the updated profile, checks for name conflicts with other profiles,
        /// and then saves the modified profile list to disk.
        /// </summary>
        /// <param name="profile">The <see cref="mdlProfile"/> object with updated data.</param>
        /// <exception cref="ArgumentNullException">Thrown if the profile is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the profile is not found,
        /// validation fails, or a name conflict occurs.</exception>
        public static void UpdateProfile(mdlProfile profile)
        {
            try
            {
                if (!_isInitialized) Initialize();

                if (profile == null)
                    throw new ArgumentNullException(nameof(profile));

                // Find the existing profile by its unique ID.
                var existing = _profiles.FirstOrDefault(p => p.Id == profile.Id);
                if (existing == null)
                    throw new InvalidOperationException("Profile not found for update.");

                // Validate the updated profile's data.
                var errors = profile.Validate();
                if (errors.Any())
                    throw new InvalidOperationException($"Profile validation failed: {string.Join(", ", errors)}");

                // Check for name conflicts with other profiles (excluding itself).
                if (_profiles.Any(p => p.Id != profile.Id && p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("A profile with this name already exists.");

                profile.Touch(); // Update the profile's last modified timestamp.
                // Replace the old profile object with the updated one in the collection.
                _profiles[_profiles.IndexOf(existing)] = profile;

                // If the updated profile is the currently active one, refresh the current profile reference.
                if (_currentProfile?.Id == profile.Id)
                    _currentProfile = profile;

                SaveProfiles(); // Persist the updated profile list.
                ProfilesChanged?.Invoke(null, EventArgs.Empty); // Notify subscribers.
            }
            catch (Exception ex)
            {
                HandleCriticalException("Failed to update profile", ex);
                throw;
            }
        }

        /// <summary>
        /// Removes an audio profile from the collection.
        /// If the removed profile was the currently active one, it attempts to set a new current profile
        /// (either the first available or a new default set).
        /// </summary>
        /// <param name="profile">The <see cref="mdlProfile"/> object to remove.</param>
        public static void RemoveProfile(mdlProfile profile)
        {
            try
            {
                if (!_isInitialized) Initialize();
                if (profile == null) return; // Nothing to do if profile is null.

                // Find the profile to remove by its ID.
                var existing = _profiles.FirstOrDefault(p => p.Id == profile.Id);
                if (existing == null) return; // Profile not found, nothing to remove.

                _profiles.Remove(existing); // Remove from the observable collection.

                // If the removed profile was the current active profile.
                if (_currentProfile?.Id == profile.Id)
                {
                    // Set the current profile to the first available, or create defaults if none exist.
                    _currentProfile = _profiles.FirstOrDefault() ?? CreateAndSetDefaultProfiles();
                    // Notify that the current profile has changed.
                    CurrentProfileChanged?.Invoke(null, _currentProfile);
                }

                SaveProfiles(); // Persist the updated profile list.
                ProfilesChanged?.Invoke(null, EventArgs.Empty); // Notify subscribers.
            }
            catch (Exception ex)
            {
                HandleCriticalException("Failed to remove profile", ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves an audio profile by its unique identifier.
        /// </summary>
        /// <param name="id">The unique ID of the profile.</param>
        /// <returns>The <see cref="mdlProfile"/> matching the ID, or <c>null</c> if not found.</returns>
        public static mdlProfile GetProfileById(string id)
        {
            try
            {
                if (!_isInitialized) Initialize();
                return _profiles.FirstOrDefault(p => p.Id == id);
            }
            catch (Exception ex)
            {
                HandleCriticalException("Error getting profile by ID", ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves an audio profile by its name (case-insensitive).
        /// </summary>
        /// <param name="name">The name of the profile.</param>
        /// <returns>The <see cref="mdlProfile"/> matching the name, or <c>null</c> if not found.</returns>
        public static mdlProfile GetProfileByName(string name)
        {
            try
            {
                if (!_isInitialized) Initialize();
                return _profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                HandleCriticalException("Error getting profile by name", ex);
                throw;
            }
        }

        /// <summary>
        /// Checks if a profile with a given name already exists in the collection,
        /// optionally excluding a profile with a specific ID (useful for update scenarios).
        /// </summary>
        /// <param name="name">The name to check for existence.</param>
        /// <param name="excludeId">Optional: The ID of a profile to exclude from the check (e.g., the profile being updated).</param>
        /// <returns><c>true</c> if a profile with the given name exists (and is not excluded); otherwise, <c>false</c>.</returns>
        public static bool ProfileNameExists(string name, string excludeId = null)
        {
            try
            {
                if (!_isInitialized) Initialize();
                // Checks if any profile matches the name (case-insensitive) AND has a different ID (if excludeId is provided).
                return _profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.Id != excludeId);
            }
            catch (Exception ex)
            {
                HandleCriticalException("Error checking if profile name exists", ex);
                throw;
            }
        }

        /// <summary>
        /// Resets the application's profiles to their default set and clears existing ones.
        /// Also resets the current active profile to the first default.
        /// </summary>
        public static void ResetToDefaults()
        {
            try
            {
                _profiles.Clear(); // Clear all existing profiles.
                CreateDefaultProfiles(); // Re-populate with default profiles.
                _currentProfile = _profiles.FirstOrDefault(); // Set the first default as current.

                SaveProfiles(); // Save the new default profiles to disk.
                SaveSettings(); // Save the updated current profile setting.

                // Notify UI components of the changes.
                CurrentProfileChanged?.Invoke(null, _currentProfile);
                ProfilesChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                HandleCriticalException("Failed to reset to defaults", ex);
                throw;
            }
        }

        /// <summary>
        /// Exports all current audio profiles to a specified JSON file.
        /// </summary>
        /// <param name="filePath">The full path to the file where profiles will be exported.</param>
        public static void ExportProfiles(string filePath)
        {
            try
            {
                if (!_isInitialized) Initialize();
                // Serialize the profiles list to a human-readable (indented) JSON string.
                var json = JsonConvert.SerializeObject(_profiles.ToList(), Formatting.Indented);
                // Write the JSON string to the specified file.
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                HandleCriticalException("Failed to export profiles", ex);
                throw;
            }
        }

        /// <summary>
        /// Imports audio profiles from a specified JSON file.
        /// Existing profiles can either be cleared before import (merge = false) or
        /// new profiles can be merged with existing ones. Imported profiles are given new unique IDs
        /// and their names are made unique if conflicts arise.
        /// </summary>
        /// <param name="filePath">The full path to the JSON file containing profiles to import.</param>
        /// <param name="merge">If <c>true</c>, imported profiles are added to existing ones.
        /// If <c>false</c>, existing profiles are cleared before import.</param>
        /// <exception cref="FileNotFoundException">Thrown if the import file does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown if no valid profiles are found in the import file.</exception>
        public static void ImportProfiles(string filePath, bool merge = true)
        {
            try
            {
                if (!_isInitialized) Initialize();

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Import file not found.");

                var json = File.ReadAllText(filePath);
                // Deserialize the JSON content into a list of mdlProfile objects.
                var imported = JsonConvert.DeserializeObject<List<mdlProfile>>(json);

                if (imported == null || !imported.Any())
                    throw new InvalidOperationException("No valid profiles found in import file.");

                if (!merge) _profiles.Clear(); // Clear existing profiles if not merging.

                foreach (var profile in imported)
                {
                    // Assign a new unique ID to each imported profile to prevent conflicts with existing ones.
                    profile.Id = Guid.NewGuid().ToString();
                    string original = profile.Name;
                    int counter = 1;

                    // Ensure imported profile names are unique within the current collection.
                    while (_profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        profile.Name = $"{original} (Imported {counter++})";
                    }

                    _profiles.Add(profile); // Add the processed imported profile to the collection.
                }

                SaveProfiles(); // Persist the updated profile list.
                ProfilesChanged?.Invoke(null, EventArgs.Empty); // Notify subscribers.
            }
            catch (Exception ex)
            {
                HandleCriticalException("Failed to import profiles", ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves the entire content of the application's log file.
        /// </summary>
        /// <returns>A string containing the log file content, or a message if the file is not found.</returns>
        public static string GetLogContent() =>
            File.Exists(LogFile) ? File.ReadAllText(LogFile) : "No log file found.";

        /// <summary>
        /// Gathers and returns a string containing various system diagnostic information.
        /// This is useful for debugging and support purposes.
        /// </summary>
        /// <returns>A formatted string with system diagnostics.</returns>
        public static string GetSystemDiagnostics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SYSTEM DIAGNOSTICS ===");
            sb.AppendLine($"App Version: {Assembly.GetExecutingAssembly().GetName().Version}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"CLR: {Environment.Version}"); // Common Language Runtime version.
            sb.AppendLine($"User: {Environment.UserName}");
            sb.AppendLine($"Machine: {Environment.MachineName}");
            sb.AppendLine($"Working Dir: {Environment.CurrentDirectory}");
            sb.AppendLine($"Profiles: {_profiles?.Count ?? 0}"); // Number of loaded profiles.
            sb.AppendLine($"Current: {_currentProfile?.Name ?? "None"}"); // Name of current profile.
            sb.AppendLine($"Initialized: {_isInitialized}"); // Initialization status.
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            return sb.ToString();
        }

        /// <summary>
        /// Clears the application's log file by deleting it.
        /// </summary>
        public static void ClearLog()
        {
            try
            {
                if (File.Exists(LogFile))
                    File.Delete(LogFile);
            }
            catch (Exception ex)
            {
                LogError($"Failed to clear log: {ex}"); // Log any errors during log clearing.
            }
        }

        /// <summary>
        /// Loads audio profiles from the <see cref="ProfilesFile"/>.
        /// If the file does not exist or loading fails, it initializes an empty collection
        /// and creates default profiles.
        /// </summary>
        private static void LoadProfiles()
        {
            try
            {
                if (File.Exists(ProfilesFile))
                {
                    var json = File.ReadAllText(ProfilesFile);
                    // Deserialize the JSON into an ObservableCollection of profiles.
                    _profiles = new ObservableCollection<mdlProfile>(
                        JsonConvert.DeserializeObject<List<mdlProfile>>(json) ?? new List<mdlProfile>());
                }
                else
                {
                    // If the profiles file doesn't exist, start with an empty collection.
                    _profiles = new ObservableCollection<mdlProfile>();
                }

                // If no profiles were loaded (either file was empty or didn't exist), create default ones.
                if (!_profiles.Any())
                    CreateDefaultProfiles();
            }
            catch (Exception ex)
            {
                // Log the error if loading fails and fall back to default profiles.
                LogError($"LoadProfiles failed: {ex}");
                _profiles = new ObservableCollection<mdlProfile>();
                CreateDefaultProfiles();
            }
        }

        /// <summary>
        /// Saves the current collection of audio profiles to the <see cref="ProfilesFile"/> in JSON format.
        /// </summary>
        private static void SaveProfiles()
        {
            try
            {
                // Serialize the current profiles to an indented JSON string.
                var json = JsonConvert.SerializeObject(_profiles.ToList(), Formatting.Indented);
                // Write the JSON string to the profiles file.
                File.WriteAllText(ProfilesFile, json);
            }
            catch (Exception ex)
            {
                // Log any errors that occur during saving profiles.
                HandleCriticalException("SaveProfiles failed", ex);
            }
        }

        /// <summary>
        /// Loads application settings from the <see cref="SettingsFile"/>.
        /// Currently, this primarily involves loading the ID of the last active profile
        /// and setting it as the <see cref="CurrentProfile"/>.
        /// </summary>
        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    // Deserialize settings into a dictionary.
                    var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    // Attempt to retrieve and set the current profile based on its ID.
                    if (settings.TryGetValue("CurrentProfileId", out var id))
                        _currentProfile = _profiles.FirstOrDefault(p => p.Id == id.ToString());
                }

                // If no current profile was loaded from settings (or settings file didn't exist),
                // default to the first available profile.
                if (_currentProfile == null)
                {
                    _currentProfile = _profiles.FirstOrDefault();
                }

            }
            catch (Exception ex)
            {
                // Log errors during settings loading and default to the first profile.
                LogError($"LoadSettings failed: {ex}");
                _currentProfile = _profiles.FirstOrDefault();
            }
        }

        /// <summary>
        /// Saves application settings (specifically the current profile ID) to the <see cref="SettingsFile"/>.
        /// </summary>
        private static void SaveSettings()
        {
            try
            {
                // Create a dictionary to hold settings, including the current profile's ID.
                var settings = new Dictionary<string, object>
                {
                    ["CurrentProfileId"] = _currentProfile?.Id ?? string.Empty // Store ID or empty string if no profile.
                };
                // Serialize settings to an indented JSON string.
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                // Write the JSON string to the settings file.
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                // Log any errors during saving settings.
                LogError($"SaveSettings failed: {ex}");
            }
        }

        /// <summary>
        /// Populates the <see cref="_profiles"/> collection with a set of pre-defined default audio profiles.
        /// </summary>
        private static void CreateDefaultProfiles()
        {
            _profiles.Add(mdlProfile.Create3CXTemplate());
            _profiles.Add(mdlProfile.CreateFreePBXTemplate());
            _profiles.Add(mdlProfile.CreateSangomaTemplate());
            _profiles.Add(mdlProfile.CreateGenesysTemplate());
            _profiles.Add(mdlProfile.Create8x8Template());
            _profiles.Add(mdlProfile.CreateHighQualityTemplate());
        }

        /// <summary>
        /// Creates the default profiles and sets the first one as the current active profile.
        /// This is used as a fallback mechanism during initialization or when resetting to defaults.
        /// </summary>
        /// <returns>The first default profile created.</returns>
        private static mdlProfile CreateAndSetDefaultProfiles()
        {
            CreateDefaultProfiles();
            return _profiles.FirstOrDefault();
        }

        /// <summary>
        /// Handles critical exceptions by logging the error, writing a crash log,
        /// and displaying a user-friendly exception window.
        /// </summary>
        /// <param name="context">A string describing the context in which the exception occurred.</param>
        /// <param name="ex">The <see cref="Exception"/> object that occurred.</param>
        public static void HandleCriticalException(string context, Exception ex)
        {
            LogError($"{context}: {ex}"); // Log the error message.
            WriteCrashLog(ex); // Write a detailed crash log to file.
            ShowExceptionWindow(ex); // Display a window to the user with exception details.
        }

        /// <summary>
        /// Displays a dedicated exception window to the user, showing details of a critical error.
        /// It attempts to do this on the UI thread. If the UI dispatcher is unavailable, it falls back
        /// to a simple Windows Forms message box.
        /// </summary>
        /// <param name="ex">The <see cref="Exception"/> object to display.</param>
        private static void ShowExceptionWindow(Exception ex)
        {
            try
            {
                // Check if the WPF Application dispatcher is available.
                if (Application.Current?.Dispatcher != null)
                {
                    // Invoke on the UI thread to ensure safe UI interaction.
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Create and show a custom exception window.
                        new wndException(ex.ToString(), CrashLogFile).ShowDialog();
                    });
                }
                else
                {
                    // Fallback to a simple Windows Forms message box if WPF dispatcher is not available.
                    // This might happen in console applications or very early startup failures.
                    System.Windows.Forms.MessageBox.Show(
                        "A critical error occurred:\n\n" + ex.Message,
                        "Critical Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error
                    );
                }
            }
            catch
            {
                // If even showing the exception window fails, there's nothing more to do.
                // This catch block prevents an infinite loop of exceptions.
            }
        }

        /// <summary>
        /// Writes a detailed crash log to the <see cref="CrashLogFile"/>.
        /// Includes timestamp, exception message, stack trace, inner exception details, and system diagnostics.
        /// </summary>
        /// <param name="ex">The <see cref="Exception"/> object representing the crash.</param>
        private static void WriteCrashLog(Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== CRASH REPORT ===");
                sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null) // Include details of inner exceptions if present.
                {
                    sb.AppendLine("=== INNER EXCEPTION ===");
                    sb.AppendLine($"Message: {ex.InnerException.Message}");
                    sb.AppendLine($"Stack Trace: {ex.InnerException.StackTrace}");
                }
                sb.AppendLine();
                sb.AppendLine(GetSystemDiagnostics()); // Append general system diagnostics.

                File.WriteAllText(CrashLogFile, sb.ToString()); // Write the compiled report to file.
            }
            catch { } // Suppress any exceptions during crash log writing to avoid further issues.
        }

        /// <summary>
        /// Logs an informational message to the application log file.
        /// </summary>
        /// <param name="message">The informational message to log.</param>
        private static void LogInfo(string message) => WriteToLog("INFO", message);

        /// <summary>
        /// Logs a warning message to the application log file.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        private static void LogWarning(string message) => WriteToLog("WARN", message);

        /// <summary>
        /// Logs an error message to the application log file and triggers the <see cref="ErrorOccurred"/> event.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        private static void LogError(string message)
        {
            WriteToLog("ERROR", message);
            ErrorOccurred?.Invoke(null, message); // Notify subscribers of the error.
        }

        /// <summary>
        /// Internal helper method to write a formatted log entry to the <see cref="LogFile"/>.
        /// Also writes to Debug output for immediate feedback during development.
        /// </summary>
        /// <param name="level">The log level (e.g., "INFO", "WARN", "ERROR").</param>
        /// <param name="message">The message content to log.</param>
        private static void WriteToLog(string level, string message)
        {
            try
            {
                // Format the log entry with timestamp, level, and message.
                string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                // Append the log entry to the log file.
                File.AppendAllText(LogFile, log + Environment.NewLine);
                // Also write to the Debug output window.
                Debug.WriteLine($"sysState: {log}");
            }
            catch { } // Suppress any exceptions during log writing.
        }

        /// <summary>
        /// Logs various system and application information to the log file.
        /// Called during initialization to provide context for subsequent log entries.
        /// </summary>
        private static void LogSystemInfo()
        {
            LogInfo($"OS: {Environment.OSVersion}");
            LogInfo($"CLR: {Environment.Version}");
            LogInfo($"App Version: {Assembly.GetExecutingAssembly().GetName().Version}");
            LogInfo($"Working Dir: {Environment.CurrentDirectory}");
        }
    }
}
