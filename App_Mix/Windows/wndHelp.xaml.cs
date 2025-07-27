using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents; // For Run, LineBreak, etc., used in formatted text.
using System.Windows.Media; // For SolidColorBrush, Color, ColorConverter.

namespace App_Mix.Windows
{
    /// <summary>
    /// Interaction logic for wndHelp.xaml. This window provides a comprehensive help system
    /// for the ProfMix application. It displays various help topics in a navigable list
    /// and renders rich text content for each selected topic.
    /// </summary>
    public partial class wndHelp : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="wndHelp"/> class.
        /// Sets up the UI components and defaults the help topic to the first item in the list.
        /// </summary>
        public wndHelp()
        {
            InitializeComponent();

            // Automatically select the first help topic when the window loads.
            if (LstHelpTopics.Items.Count > 0)
            {
                LstHelpTopics.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Event handler for when the selection in the help topics list (LstHelpTopics) changes.
        /// Loads the content for the newly selected help topic.
        /// </summary>
        /// <param name="sender">The source of the event (LstHelpTopics).</param>
        /// <param name="e">Event arguments containing information about the selection change.</param>
        private void LstHelpTopics_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ensures that a ListBoxItem is actually selected.
            if (LstHelpTopics.SelectedItem is ListBoxItem selectedItem)
            {
                // Retrieves the 'Tag' property of the selected ListBoxItem, which
                // is used to identify the specific help topic.
                string tag = selectedItem.Tag?.ToString();
                // Loads the corresponding help content based on the tag.
                LoadHelpContent(tag);
            }
        }

        /// <summary>
        /// Event handler for the "Close" button click.
        /// Closes the help window.
        /// </summary>
        /// <param name="sender">The source of the event (BtnClose).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Loads the appropriate help content into the display area based on the provided topic tag.
        /// Uses a switch statement to direct to specific content loading methods.
        /// </summary>
        /// <param name="topicTag">A string tag identifying the help topic (e.g., "getting-started", "audio-mixing").</param>
        private void LoadHelpContent(string topicTag)
        {
            // Uses a switch expression to call the relevant content loading method.
            switch (topicTag)
            {
                case "getting-started":
                    LoadGettingStarted();
                    break;
                case "voice-recording":
                    LoadVoiceRecording();
                    break;
                case "background-music":
                    LoadBackgroundMusic();
                    break;
                case "audio-mixing":
                    LoadAudioMixing();
                    break;
                case "audio-profiles":
                    LoadAudioProfiles();
                    break;
                case "preview-playback":
                    LoadPreviewPlayback();
                    break;
                case "output-settings":
                    LoadOutputSettings();
                    break;
                case "pbx-templates":
                    LoadPBXTemplates();
                    break;
                case "troubleshooting":
                    LoadTroubleshooting();
                    break;
                case "tips-tricks":
                    LoadTipsTricks();
                    break;
                default:
                    // Fallback to the welcome content if the tag is unrecognized or null.
                    LoadWelcome();
                    break;
            }
        }

        /// <summary>
        /// Sets the title and subtitle displayed at the top of the help content area.
        /// </summary>
        /// <param name="title">The main title for the help topic.</param>
        /// <param name="subtitle">A brief descriptive subtitle for the help topic.</param>
        private void SetContentHeader(string title, string subtitle)
        {
            TxtContentTitle.Text = title;
            TxtContentSubtitle.Text = subtitle;
        }

        /// <summary>
        /// Clears all existing content from the main content display panel.
        /// This is called before loading new help content to ensure a clean display.
        /// </summary>
        private void ClearContent()
        {
            ContentPanel.Children.Clear();
        }

        /// <summary>
        /// Creates a styled UI section (Border containing TextBlock) for displaying help content.
        /// This helper method ensures consistent styling across different help topics.
        /// </summary>
        /// <param name="title">The title of the section (e.g., "Supported File Formats").</param>
        /// <param name="content">The main text content of the section.</param>
        /// <param name="accentColor">Optional: A hexadecimal string for the accent color of the title (default is a shade of green).</param>
        /// <returns>A <see cref="Border"/> control containing the formatted text.</returns>
        private Border CreateContentSection(string title, string content, string accentColor = "#FF00FF90")
        {
            // Create the outer Border for styling.
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF404040")), // Dark gray background.
                CornerRadius = new CornerRadius(8), // Rounded corners.
                Padding = new Thickness(20), // Internal padding.
                Margin = new Thickness(0, 0, 0, 15) // Margin below each section.
            };

            // Add a subtle drop shadow effect for visual depth.
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Lime, // Shadow color.
                BlurRadius = 4, // Softness of the shadow.
                ShadowDepth = 0, // No offset, just a glow.
                Opacity = 0.1 // Transparency of the shadow.
            };

