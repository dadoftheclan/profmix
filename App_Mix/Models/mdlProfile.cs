using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App_Mix.Models
{
    /// <summary>
    /// Represents an audio profile, defining the technical specifications for an audio output file.
    /// This model includes properties such as sample rate, bit depth, channels, and maximum file size,
    /// along with metadata like name, description, and creation/modification dates.
    /// It also provides utility methods for validation, cloning, and generating display strings.
    /// </summary>
    public class mdlProfile
    {
        /// <summary>
        /// Gets or sets the unique identifier for the audio profile.
        /// This is a GUID string, automatically generated upon creation.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the user-friendly name of the audio profile (e.g., "3CX PBX", "High Quality").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a descriptive text explaining the purpose or characteristics of the profile.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the sample rate of the audio in Hertz (Hz) (e.g., 8000, 44100).
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Gets or sets the bit depth (audio resolution) in bits (e.g., 8, 16, 24, 32).
        /// </summary>
        public int BitDepth { get; set; }

        /// <summary>
        /// Gets or sets the number of audio channels (1 for Mono, 2 for Stereo).
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed file size for output generated with this profile, in Megabytes (MB).
        /// </summary>
        public int MaxFileSizeMB { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the profile was created.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the profile was last modified.
        /// </summary>
        public DateTime ModifiedDate { get; set; }

        /// <summary>
        /// Gets or sets a boolean indicating whether this profile is a default, built-in profile.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Gets or sets a string indicating the source of the profile, such as "3CX", "FreePBX", or "Custom".
        /// Useful for identifying profiles created from templates.
        /// </summary>
        public string TemplateSource { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="mdlProfile"/> class.
        /// Sets default values for ID, creation/modification dates, and marks it as a custom profile.
        /// </summary>
        public mdlProfile()
        {
            Id = Guid.NewGuid().ToString(); // Assigns a new unique ID.
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
            IsDefault = false; // New profiles are custom by default.
            TemplateSource = "Custom";
        }

        /// <summary>
        /// Gets a formatted string representation of the audio format for UI display.
        /// Example: "8kHz 16-bit Mono" or "44.1kHz 16-bit Stereo".
        /// </summary>
        public string FormatDisplay
        {
            get
            {
                // Format sample rate to kHz if 1000 or greater, otherwise Hz.
                string sampleRateDisplay = SampleRate >= 1000 ? $"{SampleRate / 1000}kHz" : $"{SampleRate}Hz";
                // Determine channel display (Mono or Stereo).
                string channelDisplay = Channels == 1 ? "Mono" : "Stereo";
                // Combine into a single display string.
                return $"{sampleRateDisplay} {BitDepth}-bit {channelDisplay}";
            }
        }

        /// <summary>
        /// Gets a formatted string representation of the maximum file size for UI display.
        /// Example: "10MB".
        /// </summary>
        public string MaxSizeDisplay
        {
            get
            {
                return $"{MaxFileSizeMB}MB";
            }
        }

        /// <summary>
        /// Gets a comprehensive formatted description of the audio profile's specifications,
        /// including file type, PCM encoding, sample rate, bit depth, channels, and maximum file size.
        /// Example: "WAV (PCM, 8kHz, 16-bit, Mono, max: 10MB)".
        /// </summary>
        public string FullFormatDescription
        {
            get
            {
                // Re-use sample rate and channel display logic from FormatDisplay.
                string sampleRateDisplay = SampleRate >= 1000 ? $"{SampleRate / 1000}kHz" : $"{SampleRate}Hz";
                string channelDisplay = Channels == 1 ? "Mono" : "Stereo";
                // Combine all details into a full description string.
                return $"WAV (PCM, {sampleRateDisplay}, {BitDepth}-bit, {channelDisplay}, max: {MaxFileSizeMB}MB)";
            }
        }

        /// <summary>
        /// Creates a deep copy (clone) of the current <see cref="mdlProfile"/> instance.
        /// The cloned profile will have a new unique ID, and its name will be appended with "(Copy)".
        /// Creation and modification dates are set to the current time.
        /// </summary>
        /// <returns>A new <see cref="mdlProfile"/> object that is a copy of the current instance.</returns>
        public mdlProfile Clone()
        {
            return new mdlProfile
            {
                Id = Guid.NewGuid().ToString(), // Assign a new ID for the cloned profile.
                Name = this.Name + " (Copy)", // Append "(Copy)" to the name.
                Description = this.Description,
                SampleRate = this.SampleRate,
                BitDepth = this.BitDepth,
                Channels = this.Channels,
                MaxFileSizeMB = this.MaxFileSizeMB,
                CreatedDate = DateTime.Now, // Set new creation date.
                ModifiedDate = DateTime.Now, // Set new modification date.
                IsDefault = false, // Cloned profiles are not considered default.
                TemplateSource = this.TemplateSource // Retain template source info.
            };
        }

        /// <summary>
        /// Updates the <see cref="ModifiedDate"/> property of the profile to the current date and time.
        /// This method should be called whenever the profile's settings are changed.
        /// </summary>
        public void Touch()
        {
            ModifiedDate = DateTime.Now;
        }

        /// <summary>
        /// Validates the current settings of the profile to ensure they meet application requirements.
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of strings, where each string is an error message.
        /// If the list is empty, the profile is considered valid.</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // Validate Name property.
            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Profile name is required.");
            if (Name?.Length > 50)
                errors.Add("Profile name must be 50 characters or less.");

            // Validate SampleRate property.
            if (SampleRate <= 0)
                errors.Add("Sample rate must be greater than 0.");

            // Validate BitDepth property.
            if (BitDepth <= 0 || BitDepth > 32)
                errors.Add("Bit depth must be between 1 and 32.");

            // Validate Channels property.
            if (Channels <= 0 || Channels > 2)
                errors.Add("Channels must be 1 (Mono) or 2 (Stereo).");

            // Validate MaxFileSizeMB property.
            if (MaxFileSizeMB <= 0)
                errors.Add("Maximum file size must be greater than 0 MB.");
            if (MaxFileSizeMB > 1000) // Arbitrary upper limit to prevent extreme values.
                errors.Add("Maximum file size cannot exceed 1000 MB.");

            return errors;
        }

        /// <summary>
        /// Creates and returns a new <see cref="mdlProfile"/> instance configured with settings
        /// optimized for 3CX PBX systems.
        /// </summary>
        /// <returns>A new <see cref="mdlProfile"/> representing the 3CX template.</returns>
        public static mdlProfile Create3CXTemplate()
        {
            return new mdlProfile
            {
                Name = "3CX PBX",
                Description = "Standard 3CX phone system requirements for IVR prompts",
                SampleRate = 8000, // 8 kHz
                BitDepth = 16,     // 16-bit
                Channels = 1,      // Mono
                MaxFileSizeMB = 10, // 10 MB limit
                TemplateSource = "3CX"
            };
        }

        /// <summary>
        /// Creates and returns a new <see cref="mdlProfile"/> instance configured with settings
        /// optimized for FreePBX systems.
        /// </summary>
        /// <returns>A new <see cref="mdlProfile"/> representing the FreePBX template.</returns>
        public static mdlProfile CreateFreePBXTemplate()
        {
            return new mdlProfile
            {
                Name = "FreePBX",
                Description = "FreePBX open-source phone system requirements",
                SampleRate = 8000,
                BitDepth = 16,
                Channels = 1,
                MaxFileSizeMB = 50, // Higher limit for FreePBX
                TemplateSource = "FreePBX"
            };
        }

        /// <summary>
        /// Creates and returns a new <see cref="mdlProfile"/> instance configured with settings
        /// optimized for Sangoma PBX systems.
        /// </summary>
        /// <returns>A new <see cref="mdlProfile"/> representing the Sangoma template.</returns>
        public static mdlProfile CreateSangomaTemplate()
        {
            return new mdlProfile
            {
                Name = "Sangoma PBX",
                Description = "Sangoma business phone system requirements",
                SampleRate = 8000,
                BitDepth = 16,
                Channels = 1,
                MaxFileSizeMB = 25,
                TemplateSource = "Sangoma"
            };
        }

        /// <summary>
        /// Creates and returns a new <see cref="mdlProfile"/> instance configured with settings
        /// optimized for Genesys Cloud contact center systems.
        /// </summary>
        /// <returns>A new <see cref="mdlProfile"/> representing the Genesys template.</returns>
        public static mdlProfile CreateGenesysTemplate()
        {
            return new mdlProfile
            {
                Name = "Genesys Cloud",
                Description = "Genesys enterprise contact center requirements",
                SampleRate = 8000,
                BitDepth = 16,
                Channels = 1,
                MaxFileSizeMB = 100,
                TemplateSource = "Genesys"
            };
        }

        /// <summary>
        /// Creates and returns a new <see cref="mdlProfile"/> instance configured with settings
        /// optimized for 8x8 VoIP cloud communication platforms.
        /// </summary>
        /// <returns>A new <see cref="mdlProfile"/> representing the 8x8 template.</returns>
        public static mdlProfile Create8x8Template()
        {
            return new mdlProfile
            {
                Name = "8x8 VoIP",
                Description = "8x8 cloud communications platform requirements",
                SampleRate = 8000,
                BitDepth = 16,
                Channels = 1,
                MaxFileSizeMB = 20,
                TemplateSource = "8x8"
            };
        }

        /// <summary>
        /// Creates and returns a new <see cref="mdlProfile"/> instance configured for high-quality audio output.
        /// This template uses a higher sample rate and stereo channels, suitable for general audio production.
        /// </summary>
        /// <returns>A new <see cref="mdlProfile"/> representing the High Quality template.</returns>
        public static mdlProfile CreateHighQualityTemplate()
        {
            return new mdlProfile
            {
                Name = "High Quality",
                Description = "High-quality audio for advanced systems (44.1kHz stereo)",
                SampleRate = 44100, // CD quality sample rate
                BitDepth = 16,
                Channels = 2,      // Stereo
                MaxFileSizeMB = 200,
                TemplateSource = "HighQuality"
            };
        }
    }
}
