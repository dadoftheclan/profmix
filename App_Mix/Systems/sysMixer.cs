using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NAudio.Wave; // Core NAudio library for audio processing.
using NAudio.Wave.SampleProviders; // Provides sample providers for audio manipulation.
using NAudio.MediaFoundation; // Required for MP3 support and advanced audio format conversions.
using App_Mix.Providers; // Custom NAudio sample providers (e.g., proFadeOut, proLooping).
using App_Mix.Models; // For mdlProfile model.

namespace App_Mix.Systems
{
    /// <summary>
    /// Provides core functionality for mixing voice and music audio files.
    /// This class handles audio file reading, resampling, volume adjustment,
    /// offsetting, looping, fading, and writing the final mixed output.
    /// It uses profile specifications to determine the output format and quality.
    /// </summary>
    public class sysMixer
    {
        /// <summary>
        /// Represents the settings required for an audio mixing operation.
        /// This class encapsulates all parameters necessary for the <see cref="MixAudioFiles"/> method.
        /// </summary>
        public class MixingSettings
        {
            /// <summary>
            /// Gets or sets the full file path to the voice audio recording.
            /// </summary>
            public string VoiceFilePath { get; set; }

            /// <summary>
            /// Gets or sets the full file path to the background music audio track.
            /// </summary>
            public string MusicFilePath { get; set; }

            /// <summary>
            /// Gets or sets the full file path where the mixed audio output will be saved.
            /// </summary>
            public string OutputFilePath { get; set; }

            /// <summary>
            /// Gets or sets the volume level for the voice track, as a float between 0.0 (silent) and 1.0 (full volume).
            /// Default is 1.0 (100%).
            /// </summary>
            public float VoiceVolume { get; set; } = 1.0f;

            /// <summary>
            /// Gets or sets the volume level for the music track, as a float between 0.0 (silent) and 1.0 (full volume).
            /// Default is 0.3 (30%), typically for background music.
            /// </summary>
            public float MusicVolume { get; set; } = 0.3f;

            /// <summary>
            /// Gets or sets the time (in seconds) after which the music track should start playing.
            /// Default is 0 (starts immediately).
            /// </summary>
            public double MusicOffsetSeconds { get; set; } = 0;

            /// <summary>
            /// Gets or sets the duration (in seconds) of the buffer period added after the voice track.
            /// This period is typically used for music fade-out. Default is 10 seconds.
            /// </summary>
            public double BufferSeconds { get; set; } = 10;

            /// <summary>
            /// Gets or sets the audio profile that defines the output format specifications.
            /// If null, defaults to 8kHz, 16-bit, mono for backward compatibility.
            /// </summary>
            public mdlProfile OutputProfile { get; set; }
        }

        /// <summary>
        /// Represents the result of an audio mixing operation.
        /// This class provides feedback on success/failure, error messages, and details about the output file.
        /// </summary>
        public class MixingResult
        {
            /// <summary>
            /// Gets or sets a value indicating whether the mixing operation was successful.
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// Gets or sets an error message if the mixing operation failed.
            /// </summary>
            public string ErrorMessage { get; set; }

            /// <summary>
            /// Gets or sets the original duration of the voice audio file.
            /// </summary>
            public TimeSpan VoiceDuration { get; set; }

            /// <summary>
            /// Gets or sets the total duration of the final mixed audio file, including any buffer.
            /// </summary>
            public TimeSpan TotalDuration { get; set; }

            /// <summary>
            /// Gets or sets the size of the output file in bytes.
            /// </summary>
            public long FileSizeBytes { get; set; }

            /// <summary>
            /// Gets or sets a human-readable formatted message summarizing the mixing result.
            /// </summary>
            public string FormattedMessage { get; set; }

            /// <summary>
            /// Gets or sets the audio profile that was used for the mixing operation.
            /// </summary>
            public mdlProfile UsedProfile { get; set; }
        }

        /// <summary>
        /// Estimates the final output file size for an audio file with a given duration and profile specifications.
        /// </summary>
        /// <param name="duration">The total duration of the audio content.</param>
        /// <param name="profile">The audio profile defining sample rate, bit depth, and channels. If null, uses default 8kHz/16-bit/mono.</param>
        /// <returns>The estimated file size in bytes.</returns>
        public static long EstimateOutputFileSize(TimeSpan duration, mdlProfile profile = null)
        {
            // Use profile specifications or fall back to defaults
            int sampleRate = profile?.SampleRate ?? 8000;
            int bitDepth = profile?.BitDepth ?? 16;
            int channels = profile?.Channels ?? 1;

            // Calculate: samples/second * bytes/sample * channels * duration
            long dataSize = (long)(duration.TotalSeconds * sampleRate * (bitDepth / 8) * channels);
            // Add a typical WAV header size (44 bytes).
            return dataSize + 44;
        }

