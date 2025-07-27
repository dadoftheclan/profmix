using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32; // Although not directly used in wndProfile, often included for file dialogs.
using App_Mix.Models;
using App_Mix.Systems;

namespace App_Mix.Windows
{
    /// <summary>
    /// Interaction logic for wndProfile.xaml. This window allows users to manage audio profiles,
    /// including creating new profiles, editing existing ones, deleting profiles,
    /// and applying pre-defined templates for common audio settings (e.g., 3CX, FreePBX).
    /// It also handles saving and loading profile data and managing unsaved changes.
    /// </summary>
    public partial class wndProfile : Window
    {
        /// <summary>
        /// Stores a temporary copy of the profile currently being edited.
        /// Changes are applied to this copy and only saved to the main system state upon explicit user action.
        /// </summary>
        private mdlProfile _currentEditingProfile;

        /// <summary>
        /// A flag indicating whether the window is currently in "editing mode" for an existing profile.
        /// If false, it implies a new profile is being created.
        /// </summary>
        private bool _isEditingMode = false;

        /// <summary>
        /// A flag indicating whether there are unsaved changes to the currently edited profile.
        /// Used to prompt the user before discarding changes or switching profiles.
        /// </summary>
        private bool _hasUnsavedChanges = false;

        /// <summary>
        /// Gets the profile that was selected and applied by the user when the window was closed.
        /// This property is set when the "Apply & Close" button is clicked.
        /// </summary>
        public mdlProfile SelectedProfile { get; private set; }

        /// <summary>
        /// Gets a boolean value indicating whether a profile was successfully selected and applied
        /// (i.e., the user clicked "Apply & Close" and changes were saved).
        /// </summary>
        public bool ProfileWasSelected { get; private set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="wndProfile"/> class.
        /// This constructor sets up the UI components and performs initial data loading.
        /// </summary>
        public wndProfile()
        {
            InitializeComponent();
            InitializeWindow();
        }

        #region Window Initialization

        /// <summary>
        /// Performs initial setup for the profile management window.
        /// This includes initializing the system state, loading existing profiles into the UI,
        /// setting up event handlers, and configuring the initial UI state.
        /// </summary>
        private void InitializeWindow()
        {
            // Ensures the sysState singleton is initialized, which handles loading and saving profiles.
            sysState.Initialize();

            // Populates the list box with all available profiles.
            RefreshProfileList();

            // Subscribes to the ProfilesChanged event in sysState. This ensures that
            // if profiles are added, removed, or updated elsewhere, the list in this window refreshes automatically.
            sysState.ProfilesChanged += OnProfilesChanged;

            // Sets the UI elements to a default, cleared state.
            ClearProfileDetails();
            // Updates the enabled/disabled state of various buttons based on the current context.
            UpdateButtonStates();
        }

        /// <summary>
        /// Refreshes the list of profiles displayed in the LstProfiles ListBox.
        /// It clears the existing items and rebinds the list to the current collection of profiles
        /// from the system state. It also attempts to re-select the currently active profile if one exists.
        /// </summary>
        private void RefreshProfileList()
        {
            // Clears the current ItemsSource to force a refresh.
            LstProfiles.ItemsSource = null;
            // Binds the ListBox to the observable collection of profiles from sysState.
            LstProfiles.ItemsSource = sysState.Profiles;

            // If a current profile is set in the system state, select it in the ListBox.
            if (sysState.CurrentProfile != null)
            {
                LstProfiles.SelectedItem = sysState.CurrentProfile;
            }
        }

        /// <summary>
        /// Event handler for when the collection of profiles in <see cref="sysState"/> changes.
        /// Triggers a refresh of the profile list to show the updated list.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void OnProfilesChanged(object sender, EventArgs e)
        {
            RefreshProfileList();
        }

        #endregion

        #region Profile List Events

        /// <summary>
        /// Event handler for when the selected item in the profile list (LstProfiles) changes.
        /// This method handles potential unsaved changes before loading a new profile for editing.
        /// It prompts the user to save changes if any exist and then loads the selected profile into the UI.
        /// </summary>
        /// <param name="sender">The source of the event (LstProfiles).</param>
        /// <param name="e">Event arguments containing information about the selection change.</param>
        private void LstProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Checks if a profile has been selected in the list.
            if (LstProfiles.SelectedItem is mdlProfile selectedProfile)
            {
                // If there are unsaved changes to the current profile, prompt the user.
                if (_hasUnsavedChanges)
                {
                    var result = sysInteract.ShowConfirmationWithCancel(
                        "Unsaved Changes",
                        "You have unsaved changes. Do you want to save them before switching profiles?");

                    // If user chooses to save, attempt to save the current profile.
                    if (result == MessageBoxResult.Yes)
                    {
                        if (!SaveCurrentProfile())
                        {
                            // If save fails, revert the selection to the profile that was being edited
                            // and prevent switching to the new profile.
                            LstProfiles.SelectedItem = _currentEditingProfile;
                            return;
                        }
                    }
                    // If user chooses to cancel the switch, revert the selection.
                    else if (result == MessageBoxResult.Cancel)
                    {
                        LstProfiles.SelectedItem = _currentEditingProfile;
                        return;
                    }
                }

                // Loads the newly selected profile into the detail view for editing.
                LoadProfileForEditing(selectedProfile);
            }
            else
            {
                // If no profile is selected (e.g., after deleting the last one), clear the details panel.
                ClearProfileDetails();
            }

            // Updates the enabled/disabled state of buttons based on the new selection and unsaved changes.
            UpdateButtonStates();
        }

