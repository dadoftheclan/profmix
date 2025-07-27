using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Reflection;
using NAudio.Wave;

namespace App_Mix.Systems
{
    /// <summary>
    /// Provides centralized audio playback management as a singleton.
    /// This system ensures only one audio file can play at a time and provides
    /// a simple interface for play/pause/stop operations across the entire application.
    /// Prevents audio overlap and manages NAudio resources properly.
    /// </summary>
    public sealed class sysPlayback
    {
        #region Singleton Implementation

        /// <summary>
        /// The single instance of the sysPlayback class.
        /// </summary>
        private static readonly Lazy<sysPlayback> _instance = new Lazy<sysPlayback>(() => new sysPlayback());

        /// <summary>
        /// Gets the singleton instance of the sysPlayback system.
        /// </summary>
        public static sysPlayback Instance => _instance.Value;

        /// <summary>
        /// Private constructor to prevent external instantiation.
        /// </summary>
        private sysPlayback()
        {
            // Initialize any required resources here
        }

        #endregion

        #region Private Fields

        /// <summary>
        /// NAudio wave output device for audio playback.
        /// </summary>
        private IWavePlayer _waveOutDevice;

        /// <summary>
        /// NAudio audio file reader for reading audio data.
        /// </summary>
        private AudioFileReader _audioFileReader;

        /// <summary>
        /// The file path of the currently loaded/playing audio file.
        /// </summary>
        private string _currentFilePath;

        /// <summary>
        /// Indicates whether the playback system has been disposed.
        /// </summary>
        private bool _isDisposed = false;

        /// <summary>
        /// Cache of extracted resource files to avoid re-extraction.
        /// Key: resource name, Value: temporary file path
        /// </summary>
        private static readonly Dictionary<string, string> _extractedResources = new Dictionary<string, string>();

        /// <summary>
        /// Lock object for thread-safe access to the extracted resources cache.
        /// </summary>
        private static readonly object _extractionLock = new object();

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the current playback state (Playing, Paused, Stopped).
        /// </summary>
        public PlaybackState PlaybackState => _waveOutDevice?.PlaybackState ?? PlaybackState.Stopped;

        /// <summary>
        /// Gets the file path of the currently loaded audio file.
        /// Returns null if no file is loaded.
        /// </summary>
        public string CurrentFilePath => _currentFilePath;

