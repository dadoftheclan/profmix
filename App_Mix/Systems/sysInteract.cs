using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using App_Mix.Windows; // Assuming wndNotification is defined in this namespace.

namespace App_Mix.Systems
{
    /// <summary>
    /// Provides static utility methods for interacting with the user interface,
    /// including showing toast notifications, modal dialogs (confirmations, errors),
    /// and standard file/folder selection dialogs. This class aims to centralize
    /// UI interaction logic for consistent user experience.
    /// </summary>
    public static class sysInteract
    {
        /// <summary>
        /// A static list that keeps track of all currently active toast notification windows.
        /// This allows for managing their positions and ensuring they stack correctly.
        /// </summary>
        private static readonly List<wndNotification> activeNotifications = new List<wndNotification>();

        /// <summary>
        /// A lock object used to synchronize access to the <see cref="activeNotifications"/> list.
        /// This prevents race conditions when multiple threads might try to add or remove notifications simultaneously.
        /// </summary>
        private static readonly object notificationLock = new object();

        #region Toast Notifications

        /// <summary>
        /// Displays an informational toast notification to the user.
        /// These notifications are typically non-critical and provide general feedback.
        /// </summary>
        /// <param name="title">The title text displayed on the notification.</param>
        /// <param name="message">The main message content of the notification.</param>
        /// <param name="duration">The duration (in seconds) for which the notification remains visible.
        /// A value of 0 or less indicates a persistent notification that does not auto-dismiss.</param>
        public static void ShowInfo(string title, string message, double duration = 5.0)
        {
            ShowToastNotification(title, message, wndNotification.NotificationType.Info, duration);
        }

        /// <summary>
        /// Displays a success toast notification to the user.
        /// Used to indicate that an operation has completed successfully.
        /// </summary>
        /// <param name="title">The title text displayed on the notification.</param>
        /// <param name="message">The main message content of the notification.</param>
        /// <param name="duration">The duration (in seconds) for which the notification remains visible.</param>
        public static void ShowSuccess(string title, string message, double duration = 5.0)
        {
            ShowToastNotification(title, message, wndNotification.NotificationType.Success, duration);
        }

        /// <summary>
        /// Displays a warning toast notification to the user.
        /// Used for non-critical issues or situations that require user attention but don't stop execution.
        /// </summary>
        /// <param name="title">The title text displayed on the notification.</param>
        /// <param name="message">The main message content of the notification.</param>
        /// <param name="duration">The duration (in seconds) for which the notification remains visible.</param>
        public static void ShowWarning(string title, string message, double duration = 7.0)
        {
            ShowToastNotification(title, message, wndNotification.NotificationType.Warning, duration);
        }

        /// <summary>
        /// Displays an error toast notification to the user.
        /// Used for critical issues or failures that occurred during an operation.
        /// </summary>
        /// <param name="title">The title text displayed on the notification.</param>
        /// <param name="message">The main message content of the notification.</param>
        /// <param name="duration">The duration (in seconds) for which the notification remains visible.</param>
        public static void ShowError(string title, string message, double duration = 10.0)
        {
            ShowToastNotification(title, message, wndNotification.NotificationType.Error, duration);
        }

        /// <summary>
        /// Displays a persistent toast notification that does not automatically dismiss.
        /// These notifications remain on screen until explicitly closed by the user or code.
        /// </summary>
        /// <param name="title">The title text displayed on the notification.</param>
        /// <param name="message">The main message content of the notification.</param>
        /// <param name="type">The type of notification (e.g., Info, Success, Warning, Error),
        /// which influences its visual style.</param>
        public static void ShowPersistent(string title, string message, wndNotification.NotificationType type = wndNotification.NotificationType.Info)
        {
            // A duration of 0 indicates a persistent notification.
            ShowToastNotification(title, message, type, 0);
        }

        /// <summary>
        /// Displays a progress toast notification that can be updated or closed programmatically.
        /// This is useful for long-running operations where continuous feedback is needed.
        /// </summary>
        /// <param name="title">The title text displayed on the notification.</param>
        /// <param name="message">The initial message content of the notification.</param>
        /// <returns>The <see cref="wndNotification"/> instance, allowing for subsequent updates or closure.</returns>
        public static wndNotification ShowProgress(string title, string message)
        {
            // A duration of 0 makes it persistent, and returnInstance ensures we get the object back.
            return ShowToastNotification(title, message, wndNotification.NotificationType.Info, 0, true);
        }