        #endregion

        #region Profile Management Events

        /// <summary>
        /// Event handler for the "New Profile" button click.
        /// If there are unsaved changes to the current profile, it prompts the user to save them
        /// before creating a new profile. Then, it initializes a new blank profile for editing.
        /// </summary>
        /// <param name="sender">The source of the event (BtnNewProfile).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            // Checks for unsaved changes and prompts the user if necessary.
            if (_hasUnsavedChanges)
            {
                var result = sysInteract.ShowConfirmationWithCancel(
                    "Unsaved Changes",
                    "You have unsaved changes. Do you want to save them before creating a new profile?");

                // If user chooses to save and save fails, abort.
                if (result == MessageBoxResult.Yes)
                {
                    if (!SaveCurrentProfile())
                        return;
                }
                // If user chooses to cancel, abort.
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            // Creates and loads a new default profile into the UI for editing.
            CreateNewProfile();
        }

        /// <summary>
        /// Event handler for the "Delete Profile" button click.
        /// Prompts the user for confirmation before deleting the currently selected profile.
        /// If confirmed, it removes the profile from the system state and clears the UI details.
        /// </summary>
        /// <param name="sender">The source of the event (BtnDeleteProfile).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            // Ensures a profile is selected before attempting to delete.
            if (LstProfiles.SelectedItem is mdlProfile selectedProfile)
            {
                // Prompts the user for confirmation due to the irreversible nature of deletion.
                var confirmed = sysInteract.ShowConfirmation(
                    "Confirm Delete",
                    $"Are you sure you want to delete the profile '{selectedProfile.Name}'?\n\nThis action cannot be undone.");

                if (confirmed)
                {
                    // Removes the profile from the global system state.
                    sysState.RemoveProfile(selectedProfile);
                    // Clears the details panel as the profile no longer exists.
                    ClearProfileDetails();
                    _hasUnsavedChanges = false; // No unsaved changes after deletion.
                    // Updates button states after the deletion.
                    UpdateButtonStates();

                    // Displays a success notification.
                    sysInteract.ShowSuccess("Profile Deleted", $"Profile '{selectedProfile.Name}' has been deleted.");
                }
            }
        }

        #endregion

        #region Template Events

