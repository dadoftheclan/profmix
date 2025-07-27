using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using System.Reflection;
using App_Mix.Systems;

namespace App_Mix.Windows
{
    /// <summary>
    /// Interaction logic for wndSplash.xaml
    /// This splash screen plays an embedded audio resource when it loads
    /// and provides a welcoming introduction to the ProfMix application.
    /// </summary>
    public partial class wndSplash : Window
    {
        /// <summary>
        /// Temporary file path for the extracted audio resource.
        /// </summary>
        private string tempAudioFile;

        /// <summary>
        /// Initializes a new instance of the wndSplash window.
        /// </summary>
        public wndSplash()
        {
            InitializeComponent();
            
            // Subscribe to the Loaded event to play audio when the window appears
            this.Loaded += WndSplash_Loaded;
            this.Closing += WndSplash_Closing;
        }
        private async void WndSplash_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = await sysPlayback.Instance.PlayEmbeddedResourceAsync(
                    "ProfMix_Splash_Mixed.wav", 0.6f);

                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to play splash audio");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Splash audio error: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for when the splash window is closing.
        /// Cleans up temporary files and stops any audio playback.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void WndSplash_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Stop any playing audio
                sysPlayback.Instance.Stop();
                
                // Clean up the temporary audio file
                CleanupTempAudioFile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during splash cleanup: {ex.Message}");
            }
        }




        /// <summary>
        /// Event handler for when the splash audio playback stops (completes or is stopped).
        /// </summary>
        /// <param name="sender">The playback system.</param>
        /// <param name="e">Playback stopped event arguments.</param>
        private void OnSplashAudioStopped(object sender, PlaybackStoppedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Splash audio playback completed");
            
            // Unsubscribe from events
            var player = sysPlayback.Instance;
            player.PlaybackStopped -= OnSplashAudioStopped;
            player.PlaybackError -= OnSplashAudioError;
            
            // Clean up the temporary file
            CleanupTempAudioFile();
        }

        /// <summary>
        /// Event handler for when splash audio playback encounters an error.
        /// </summary>
        /// <param name="sender">The playback system.</param>
        /// <param name="errorMessage">The error message.</param>
        private void OnSplashAudioError(object sender, string errorMessage)
        {
            System.Diagnostics.Debug.WriteLine($"Splash audio error: {errorMessage}");
            
            // Unsubscribe from events
            var player = sysPlayback.Instance;
            player.PlaybackStopped -= OnSplashAudioStopped;
            player.PlaybackError -= OnSplashAudioError;
            
            // Clean up the temporary file
            CleanupTempAudioFile();
        }

        /// <summary>
        /// Cleans up the temporary audio file created for splash playback.
        /// </summary>
        private void CleanupTempAudioFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(tempAudioFile) && File.Exists(tempAudioFile))
                {
                    File.Delete(tempAudioFile);
                    System.Diagnostics.Debug.WriteLine($"Cleaned up temporary splash audio file: {tempAudioFile}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up temporary audio file: {ex.Message}");
                // Don't throw - this is cleanup code
            }
            finally
            {
                tempAudioFile = null;
            }
        }

        /// <summary>
        /// Public method to manually stop the splash audio (can be called from parent windows).
        /// </summary>
        public void StopSplashAudio()
        {
            try
            {
                sysPlayback.Instance.Stop();
                CleanupTempAudioFile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping splash audio: {ex.Message}");
            }
        }

        /// <summary>
        /// Public method to check if splash audio is currently playing.
        /// </summary>
        /// <returns>True if splash audio is playing; otherwise, false.</returns>
        public bool IsSplashAudioPlaying()
        {
            try
            {
                return sysPlayback.Instance.PlaybackState == NAudio.Wave.PlaybackState.Playing &&
                       sysPlayback.Instance.IsFileCurrentlyLoaded(tempAudioFile);
            }
            catch
            {
                return false;
            }
        }
    }
}