        /// <summary>
        /// Checks if the estimated audio file size exceeds the specified profile's maximum file size limit.
        /// </summary>
        /// <param name="targetDuration">The target duration of the mixed audio file.</param>
        /// <param name="profile">The audio profile containing the maximum file size limit.</param>
        /// <param name="estimatedMB">An output parameter that will contain the estimated file size in megabytes.</param>
        /// <returns><c>true</c> if the estimated size is within the profile's limit; otherwise, <c>false</c>.</returns>
        public static bool CheckFileSizeForProfile(TimeSpan targetDuration, mdlProfile profile, out long estimatedMB)
        {
            long estimatedSize = EstimateOutputFileSize(targetDuration, profile);
            estimatedMB = estimatedSize / (1024 * 1024); // Convert bytes to megabytes.

            int maxSizeMB = profile?.MaxFileSizeMB ?? 10; // Default to 10MB if no profile
            return estimatedMB <= maxSizeMB;
        }

        /// <summary>
        /// Legacy method for backward compatibility. Checks against a 10MB limit using default format.
        /// </summary>
        /// <param name="targetDuration">The target duration of the mixed audio file.</param>
        /// <param name="estimatedMB">An output parameter that will contain the estimated file size in megabytes.</param>
        /// <returns><c>true</c> if the estimated size is less than or equal to 10MB; otherwise, <c>false</c>.</returns>
        public static bool CheckFileSizeForCX(TimeSpan targetDuration, out long estimatedMB)
        {
            return CheckFileSizeForProfile(targetDuration, null, out estimatedMB);
        }

        /// <summary>
        /// Validates the essential mixing settings provided by the user.
        /// Checks if file paths are provided, if the input files actually exist, and if the profile is valid.
        /// </summary>
        /// <param name="settings">The <see cref="MixingSettings"/> object containing the paths and profile.</param>
        /// <returns>A string containing an error message if validation fails; otherwise, <c>null</c>.</returns>
        public static string ValidateSettings(MixingSettings settings)
        {
            // Check if voice file path is valid and file exists.
            if (string.IsNullOrEmpty(settings.VoiceFilePath) || !File.Exists(settings.VoiceFilePath))
                return "Voice file path is invalid or file does not exist.";

            // Check if music file path is valid and file exists.
            if (string.IsNullOrEmpty(settings.MusicFilePath) || !File.Exists(settings.MusicFilePath))
                return "Music file path is invalid or file does not exist.";

            // Check if output file path is specified.
            if (string.IsNullOrEmpty(settings.OutputFilePath))
                return "Output file path is not specified.";

            // Validate the output profile if provided
            if (settings.OutputProfile != null)
            {
                var profileErrors = settings.OutputProfile.Validate();
                if (profileErrors.Any())
                    return $"Invalid output profile: {string.Join(", ", profileErrors)}";
            }

            return null; // No validation errors found.
        }

        /// <summary>
        /// Validates that the music start offset does not exceed the total duration of the music file.
        /// </summary>
        /// <param name="musicFilePath">The path to the music file.</param>
        /// <param name="musicOffsetSeconds">The requested music offset in seconds.</param>
        /// <returns>A string containing an error message if validation fails; otherwise, <c>null</c>.</returns>
        public static string ValidateMusicOffset(string musicFilePath, double musicOffsetSeconds)
        {
            try
            {
                // Use AudioFileReader to get the total duration of the music file.
                using (var musicReader = new AudioFileReader(musicFilePath))
                {
                    TimeSpan musicOffset = TimeSpan.FromSeconds(musicOffsetSeconds);
                    // If the offset is greater than or equal to the total music duration, it's an invalid offset.
                    if (musicOffset >= musicReader.TotalTime)
                    {
                        return $"Music start offset ({musicOffsetSeconds:F0}s) exceeds music duration ({musicReader.TotalTime:mm\\:ss}). Please choose a smaller offset.";
                    }
                }
                return null; // No validation errors.
            }
            catch (Exception ex)
            {
                // Catch and return any errors encountered while trying to read the music file.
                return $"Error reading music file: {ex.Message}";
            }
        }

