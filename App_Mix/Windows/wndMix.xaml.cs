using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.MediaFoundation;
using App_Mix.Systems;
using App_Mix.Models;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

namespace App_Mix.Windows
{
    /// <summary>
    /// Interaction logic for wndMix.xaml. This window provides the main user interface
    /// for selecting voice and music audio files, setting mixing parameters (volume, offset, buffer),
    /// previewing audio, and initiating the audio mixing process. It also manages user profiles
    /// for audio settings and uses the selected profile to determine output format.
    /// </summary>
    public partial class wndMix : Window
    {
        /// <summary>
        /// Stores the file path for the selected voice audio file.
        /// </summary>
        private string voiceFilePath;

        /// <summary>
        /// Stores the file path for the selected music audio file.
        /// </summary>
        private string musicFilePath;

        /// <summary>
        /// Stores the file path where the mixed audio output will be saved.
        /// </summary>
        private string outputFilePath;

        /// <summary>
        /// NAudio object responsible for playing audio waves.
        /// </summary>
        private IWavePlayer waveOutDevice;

        /// <summary>
        /// NAudio object used to read audio data from a file.
        /// </summary>
        private AudioFileReader audioFileReader;

        /// <summary>
        /// Reference to the Button control that is currently associated with playing audio.
        /// Used to update its text (e.g., Play/Pause icon) based on playback state.
        /// </summary>
        private Button currentPlayingButton;

        /// <summary>
        /// Stores the file path of the audio file currently being played.
        /// </summary>
        private string currentPlayingFile;

        /// <summary>
        /// Temporary file path for the extracted main theme audio resource.
        /// </summary>
        private string tempMainThemeFile;

        /// <summary>
        /// Initializes a new instance of the <see cref="wndMix"/> class.
        /// This constructor sets up the UI components and initializes necessary audio libraries
        /// and user profile management.
        /// </summary>
        public wndMix()
        {
            InitializeComponent();
            // Initializes the MediaFoundation API, which is required by NAudio for
            // decoding MP3 files on Windows. This must be called once at application startup.
            MediaFoundationApi.Startup();

            // Initializes and loads user profiles for audio settings.
            InitializeProfiles();

            // Subscribe to the Loaded event to play main theme when the window appears
            this.Loaded += WndMix_Loaded;
        }

        /// <summary>
        /// Initializes the application's profile management system.
        /// This includes initializing the global state manager (sysState) and
        /// subscribing to events for profile changes to keep the UI updated.
        /// </summary>
        private void InitializeProfiles()
        {
            // Ensures the sysState singleton is initialized, which handles loading and saving profiles.
            sysState.Initialize();

            // Populates the profile dropdown with available profiles.
            RefreshProfileDropdown();

            // Subscribes to events that notify when the current profile changes or the list of profiles changes,
            // allowing the UI to react accordingly.
            sysState.CurrentProfileChanged += OnCurrentProfileChanged;
            sysState.ProfilesChanged += OnProfilesChanged;
        }

        /// <summary>
        /// Refreshes the items displayed in the profile dropdown (CmbProfiles).
        /// It rebinds the ItemsSource to the current list of profiles and
        /// attempts to select the currently active profile.
        /// </summary>
        private void RefreshProfileDropdown()
        {
            // Clears the current items to ensure a fresh load.
            CmbProfiles.ItemsSource = null;
            // Binds the dropdown to the list of profiles managed by sysState.
            CmbProfiles.ItemsSource = sysState.Profiles;

            // If a current profile is set in the system state, select it in the dropdown.
            if (sysState.CurrentProfile != null)
            {
                CmbProfiles.SelectedItem = sysState.CurrentProfile;
            }
        }

        /// <summary>
        /// Event handler for when the current profile in <see cref="sysState"/> changes.
        /// Updates the selected item in the profile dropdown to reflect the new current profile.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The new current profile (<see cref="mdlProfile"/>).</param>
        private void OnCurrentProfileChanged(object sender, mdlProfile e)
        {
            CmbProfiles.SelectedItem = e;
        }

