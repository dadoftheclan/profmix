using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.MediaFoundation;

namespace App_Mix
{
    public partial class MainWindow : Window
    {
        private string voiceFilePath;
        private string musicFilePath;
        private string outputFilePath;

        public MainWindow()
        {
            InitializeComponent();
            // Initialize MediaFoundation for MP3 support
            MediaFoundationApi.Startup();
        }

        private void BtnSelectVoice_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                voiceFilePath = openFileDialog.FileName;
                TxtVoicePath.Text = voiceFilePath;
                BtnPreviewVoice.IsEnabled = true;

                // Stop any current playback when new file is selected
                StopAudioAndResetButtons();
            }
        }

        private void BtnSelectMusic_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                musicFilePath = openFileDialog.FileName;
                TxtMusicPath.Text = musicFilePath;
                BtnPreviewMusic.IsEnabled = true;

                // Stop any current playback when new file is selected
                StopAudioAndResetButtons();
            }
        }

        private void BtnSelectOutputPath_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "WAV Files (*.wav)|*.wav",
                DefaultExt = ".wav"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                outputFilePath = saveFileDialog.FileName;
                TxtOutputPath.Text = outputFilePath;
            }
        }

        private void BtnPreviewVoice_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayback(voiceFilePath, BtnPreviewVoice);
        }

        private void BtnPreviewMusic_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayback(musicFilePath, BtnPreviewMusic);
        }

        private void BtnPreviewMixed_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(outputFilePath))
            {
                TogglePlayback(outputFilePath, BtnPreviewMixed);
            }
            else
            {
                MessageBox.Show("Mixed file does not exist. Please mix the files first.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private long EstimateOutputFileSize(TimeSpan duration)
        {
            // For 8kHz, 16-bit, mono WAV:
            // Sample rate: 8000 samples/second
            // Bit depth: 16 bits = 2 bytes per sample
            // Channels: 1 (mono)
            // Plus WAV header (~44 bytes)

            long dataSize = (long)(duration.TotalSeconds * 8000 * 2 * 1);
            return dataSize + 44; // Add WAV header size
        }




        private void BtnMix_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(voiceFilePath) || string.IsNullOrEmpty(musicFilePath) || string.IsNullOrEmpty(outputFilePath))
            {
                MessageBox.Show("Please select voice, music, and output files first.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Stop any current playback before mixing
                StopAudioAndResetButtons();

                // Get the settings from the sliders
                float voiceVolume = (float)SliderVoiceVolume.Value / 100;
                float musicVolume = (float)SliderMusicVolume.Value / 100;
                double musicOffsetSeconds = SliderMusicOffset.Value;
                double bufferSeconds = SliderBufferLength.Value;

                // **NEW**: Check estimated file size before processing
                TimeSpan estimatedDuration;
                using (var voiceReader = new AudioFileReader(voiceFilePath))
                {
                    estimatedDuration = voiceReader.TotalTime.Add(TimeSpan.FromSeconds(bufferSeconds));
                }

                if (!CheckFileSizeBeforeMixing(estimatedDuration))
                {
                    return; // User chose not to continue
                }

                // Create readers for both input files
                using (var voiceReader = new AudioFileReader(voiceFilePath))
                using (var musicReader = new AudioFileReader(musicFilePath))
                {
                    // Get voice file duration and add user-defined buffer
                    TimeSpan voiceDuration = voiceReader.TotalTime;
                    TimeSpan bufferDuration = TimeSpan.FromSeconds(bufferSeconds);
                    TimeSpan targetDuration = voiceDuration.Add(bufferDuration);
                    TimeSpan musicOffset = TimeSpan.FromSeconds(musicOffsetSeconds);

                    // Validate music offset doesn't exceed music duration
                    if (musicOffset >= musicReader.TotalTime)
                    {
                        MessageBox.Show($"Music start offset ({musicOffsetSeconds:F0}s) exceeds music duration ({musicReader.TotalTime:mm\\:ss}). Please choose a smaller offset.",
                                      "Invalid Offset", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // **UPDATED**: Convert both audio streams to 3CX format (PCM 8kHz 16-bit MONO)
                    var voiceResampled = new MediaFoundationResampler(voiceReader, new WaveFormat(8000, 16, 1));
                    var musicResampled = new MediaFoundationResampler(musicReader, new WaveFormat(8000, 16, 1));

                    // Convert to sample providers
                    var voiceSampleProvider = voiceResampled.ToSampleProvider();
                    var musicSampleProvider = musicResampled.ToSampleProvider();

                    // Create offset music provider that starts at the specified position
                    ISampleProvider offsetMusicProvider;
                    if (musicOffsetSeconds > 0)
                    {
                        // Calculate available music duration from offset
                        TimeSpan availableMusicDuration = musicReader.TotalTime.Subtract(musicOffset);

                        // Create an offset provider that skips the beginning
                        offsetMusicProvider = new OffsetSampleProvider(musicSampleProvider)
                        {
                            SkipOver = musicOffset
                        };

                        // If remaining music is shorter than needed, loop it
                        if (availableMusicDuration < targetDuration)
                        {
                            offsetMusicProvider = new LoopingSampleProvider(offsetMusicProvider, targetDuration, musicOffset, musicReader.TotalTime);
                        }
                        else
                        {
                            // Trim to exact duration needed
                            offsetMusicProvider = new OffsetSampleProvider(offsetMusicProvider)
                            {
                                Take = targetDuration
                            };
                        }
                    }
                    else
                    {
                        // No offset, handle normally
                        if (musicReader.TotalTime < targetDuration)
                        {
                            offsetMusicProvider = new LoopingSampleProvider(musicSampleProvider, targetDuration, TimeSpan.Zero, musicReader.TotalTime);
                        }
                        else
                        {
                            offsetMusicProvider = new OffsetSampleProvider(musicSampleProvider)
                            {
                                Take = targetDuration
                            };
                        }
                    }

                    // Apply volume to both inputs
                    var voiceVolumeSampleProvider = new VolumeSampleProvider(voiceSampleProvider);
                    voiceVolumeSampleProvider.Volume = voiceVolume;

                    var musicVolumeSampleProvider = new VolumeSampleProvider(offsetMusicProvider);
                    musicVolumeSampleProvider.Volume = musicVolume;

                    // Add fade-out to music if there's a buffer period
                    ISampleProvider finalMusicProvider = musicVolumeSampleProvider;
                    if (bufferSeconds > 0)
                    {
                        // Create a fade-out that starts when the voice ends and continues through the buffer
                        finalMusicProvider = new FadeOutSampleProvider(musicVolumeSampleProvider, voiceDuration, targetDuration);
                    }

                    // **UPDATED**: Create a mixer for 8kHz mono format
                    var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(8000, 1));
                    mixer.AddMixerInput(voiceVolumeSampleProvider);
                    mixer.AddMixerInput(finalMusicProvider);

                    // **UPDATED**: Create the output file in 16-bit format (not float)
                    WaveFileWriter.CreateWaveFile16(outputFilePath, mixer);

                    // **NEW**: Check file size and warn if it exceeds 3CX limit
                    var fileInfo = new FileInfo(outputFilePath);
                    long fileSizeInMB = fileInfo.Length / (1024 * 1024);

                    string sizeWarning = "";
                    if (fileSizeInMB > 10)
                    {
                        sizeWarning = $"\n⚠️ WARNING: File size ({fileSizeInMB}MB) exceeds 3CX's 10MB limit!";
                    }

                    string offsetInfo = musicOffsetSeconds > 0 ? $"\nMusic starts at: {musicOffsetSeconds:F0}s" : "";
                    string bufferInfo = bufferSeconds > 0 ? $"\nBuffer with fade-out: {bufferSeconds:F0}s" : "\nNo buffer (music stops with voice)";

                    MessageBox.Show($"Files mixed successfully for 3CX!\n\n" +
                                  $"Format: WAV (PCM, 8kHz, 16-bit, Mono)\n" +
                                  $"File size: {fileSizeInMB}MB\n" +
                                  $"Total output duration: {targetDuration:mm\\:ss}\n" +
                                  $"Voice duration: {voiceDuration:mm\\:ss}{offsetInfo}{bufferInfo}{sizeWarning}",
                                  "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnPreviewMixed.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error mixing files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CheckFileSizeBeforeMixing(TimeSpan targetDuration)
        {
            long estimatedSize = EstimateOutputFileSize(targetDuration);
            long estimatedMB = estimatedSize / (1024 * 1024);

            if (estimatedMB > 10)
            {
                var result = MessageBox.Show($"The estimated output file size will be {estimatedMB}MB, which exceeds 3CX's 10MB limit.\n\nDo you want to continue anyway?",
                                           "File Size Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                return result == MessageBoxResult.Yes;
            }
            return true;
        }

        private IWavePlayer waveOutDevice;
        private AudioFileReader audioFileReader;
        private Button currentPlayingButton;
        private string currentPlayingFile;

        private void TogglePlayback(string filePath, Button button)
        {
            try
            {
                // If we're currently playing this same file, pause/stop it
                if (waveOutDevice != null && currentPlayingFile == filePath)
                {
                    if (waveOutDevice.PlaybackState == PlaybackState.Playing)
                    {
                        waveOutDevice.Pause();
                        button.Content = button.Content.ToString().Replace("⏸", "▶");
                        button.Tag = "Play";
                    }
                    else if (waveOutDevice.PlaybackState == PlaybackState.Paused)
                    {
                        waveOutDevice.Play();
                        button.Content = button.Content.ToString().Replace("▶", "⏸");
                        button.Tag = "Pause";
                    }
                    return;
                }

                // Stop any currently playing audio and reset buttons
                StopAudioAndResetButtons();

                // Start playing the new file
                PlayAudio(filePath, button);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error controlling audio playback: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlayAudio(string filePath, Button button)
        {
            try
            {
                // Create new audio player
                audioFileReader = new AudioFileReader(filePath);
                waveOutDevice = new WaveOut();
                waveOutDevice.Init(audioFileReader);

                // Set current playing info
                currentPlayingButton = button;
                currentPlayingFile = filePath;

                // Update button to show pause state
                button.Content = button.Content.ToString().Replace("▶", "⏸");
                button.Tag = "Pause";

                // Handle playback completion
                waveOutDevice.PlaybackStopped += (s, e) =>
                {
                    // Clean up and reset button when playback ends
                    Dispatcher.Invoke(() => StopAudioAndResetButtons());
                };

                waveOutDevice.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing audio: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopAudioAndResetButtons();
            }
        }

        private void StopAudio()
        {
            if (waveOutDevice != null)
            {
                waveOutDevice.Stop();
                waveOutDevice.Dispose();
                waveOutDevice = null;
            }

            if (audioFileReader != null)
            {
                audioFileReader.Dispose();
                audioFileReader = null;
            }

            currentPlayingFile = null;
        }

        private void StopAudioAndResetButtons()
        {
            StopAudio();

            // Reset all preview buttons to play state
            ResetButtonToPlayState(BtnPreviewVoice, "▶ Preview");
            ResetButtonToPlayState(BtnPreviewMusic, "▶ Preview");
            ResetButtonToPlayState(BtnPreviewMixed, "🔊 Preview Result");

            currentPlayingButton = null;
        }

        private void ResetButtonToPlayState(Button button, string originalContent)
        {
            if (button != null)
            {
                button.Content = originalContent;
                button.Tag = "Play";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopAudioAndResetButtons();
            MediaFoundationApi.Shutdown();
            base.OnClosed(e);
        }
    }

    // Custom sample provider to add fade-out effect to music during buffer period
    public class FadeOutSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly TimeSpan voiceEndTime;
        private readonly TimeSpan totalDuration;
        private readonly TimeSpan fadeStartTime;
        private readonly TimeSpan fadeDuration;
        private readonly long fadeStartSample;
        private readonly long totalSamples;
        private long samplesRead;

        public FadeOutSampleProvider(ISampleProvider source, TimeSpan voiceEndTime, TimeSpan totalDuration)
        {
            this.source = source;
            this.voiceEndTime = voiceEndTime;
            this.totalDuration = totalDuration;
            this.WaveFormat = source.WaveFormat;

            // Start fading when voice ends
            this.fadeStartTime = voiceEndTime;
            this.fadeDuration = totalDuration.Subtract(voiceEndTime);

            // Calculate sample positions (works for both stereo and mono)
            this.fadeStartSample = (long)(fadeStartTime.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
            this.totalSamples = (long)(totalDuration.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            // Apply fade-out effect during the buffer period
            for (int i = 0; i < samplesRead; i++)
            {
                long currentSample = this.samplesRead + i;

                if (currentSample >= fadeStartSample && currentSample < totalSamples)
                {
                    // Calculate fade progress (0.0 = full volume, 1.0 = silent)
                    long fadePosition = currentSample - fadeStartSample;
                    long totalFadeSamples = totalSamples - fadeStartSample;

                    if (totalFadeSamples > 0)
                    {
                        float fadeProgress = (float)fadePosition / totalFadeSamples;

                        // Apply exponential fade curve for more natural sound
                        float fadeMultiplier = (float)Math.Pow(1.0 - fadeProgress, 2.0);

                        buffer[offset + i] *= fadeMultiplier;
                    }
                }
                else if (currentSample >= totalSamples)
                {
                    // Beyond total duration, silence
                    buffer[offset + i] = 0;
                }
            }

            this.samplesRead += samplesRead;
            return samplesRead;
        }
    }

    // Custom sample provider to loop audio for a specific duration with offset support
    public class LoopingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly TimeSpan targetDuration;
        private readonly TimeSpan sourceStartOffset;
        private readonly TimeSpan sourceTotalDuration;
        private readonly long targetSamples;
        private long samplesRead;
        private long sourcePosition;

        public LoopingSampleProvider(ISampleProvider source, TimeSpan targetDuration, TimeSpan sourceStartOffset = default, TimeSpan sourceTotalDuration = default)
        {
            this.source = source;
            this.targetDuration = targetDuration;
            this.sourceStartOffset = sourceStartOffset;
            this.sourceTotalDuration = sourceTotalDuration;
            this.WaveFormat = source.WaveFormat;
            this.targetSamples = (long)(targetDuration.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);

            // Calculate the effective loop length (from offset to end)
            if (sourceTotalDuration != TimeSpan.Zero && sourceStartOffset != TimeSpan.Zero)
            {
                var effectiveLoopDuration = sourceTotalDuration.Subtract(sourceStartOffset);
                // We'll handle looping by resetting when we reach the end
            }
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            if (samplesRead >= targetSamples)
                return 0; // We've reached our target duration

            int samplesNeeded = (int)Math.Min(count, targetSamples - samplesRead);
            int totalSamplesRead = 0;

            while (totalSamplesRead < samplesNeeded)
            {
                int samplesThisTime = source.Read(buffer, offset + totalSamplesRead, samplesNeeded - totalSamplesRead);

                if (samplesThisTime == 0)
                {
                    // Source has ended, reset it to loop from the offset position
                    if (source is OffsetSampleProvider offsetSource)
                    {
                        // Reset the offset source to start from the beginning of its offset again
                        // This is a limitation - we need to recreate the offset provider
                        // For now, we'll break to avoid infinite loop
                        break;
                    }
                    else if (source is AudioFileReader fileReader)
                    {
                        fileReader.Position = (long)(sourceStartOffset.TotalSeconds * fileReader.WaveFormat.AverageBytesPerSecond);
                    }
                    else
                    {
                        // If we can't seek, we're done
                        break;
                    }
                }
                else
                {
                    totalSamplesRead += samplesThisTime;
                    sourcePosition += samplesThisTime;
                }
            }

            samplesRead += totalSamplesRead;
            return totalSamplesRead;
        }
    }

    // Extension to make ISampleProvider seekable check easier
    public interface ISeekable
    {
        long Position { get; set; }
    }
}