        /// <summary>
        /// Internal helper method to create and display a toast notification.
        /// Ensures the notification is created and shown on the UI thread.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message of the notification.</param>
        /// <param name="type">The type of notification.</param>
        /// <param name="duration">The duration for which the notification should be visible.</param>
        /// <param name="returnInstance">If true, returns the created <see cref="wndNotification"/> instance.</param>
        /// <returns>The created <see cref="wndNotification"/> instance if <paramref name="returnInstance"/> is true; otherwise, null.</returns>
        private static wndNotification ShowToastNotification(string title, string message, wndNotification.NotificationType type, double duration, bool returnInstance = false)
        {
            wndNotification notification = null;

            // Check if the current thread has access to the UI dispatcher.
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                // If on UI thread, create and show directly.
                notification = CreateAndShowNotification(title, message, type, duration);
            }
            else
            {
                // If not on UI thread, invoke the operation on the UI thread's dispatcher.
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    notification = CreateAndShowNotification(title, message, type, duration);
                });
            }

            // Return the notification instance if requested, otherwise null.
            return returnInstance ? notification : null;
        }

        /// <summary>
        /// Creates a new <see cref="wndNotification"/> instance, adds it to the tracking list,
        /// positions it on the screen, and sets up event handlers for its closure.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message of the notification.</param>
        /// <param name="type">The type of notification.</param>
        /// <param name="duration">The duration for which the notification should be visible.</param>
        /// <returns>The newly created <see cref="wndNotification"/> instance.</returns>
        private static wndNotification CreateAndShowNotification(string title, string message, wndNotification.NotificationType type, double duration)
        {
            var notification = new wndNotification(title, message, type, duration);

            // Add the new notification to the list of active notifications within a lock
            // to ensure thread safety before positioning.
            lock (notificationLock)
            {
                activeNotifications.Add(notification);
            }

            // Position the notification on the screen relative to other active notifications.
            PositionNotification(notification);

            // Subscribe to the notification's Closed event. When a notification closes,
            // it should be removed from the tracking list, and remaining notifications should be repositioned.
            notification.Closed += (s, e) =>
            {
                lock (notificationLock)
                {
                    activeNotifications.Remove(notification);
                }
                // Reposition all remaining notifications to fill the gap or stack correctly.
                RepositionAllNotifications();
            };

            // Display the notification window.
            notification.ShowNotification();
            return notification;
        }

        /// <summary>
        /// Positions a given toast notification on the screen, stacking it above other active notifications.
        /// Notifications are positioned in the bottom-right corner of the working area.
        /// </summary>
        /// <param name="notification">The <see cref="wndNotification"/> to position.</param>
        private static void PositionNotification(wndNotification notification)
        {
            // Get the dimensions of the primary screen's working area (excluding taskbar).
            var workingArea = SystemParameters.WorkArea;
            var screenWidth = workingArea.Width;
            var screenHeight = workingArea.Height;
            var rightMargin = 20.0; // Margin from the right edge of the screen.
            var bottomMargin = 20.0; // Margin from the bottom edge of the screen.
            var notificationSpacing = 10.0; // Vertical spacing between stacked notifications.

            // Set the horizontal position: right-aligned with a specified margin.
            notification.Left = screenWidth - notification.Width - rightMargin;

            // Calculate the vertical position based on the height of existing notifications below it.
            lock (notificationLock)
            {
                var totalHeight = bottomMargin; // Start with the bottom margin.

                // Find the index of the current notification in the list.
                var notificationIndex = activeNotifications.IndexOf(notification);
                // Iterate through notifications that are positioned *below* the current one (higher index).
                for (int i = activeNotifications.Count - 1; i > notificationIndex; i--)
                {
                    var existingNotification = activeNotifications[i];
                    // If the existing notification is loaded, use its actual height; otherwise, use an estimated height.
                    if (existingNotification.IsLoaded)
                    {
                        totalHeight += existingNotification.Height + notificationSpacing;
                    }
                    else
                    {
                        // Use an estimated height if the notification hasn't fully rendered yet.
                        totalHeight += 80.0 + notificationSpacing; // Estimated notification height.
                    }
                }

                // Calculate the new top position for the current notification.
                // It's positioned above the `totalHeight` accumulated from notifications below it.
                var newTop = screenHeight - totalHeight - (notification.IsLoaded ? notification.Height : 80.0);
                // Ensure the notification doesn't go too high on the screen (e.g., not above 50 pixels from top).
                notification.Top = Math.Max(50, newTop);
            }
        }

        /// <summary>
        /// Repositions all currently active toast notifications. This method is called
        /// when a notification closes to ensure the remaining notifications slide down
        /// and maintain proper spacing.
        /// </summary>
        private static void RepositionAllNotifications()
        {
            lock (notificationLock)
            {
                // Get screen dimensions and margins, similar to PositionNotification.
                var workingArea = SystemParameters.WorkArea;
                var screenWidth = workingArea.Width;
                var screenHeight = workingArea.Height;
                var rightMargin = 20.0;
                var bottomMargin = 20.0;
                var notificationSpacing = 10.0;

                // Start positioning from the bottom of the screen, moving upwards.
                var currentBottom = screenHeight - bottomMargin;

                // Iterate through notifications from the last (bottom-most) to the first (top-most).
                for (int i = activeNotifications.Count - 1; i >= 0; i--)
                {
                    var notification = activeNotifications[i];
                    // Only reposition visible notifications.
                    if (notification != null && notification.IsVisible)
                    {
                        // Set horizontal position (right-aligned).
                        notification.Left = screenWidth - notification.Width - rightMargin;

                        // Calculate vertical position. Use actual height if loaded, otherwise an estimate.
                        var notificationHeight = notification.IsLoaded ? notification.Height : 80.0;
                        var newTop = currentBottom - notificationHeight;

                        // Ensure the notification doesn't go above the top of the screen.
                        newTop = Math.Max(50, newTop);

                        // Animate the notification to its new position for a smoother visual effect.
                        AnimateNotificationPosition(notification, newTop);

                        // Update the 'currentBottom' for the next notification to be positioned above this one.
                        currentBottom = newTop - notificationSpacing;
                    }
                }
            }
        }

        /// <summary>
        /// Animates the vertical position of a toast notification to a new target 'Top' value.
        /// Uses a simple WPF DoubleAnimation for smooth movement.
        /// </summary>
        /// <param name="notification">The <see cref="wndNotification"/> to animate.</param>
        /// <param name="newTop">The target 'Top' coordinate for the notification.</param>
        private static void AnimateNotificationPosition(wndNotification notification, double newTop)
        {
            // Only animate if the change in position is significant to avoid unnecessary animations.
            if (Math.Abs(notification.Top - newTop) > 5)
            {
                // Create a DoubleAnimation to smoothly transition the 'Top' property.
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = notification.Top, // Starting position.
                    To = newTop, // Target position.
                    Duration = TimeSpan.FromMilliseconds(200), // Duration of the animation.
                    // Add an easing function for a more natural, decelerating movement.
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                // Start the animation on the Window.TopProperty.
                notification.BeginAnimation(Window.TopProperty, animation);
            }
            else
            {
                // If the change is small, just set the position directly without animation.
                notification.Top = newTop;
            }
        }

        /// <summary>
        /// Closes all currently active toast notifications that are being tracked.
        /// </summary>
        public static void CloseAllNotifications()
        {
            lock (notificationLock)
            {
                // Create a copy of the list to avoid modification during iteration.
                var notifications = activeNotifications.ToList();
                foreach (var notification in notifications)
                {
                    // Call the CloseNotification method on each active notification.
                    notification.CloseNotification();
                }
                activeNotifications.Clear(); // Clear the tracking list after all are closed.
            }
        }

        #endregion

        #region Modal Dialogs

        /// <summary>
        /// Displays a modal confirmation dialog with "Yes" and "No" buttons.
        /// This dialog blocks interaction with the parent window until a choice is made.
        /// </summary>
        /// <param name="title">The title of the confirmation dialog.</param>
        /// <param name="message">The message content of the dialog.</param>
        /// <param name="yesText">Optional: The text for the "Yes" button (default is "Yes").</param>
        /// <param name="noText">Optional: The text for the "No" button (default is "No").</param>
        /// <returns><c>true</c> if the user clicks "Yes"; otherwise, <c>false</c>.</returns>
        public static bool ShowConfirmation(string title, string message, string yesText = "Yes", string noText = "No")
        {
            // Uses the standard WPF MessageBox for a simple confirmation.
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Displays a modal confirmation dialog with "Yes", "No", and "Cancel" buttons.
        /// </summary>
        /// <param name="title">The title of the confirmation dialog.</param>
        /// <param name="message">The message content of the dialog.</param>
        /// <param name="yesText">Optional: The text for the "Yes" button (default is "Yes").</param>
        /// <param name="noText">Optional: The text for the "No" button (default is "No").</param>
        /// <param name="cancelText">Optional: The text for the "Cancel" button (default is "Cancel").</param>
        /// <returns>A <see cref="MessageBoxResult"/> indicating which button the user clicked (Yes, No, or Cancel).</returns>
        public static MessageBoxResult ShowConfirmationWithCancel(string title, string message, string yesText = "Yes", string noText = "No", string cancelText = "Cancel")
        {
            // Uses the standard WPF MessageBox with YesNoCancel buttons.
            return MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        }

        /// <summary>
        /// Displays a modal information dialog with an "OK" button.
        /// </summary>
        /// <param name="title">The title of the information dialog.</param>
        /// <param name="message">The message content of the dialog.</param>
        public static void ShowInformation(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Displays a modal warning dialog with an "OK" button.
        /// </summary>
        /// <param name="title">The title of the warning dialog.</param>
        /// <param name="message">The message content of the dialog.</param>
        public static void ShowWarningDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Displays a modal error dialog with an "OK" button.
        /// </summary>
        /// <param name="title">The title of the error dialog.</param>
        /// <param name="message">The message content of the dialog.</param>
        public static void ShowErrorDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// A generic method to display a modal dialog with customizable buttons and icon.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The message content of the dialog.</param>
        /// <param name="buttons">The <see cref="MessageBoxButton"/> enumeration specifying the buttons to display (default is OK).</param>
        /// <param name="icon">The <see cref="MessageBoxImage"/> enumeration specifying the icon to display (default is Information).</param>
        /// <returns>A <see cref="MessageBoxResult"/> indicating which button the user clicked.</returns>
        public static MessageBoxResult ShowDialog(string title, string message, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            return MessageBox.Show(message, title, buttons, icon);
        }

        #endregion

        #region File Dialogs

        /// <summary>
        /// Displays a standard "Open File" dialog, allowing the user to select one or more files.
        /// </summary>
        /// <param name="title">The title of the file dialog (default is "Open File").</param>
        /// <param name="filter">A string specifying the file filter (e.g., "Text Files (*.txt)|*.txt|All Files (*.*)|*.*").</param>
        /// <param name="initialDirectory">Optional: The initial directory to open the dialog in.</param>
        /// <returns>The full path of the selected file if the user clicks "Open"; otherwise, <c>null</c>.</returns>
        public static string ShowOpenFileDialog(string title = "Open File", string filter = "All Files (*.*)|*.*", string initialDirectory = null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter
            };

            // Set the initial directory if provided.
            if (!string.IsNullOrEmpty(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            // Show the dialog and return the selected file name if the result is OK.
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// Displays a standard "Save File" dialog, allowing the user to specify a file name and location to save.
        /// </summary>
        /// <param name="title">The title of the save dialog (default is "Save File").</param>
        /// <param name="filter">A string specifying the file filter (e.g., "WAV Files (*.wav)|*.wav").</param>
        /// <param name="defaultExt">Optional: The default file extension (e.g., ".wav").</param>
        /// <param name="initialDirectory">Optional: The initial directory to open the dialog in.</param>
        /// <returns>The full path where the file should be saved if the user clicks "Save"; otherwise, <c>null</c>.</returns>
        public static string ShowSaveFileDialog(string title = "Save File", string filter = "All Files (*.*)|*.*", string defaultExt = null, string initialDirectory = null)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = filter
            };

            // Set the default file extension if provided.
            if (!string.IsNullOrEmpty(defaultExt))
                dialog.DefaultExt = defaultExt;

            // Set the initial directory if provided.
            if (!string.IsNullOrEmpty(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            // Show the dialog and return the selected file name if the result is OK.
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// Displays a standard "Browse For Folder" dialog, allowing the user to select a directory.
        /// Note: This uses System.Windows.Forms.FolderBrowserDialog, which requires a reference to System.Windows.Forms.
        /// </summary>
        /// <param name="title">The description text displayed at the top of the dialog.</param>
        /// <returns>The path of the selected folder if the user clicks "OK"; otherwise, <c>null</c>.</returns>
        public static string ShowFolderDialog(string title = "Select Folder")
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = title,
                ShowNewFolderButton = true // Allows the user to create a new folder.
            };

            // Show the dialog and return the selected path if the result is OK.
            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Shows a generic success toast notification for common operations.
        /// </summary>
        /// <param name="operation">A string describing the operation that completed successfully (e.g., "Save", "Load").</param>
        public static void NotifySuccess(string operation)
        {
            ShowSuccess("Success", $"{operation} completed successfully!");
        }

        /// <summary>
        /// Shows a generic error toast notification for common operations.
        /// </summary>
        /// <param name="operation">A string describing the operation that failed (e.g., "Save", "Process").</param>
        /// <param name="error">Optional: A specific error message to display (default is a generic failure message).</param>
        public static void NotifyError(string operation, string error = null)
        {
            var message = string.IsNullOrEmpty(error)
                ? $"{operation} failed."
                : $"{operation} failed: {error}";
            ShowError("Error", message);
        }

        /// <summary>
        /// Shows a generic warning toast notification.
        /// </summary>
        /// <param name="message">The warning message to display.</param>
        public static void NotifyWarning(string message)
        {
            ShowWarning("Warning", message);
        }

        /// <summary>
        /// Shows a specific success toast notification for when an audio profile is saved.
        /// </summary>
        /// <param name="profileName">The name of the profile that was saved.</param>
        public static void NotifyProfileSaved(string profileName)
        {
            ShowSuccess("Profile Saved", $"Audio profile '{profileName}' saved successfully!");
        }

        /// <summary>
        /// Shows a specific success toast notification when audio mixing is complete.
        /// Provides details about the output file, format, and size.
        /// </summary>
        /// <param name="fileName">The name of the mixed audio file.</param>
        /// <param name="format">The audio format of the mixed file (e.g., "8kHz, 16-bit, Mono").</param>
        /// <param name="fileSizeMB">The size of the mixed file in megabytes.</param>
        public static void NotifyMixingComplete(string fileName, string format, long fileSizeMB)
        {
            ShowSuccess("Mixing Complete",
                $"Audio mixed successfully!\n" +
                $"File: {fileName}\n" +
                $"Format: {format}\n" +
                $"Size: {fileSizeMB}MB");
        }

        /// <summary>
        /// Shows a warning toast notification specifically for validation errors.
        /// </summary>
        /// <param name="field">The name of the field or property that failed validation.</param>
        /// <param name="error">The specific error message for the validation failure.</param>
        public static void NotifyValidationError(string field, string error)
        {
            ShowWarning("Validation Error", $"{field}: {error}");
        }

        /// <summary>
        /// Shows a toast notification for file operations, indicating success or failure.
        /// </summary>
        /// <param name="operation">A description of the file operation (e.g., "File Open", "File Save").</param>
        /// <param name="success">A boolean indicating whether the operation was successful.</param>
        /// <param name="fileName">Optional: The name of the file involved in the operation.</param>
        /// <param name="error">Optional: An error message if the operation failed.</param>
        public static void NotifyFileOperation(string operation, bool success, string fileName = null, string error = null)
        {
            if (success)
            {
                var message = string.IsNullOrEmpty(fileName)
                    ? $"{operation} completed successfully!"
                    : $"{operation} completed successfully!\n{fileName}";
                ShowSuccess(operation, message);
            }
            else
            {
                var message = string.IsNullOrEmpty(error)
                    ? $"{operation} failed."
                    : $"{operation} failed: {error}";
                ShowError(operation, message);
            }
        }

        #endregion
    }
}