        /// <summary>
        /// Event handler for template buttons (e.g., "3CX", "FreePBX").
        /// Applies the settings of a pre-defined template to the currently edited profile.
        /// </summary>
        /// <param name="sender">The source of the event (the template Button).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnTemplate_Click(object sender, RoutedEventArgs e)
        {
            // Checks if the sender is a Button and if its Tag property contains a valid template name.
            if (sender is Button button && button.Tag is string template)
            {
                mdlProfile templateProfile = null;

                // Uses a switch statement to create the appropriate template profile based on the button's tag.
                switch (template)
                {
                    case "3CX":
                        templateProfile = mdlProfile.Create3CXTemplate();
                        break;
                    case "FreePBX":
                        templateProfile = mdlProfile.CreateFreePBXTemplate();
                        break;
                    case "Sangoma":
                        templateProfile = mdlProfile.CreateSangomaTemplate();
                        break;
                    case "Genesys":
                        templateProfile = mdlProfile.CreateGenesysTemplate();
                        break;
                    case "8x8":
                        templateProfile = mdlProfile.Create8x8Template();
                        break;
                }

                // If a template profile was successfully created, apply its settings.
                if (templateProfile != null)
                {
                    ApplyTemplate(templateProfile);
                    // Displays an informational notification.
                    sysInteract.ShowInfo("Template Applied", $"{template} template has been applied to the current profile.");
                }
            }
        }

        /// <summary>
        /// Applies the settings from a given template profile to the currently edited profile.
        /// If editing an existing profile, it preserves the ID and name. If creating a new profile,
        /// it uses the template's name and description.
        /// </summary>
        /// <param name="template">The <see cref="mdlProfile"/> object representing the template to apply.</param>
        private void ApplyTemplate(mdlProfile template)
        {
            // Ensures there's a profile being edited to apply the template to.
            if (_currentEditingProfile != null)
            {
                // If in editing mode (modifying an existing profile), keep its original name and ID.
                if (_isEditingMode)
                {
                    _currentEditingProfile.Description = template.Description;
                    _currentEditingProfile.SampleRate = template.SampleRate;
                    _currentEditingProfile.BitDepth = template.BitDepth;
                    _currentEditingProfile.Channels = template.Channels;
                    _currentEditingProfile.MaxFileSizeMB = template.MaxFileSizeMB;
                    _currentEditingProfile.TemplateSource = template.TemplateSource;
                }
                else
                {
                    // If creating a new profile, use the template's name and description.
                    _currentEditingProfile.Name = template.Name;
                    _currentEditingProfile.Description = template.Description;
                    _currentEditingProfile.SampleRate = template.SampleRate;
                    _currentEditingProfile.BitDepth = template.BitDepth;
                    _currentEditingProfile.Channels = template.Channels;
                    _currentEditingProfile.MaxFileSizeMB = template.MaxFileSizeMB;
                    _currentEditingProfile.TemplateSource = template.TemplateSource;
                }

                // Loads the updated profile data into the UI controls.
                LoadProfileIntoUI();
                _hasUnsavedChanges = true; // Applying a template counts as an unsaved change.
                // Updates the enabled/disabled state of buttons.
                UpdateButtonStates();
            }
        }

        #endregion

        #region UI Change Events