            // Create a TextBlock to hold the actual text content.
            var textBlock = new TextBlock
            {
                Foreground = Brushes.White, // White text color.
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap, // Ensures text wraps within the bounds.
                LineHeight = 22 // Spacing between lines of text.
            };

            // Create a Run for the section title, applying bold font and accent color.
            var titleRun = new Run(title)
            {
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor))
            };

            // Add the title, line breaks, and the main content to the TextBlock's Inlines collection
            // to allow for rich text formatting.
            textBlock.Inlines.Add(titleRun);
            textBlock.Inlines.Add(new LineBreak()); // Adds a newline.
            textBlock.Inlines.Add(new LineBreak()); // Adds another newline for spacing.
            textBlock.Inlines.Add(new Run(content)); // Adds the main content.

            border.Child = textBlock; // Set the TextBlock as the child of the Border.
            return border;
        }

        /// <summary>
        /// Creates a styled "Tip Box" UI element for displaying quick tips or important notes.
        /// </summary>
        /// <param name="tipText">The text content of the tip.</param>
        /// <returns>A <see cref="Border"/> control containing the tip text.</returns>
        private Border CreateTipBox(string tipText)
        {
            // Create the outer Border for styling.
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00FF90")), // Bright green background for tips.
                CornerRadius = new CornerRadius(8), // Rounded corners.
                Padding = new Thickness(15), // Internal padding.
                Margin = new Thickness(0, 10, 0, 0) // Margin above the tip box.
            };

            // Add a more prominent drop shadow for tips.
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Lime,
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.6
            };

            // Create a TextBlock for the tip text.
            var textBlock = new TextBlock
            {
                Foreground = Brushes.Black, // Black text color for contrast.
                FontSize = 12,
                FontWeight = FontWeights.SemiBold, // Semi-bold font.
                TextWrapping = TextWrapping.Wrap,
                Text = tipText // Set the tip text.
            };

            border.Child = textBlock; // Set the TextBlock as the child of the Border.
            return border;
        }

        /// <summary>
        /// Loads the "Welcome" help content into the display panel.
        /// </summary>
        private void LoadWelcome()
        {
            SetContentHeader("Welcome to ProfMix", "Professional voice over mixing made simple");
            ClearContent(); // Clear previous content.

            ContentPanel.Children.Add(CreateContentSection(
                "Welcome to ProfMix Help!",
                "ProfMix is a professional voice over mixing tool designed to help you create high-quality audio content for various applications including IVR systems, podcasts, presentations, and more.\n\n" +
                "Select a topic from the left panel to get started:\n" +
                "• Learn the basics with our Getting Started guide\n" +
                "• Understand how to work with voice recordings and background music\n" +
                "• Master the audio mixing process\n" +
                "• Configure audio profiles for different systems\n" +
                "• Troubleshoot common issues"));

            ContentPanel.Children.Add(CreateTipBox("💡 Quick Tip: Use the navigation panel on the left to jump between different help topics quickly!"));
        }

        /// <summary>
        /// Loads the "Getting Started" help content.
        /// </summary>
        private void LoadGettingStarted()
        {
            SetContentHeader("Getting Started", "Your first steps with ProfMix");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Quick Start Guide",
                "Follow these simple steps to create your first professional voice over mix:\n\n" +
                "1. Select Voice Recording: Click 'Browse' next to Voice Recording and choose your main audio file (WAV or MP3)\n\n" +
                "2. Choose Background Music: Click 'Browse' next to Background Music and select your background track\n\n" +
                "3. Adjust Volumes: Use the volume sliders to balance your voice and music levels\n\n" +
                "4. Configure Music Settings: Set the start time and fade out duration for your background music\n\n" +
                "5. Select Output Location: Choose where to save your final mixed audio file\n\n" +
                "6. Choose Audio Profile: Select the appropriate profile for your target system\n\n" +
                "7. Mix Your Audio: Click 'Mix Audio Files' to create your professional output"));

            ContentPanel.Children.Add(CreateTipBox("💡 Pro Tip: Always preview your individual files before mixing to ensure they sound correct!"));
        }

        /// <summary>
        /// Loads the "Voice Recording" help content.
        /// </summary>
        private void LoadVoiceRecording()
        {
            SetContentHeader("Voice Recording", "Working with voice audio files");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Supported File Formats",
                "ProfMix supports the following audio formats for voice recordings:\n\n" +
                "• WAV files (.wav) - Recommended for highest quality\n" +
                "• MP3 files (.mp3) - Good for compressed files\n\n" +
                "The application uses NAudio and MediaFoundation for robust audio file handling."));

            ContentPanel.Children.Add(CreateContentSection(
                "Voice File Selection",
                "To select your voice recording:\n\n" +
                "1. Click the 'Browse' button next to Voice Recording\n" +
                "2. Navigate to your audio file location\n" +
                "3. Select your WAV or MP3 file\n" +
                "4. The file path will appear in the text box\n" +
                "5. The Preview button will become enabled"));

            ContentPanel.Children.Add(CreateContentSection(
                "Volume Control",
                "Use the Voice Volume slider to adjust the level of your voice recording:\n\n" +
                "• Range: 0% to 100%\n" +
                "• Default: 100%\n" +
                "• Real-time preview shows current percentage\n" +
                "• Changes are applied during the mixing process"));

            ContentPanel.Children.Add(CreateTipBox("💡 Recording Tip: For best results, record your voice in a quiet environment with consistent volume levels."));
        }

        /// <summary>
        /// Loads the "Background Music" help content.
        /// </summary>
        private void LoadBackgroundMusic()
        {
            SetContentHeader("Background Music", "Adding and controlling background audio");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Music File Selection",
                "Background music enhances your voice over with professional ambiance:\n\n" +
                "• Supported formats: WAV and MP3\n" +
                "• Click 'Browse' to select your music file\n" +
                "• Preview button allows you to listen before mixing\n" +
                "• File path displays in the text box once selected"));

            ContentPanel.Children.Add(CreateContentSection(
                "Volume Control",
                "The Music Volume slider controls background audio level:\n\n" +
                "• Range: 0% to 100%\n" +
                "• Default: 30% (recommended for background)\n" +
                "• Typically set lower than voice to avoid overpowering\n" +
                "• Real-time percentage display"));

            ContentPanel.Children.Add(CreateContentSection(
                "Start Offset",
                "Control when background music begins playing:\n\n" +
                "• Range: 0 to 120 seconds\n" +
                "• Default: 0 seconds (starts immediately)\n" +
                "• Useful for delayed music entry\n" +
                "• Perfect for intro voice sections"));

            ContentPanel.Children.Add(CreateContentSection(
                "Fade Out Duration",
                "Set how long the music takes to fade out:\n\n" +
                "• Range: 0 to 30 seconds\n" +
                "• Default: 10 seconds\n" +
                "• Creates smooth professional ending\n" +
                "• Prevents abrupt music cutoff"));

            ContentPanel.Children.Add(CreateTipBox("💡 Music Tip: Keep background music volume around 20-40% to maintain voice clarity while adding ambiance."));
        }

        /// <summary>
        /// Loads the "Audio Mixing" help content.
        /// </summary>
        private void LoadAudioMixing()
        {
            SetContentHeader("Audio Mixing", "The core mixing process and controls");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Mixing Process",
                "ProfMix uses advanced audio processing to combine your voice and music:\n\n" +
                "• Real-time volume adjustment during mixing\n" +
                "• Professional fade-in/fade-out effects\n" +
                "• Maintains audio quality throughout process\n" +
                "• Progress notifications keep you informed\n" +
                "• Automatic file size estimation and warnings"));

            ContentPanel.Children.Add(CreateContentSection(
                "Mixing Settings",
                "Key settings that affect your final output:\n\n" +
                "Voice Volume: Controls the level of your main recording\n" +
                "Music Volume: Sets background music intensity\n" +
                "Music Offset: Delays music start time\n" +
                "Buffer Length: Determines fade-out duration\n" +
                "Audio Profile: Defines output format and quality"));

            ContentPanel.Children.Add(CreateContentSection(
                "Quality Considerations",
                "ProfMix ensures professional output quality:\n\n" +
                "• Maintains original audio bit depth when possible\n" +
                "• Preserves sample rate consistency\n" +
                "• Applies professional crossfading techniques\n" +
                "• Prevents clipping and distortion\n" +
                "• Optimizes for target system requirements"));

            ContentPanel.Children.Add(CreateContentSection(
                "File Size Management",
                "Automatic file size checking helps ensure compatibility:\n\n" +
                "• Estimates final file size before mixing\n" +
                "• Warns if output exceeds common system limits\n" +
                "• Provides option to continue with large files\n" +
                "• Shows final file size after successful mixing"));

            ContentPanel.Children.Add(CreateTipBox("💡 Mixing Tip: Always check that both voice and music files are selected before clicking 'Mix Audio Files'."));
        }

        /// <summary>
        /// Loads the "Audio Profiles" help content.
        /// </summary>
        private void LoadAudioProfiles()
        {
            SetContentHeader("Audio Profiles", "Configuring output formats for different systems");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "What Are Audio Profiles?",
                "Audio profiles define the technical specifications for your output files:\n\n" +
                "• Sample Rate: How many audio samples per second (Hz)\n" +
                "• Bit Depth: Audio resolution (8, 16, 24, or 32-bit)\n" +
                "• Channels: Mono (1) or Stereo (2)\n" +
                "• File Size Limits: Maximum allowable output size\n" +
                "• Format: Output file type (typically WAV)"));

            ContentPanel.Children.Add(CreateContentSection(
                "Managing Profiles",
                "Create and customize profiles for different needs:\n\n" +
                "• Click 'Manage' button to open Profile Manager\n" +
                "• Use templates for common PBX systems\n" +
                "• Create custom profiles for specific requirements\n" +
                "• Delete unused profiles to keep list clean\n" +
                "• Current profile shown in dropdown"));

            ContentPanel.Children.Add(CreateContentSection(
                "Profile Selection",
                "Choose the right profile for your target system:\n\n" +
                "• Dropdown shows all available profiles\n" +
                "• Profile description helps identify purpose\n" +
                "• Format display shows technical specifications\n" +
                "• File size limit prevents compatibility issues\n" +
                "• Selection is remembered between sessions"));

            ContentPanel.Children.Add(CreateTipBox("💡 Profile Tip: When in doubt, use the template profiles - they're optimized for popular PBX systems."));
        }

        /// <summary>
        /// Loads the "Preview & Playback" help content.
        /// </summary>
        private void LoadPreviewPlayback()
        {
            SetContentHeader("Preview & Playback", "Testing your audio before mixing");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Preview Controls",
                "ProfMix provides comprehensive audio preview capabilities:\n\n" +
                "• Voice Preview: Test your voice recording\n" +
                "• Music Preview: Listen to background music\n" +
                "• Mixed Preview: Play the final mixed result\n" +
                "• Play/Pause functionality for all previews\n" +
                "• Automatic playback notifications"));

            ContentPanel.Children.Add(CreateContentSection(
                "Playback Features",
                "Advanced playback management:\n\n" +
                "• One-click play/pause toggle\n" +
                "• Automatic stopping when switching files\n" +
                "• Visual feedback with button state changes\n" +
                "• Playback progress notifications\n" +
                "• Error handling for corrupted files"));

            ContentPanel.Children.Add(CreateContentSection(
                "Using Preview Effectively",
                "Best practices for audio preview:\n\n" +
                "1. Always preview voice file first to check quality\n" +
                "2. Test background music to ensure it fits your content\n" +
                "3. Adjust volume levels based on preview feedback\n" +
                "4. Preview mixed result before finalizing\n" +
                "5. Use preview to catch issues early"));

            ContentPanel.Children.Add(CreateTipBox("💡 Preview Tip: Use headphones when previewing to better judge the mix balance and catch any audio issues."));
        }

        /// <summary>
        /// Loads the "Output Settings" help content.
        /// </summary>
        private void LoadOutputSettings()
        {
            SetContentHeader("Output Settings", "Configuring your final audio file");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Output File Selection",
                "Choose where to save your mixed audio:\n\n" +
                "• Click 'Save As' to open file dialog\n" +
                "• Default format is WAV for best quality\n" +
                "• Choose descriptive filename for easy identification\n" +
                "• File path appears in text box\n" +
                "• Existing files will be overwritten"));

            ContentPanel.Children.Add(CreateContentSection(
                "File Format Considerations",
                "Understanding output formats:\n\n" +
                "• WAV format provides uncompressed quality\n" +
                "• Compatible with most PBX and audio systems\n" +
                "• File size depends on duration and profile settings\n" +
                "• Professional standard for voice applications\n" +
                "• Preserves full audio fidelity"));

            ContentPanel.Children.Add(CreateContentSection(
                "Quality Settings",
                "Output quality is determined by your selected profile:\n\n" +
                "• Sample rate affects frequency response\n" +
                "• Bit depth controls dynamic range\n" +
                "• Higher settings = better quality but larger files\n" +
                "• Choose profile based on target system requirements\n" +
                "• PBX systems often prefer specific formats"));

            ContentPanel.Children.Add(CreateTipBox("💡 Output Tip: Save your mixed files with descriptive names including date and project info for easy organization."));
        }

        /// <summary>
        /// Loads the "PBX Templates" help content.
        /// </summary>
        private void LoadPBXTemplates()
        {
            SetContentHeader("PBX Templates", "Quick setup for popular phone systems");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Template Overview",
                "ProfMix includes optimized templates for popular PBX systems:\n\n" +
                "• 3CX PBX: 8kHz, 16-bit, Mono, 10MB limit\n" +
                "• FreePBX: Flexible settings, up to 50MB\n" +
                "• Sangoma: Optimized for Sangoma systems\n" +
                "• Genesys: Enterprise-grade specifications\n" +
                "• 8x8: Cloud PBX optimized settings"));

            ContentPanel.Children.Add(CreateContentSection(
                "Using Templates",
                "How to apply PBX templates:\n\n" +
                "1. Open Profile Manager from main window\n" +
                "2. Click on the appropriate template button\n" +
                "3. Template automatically configures all settings\n" +
                "4. Customize if needed for your specific requirements\n" +
                "5. Save the profile with a descriptive name"));

            ContentPanel.Children.Add(CreateContentSection(
                "Template Specifications",
                "Common PBX requirements addressed by templates:\n\n" +
                "• Sample rates optimized for voice clarity\n" +
                "• File size limits prevent upload issues\n" +
                "• Bit depth balances quality and compatibility\n" +
                "• Mono format reduces file size\n" +
                "• Tested with real-world PBX systems"));

            ContentPanel.Children.Add(CreateContentSection(
                "Custom Templates",
                "Create your own templates:\n\n" +
                "• Start with closest existing template\n" +
                "• Modify settings for your specific PBX\n" +
                "• Test with small files first\n" +
                "• Save as new profile for future use\n" +
                "• Document any special requirements"));

            ContentPanel.Children.Add(CreateTipBox("💡 Template Tip: When working with a new PBX system, start with the closest template and adjust based on testing results."));
        }

        /// <summary>
        /// Loads the "Troubleshooting" help content.
        /// </summary>
        private void LoadTroubleshooting()
        {
            SetContentHeader("Troubleshooting", "Solving common issues");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Common Audio Issues",
                "Solving typical audio problems:\n\n" +
                "File Won't Load:\n" +
                "• Check file format (WAV/MP3 only)\n" +
                "• Verify file isn't corrupted\n" +
                "• Try converting with another tool first\n\n" +
                "Preview Not Working:\n" +
                "• Ensure audio drivers are installed\n" +
                "• Check Windows audio settings\n" +
                "• Try restarting the application"));

            ContentPanel.Children.Add(CreateContentSection(
                "Mixing Problems",
                "Resolving mixing issues:\n\n" +
                "Mixing Fails:\n" +
                "• Verify both voice and music files are selected\n" +
                "• Check output path is writable\n" +
                "• Ensure sufficient disk space\n\n" +
                "Poor Audio Quality:\n" +
                "• Use higher quality source files\n" +
                "• Check profile settings\n" +
                "• Avoid extreme volume settings"));

            ContentPanel.Children.Add(CreateContentSection(
                "File Size Issues",
                "Managing output file size:\n\n" +
                "File Too Large:\n" +
                "• Reduce bit depth in profile\n" +
                "• Lower sample rate if acceptable\n" +
                "• Trim source audio length\n" +
                "• Use mono instead of stereo\n\n" +
                "File Too Small/Quiet:\n" +
                "• Increase voice volume setting\n" +
                "• Check source file quality\n" +
                "• Verify profile bit depth"));

            ContentPanel.Children.Add(CreateContentSection(
                "Performance Issues",
                "Improving application performance:\n\n" +
                "Slow Mixing:\n" +
                "• Close other audio applications\n" +
                "• Use local files (not network drives)\n" +
                "• Ensure adequate RAM available\n\n" +
                "Application Crashes:\n" +
                "• Update Windows audio drivers\n" +
                "• Run as administrator if needed\n" +
                "• Check antivirus isn't blocking"));

            ContentPanel.Children.Add(CreateTipBox("💡 Troubleshooting Tip: Most issues are resolved by ensuring your source files are high quality and in supported formats."));
        }

        /// <summary>
        /// Loads the "Tips & Tricks" help content.
        /// </summary>
        private void LoadTipsTricks()
        {
            SetContentHeader("Tips & Tricks", "Advanced usage and best practices");
            ClearContent();

            ContentPanel.Children.Add(CreateContentSection(
                "Professional Voice Over Tips",
                "Creating broadcast-quality results:\n\n" +
                "Recording Quality:\n" +
                "• Use a good microphone and audio interface\n" +
                "• Record in a quiet, treated environment\n" +
                "• Maintain consistent distance from microphone\n" +
                "• Record at appropriate levels (avoid clipping)\n\n" +
                "Voice Techniques:\n" +
                "• Speak clearly and at consistent pace\n" +
                "• Take pauses for natural breathing\n" +
                "• Match tone to content purpose\n" +
                "• Consider your target audience"));

            ContentPanel.Children.Add(CreateContentSection(
                "Music Selection & Usage",
                "Choosing and using background music effectively:\n\n" +
                "Music Selection:\n" +
                "• Choose music that complements voice tone\n" +
                "• Avoid music with strong melodies that compete\n" +
                "• Consider royalty-free or licensed music\n" +
                "• Match tempo to content pacing\n\n" +
                "Volume Balance:\n" +
                "• Start with music at 20-30% of voice level\n" +
                "• Adjust based on music complexity\n" +
                "• Test on different speakers/headphones\n" +
                "• Ensure voice remains clearly intelligible"));

            ContentPanel.Children.Add(CreateContentSection(
                "Workflow Optimization",
                "Streamlining your production process:\n\n" +
                "Preparation:\n" +
                "• Organize source files in project folders\n" +
                "• Create naming conventions for outputs\n" +
                "• Set up profiles for regular clients\n" +
                "• Keep backup copies of source materials\n\n" +
                "Efficiency Tips:\n" +
                "• Preview files before final mixing\n" +
                "• Save commonly used settings as profiles\n" +
                "• Use templates for repeated work\n" +
                "• Document any special requirements"));

            ContentPanel.Children.Add(CreateContentSection(
                "Quality Assurance",
                "Ensuring professional results every time:\n\n" +
                "Before Mixing:\n" +
                "• Check source file quality and format\n" +
                "• Verify all settings match requirements\n" +
                "• Preview individual files for issues\n" +
                "• Confirm output path and filename\n\n" +
                "After Mixing:\n" +
                "• Test playback on target system if possible\n" +
                "• Check file size meets requirements\n" +
                "• Listen to full output for quality issues\n" +
                "• Archive project settings for future reference"));

            ContentPanel.Children.Add(CreateContentSection(
                "Advanced Techniques",
                "Professional-level mixing strategies:\n\n" +
                "• Use music offset to create intro sections\n" +
                "• Experiment with fade durations for different effects\n" +
                "• Create multiple versions with different music levels\n" +
                "• Consider creating alternate versions for different uses\n" +
                "• Document successful settings combinations\n" +
                "• Build a library of tested music tracks\n" +
                "• Create project templates for different content types"));

            ContentPanel.Children.Add(CreateTipBox("💡 Pro Tip: Always keep your original voice recordings unprocessed - you can remix with different music or settings later!"));
        }
    }
}