        /// <summary>
        /// Event handler for when the collection of profiles in <see cref="sysState"/> changes.
        /// Triggers a refresh of the profile dropdown to show the updated list.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void OnProfilesChanged(object sender, EventArgs e)
        {
            RefreshProfileDropdown();
        }

        /// <summary>
        /// Event handler for when the main window is fully loaded.
        /// Extracts and plays the embedded main theme audio resource.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private async void WndMix_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = await sysPlayback.Instance.PlayEmbeddedResourceAsync(
                    "ProfMix_Main_Mixed.wav", 0.5f);

                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to play main theme");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Main theme error: {ex.Message}");
            }
        }

        #region File Selection Events

        /// <summary>
        /// Event handler for the "Select Voice File" button click.
        /// Opens a file dialog to allow the user to select a voice audio file (WAV or MP3).
        /// Updates the UI with the selected file path and enables the preview button.
        /// </summary>
        /// <param name="sender">The source of the event (BtnSelectVoice).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnSelectVoice_Click(object sender, RoutedEventArgs e)
        {
            // Configures an OpenFileDialog to filter for audio files.
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*"
            };

            // Shows the dialog and processes the selected file if the user clicks OK.
            if (openFileDialog.ShowDialog() == true)
            {
                voiceFilePath = openFileDialog.FileName;
                TxtVoicePath.Text = voiceFilePath; // Displays the selected file path in the UI.
                BtnPreviewVoice.IsEnabled = true; // Enables the preview button for the voice file.

                // Stops any currently playing audio to prevent conflicts when a new file is selected.
                StopAudioAndResetButtons();

                // Displays a success notification to the user.
                sysInteract.ShowSuccess("Voice File Selected", $"Voice file loaded: {Path.GetFileName(voiceFilePath)}");
            }
        }

        /// <summary>
        /// Event handler for the "Help" button click.
        /// Opens a new help window (<see cref="wndHelp"/>) to provide assistance to the user.
        /// </summary>
        /// <param name="sender">The source of the event (BtnHelp).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new wndHelp();
            helpWindow.Show(); // Shows the help window non-modally. Use ShowDialog() for modal behavior.
        }

        /// <summary>
        /// Event handler for the "Select Music File" button click.
        /// Opens a file dialog to allow the user to select a music audio file (WAV or MP3).
        /// Updates the UI with the selected file path and enables the preview button.
        /// </summary>
        /// <param name="sender">The source of the event (BtnSelectMusic).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnSelectMusic_Click(object sender, RoutedEventArgs e)
        {
            // Configures an OpenFileDialog to filter for audio files.
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*"
            };

            // Shows the dialog and processes the selected file if the user clicks OK.
            if (openFileDialog.ShowDialog() == true)
            {
                musicFilePath = openFileDialog.FileName;
                TxtMusicPath.Text = musicFilePath; // Displays the selected file path in the UI.
                BtnPreviewMusic.IsEnabled = true; // Enables the preview button for the music file.

                // Stops any currently playing audio to prevent conflicts when a new file is selected.
                StopAudioAndResetButtons();

                // Displays a success notification to the user.
                sysInteract.ShowSuccess("Music File Selected", $"Music file loaded: {Path.GetFileName(musicFilePath)}");
            }
        }

        /// <summary>
        /// Event handler for the "Select Output Path" button click.
        /// Opens a save file dialog to allow the user to specify where the mixed audio file will be saved.
        /// Updates the UI with the selected output path.
        /// </summary>
        /// <param name="sender">The source of the event (BtnSelectOutputPath).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnSelectOutputPath_Click(object sender, RoutedEventArgs e)
        {
            // Configures a SaveFileDialog to save as a WAV file by default.
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "WAV Files (*.wav)|*.wav",
                DefaultExt = ".wav"
            };

            // Shows the dialog and processes the selected path if the user clicks OK.
            if (saveFileDialog.ShowDialog() == true)
            {
                outputFilePath = saveFileDialog.FileName;
                TxtOutputPath.Text = outputFilePath; // Displays the selected output path in the UI.

                // Displays an informational notification to the user.
                sysInteract.ShowInfo("Output Path Set", $"Output will be saved to: {Path.GetFileName(outputFilePath)}");
            }
        }

        #endregion

        #region Preview Events

        /// <summary>
        /// Event handler for the "Preview Voice" button click.
        /// Toggles playback of the selected voice audio file.
        /// </summary>
        /// <param name="sender">The source of the event (BtnPreviewVoice).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnPreviewVoice_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayback(voiceFilePath, BtnPreviewVoice);
        }

        /// <summary>
        /// Event handler for the "Preview Music" button click.
        /// Toggles playback of the selected music audio file.
        /// </summary>
        /// <param name="sender">The source of the event (BtnPreviewMusic).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnPreviewMusic_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayback(musicFilePath, BtnPreviewMusic);
        }

        /// <summary>
        /// Event handler for the "Preview Mixed" button click.
        /// Toggles playback of the mixed audio file. Before attempting playback,
        /// it checks if the mixed file actually exists.
        /// </summary>
        /// <param name="sender">The source of the event (BtnPreviewMixed).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnPreviewMixed_Click(object sender, RoutedEventArgs e)
        {
            // Checks if the output file exists before attempting to play it.
            if (File.Exists(outputFilePath))
            {
                TogglePlayback(outputFilePath, BtnPreviewMixed);
            }
            else
            {
                // Displays a warning if the mixed file has not yet been created.
                sysInteract.ShowWarning("File Not Found", "Mixed file does not exist. Please mix the files first.");
            }
        }

        #endregion

        #region Profile Events

        /// <summary>
        /// Event handler for when the selected item in the profile dropdown changes.
        /// Updates the application's current profile in <see cref="sysState"/> to the newly selected one.
        /// </summary>
        /// <param name="sender">The source of the event (CmbProfiles).</param>
        /// <param name="e">Event arguments containing information about the selection change.</param>
        private void CmbProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Casts the selected item to an mdlProfile and updates the system state.
            if (CmbProfiles.SelectedItem is mdlProfile selectedProfile)
            {
                sysState.CurrentProfile = selectedProfile;
                sysInteract.ShowInfo("Profile Selected",
                    $"Using profile: {selectedProfile.Name}\nFormat: {selectedProfile.FormatDisplay}\nMax size: {selectedProfile.MaxSizeDisplay}");
            }
        }

        /// <summary>
        /// Event handler for the "Manage Profiles" button click.
        /// Opens a new window (<see cref="wndProfile"/>) to allow the user to manage (add, edit, delete)
        /// audio profiles. Refreshes the profile dropdown upon the profile management window's closure.
        /// </summary>
        /// <param name="sender">The source of the event (BtnManageProfiles).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnManageProfiles_Click(object sender, RoutedEventArgs e)
        {
            var profileWindow = new wndProfile();
            // Shows the profile window modally. If the window is closed with a positive result (e.g., "Apply & Close"),
            // the profile dropdown is refreshed.
            if (profileWindow.ShowDialog() == true)
            {
                // The profile window is responsible for updating sysState directly.
                // This call ensures the dropdown reflects any changes made.
                RefreshProfileDropdown();
            }
        }

        #endregion

        #region Main Mixing Event

        /// <summary>
        /// Event handler for the "Mix Audio" button click.
        /// Gathers all necessary mixing parameters from the UI, performs validation,
        /// and then initiates the audio mixing process using the <see cref="sysMixer"/> system.
        /// Now properly passes the selected profile to determine output format.
        /// Displays progress, success, or error notifications to the user.
        /// </summary>
        /// <param name="sender">The source of the event (BtnMix).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnMix_Click(object sender, RoutedEventArgs e)
        {
            // Ensures any active audio playback is stopped before starting a new mixing operation.
            StopAudioAndResetButtons();

            // Retrieves the currently active audio profile from the system state.
            var currentProfile = sysState.CurrentProfile;
            if (currentProfile == null)
            {
                // Warns the user if no profile is selected, as the output format depends on it.
                sysInteract.ShowWarning("No Profile Selected", "No audio profile selected. Please select a profile first.");
                return; // Aborts the mixing process.
            }

            // Creates a MixingSettings object, populating it with values from the UI controls.
            var mixingSettings = new sysMixer.MixingSettings
            {
                VoiceFilePath = voiceFilePath,
                MusicFilePath = musicFilePath,
                OutputFilePath = outputFilePath,
                // Convert slider values (0-100) to float volumes (0.0-1.0).
                VoiceVolume = (float)SliderVoiceVolume.Value / 100,
                MusicVolume = (float)SliderMusicVolume.Value / 100,
                MusicOffsetSeconds = SliderMusicOffset.Value,
                BufferSeconds = SliderBufferLength.Value,
                // **IMPORTANT: Pass the selected profile to the mixer!**
                OutputProfile = currentProfile
            };

            // Performs basic validation to ensure all required file paths are set.
            if (string.IsNullOrEmpty(mixingSettings.VoiceFilePath) ||
                string.IsNullOrEmpty(mixingSettings.MusicFilePath) ||
                string.IsNullOrEmpty(mixingSettings.OutputFilePath))
            {
                sysInteract.ShowError("Missing Files", "Please select voice, music, and output files first.");
                return; // Aborts the mixing process.
            }

            // Checks the estimated output file size against the profile's limit.
            TimeSpan estimatedDuration;
            if (sysMixer.IsValidAudioFile(mixingSettings.VoiceFilePath))
            {
                // Calculates the estimated duration based on voice file length and buffer.
                estimatedDuration = sysMixer.GetAudioDuration(mixingSettings.VoiceFilePath)
                    .Add(TimeSpan.FromSeconds(mixingSettings.BufferSeconds));

                // Use the new profile-aware file size checking method
                if (!sysMixer.CheckFileSizeForProfile(estimatedDuration, currentProfile, out long estimatedMB))
                {
                    var continueAnyway = sysInteract.ShowConfirmation(
                        "File Size Warning",
                        $"The estimated output file size will be {estimatedMB}MB, which exceeds the '{currentProfile.Name}' profile's {currentProfile.MaxFileSizeMB}MB limit.\n\n" +
                        $"Output format: {currentProfile.FormatDisplay}\n\n" +
                        $"Do you want to continue anyway?");

                    if (!continueAnyway)
                        return; // Aborts if the user chooses not to continue.
                }
                else
                {
                    // Show a preview of what will be created
                    sysInteract.ShowInfo("Mixing Preview",
                        $"Ready to mix with '{currentProfile.Name}' profile\n" +
                        $"Output format: {currentProfile.FormatDisplay}\n" +
                        $"Estimated duration: {estimatedDuration:mm\\:ss}\n" +
                        $"Estimated size: {estimatedMB}MB (limit: {currentProfile.MaxFileSizeMB}MB)");
                }
            }

            try
            {
                // Displays a progress notification while the mixing operation is in progress.
                var progressNotification = sysInteract.ShowProgress("Mixing Audio",
                    $"Processing audio files using '{currentProfile.Name}' profile...\n" +
                    $"Output format: {currentProfile.FormatDisplay}");

                // Calls the core mixing logic in the sysMixer system with the profile.
                var mixingResult = sysMixer.MixAudioFiles(mixingSettings);

                // Closes the progress notification once mixing is complete (or an error occurs).
                progressNotification?.CloseNotification();

                if (mixingResult.Success)
                {
                    // If mixing was successful, displays a success notification with profile details.
                    var fileSizeMB = mixingResult.FileSizeBytes / (1024 * 1024); // Converts bytes to megabytes.

                    // Enhanced success message showing the profile that was used
                    sysInteract.ShowSuccess("Mixing Complete!",
                        $"Audio mixed successfully!\n\n" +
                        $"Output file: {Path.GetFileName(outputFilePath)}\n" +
                        $"Profile used: {mixingResult.UsedProfile.Name}\n" +
                        $"Format: {mixingResult.UsedProfile.FormatDisplay}\n" +
                        $"File size: {fileSizeMB}MB (limit: {mixingResult.UsedProfile.MaxFileSizeMB}MB)\n" +
                        $"Duration: {mixingResult.TotalDuration:mm\\:ss}");

                    // Enables the preview button for the mixed file.
                    BtnPreviewMixed.IsEnabled = true;
                }
                else
                {
                    // If mixing failed, displays an error notification with the error message.
                    sysInteract.ShowError("Mixing Failed",
                        $"Failed to mix audio files:\n\n{mixingResult.ErrorMessage}\n\n" +
                        $"Profile: {currentProfile.Name} ({currentProfile.FormatDisplay})");
                }
            }
            catch (Exception ex)
            {
                // Catches any unexpected exceptions during the mixing process and displays an error.
                sysInteract.ShowError("Unexpected Error",
                    $"An unexpected error occurred during mixing:\n\n{ex.Message}\n\n" +
                    $"Profile: {currentProfile.Name} ({currentProfile.FormatDisplay})");
            }
        }

        #endregion

        #region Audio Playback Management

        /// <summary>
        /// Toggles the playback state (play/pause/stop) for a given audio file.
        /// If the same file is already playing, it pauses/resumes. If a different file
        /// is playing, it stops the current playback and starts the new one.
        /// </summary>
        /// <param name="filePath">The path to the audio file to play.</param>
        /// <param name="button">The UI button associated with this playback (e.g., Preview Voice, Preview Music).</param>
        private void TogglePlayback(string filePath, Button button)
        {
            try
            {
                // Checks if audio is currently playing and if it's the same file.
                if (waveOutDevice != null && currentPlayingFile == filePath)
                {
                    if (waveOutDevice.PlaybackState == PlaybackState.Playing)
                    {
                        // If playing, pause it and update the button icon.
                        waveOutDevice.Pause();
                        button.Content = button.Content.ToString().Replace("⏸", "▶");
                        button.Tag = "Play"; // Update button tag to reflect "Play" state.
                        sysInteract.ShowInfo("Playback Paused", $"Paused: {Path.GetFileName(filePath)}", 2.0);
                    }
                    else if (waveOutDevice.PlaybackState == PlaybackState.Paused)
                    {
                        // If paused, resume playback and update the button icon.
                        waveOutDevice.Play();
                        button.Content = button.Content.ToString().Replace("▶", "⏸");
                        button.Tag = "Pause"; // Update button tag to reflect "Pause" state.
                        sysInteract.ShowInfo("Playback Resumed", $"Playing: {Path.GetFileName(filePath)}", 2.0);
                    }
                    return; // Exit as action for current file is complete.
                }

                // If a different file is playing or no file is playing, stop any existing playback
                // and reset all preview buttons to their default "Play" state.
                StopAudioAndResetButtons();

                // Starts playing the newly selected file.
                PlayAudio(filePath, button);
            }
            catch (Exception ex)
            {
                // Handles and displays any errors encountered during audio playback control.
                sysInteract.ShowError("Playback Error", $"Error controlling audio playback: {ex.Message}");
            }
        }

        /// <summary>
        /// Initiates audio playback for a specified file.
        /// Sets up the NAudio player, updates the UI button, and subscribes to the PlaybackStopped event
        /// to clean up resources when playback finishes.
        /// </summary>
        /// <param name="filePath">The path to the audio file to play.</param>
        /// <param name="button">The UI button associated with this playback.</param>
        private void PlayAudio(string filePath, Button button)
        {
            try
            {
                // Initializes AudioFileReader to read the specified audio file.
                audioFileReader = new AudioFileReader(filePath);
                // Initializes WaveOut for audio output.
                waveOutDevice = new WaveOut();
                // Initializes the waveOutDevice with the audioFileReader.
                waveOutDevice.Init(audioFileReader);

                // Stores references to the currently playing button and file path.
                currentPlayingButton = button;
                currentPlayingFile = filePath;

                // Updates the button content to show a "Pause" icon and sets its tag.
                button.Content = button.Content.ToString().Replace("▶", "⏸");
                button.Tag = "Pause";

                // Displays a notification indicating that playback has started.
                sysInteract.ShowInfo("Playback Started", $"Playing: {Path.GetFileName(filePath)}", 2.0);

                // Subscribes to the PlaybackStopped event to handle cleanup when the audio finishes playing.
                waveOutDevice.PlaybackStopped += (s, e) =>
                {
                    // Uses Dispatcher.Invoke to ensure UI updates happen on the main UI thread.
                    Dispatcher.Invoke(() =>
                    {
                        // Stops audio and resets buttons.
                        StopAudioAndResetButtons();
                        // Displays a notification that playback is complete.
                        sysInteract.ShowInfo("Playback Complete", $"Finished playing: {Path.GetFileName(filePath)}", 2.0);
                    });
                };

                // Starts the audio playback.
                waveOutDevice.Play();
            }
            catch (Exception ex)
            {
                // Handles and displays any errors that occur during audio playback initialization or start.
                sysInteract.ShowError("Playback Error", $"Error playing audio: {ex.Message}");
                // Ensures audio resources are cleaned up even if an error occurs.
                StopAudioAndResetButtons();
            }
        }

        /// <summary>
        /// Stops any currently active audio playback and disposes of NAudio resources.
        /// Resets the internal state variables related to playback.
        /// </summary>
        private void StopAudio()
        {
            // Stops and disposes the wave output device if it's active.
            if (waveOutDevice != null)
            {
                waveOutDevice.Stop();
                waveOutDevice.Dispose();
                waveOutDevice = null;
            }

            // Disposes the audio file reader if it's active.
            if (audioFileReader != null)
            {
                audioFileReader.Dispose();
                audioFileReader = null;
            }

            // Clears the currently playing file path.
            currentPlayingFile = null;
        }

        /// <summary>
        /// Stops any currently active audio playback and resets the content and tag of all
        /// preview buttons to their default "Play" state.
        /// </summary>
        private void StopAudioAndResetButtons()
        {
            // Calls the core method to stop audio and dispose resources.
            StopAudio();

            // Resets the text and tag of all relevant preview buttons.
            ResetButtonToPlayState(BtnPreviewVoice, "▶ Preview");
            ResetButtonToPlayState(BtnPreviewMusic, "▶ Preview");
            ResetButtonToPlayState(BtnPreviewMixed, "🔊 Preview Result");

            // Clears the reference to the button that was previously playing.
            currentPlayingButton = null;
        }

        /// <summary>
        /// Resets a specific UI button to its default "Play" state, updating its content and tag.
        /// </summary>
        /// <param name="button">The Button control to reset.</param>
        /// <param name="originalContent">The original text content for the button (e.g., "▶ Preview").</param>
        private void ResetButtonToPlayState(Button button, string originalContent)
        {
            if (button != null)
            {
                button.Content = originalContent; // Sets the button text back to the "Play" icon and label.
                button.Tag = "Play"; // Sets the button's tag to indicate it's in a "Play" state.
            }
        }

        #endregion

        #region Window Lifecycle

        /// <summary>
        /// Overrides the OnClosed event handler for the window.
        /// This method is called when the window is about to close. It ensures that
        /// all audio playback resources are properly stopped and disposed, and
        /// unsubscribes from system events to prevent memory leaks.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected override void OnClosed(EventArgs e)
        {
            try
            {

                // Stop any other active audio playback and clean up audio resources
                StopAudioAndResetButtons();

                // Shut down the MediaFoundation API, releasing its resources
                MediaFoundationApi.Shutdown();

                // Unsubscribe from sysState events to prevent the window from holding references
                // after it has been closed, which could lead to memory leaks
                sysState.CurrentProfileChanged -= OnCurrentProfileChanged;
                sysState.ProfilesChanged -= OnProfilesChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during window cleanup: {ex.Message}");
            }
            finally
            {
                // Calls the base class's OnClosed method to ensure proper WPF window closing behavior
                base.OnClosed(e);
            }
        }

        #endregion
    }
}