        /// <summary>
        /// Event handler for when the text in the profile name textbox changes.
        /// Updates the name of the current editing profile and marks that there are unsaved changes.
        /// </summary>
        /// <param name="sender">The source of the event (TxtProfileName).</param>
        /// <param name="e">Event arguments.</param>
        private void TxtProfileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Only update if a profile is currently being edited.
            if (_currentEditingProfile != null)
            {
                _currentEditingProfile.Name = TxtProfileName.Text;
                _hasUnsavedChanges = true; // Indicates changes need to be saved.
                UpdateButtonStates(); // Updates button states (e.g., enables Save button).
            }
        }

        /// <summary>
        /// Event handler for when the text in the description textbox changes.
        /// Updates the description of the current editing profile and marks that there are unsaved changes.
        /// </summary>
        /// <param name="sender">The source of the event (TxtDescription).</param>
        /// <param name="e">Event arguments.</param>
        private void TxtDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Only update if a profile is currently being edited.
            if (_currentEditingProfile != null)
            {
                _currentEditingProfile.Description = TxtDescription.Text;
                _hasUnsavedChanges = true; // Indicates changes need to be saved.
                UpdateButtonStates(); // Updates button states (e.g., enables Save button).
            }
        }

        /// <summary>
        /// Event handler for when the selection in any of the audio format ComboBoxes changes
        /// (Sample Rate, Bit Depth, Channels).
        /// Updates the profile model with the new UI values, refreshes the format preview,
        /// and marks that there are unsaved changes.
        /// </summary>
        /// <param name="sender">The source of the event (one of the audio format ComboBoxes).</param>
        /// <param name="e">Event arguments.</param>
        private void AudioFormat_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Ensures a profile is being edited and the window is fully loaded before processing changes.
            if (_currentEditingProfile != null && IsLoaded)
            {
                UpdateProfileFromUI(); // Pulls all current UI values into the _currentEditingProfile.
                UpdateFormatPreview(); // Updates the displayed format string.
                _hasUnsavedChanges = true; // Indicates changes need to be saved.
                UpdateButtonStates(); // Updates button states.
            }
        }

        /// <summary>
        /// Event handler for when the text in the maximum file size textbox changes.
        /// Parses the input, updates the MaxFileSizeMB property of the current editing profile,
        /// refreshes the format preview, and marks that there are unsaved changes.
        /// </summary>
        /// <param name="sender">The source of the event (TxtMaxSize).</param>
        /// <param name="e">Event arguments.</param>
        private void TxtMaxSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Ensures a profile is being edited and the window is fully loaded before processing changes.
            if (_currentEditingProfile != null && IsLoaded)
            {
                // Attempts to parse the text as an integer.
                if (int.TryParse(TxtMaxSize.Text, out int maxSize))
                {
                    _currentEditingProfile.MaxFileSizeMB = maxSize;
                    UpdateFormatPreview(); // Updates the displayed format string, which might include max file size.
                    _hasUnsavedChanges = true; // Indicates changes need to be saved.
                    UpdateButtonStates(); // Updates button states.
                }
                // No explicit error handling here for invalid input, as validation occurs on save.
            }
        }

        #endregion

        #region Action Button Events

        /// <summary>
        /// Event handler for the "Save" button click.
        /// Attempts to save the currently edited profile to the system state.
        /// </summary>
        /// <param name="sender">The source of the event (BtnSave).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentProfile();
        }

        /// <summary>
        /// Event handler for the "Cancel" button click.
        /// If there are unsaved changes, it prompts the user for confirmation before discarding them
        /// and closing the window.
        /// </summary>
        /// <param name="sender">The source of the event (BtnCancel).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Checks for unsaved changes and prompts the user.
            if (_hasUnsavedChanges)
            {
                var confirmed = sysInteract.ShowConfirmation(
                    "Confirm Cancel",
                    "You have unsaved changes. Are you sure you want to cancel?");

                // If the user does not confirm, abort the cancel operation.
                if (!confirmed)
                    return;
            }

            // Sets the DialogResult to false, indicating the window was cancelled.
            this.DialogResult = false;
            // Closes the window.
            this.Close();
        }

        /// <summary>
        /// Event handler for the "Apply & Close" button click.
        /// Attempts to save the currently edited profile. If successful, it sets the
        /// <see cref="SelectedProfile"/> and <see cref="ProfileWasSelected"/> properties,
        /// updates the global current profile in <see cref="sysState"/>, and closes the window.
        /// </summary>
        /// <param name="sender">The source of the event (BtnApply).</param>
        /// <param name="e">Event arguments.</param>
        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // Attempts to save the current profile.
            if (SaveCurrentProfile())
            {
                // If save is successful and a profile is selected in the list,
                // set the public properties for the calling window.
                if (LstProfiles.SelectedItem is mdlProfile selectedProfile)
                {
                    SelectedProfile = selectedProfile;
                    ProfileWasSelected = true;
                    sysState.CurrentProfile = selectedProfile; // Also update the global current profile.
                }

                // Sets the DialogResult to true, indicating the window was closed with a positive result.
                this.DialogResult = true;
                // Closes the window.
                this.Close();
            }
        }

        #endregion

        #region Profile Management Methods

        /// <summary>
        /// Initializes a new default audio profile and loads it into the UI for editing.
        /// Sets the window to "new profile" mode and marks that there are unsaved changes.
        /// </summary>
        private void CreateNewProfile()
        {
            // Creates a new mdlProfile instance with default values.
            _currentEditingProfile = new mdlProfile
            {
                Name = "New Profile",
                Description = "Custom audio profile",
                SampleRate = 8000,
                BitDepth = 16,
                Channels = 1,
                MaxFileSizeMB = 10
            };

            _isEditingMode = false; // Indicates that a new profile is being created, not an existing one edited.
            LoadProfileIntoUI(); // Populates the UI with the new profile's default values.
            _hasUnsavedChanges = true; // A new profile immediately has unsaved changes.
            UpdateButtonStates(); // Updates button states to reflect the new mode.

            // Sets focus to the profile name textbox and selects all text for easy renaming.
            TxtProfileName.Focus();
            TxtProfileName.SelectAll();

            // Displays an informational notification to guide the user.
            sysInteract.ShowInfo("New Profile", "New profile created. Enter a name and configure settings.");
        }

        /// <summary>
        /// Loads the details of a specified profile into the UI controls for editing.
        /// Creates a clone of the profile to allow modifications without affecting the original
        /// until explicitly saved.
        /// </summary>
        /// <param name="profile">The <see cref="mdlProfile"/> to load for editing.</param>
        private void LoadProfileForEditing(mdlProfile profile)
        {
            if (profile == null)
            {
                ClearProfileDetails();
                return;
            }

            // Creates a deep copy of the profile to allow editing without modifying the original
            // object in the sysState.Profiles collection until the changes are explicitly saved.
            _currentEditingProfile = profile.Clone();
            // The ID and Name are typically kept from the original for updating purposes,
            // though the name can be changed by the user in the UI.
            _currentEditingProfile.Id = profile.Id;
            _currentEditingProfile.Name = profile.Name;

            _isEditingMode = true; // Sets the window to editing an existing profile.
            LoadProfileIntoUI(); // Populates the UI with the copied profile's data.
            _hasUnsavedChanges = false; // Initially, no unsaved changes when loading an existing profile.
            UpdateButtonStates(); // Updates button states.
        }

        /// <summary>
        /// Populates the UI controls (textboxes, comboboxes) with the data from the
        /// <see cref="_currentEditingProfile"/> object.
        /// </summary>
        private void LoadProfileIntoUI()
        {
            if (_currentEditingProfile == null)
            {
                ClearProfileDetails();
                return;
            }

            // Populates basic profile information.
            TxtProfileName.Text = _currentEditingProfile.Name;
            TxtDescription.Text = _currentEditingProfile.Description;

            // Sets the selected items in the ComboBoxes based on the profile's audio format settings.
            // It uses the Tag property of ComboBoxItems for selection.
            SetComboBoxByTag(CmbSampleRate, _currentEditingProfile.SampleRate.ToString());
            SetComboBoxByTag(CmbBitDepth, _currentEditingProfile.BitDepth.ToString());
            SetComboBoxByTag(CmbChannels, _currentEditingProfile.Channels.ToString());
            TxtMaxSize.Text = _currentEditingProfile.MaxFileSizeMB.ToString();

            // Updates the preview text for the audio format.
            UpdateFormatPreview();
        }

        /// <summary>
        /// Clears all input fields and resets the UI to a default, empty state.
        /// Also resets internal state flags related to profile editing.
        /// </summary>
        private void ClearProfileDetails()
        {
            TxtProfileName.Text = "";
            TxtDescription.Text = "";
            CmbSampleRate.SelectedIndex = -1; // Deselects any item.
            CmbBitDepth.SelectedIndex = -1;
            CmbChannels.SelectedIndex = -1;
            TxtMaxSize.Text = "";
            TxtFormatPreview.Text = "Select a profile to view format details"; // Default message.

            _currentEditingProfile = null; // No profile is currently being edited.
            _isEditingMode = false; // Not in editing mode.
            _hasUnsavedChanges = false; // No unsaved changes.
        }

        /// <summary>
        /// Saves the <see cref="_currentEditingProfile"/> to the <see cref="sysState"/>.
        /// This method first validates the profile data, then either adds a new profile
        /// or updates an existing one in the system state.
        /// </summary>
        /// <returns><c>true</c> if the profile was successfully saved; otherwise, <c>false</c>.</returns>
        private bool SaveCurrentProfile()
        {
            // If no profile is being edited, there's nothing to save.
            if (_currentEditingProfile == null)
                return false;

            // Ensures the _currentEditingProfile object reflects the latest values from the UI.
            UpdateProfileFromUI();

            // Performs validation on the profile data.
            var errors = _currentEditingProfile.Validate();
            if (errors.Any()) // If there are any validation errors.
            {
                sysInteract.ShowError("Validation Error",
                    $"Profile validation failed:\n\n{string.Join("\n", errors)}");
                return false; // Aborts save if validation fails.
            }

            try
            {
                // Determines whether to update an existing profile or add a new one.
                if (_isEditingMode)
                {
                    sysState.UpdateProfile(_currentEditingProfile); // Update existing.
                }
                else
                {
                    sysState.AddProfile(_currentEditingProfile); // Add new.
                    _isEditingMode = true; // After adding, it's now an existing profile being edited.
                }

                _hasUnsavedChanges = false; // No unsaved changes after a successful save.
                UpdateButtonStates(); // Updates button states (e.g., disables Save button).

                // Displays a specialized success notification for profile saving.
                sysInteract.NotifyProfileSaved(_currentEditingProfile.Name);

                return true; // Save successful.
            }
            catch (Exception ex)
            {
                // Catches and displays any errors that occur during the save process.
                sysInteract.ShowError("Save Error", $"Error saving profile: {ex.Message}");
                return false; // Save failed.
            }
        }

        #endregion

        #region UI Helper Methods

        /// <summary>
        /// Updates the properties of the <see cref="_currentEditingProfile"/> object
        /// with the current values from the UI input controls.
        /// </summary>
        private void UpdateProfileFromUI()
        {
            if (_currentEditingProfile == null)
                return;

            // Updates basic text properties, trimming whitespace and handling nulls.
            _currentEditingProfile.Name = TxtProfileName.Text?.Trim() ?? "";
            _currentEditingProfile.Description = TxtDescription.Text?.Trim() ?? "";

            // Parses and updates integer properties from ComboBox selections and textboxes.
            // Uses GetComboBoxTagValue to retrieve the numerical value from the ComboBoxItem's Tag.
            if (GetComboBoxTagValue(CmbSampleRate) is string sampleRateStr && int.TryParse(sampleRateStr, out int sampleRate))
                _currentEditingProfile.SampleRate = sampleRate;

            if (GetComboBoxTagValue(CmbBitDepth) is string bitDepthStr && int.TryParse(bitDepthStr, out int bitDepth))
                _currentEditingProfile.BitDepth = bitDepth;

            if (GetComboBoxTagValue(CmbChannels) is string channelsStr && int.TryParse(channelsStr, out int channels))
                _currentEditingProfile.Channels = channels;

            if (int.TryParse(TxtMaxSize.Text, out int maxSize))
                _currentEditingProfile.MaxFileSizeMB = maxSize;
        }

        /// <summary>
        /// Updates the text in the TxtFormatPreview textbox to display a formatted
        /// description of the current audio profile's settings.
        /// </summary>
        private void UpdateFormatPreview()
        {
            if (_currentEditingProfile != null)
            {
                // Uses a property of mdlProfile to get a nicely formatted string.
                TxtFormatPreview.Text = _currentEditingProfile.FullFormatDescription;
            }
        }

        /// <summary>
        /// Updates the enabled/disabled state of various buttons on the window
        /// based on the current selection in the profile list and whether there are unsaved changes.
        /// </summary>
        private void UpdateButtonStates()
        {
            // Delete button is enabled only if a profile is selected in the list.
            BtnDeleteProfile.IsEnabled = LstProfiles.SelectedItem != null;
            // Save button is enabled only if a profile is being edited AND there are unsaved changes.
            BtnSave.IsEnabled = _currentEditingProfile != null && _hasUnsavedChanges;
            // Apply button is enabled if a profile is being edited AND there are NO unsaved changes
            // (meaning it's ready to be applied, or has just been saved).
            BtnApply.IsEnabled = _currentEditingProfile != null && !_hasUnsavedChanges;
        }

        /// <summary>
        /// Sets the selected item in a ComboBox by matching the <see cref="ComboBoxItem.Tag"/> property.
        /// This is useful when ComboBox items are populated with arbitrary objects but need to be selected
        /// based on a specific identifying string in their Tag.
        /// </summary>
        /// <param name="comboBox">The ComboBox control to update.</param>
        /// <param name="tagValue">The string value to match against the Tag property of ComboBoxItems.</param>
        private void SetComboBoxByTag(ComboBox comboBox, string tagValue)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tagValue)
                {
                    comboBox.SelectedItem = item;
                    return; // Exit once the item is found and selected.
                }
            }
        }

        /// <summary>
        /// Retrieves the string value from the <see cref="ComboBoxItem.Tag"/> property
        /// of the currently selected item in a ComboBox.
        /// </summary>
        /// <param name="comboBox">The ComboBox control to query.</param>
        /// <returns>The string value of the selected item's Tag, or null if no item is selected or Tag is null.</returns>
        private string GetComboBoxTagValue(ComboBox comboBox)
        {
            // Safely casts the selected item to ComboBoxItem and then accesses its Tag property.
            return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        }

        #endregion

        #region Window Events

        /// <summary>
        /// Overrides the OnClosing event handler for the window.
        /// This method is called when the window is about to close. It checks for unsaved changes
        /// and prompts the user to save them before allowing the window to close.
        /// It also unsubscribes from system events to prevent memory leaks.
        /// </summary>
        /// <param name="e">Event arguments, which can be used to cancel the closing operation.</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Checks if there are unsaved changes.
            if (_hasUnsavedChanges)
            {
                var result = sysInteract.ShowConfirmationWithCancel(
                    "Unsaved Changes",
                    "You have unsaved changes. Do you want to save them before closing?");

                // If user chooses to save and save fails, cancel the window closing.
                if (result == MessageBoxResult.Yes)
                {
                    if (!SaveCurrentProfile())
                    {
                        e.Cancel = true; // Prevent window from closing.
                        return;
                    }
                }
                // If user chooses to cancel the closing operation, prevent window from closing.
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true; // Prevent window from closing.
                    return;
                }
            }

            // Unsubscribes from sysState events to prevent the window from holding references
            // after it has been closed, which could lead to memory leaks.
            sysState.ProfilesChanged -= OnProfilesChanged;

            // Calls the base class's OnClosing method to ensure proper WPF window closing behavior.
            base.OnClosing(e);
        }

        #endregion
    }
}