        /// <summary>
        /// Gets the current playback position as a TimeSpan.
        /// Returns TimeSpan.Zero if no file is loaded or an error occurs.
        /// </summary>
        public TimeSpan CurrentPosition
        {
            get
            {
                try
                {
                    return _audioFileReader?.CurrentTime ?? TimeSpan.Zero;
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
            set
            {
                try
                {
                    if (_audioFileReader != null)
                    {
                        _audioFileReader.CurrentTime = value;
                    }
                }
                catch
                {
                    // Ignore seek errors
                }
            }
        }

        /// <summary>
        /// Gets the total duration of the currently loaded audio file.
        /// Returns TimeSpan.Zero if no file is loaded.
        /// </summary>
        public TimeSpan TotalDuration => _audioFileReader?.TotalTime ?? TimeSpan.Zero;

        /// <summary>
        /// Gets or sets the playback volume (0.0 to 1.0).
        /// </summary>
        public float Volume
        {
            get => _audioFileReader?.Volume ?? 1.0f;
            set
            {
                if (_audioFileReader != null)
                {
                    _audioFileReader.Volume = Math.Max(0.0f, Math.Min(1.0f, value));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether an audio file is currently loaded.
        /// </summary>
        public bool IsFileLoaded => _audioFileReader != null && !string.IsNullOrEmpty(_currentFilePath);

        #endregion

        #region Public Events

        /// <summary>
        /// Raised when playback stops (either completed or stopped manually).
        /// </summary>
        public event EventHandler<PlaybackStoppedEventArgs> PlaybackStopped;

        /// <summary>
        /// Raised when playback state changes (Playing, Paused, Stopped).
        /// </summary>
        public event EventHandler<PlaybackState> PlaybackStateChanged;

        /// <summary>
        /// Raised when a new audio file is loaded.
        /// </summary>
        public event EventHandler<string> FileLoaded;

        /// <summary>
        /// Raised when an error occurs during playback operations.
        /// </summary>
        public event EventHandler<string> PlaybackError;

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads an audio file for playback. If another file is currently loaded, it will be unloaded first.
        /// </summary>
        /// <param name="filePath">The path to the audio file to load.</param>
        /// <returns>True if the file was loaded successfully; otherwise, false.</returns>
        public bool LoadFile(string filePath)
        {
            try
            {
                // Validate file path
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    OnPlaybackError($"File not found: {filePath}");
                    return false;
                }

                // Stop and unload any existing file
                Stop();

                // Load the new file
                _audioFileReader = new AudioFileReader(filePath);
                _waveOutDevice = new WaveOut();
                _waveOutDevice.Init(_audioFileReader);

                // Subscribe to the PlaybackStopped event
                _waveOutDevice.PlaybackStopped += OnWaveOutPlaybackStopped;

                _currentFilePath = filePath;

                // Raise the FileLoaded event
                OnFileLoaded(filePath);

                return true;
            }
            catch (Exception ex)
            {
                OnPlaybackError($"Error loading file '{filePath}': {ex.Message}");
                CleanupResources();
                return false;
            }
        }

        /// <summary>
        /// Starts or resumes playback of the currently loaded audio file.
        /// </summary>
        /// <returns>True if playback started successfully; otherwise, false.</returns>
        public bool Play()
        {
            try
            {
                if (_waveOutDevice == null)
                {
                    OnPlaybackError("No audio file loaded. Please load a file first.");
                    return false;
                }

                var previousState = PlaybackState;
                _waveOutDevice.Play();

                if (PlaybackState != previousState)
                {
                    OnPlaybackStateChanged(PlaybackState);
                }

                return true;
            }
            catch (Exception ex)
            {
                OnPlaybackError($"Error starting playback: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pauses the currently playing audio file.
        /// </summary>
        /// <returns>True if playback was paused successfully; otherwise, false.</returns>
        public bool Pause()
        {
            try
            {
                if (_waveOutDevice == null)
                {
                    return false; // No audio loaded, nothing to pause
                }

                var previousState = PlaybackState;
                _waveOutDevice.Pause();

                if (PlaybackState != previousState)
                {
                    OnPlaybackStateChanged(PlaybackState);
                }

                return true;
            }
            catch (Exception ex)
            {
                OnPlaybackError($"Error pausing playback: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops playback and resets the position to the beginning.
        /// </summary>
        /// <returns>True if playback was stopped successfully; otherwise, false.</returns>
        public bool Stop()
        {
            try
            {
                var wasPlaying = PlaybackState == PlaybackState.Playing || PlaybackState == PlaybackState.Paused;

                CleanupResources();

                if (wasPlaying)
                {
                    OnPlaybackStateChanged(PlaybackState.Stopped);
                }

                return true;
            }
            catch (Exception ex)
            {
                OnPlaybackError($"Error stopping playback: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Toggles between play and pause states.
        /// If stopped, starts playback. If playing, pauses. If paused, resumes.
        /// </summary>
        /// <returns>True if the toggle operation was successful; otherwise, false.</returns>
        public bool TogglePlayPause()
        {
            switch (PlaybackState)
            {
                case PlaybackState.Playing:
                    return Pause();
                case PlaybackState.Paused:
                case PlaybackState.Stopped:
                    return Play();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Loads and immediately starts playing an audio file.
        /// This is a convenience method that combines LoadFile and Play.
        /// </summary>
        /// <param name="filePath">The path to the audio file to play.</param>
        /// <returns>True if the file was loaded and playback started successfully; otherwise, false.</returns>
        public bool PlayFile(string filePath)
        {
            return LoadFile(filePath) && Play();
        }

        /// <summary>
        /// Seeks to a specific position in the currently loaded audio file.
        /// </summary>
        /// <param name="position">The position to seek to.</param>
        /// <returns>True if seek was successful; otherwise, false.</returns>
        public bool Seek(TimeSpan position)
        {
            try
            {
                if (_audioFileReader == null)
                {
                    return false;
                }

                // Ensure position is within bounds
                if (position < TimeSpan.Zero)
                    position = TimeSpan.Zero;
                else if (position > TotalDuration)
                    position = TotalDuration;

                CurrentPosition = position;
                return true;
            }
            catch (Exception ex)
            {
                OnPlaybackError($"Error seeking to position {position}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Seeks to a specific percentage of the total duration.
        /// </summary>
        /// <param name="percentage">The percentage (0.0 to 1.0) of the total duration to seek to.</param>
        /// <returns>True if seek was successful; otherwise, false.</returns>
        public bool SeekToPercentage(double percentage)
        {
            percentage = Math.Max(0.0, Math.Min(1.0, percentage));
            var position = TimeSpan.FromTicks((long)(TotalDuration.Ticks * percentage));
            return Seek(position);
        }

        /// <summary>
        /// Gets the current playback progress as a percentage (0.0 to 1.0).
        /// </summary>
        /// <returns>The playback progress percentage.</returns>
        public double GetProgressPercentage()
        {
            if (TotalDuration.TotalMilliseconds <= 0)
                return 0.0;

            return CurrentPosition.TotalMilliseconds / TotalDuration.TotalMilliseconds;
        }

        /// <summary>
        /// Checks if the specified file is currently loaded (regardless of playback state).
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <returns>True if the specified file is currently loaded; otherwise, false.</returns>
        public bool IsFileCurrentlyLoaded(string filePath)
        {
            return !string.IsNullOrEmpty(_currentFilePath) &&
                   _currentFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Resource Extraction Methods

        /// <summary>
        /// Extracts an embedded audio resource to a temporary file and loads it for playback.
        /// Uses caching to avoid re-extracting the same resource multiple times.
        /// </summary>
        /// <param name="resourceFileName">The filename of the embedded resource (e.g., "ProfMix_Splash_Mixed.wav")</param>
        /// <param name="volume">Optional volume level (0.0 to 1.0). If null, uses current volume.</param>
        /// <returns>True if the resource was extracted and loaded successfully; otherwise, false.</returns>
        public async Task<bool> LoadEmbeddedResourceAsync(string resourceFileName, float? volume = null)
        {
            try
            {
                // Extract the resource to a temporary file
                string tempFilePath = await ExtractEmbeddedResourceAsync(resourceFileName);

                if (string.IsNullOrEmpty(tempFilePath))
                {
                    OnPlaybackError($"Failed to extract embedded resource: {resourceFileName}");
                    return false;
                }

                // Load the extracted file
                bool loaded = LoadFile(tempFilePath);

                if (loaded && volume.HasValue)
                {
                    Volume = volume.Value;
                }

                return loaded;
            }
            catch (Exception ex)
            {
                OnPlaybackError($"Error loading embedded resource '{resourceFileName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts an embedded audio resource to a temporary file and immediately starts playback.
        /// This is a convenience method that combines extraction, loading, and playing.
        /// </summary>
        /// <param name="resourceFileName">The filename of the embedded resource (e.g., "ProfMix_Main_Mixed.wav")</param>
        /// <param name="volume">Optional volume level (0.0 to 1.0). Defaults to 0.5 if not specified.</param>
        /// <returns>True if the resource was extracted and playback started successfully; otherwise, false.</returns>
        public async Task<bool> PlayEmbeddedResourceAsync(string resourceFileName, float volume = 0.5f)
        {
            bool loaded = await LoadEmbeddedResourceAsync(resourceFileName, volume);
            return loaded && Play();
        }

        /// <summary>
        /// Extracts an embedded audio resource to a temporary file with caching to avoid re-extraction.
        /// </summary>
        /// <param name="resourceFileName">The filename of the embedded resource</param>
        /// <returns>The path to the extracted temporary file, or null if extraction failed</returns>
        public static async Task<string> ExtractEmbeddedResourceAsync(string resourceFileName)
        {
            if (string.IsNullOrWhiteSpace(resourceFileName))
            {
                System.Diagnostics.Debug.WriteLine("Resource filename cannot be null or empty");
                return null;
            }

            lock (_extractionLock)
            {
                // Check if we've already extracted this resource
                if (_extractedResources.TryGetValue(resourceFileName, out string cachedPath))
                {
                    // Verify the cached file still exists
                    if (File.Exists(cachedPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Using cached resource: {resourceFileName} -> {cachedPath}");
                        return cachedPath;
                    }
                    else
                    {
                        // Remove invalid cache entry
                        _extractedResources.Remove(resourceFileName);
                        System.Diagnostics.Debug.WriteLine($"Cached file no longer exists, re-extracting: {resourceFileName}");
                    }
                }
            }

            try
            {
                // Get the current assembly (where the resources are embedded)
                var assembly = Assembly.GetExecutingAssembly();

                // Try to find the resource with various naming patterns
                string actualResourceName = FindEmbeddedResource(assembly, resourceFileName);

                if (actualResourceName == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Embedded resource not found: {resourceFileName}");
                    LogAvailableResources(assembly);
                    return null;
                }

                // Extract the resource to a temporary file
                using (var resourceStream = assembly.GetManifestResourceStream(actualResourceName))
                {
                    if (resourceStream == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not load resource stream for: {actualResourceName}");
                        return null;
                    }

                    // Create a unique temporary file
                    string tempFilePath = Path.Combine(Path.GetTempPath(),
                        $"ProfMix_{Path.GetFileNameWithoutExtension(resourceFileName)}_{Guid.NewGuid():N}{Path.GetExtension(resourceFileName)}");

                    // Copy the resource to the temporary file
                    using (var fileStream = File.Create(tempFilePath))
                    {
                        await resourceStream.CopyToAsync(fileStream);
                    }

                    // Cache the extracted file path
                    lock (_extractionLock)
                    {
                        _extractedResources[resourceFileName] = tempFilePath;
                    }

                    System.Diagnostics.Debug.WriteLine($"Extracted resource: {actualResourceName} -> {tempFilePath}");
                    return tempFilePath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting embedded resource '{resourceFileName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds an embedded resource using various naming patterns and case-insensitive matching.
        /// </summary>
        /// <param name="assembly">The assembly to search in</param>
        /// <param name="resourceFileName">The resource filename to find</param>
        /// <returns>The actual resource name if found, or null if not found</returns>
        private static string FindEmbeddedResource(Assembly assembly, string resourceFileName)
        {
            var resourceNames = assembly.GetManifestResourceNames();

            // Try exact match first (case-insensitive)
            var exactMatch = resourceNames.FirstOrDefault(name =>
                name.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
                return exactMatch;

            // Try with common namespace patterns
            string[] commonPatterns = {
                $"App_Mix.Resources.{resourceFileName}",
                $"App_Mix.{resourceFileName}",
                $"Resources.{resourceFileName}"
            };

            foreach (var pattern in commonPatterns)
            {
                var match = resourceNames.FirstOrDefault(name =>
                    name.Equals(pattern, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            // Try partial name matching (without extension)
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(resourceFileName);
            var partialMatch = resourceNames.FirstOrDefault(name =>
                name.IndexOf(nameWithoutExtension, StringComparison.OrdinalIgnoreCase) >= 0);

            return partialMatch;
        }

        /// <summary>
        /// Logs all available embedded resources for debugging purposes.
        /// </summary>
        /// <param name="assembly">The assembly to examine</param>
        private static void LogAvailableResources(Assembly assembly)
        {
            var resourceNames = assembly.GetManifestResourceNames();
            System.Diagnostics.Debug.WriteLine("Available embedded resources:");
            foreach (var name in resourceNames.OrderBy(n => n))
            {
                System.Diagnostics.Debug.WriteLine($"  - {name}");
            }
        }

        /// <summary>
        /// Clears the extracted resources cache and optionally deletes the temporary files.
        /// </summary>
        /// <param name="deleteTempFiles">If true, deletes the temporary files from disk</param>
        public static void ClearResourceCache(bool deleteTempFiles = true)
        {
            lock (_extractionLock)
            {
                if (deleteTempFiles)
                {
                    foreach (var tempFile in _extractedResources.Values)
                    {
                        try
                        {
                            if (File.Exists(tempFile))
                            {
                                File.Delete(tempFile);
                                System.Diagnostics.Debug.WriteLine($"Deleted cached resource file: {tempFile}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error deleting cached resource file '{tempFile}': {ex.Message}");
                        }
                    }
                }

                _extractedResources.Clear();
                System.Diagnostics.Debug.WriteLine("Resource cache cleared");
            }
        }

        /// <summary>
        /// Gets information about currently cached extracted resources.
        /// </summary>
        /// <returns>A dictionary containing resource names and their temporary file paths</returns>
        public static Dictionary<string, string> GetCachedResources()
        {
            lock (_extractionLock)
            {
                return new Dictionary<string, string>(_extractedResources);
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the PlaybackStopped event from the NAudio wave output device.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void OnWaveOutPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Use dispatcher to ensure we're on the UI thread for event raising
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var eventArgs = new PlaybackStoppedEventArgs();
                if (e.Exception != null)
                {
                    eventArgs.Exception = e.Exception;
                    OnPlaybackError($"Playback stopped due to error: {e.Exception.Message}");
                }

                OnPlaybackStopped(eventArgs);
                OnPlaybackStateChanged(PlaybackState.Stopped);

                // Clean up resources when playback completes
                CleanupResources();
            });
        }

        #endregion

        #region Private Event Raisers

        /// <summary>
        /// Raises the PlaybackStopped event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnPlaybackStopped(PlaybackStoppedEventArgs e)
        {
            PlaybackStopped?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the PlaybackStateChanged event.
        /// </summary>
        /// <param name="state">The new playback state.</param>
        private void OnPlaybackStateChanged(PlaybackState state)
        {
            PlaybackStateChanged?.Invoke(this, state);
        }

        /// <summary>
        /// Raises the FileLoaded event.
        /// </summary>
        /// <param name="filePath">The path of the loaded file.</param>
        private void OnFileLoaded(string filePath)
        {
            FileLoaded?.Invoke(this, filePath);
        }

        /// <summary>
        /// Raises the PlaybackError event.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        private void OnPlaybackError(string errorMessage)
        {
            PlaybackError?.Invoke(this, errorMessage);
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Cleans up NAudio resources and resets internal state.
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                // Unsubscribe from events before disposing
                if (_waveOutDevice != null)
                {
                    _waveOutDevice.PlaybackStopped -= OnWaveOutPlaybackStopped;
                    _waveOutDevice.Stop();
                    _waveOutDevice.Dispose();
                    _waveOutDevice = null;
                }

                if (_audioFileReader != null)
                {
                    _audioFileReader.Dispose();
                    _audioFileReader = null;
                }

                _currentFilePath = null;
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the sysPlayback instance.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                CleanupResources();
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Static cleanup method to clear all cached resources when the application shuts down.
        /// Should be called from Application.Exit or similar cleanup events.
        /// </summary>
        public static void GlobalCleanup()
        {
            ClearResourceCache(true);
        }

        #endregion
    }

    /// <summary>
    /// Provides data for the PlaybackStopped event.
    /// </summary>
    public class PlaybackStoppedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the exception that caused playback to stop, if any.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets a value indicating whether playback stopped due to an error.
        /// </summary>
        public bool HasError => Exception != null;
    }
}