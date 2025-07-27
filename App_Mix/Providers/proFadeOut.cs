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
    /// A custom NAudio <see cref="ISampleProvider"/> that applies a fade-out effect to its
    /// source audio. The fade-out begins at a specified time (typically when a voice-over ends)
    /// and continues until a total specified duration is reached, creating a smooth transition to silence.
    /// This is commonly used for background music to gently fade out after the main audio content.
    /// </summary>
    public class proFadeOut : ISampleProvider
    {
        /// <summary>
        /// The original audio source <see cref="ISampleProvider"/> to which the fade-out effect will be applied.
        /// </summary>
        private readonly ISampleProvider source;

        /// <summary>
        /// The time at which the main voice-over or primary audio content is expected to end.
        /// The fade-out effect for the background music typically starts at this point.
        /// </summary>
        private readonly TimeSpan voiceEndTime;

        /// <summary>
        /// The total desired duration of the output audio from this provider.
        /// The fade-out will complete by this time.
        /// </summary>
        private readonly TimeSpan totalDuration;

        /// <summary>
        /// The calculated time at which the fade-out effect should begin.
        /// This is typically set to <see cref="voiceEndTime"/>.
        /// </summary>
        private readonly TimeSpan fadeStartTime;

        /// <summary>
        /// The calculated duration over which the fade-out effect will occur.
        /// This is the difference between <see cref="totalDuration"/> and <see cref="fadeStartTime"/>.
        /// </summary>
        private readonly TimeSpan fadeDuration;

        /// <summary>
        /// The sample index in the audio stream where the fade-out effect should begin.
        /// Calculated from <see cref="fadeStartTime"/> and the <see cref="WaveFormat"/>.
        /// </summary>
        private readonly long fadeStartSample;

        /// <summary>
        /// The total number of samples (float values) that this provider is expected to output.
        /// Calculated from <see cref="totalDuration"/> and the <see cref="WaveFormat"/>.
        /// </summary>
        private readonly long totalSamples;

        /// <summary>
        /// Tracks the total number of samples that have been read and provided by this provider so far.
        /// Used to determine the current position within the audio stream for applying the fade.
        /// </summary>
        private long samplesRead;

        /// <summary>
        /// Initializes a new instance of the <see cref="proFadeOut"/> class.
        /// </summary>
        /// <param name="source">The original audio source <see cref="ISampleProvider"/> to apply the fade-out to.</param>
        /// <param name="voiceEndTime">The time at which the voice-over ends; this is typically when the music fade-out should begin.</param>
        /// <param name="totalDuration">The total desired duration of the output audio, by which time the fade-out should be complete.</param>
        public proFadeOut(ISampleProvider source, TimeSpan voiceEndTime, TimeSpan totalDuration)
        {
            this.source = source;
            this.voiceEndTime = voiceEndTime;
            this.totalDuration = totalDuration;
            this.WaveFormat = source.WaveFormat; // The output format is the same as the source format.

            // The fade-out starts at the voice's end time.
            this.fadeStartTime = voiceEndTime;
            // The fade duration is the remaining time from the voice end to the total desired duration.
            this.fadeDuration = totalDuration.Subtract(voiceEndTime);

            // Calculate the sample indices corresponding to the fade start and total duration.
            // This calculation correctly accounts for both mono and stereo channels.
            this.fadeStartSample = (long)(fadeStartTime.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
            this.totalSamples = (long)(totalDuration.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
        }

        /// <summary>
        /// Gets the <see cref="WaveFormat"/> of the audio provided by this sample provider.
        /// This will be the same as the source provider's WaveFormat.
        /// </summary>
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Reads audio samples from the source provider and applies a fade-out effect
        /// based on the current position relative to the fade start and total duration.
        /// </summary>
        /// <param name="buffer">The buffer to fill with audio samples.</param>
        /// <param name="offset">The offset in the buffer at which to begin writing.</param>
        /// <param name="count">The maximum number of samples to read into the buffer.</param>
        /// <returns>The number of samples actually read into the buffer.</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            // Read samples from the underlying source provider.
            int samplesReadThisCall = source.Read(buffer, offset, count);

            // Iterate through the samples read in this call to apply the fade-out effect.
            for (int i = 0; i < samplesReadThisCall; i++)
            {
                // Calculate the absolute current sample index within the entire output stream.
                long currentSampleAbsolute = this.samplesRead + i;

                // Check if the current sample falls within the defined fade-out period.
                if (currentSampleAbsolute >= fadeStartSample && currentSampleAbsolute < totalSamples)
                {
                    // Calculate the position within the fade segment (0 at start of fade, increasing to totalFadeSamples).
                    long fadePosition = currentSampleAbsolute - fadeStartSample;
                    // Calculate the total number of samples over which the fade will occur.
                    long totalFadeSamples = totalSamples - fadeStartSample;

                    // Only apply fade if there's a valid fade duration.
                    if (totalFadeSamples > 0)
                    {
                        // Calculate fade progress: 0.0 at the beginning of the fade, 1.0 at the end.
                        float fadeProgress = (float)fadePosition / totalFadeSamples;

                        // Apply an exponential fade curve (squared) for a more natural-sounding fade-out.
                        // The multiplier goes from 1.0 (full volume) down to 0.0 (silent).
                        float fadeMultiplier = (float)Math.Pow(1.0 - fadeProgress, 2.0);

                        // Apply the calculated multiplier to the current sample in the buffer.
                        buffer[offset + i] *= fadeMultiplier;
                    }
                }
                // If the current sample is beyond the total desired duration, silence it.
                else if (currentSampleAbsolute >= totalSamples)
                {
                    buffer[offset + i] = 0; // Set sample to zero (silence).
                }
            }

            // Update the total number of samples read by this provider.
            this.samplesRead += samplesReadThisCall;
            return samplesReadThisCall; // Return the number of samples processed in this call.
        }
    }
}