        /// <summary>
        /// The main method for mixing voice and music audio files.
        /// It takes mixing settings (including output profile), processes the audio streams (resampling, applying volumes,
        /// offsetting, fading, looping), and writes the combined output to a WAV file using the profile's specifications.
        /// </summary>
        /// <param name="settings">A <see cref="MixingSettings"/> object containing all parameters for mixing.</param>
        /// <returns>A <see cref="MixingResult"/> object indicating success or failure and providing details about the output.</returns>
        public static MixingResult MixAudioFiles(MixingSettings settings)
        {
            var result = new MixingResult(); // Initialize a new result object.

            try
            {
                // Perform initial validation of file paths and settings.
                string validationError = ValidateSettings(settings);
                if (!string.IsNullOrEmpty(validationError))
                {
                    result.Success = false;
                    result.ErrorMessage = validationError;
                    return result; // Return immediately if basic settings are invalid.
                }

                // Determine output format from profile or use defaults
                mdlProfile outputProfile = settings.OutputProfile ?? CreateDefaultProfile();
                result.UsedProfile = outputProfile;

                // Create target format from profile specifications
                var targetFormat = new WaveFormat(
                    outputProfile.SampleRate,
                    outputProfile.BitDepth,
                    outputProfile.Channels);

                // Use 'using' blocks to ensure AudioFileReader resources are properly disposed.
                using (var voiceReader = new AudioFileReader(settings.VoiceFilePath))
                using (var musicReader = new AudioFileReader(settings.MusicFilePath))
                {
                    // Calculate voice duration and the total target duration including the buffer.
                    TimeSpan voiceDuration = voiceReader.TotalTime;
                    TimeSpan bufferDuration = TimeSpan.FromSeconds(settings.BufferSeconds);
                    TimeSpan targetDuration = voiceDuration.Add(bufferDuration);
                    TimeSpan musicOffset = TimeSpan.FromSeconds(settings.MusicOffsetSeconds);

                    // Store calculated durations in the result object.
                    result.VoiceDuration = voiceDuration;
                    result.TotalDuration = targetDuration;

                    // Validate the music offset against the music file's total duration.
                    string offsetValidation = ValidateMusicOffset(settings.MusicFilePath, settings.MusicOffsetSeconds);
                    if (!string.IsNullOrEmpty(offsetValidation))
                    {
                        result.Success = false;
                        result.ErrorMessage = offsetValidation;
                        return result; // Return immediately if music offset is invalid.
                    }

                    // Check file size against profile limits
                    if (!CheckFileSizeForProfile(targetDuration, outputProfile, out long estimatedMB))
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Estimated file size ({estimatedMB}MB) exceeds profile maximum of {outputProfile.MaxFileSizeMB}MB. " +
                                            $"Please reduce the duration or choose a profile with a higher file size limit.";
                        return result;
                    }

                    // Resample both voice and music streams to the target format specified by the profile.
                    var voiceResampled = new MediaFoundationResampler(voiceReader, targetFormat);
                    var musicResampled = new MediaFoundationResampler(musicReader, targetFormat);

                    // Convert the resampled streams to ISampleProvider, which is suitable for mixing and effects.
                    var voiceSampleProvider = voiceResampled.ToSampleProvider();
                    var musicSampleProvider = musicResampled.ToSampleProvider();

                    // Create an offset music provider that handles starting the music at a specific time
                    // and potentially looping it if its duration is shorter than the target.
                    ISampleProvider offsetMusicProvider = CreateOffsetMusicProvider(
                        musicSampleProvider,
                        musicOffset,
                        targetDuration,
                        musicReader.TotalTime);

                    // Apply user-defined volume levels to both voice and music.
                    var voiceVolumeSampleProvider = new VolumeSampleProvider(voiceSampleProvider);
                    voiceVolumeSampleProvider.Volume = settings.VoiceVolume;

                    var musicVolumeSampleProvider = new VolumeSampleProvider(offsetMusicProvider);
                    musicVolumeSampleProvider.Volume = settings.MusicVolume;

                    // Apply a fade-out effect to the music if a buffer period is specified.
                    ISampleProvider finalMusicProvider = musicVolumeSampleProvider;
                    if (settings.BufferSeconds > 0)
                    {
                        // The proFadeOut provider handles fading the music out over the buffer duration,
                        // starting when the voice track ends.
                        finalMusicProvider = new proFadeOut(musicVolumeSampleProvider, voiceDuration, targetDuration);
                    }

                    // Create a MixingSampleProvider to combine the voice and music streams.
                    // The mixer's format must match the target format for processing.
                    var mixer = new MixingSampleProvider(
                        WaveFormat.CreateIeeeFloatWaveFormat(
                            outputProfile.SampleRate,
                            outputProfile.Channels));
                    mixer.AddMixerInput(voiceVolumeSampleProvider); // Add the voice track to the mixer.
                    mixer.AddMixerInput(finalMusicProvider); // Add the processed music track to the mixer.

                    // Write the mixed audio to the output file.
                    // Use the appropriate method based on the profile's bit depth
                    if (outputProfile.BitDepth == 16)
                    {
                        // Use the built-in 16-bit method
                        WaveFileWriter.CreateWaveFile16(settings.OutputFilePath, mixer);
                    }
                    else
                    {
                        // For other bit depths (8, 24, 32), convert sample provider to wave provider
                        // and use the target format from the profile
                        ISampleProvider outputSamples = mixer;

                        // Create the exact wave format we want for output
                        var outputFormat = new WaveFormat(
                            outputProfile.SampleRate,
                            outputProfile.BitDepth,
                            outputProfile.Channels);

                        // Convert sample provider to wave provider with the target format
                        IWaveProvider outputWaveProvider;

                        if (outputProfile.BitDepth == 24)
                        {
                            outputWaveProvider = new SampleToWaveProvider24(outputSamples);
                        }
                        else if (outputProfile.BitDepth == 32)
                        {
                            outputWaveProvider = new SampleToWaveProvider(outputSamples);
                        }
                        else // 8-bit or other
                        {
                            // Convert to 16-bit first, then let WaveFileWriter handle the conversion
                            outputWaveProvider = new SampleToWaveProvider16(outputSamples);
                        }

                        // Write the wave file
                        WaveFileWriter.CreateWaveFile(settings.OutputFilePath, outputWaveProvider);
                    }

                    // Get information about the newly created output file.
                    var fileInfo = new FileInfo(settings.OutputFilePath);
                    result.FileSizeBytes = fileInfo.Length;
                    long fileSizeInMB = fileInfo.Length / (1024 * 1024); // Convert file size to MB.

                    // Prepare a warning message if the file size exceeds the profile's limit.
                    string sizeWarning = "";
                    if (fileSizeInMB > outputProfile.MaxFileSizeMB)
                    {
                        sizeWarning = $"\n⚠️ WARNING: File size ({fileSizeInMB}MB) exceeds profile limit of {outputProfile.MaxFileSizeMB}MB!";
                    }

                    // Prepare informational strings about music offset and buffer.
                    string offsetInfo = settings.MusicOffsetSeconds > 0 ? $"\nMusic starts at: {settings.MusicOffsetSeconds:F0}s" : "";
                    string bufferInfo = settings.BufferSeconds > 0 ? $"\nBuffer with fade-out: {settings.BufferSeconds:F0}s" : "\nNo buffer (music stops with voice)";

                    // Construct the detailed formatted message for the mixing result.
                    result.FormattedMessage = $"Files mixed successfully using '{outputProfile.Name}' profile!\n\n" +
                                              $"Format: {outputProfile.FullFormatDescription}\n" +
                                              $"File size: {fileSizeInMB}MB (limit: {outputProfile.MaxFileSizeMB}MB)\n" +
                                              $"Total output duration: {targetDuration:mm\\:ss}\n" +
                                              $"Voice duration: {voiceDuration:mm\\:ss}{offsetInfo}{bufferInfo}{sizeWarning}";

                    result.Success = true; // Mark the operation as successful.
                    return result;
                }
            }
            catch (Exception ex)
            {
                // Catch any exceptions that occur during the mixing process and set the result to failed.
                result.Success = false;
                result.ErrorMessage = $"Error mixing files: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Creates a default audio profile for backward compatibility when no profile is specified.
        /// Uses the legacy 8kHz, 16-bit, mono format commonly used for PBX systems.
        /// </summary>
        /// <returns>A default <see cref="mdlProfile"/> with legacy specifications.</returns>
        private static mdlProfile CreateDefaultProfile()
        {
            return new mdlProfile
            {
                Name = "Default (Legacy)",
                Description = "Default format for backward compatibility",
                SampleRate = 8000,
                BitDepth = 16,
                Channels = 1,
                MaxFileSizeMB = 10,
                TemplateSource = "Default"
            };
        }

        /// <summary>
        /// Creates an <see cref="ISampleProvider"/> for the music track, handling
        /// the specified start offset and looping the music if its duration (after offset)
        /// is shorter than the target output duration.
        /// </summary>
        /// <param name="musicSampleProvider">The original sample provider for the music track.</param>
        /// <param name="musicOffset">The time offset at which the music should start.</param>
        /// <param name="targetDuration">The total desired duration of the output audio.</param>
        /// <param name="musicTotalDuration">The total original duration of the music file.</param>
        /// <returns>An <see cref="ISampleProvider"/> configured with the specified offset and looping behavior.</returns>
        private static ISampleProvider CreateOffsetMusicProvider(
            ISampleProvider musicSampleProvider,
            TimeSpan musicOffset,
            TimeSpan targetDuration,
            TimeSpan musicTotalDuration)
        {
            ISampleProvider offsetMusicProvider;

            // If a music offset is specified.
            if (musicOffset.TotalSeconds > 0)
            {
                // Calculate the remaining duration of the music after the offset.
                TimeSpan availableMusicDuration = musicTotalDuration.Subtract(musicOffset);

                // Create an OffsetSampleProvider to skip the beginning of the music track.
                offsetMusicProvider = new OffsetSampleProvider(musicSampleProvider)
                {
                    SkipOver = musicOffset // Skip the initial part of the music.
                };

                // If the available music duration (after skipping) is shorter than the target output duration, loop it.
                if (availableMusicDuration < targetDuration)
                {
                    // Use proLooping to loop the music from its start (after offset) to fill the target duration.
                    offsetMusicProvider = new proLooping(offsetMusicProvider, targetDuration, musicOffset, musicTotalDuration);
                }
                else
                {
                    // If the music is long enough, trim it to the exact target duration.
                    offsetMusicProvider = new OffsetSampleProvider(offsetMusicProvider)
                    {
                        Take = targetDuration // Take only the required portion.
                    };
                }
            }
            else
            {
                // If no music offset is specified.
                // If the total music duration is shorter than the target output duration, loop it from the beginning.
                if (musicTotalDuration < targetDuration)
                {
                    offsetMusicProvider = new proLooping(musicSampleProvider, targetDuration, TimeSpan.Zero, musicTotalDuration);
                }
                else
                {
                    // If the music is long enough, trim it to the exact target duration.
                    offsetMusicProvider = new OffsetSampleProvider(musicSampleProvider)
                    {
                        Take = targetDuration
                    };
                }
            }

            return offsetMusicProvider;
        }

        /// <summary>
        /// Retrieves the total duration of an audio file.
        /// </summary>
        /// <param name="filePath">The path to the audio file.</param>
        /// <returns>A <see cref="TimeSpan"/> representing the audio file's duration, or <see cref="TimeSpan.Zero"/> if an error occurs.</returns>
        public static TimeSpan GetAudioDuration(string filePath)
        {
            try
            {
                // Use AudioFileReader to get the total time of the audio file.
                using (var audioFileReader = new AudioFileReader(filePath))
                {
                    return audioFileReader.TotalTime;
                }
            }
            catch
            {
                // Return TimeSpan.Zero if there's any error reading the file (e.g., file not found, corrupted).
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Checks if a given file path points to a valid and readable audio file.
        /// </summary>
        /// <param name="filePath">The path to the file to check.</param>
        /// <returns><c>true</c> if the file exists and can be read as an audio file with a duration greater than zero; otherwise, <c>false</c>.</returns>
        public static bool IsValidAudioFile(string filePath)
        {
            // First, check if the file path is null/empty or if the file doesn't exist.
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                // Attempt to create an AudioFileReader. If successful, it's likely a valid audio file.
                // Check if its total time is greater than zero to ensure it's not an empty or unreadable file.
                using (var audioFileReader = new AudioFileReader(filePath))
                {
                    return audioFileReader.TotalTime > TimeSpan.Zero;
                }
            }
            catch
            {
                // Any exception during AudioFileReader creation or access indicates an invalid or unreadable audio file.
                return false;
            }
        }
    }
}