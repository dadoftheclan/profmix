using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave; // Core NAudio library for audio processing.
using NAudio.Wave.SampleProviders; // Provides base classes and interfaces for sample providers.

namespace App_Mix.Providers
{
    /// <summary>
    /// A custom NAudio <see cref="ISampleProvider"/> that allows an audio source to be looped
    /// for a specified target duration. It supports starting the loop from a specific offset
    /// within the source audio and is designed to handle various underlying NAudio providers.
    /// This is particularly useful for background music that needs to play for the entire
    /// duration of a voice-over, potentially repeating if the music track is shorter.
    /// </summary>
    public class proLooping : ISampleProvider
    {
        /// <summary>
        /// The original audio source <see cref="ISampleProvider"/> that will be read from and potentially looped.
        /// </summary>
        private readonly ISampleProvider source;

        /// <summary>
        /// The total duration for which this looping provider should output audio.
        /// Once this duration is reached, the <see cref="Read"/> method will return 0 samples.
        /// </summary>
        private readonly TimeSpan targetDuration;

        /// <summary>
        /// The initial offset within the source audio from which looping should effectively begin.
        /// This allows skipping a portion of the original source before starting the loop.
        /// </summary>
        private readonly TimeSpan sourceStartOffset;

        /// <summary>
        /// The total original duration of the underlying audio source. This is crucial
        /// for correctly calculating the loop point when the source is an <see cref="AudioFileReader"/>.
        /// </summary>
        private readonly TimeSpan sourceTotalDuration;

        /// <summary>
        /// The total number of samples (float values) that need to be read to reach the <see cref="targetDuration"/>.
        /// Calculated based on the target duration and the WaveFormat properties.
        /// </summary>
        private readonly long targetSamples;

        /// <summary>
        /// Tracks the total number of samples that have been read and provided by this looping provider so far.
        /// Used to determine when the <see cref="targetDuration"/> has been met.
        /// </summary>
        private long samplesRead;

        /// <summary>
        /// Tracks the current position within the source audio, in samples.
        /// Used internally for managing the loop point for seekable sources.
        /// </summary>
        private long sourcePosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="proLooping"/> class.
        /// </summary>
        /// <param name="source">The original audio source <see cref="ISampleProvider"/> to loop.</param>
        /// <param name="targetDuration">The total duration for which audio should be output by this provider.</param>
        /// <param name="sourceStartOffset">Optional: The time offset within the source where the loop should start. Defaults to zero.</param>
        /// <param name="sourceTotalDuration">Optional: The total duration of the original source. Required for accurate looping with <see cref="AudioFileReader"/>.</param>
        public proLooping(ISampleProvider source, TimeSpan targetDuration, TimeSpan sourceStartOffset = default, TimeSpan sourceTotalDuration = default)
        {
            this.source = source;
            this.targetDuration = targetDuration;
            this.sourceStartOffset = sourceStartOffset;
            this.sourceTotalDuration = sourceTotalDuration;
            this.WaveFormat = source.WaveFormat; // The output format is the same as the source format.
            // Calculate the total number of samples needed for the target duration.
            this.targetSamples = (long)(targetDuration.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);

            // Note: The logic for handling `effectiveLoopDuration` was commented out in the original.
            // For robust looping, the `Read` method handles resetting the source's position.
        }

        /// <summary>
        /// Gets the <see cref="WaveFormat"/> of the audio provided by this sample provider.
        /// This will be the same as the source provider's WaveFormat.
        /// </summary>
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Reads audio samples from the source provider, filling the provided buffer.
        /// This method implements the core looping logic: if the source ends before the
        /// <see cref="targetDuration"/> is reached, it attempts to reset the source's
        /// position to the <see cref="sourceStartOffset"/> and continue reading.
        /// </summary>
        /// <param name="buffer">The buffer to fill with audio samples.</param>
        /// <param name="offset">The offset in the buffer at which to begin writing.</param>
        /// <param name="count">The maximum number of samples to read into the buffer.</param>
        /// <returns>The number of samples actually read into the buffer.</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            // If the target duration has already been reached, return 0 samples.
            if (samplesRead >= targetSamples)
                return 0;

            // Calculate how many samples are still needed to reach the target duration,
            // capped by the requested 'count'.
            int samplesNeeded = (int)Math.Min(count, targetSamples - samplesRead);
            int totalSamplesRead = 0; // Tracks samples read in the current call.

            // Loop until enough samples are read for this call or the target duration is met.
            while (totalSamplesRead < samplesNeeded)
            {
                // Read samples from the underlying source provider.
                int samplesThisTime = source.Read(buffer, offset + totalSamplesRead, samplesNeeded - totalSamplesRead);

                if (samplesThisTime == 0)
                {
                    // If the source provider returns 0 samples, it means it has reached its end.
                    // Attempt to reset its position to loop.

                    // Special handling for OffsetSampleProvider:
                    // Directly seeking an OffsetSampleProvider to its "start" for looping is complex
                    // because its internal position is relative to its *original* source.
                    // For a simple loop of the *entire* original source (after offset),
                    // you'd typically need to recreate the OffsetSampleProvider or ensure the underlying
                    // source (if it's seekable like AudioFileReader) is reset correctly.
                    // The current implementation has a limitation here for nested OffsetSampleProviders.
                    if (source is OffsetSampleProvider offsetSource)
                    {
                        // This case is tricky. An OffsetSampleProvider doesn't inherently support
                        // "resetting" to its effective start without recreating it or knowing its
                        // underlying seekable source. For now, we break to prevent an infinite loop
                        // if the source cannot be reset.
                        break;
                    }
                    // Special handling for AudioFileReader:
                    // If the source is an AudioFileReader, we can directly set its position
                    // to the beginning of the desired loop segment (defined by sourceStartOffset).
                    else if (source is AudioFileReader fileReader)
                    {
                        // Calculate the byte position corresponding to the sourceStartOffset.
                        fileReader.Position = (long)(sourceStartOffset.TotalSeconds * fileReader.WaveFormat.AverageBytesPerSecond);
                    }
                    else
                    {
                        // If the source is neither an OffsetSampleProvider nor a seekable AudioFileReader,
                        // we cannot loop it, so we stop reading.
                        break;
                    }
                }
                else
                {
                    // If samples were read, update the counters.
                    totalSamplesRead += samplesThisTime;
                    sourcePosition += samplesThisTime; // Keep track of position within the source.
                }
            }

            samplesRead += totalSamplesRead; // Update the total samples read by this provider.
            return totalSamplesRead; // Return the number of samples read in this call.
        }
    }
}
