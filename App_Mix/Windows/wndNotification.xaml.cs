using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation; // For Storyboard and DoubleAnimation.
using System.Windows.Threading; // For DispatcherTimer.

namespace App_Mix.Windows
{
    /// <summary>
    /// Interaction logic for wndNotification.xaml. This window serves as a customizable
    /// toast notification system, displaying transient messages (info, success, warning, error)
    /// with optional auto-dismissal and a progress bar. It supports different visual styles
    /// based on the notification type and includes animations for a smooth user experience.
    /// </summary>
    public partial class wndNotification : Window
    {
        /// <summary>
        /// A timer responsible for automatically closing the notification after a specified duration.
        /// </summary>
        private DispatcherTimer autoCloseTimer;

        /// <summary>
        /// A timer used to update the progress bar, providing visual feedback on the remaining display time.
        /// </summary>
        private DispatcherTimer progressTimer;

        /// <summary>
        /// The Storyboard for the slide-in animation when the notification appears.
        /// </summary>
        private Storyboard slideInStoryboard;

        /// <summary>
        /// The Storyboard for the slide-out animation when the notification is closing.
        /// </summary>
        private Storyboard slideOutStoryboard;

        /// <summary>
        /// The total duration (in seconds) for which the notification should be displayed.
        /// A value of 0 indicates a persistent notification.
        /// </summary>
        private double totalDuration = 5.0; // Default duration of 5 seconds.

        /// <summary>
        /// Tracks the remaining time (in seconds) until the notification automatically closes.
        /// Used for the progress bar and pausing/resuming on mouse interaction.
        /// </summary>
        private double remainingTime;

        /// <summary>
        /// Defines the different types of notifications, each potentially having a distinct visual style.
        /// </summary>
        public enum NotificationType
        {
            /// <summary>
            /// General informational message.
            /// </summary>
            Info,
            /// <summary>
            /// Indicates a successful operation.
            /// </summary>
            Success,
            /// <summary>
            /// Warns the user about a non-critical issue.
            /// </summary>
            Warning,
            /// <summary>
            /// Indicates an error or failure.
            /// </summary>
            Error
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="wndNotification"/> class.
        /// This default constructor is typically called by the parameterized constructor.
        /// It sets up animations and timers.
        /// </summary>
        public wndNotification()
        {
            InitializeComponent(); // Initializes the WPF UI components defined in XAML.
            SetupAnimations(); // Configures the slide-in and slide-out animations.
            SetupTimers(); // Initializes the auto-close and progress update timers.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="wndNotification"/> class with specific content and type.
        /// This is the primary constructor used to create and configure a notification.
        /// </summary>
        /// <param name="title">The title text of the notification.</param>
        /// <param name="message">The main message content of the notification.</param>
        /// <param name="type">The <see cref="NotificationType"/> (e.g., Info, Success) for styling.</param>
        /// <param name="duration">The duration (in seconds) for which the notification should be displayed.
        /// A value of 0 makes it persistent.</param>
        public wndNotification(string title, string message, NotificationType type = NotificationType.Info, double duration = 5.0) : this()
        {
            // Calls the default constructor first, then sets the specific notification properties.
            SetNotification(title, message, type, duration);
        }

        /// <summary>
        /// Configures the slide-in and slide-out animations for the notification window.
        /// It retrieves the Storyboard resources defined in XAML and attaches a completion
        /// handler for the slide-out animation to close the window.
        /// </summary>
        private void SetupAnimations()
        {
            // Find the Storyboard resources defined in the window's XAML.
            slideInStoryboard = (Storyboard)FindResource("SlideInAnimation");
            slideOutStoryboard = (Storyboard)FindResource("SlideOutAnimation");

            // Attach an event handler to the completion of the slide-out animation.
            // When the slide-out animation finishes, the window will close.
            slideOutStoryboard.Completed += (s, e) => this.Close();
        }

        /// <summary>
        /// Initializes the <see cref="DispatcherTimer"/> instances used for auto-closing
        /// and updating the progress bar.
        /// </summary>
        private void SetupTimers()
        {
            // Initialize the timer for auto-closing the notification.
            autoCloseTimer = new DispatcherTimer();
            autoCloseTimer.Tick += AutoCloseTimer_Tick; // Assign the tick event handler.

            // Initialize the timer for updating the progress bar.
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromMilliseconds(50); // Update frequency (every 50ms for smoothness).
            progressTimer.Tick += ProgressTimer_Tick; // Assign the tick event handler.
        }

        /// <summary>
        /// Sets the content, type, and duration of the notification.
        /// This method updates the UI elements and starts the auto-close and progress timers
        /// if the notification is not persistent.
        /// </summary>
        /// <param name="title">The title text.</param>
        /// <param name="message">The message text.</param>
        /// <param name="type">The <see cref="NotificationType"/>.</param>
        /// <param name="duration">The duration in seconds (0 for persistent).</param>
        public void SetNotification(string title, string message, NotificationType type, double duration = 5.0)
        {
            TitleText.Text = title; // Set the title TextBlock content.
            MessageText.Text = message; // Set the message TextBlock content.
            totalDuration = duration; // Store the total duration.
            remainingTime = duration; // Initialize remaining time.

            SetNotificationStyle(type); // Apply visual styling based on notification type.

            // If duration is greater than 0, it's an auto-dismissing notification.
            if (duration > 0)
            {
                autoCloseTimer.Interval = TimeSpan.FromSeconds(duration); // Set the auto-close interval.
                autoCloseTimer.Start(); // Start the auto-close timer.
                progressTimer.Start(); // Start the progress bar update timer.
            }
            else
            {
                // For persistent notifications (duration <= 0), hide the progress bar.
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Applies specific visual styling (background color, icon, progress bar color)
        /// to the notification based on its <see cref="NotificationType"/>.
        /// </summary>
        /// <param name="type">The type of notification.</param>
        private void SetNotificationStyle(NotificationType type)
        {
            // Use a switch statement to apply different styles for each notification type.
            switch (type)
            {
                case NotificationType.Success:
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(80, 200, 120)); // Green background for icon.
                    IconText.Text = "✓"; // Checkmark icon.
                    ProgressBar.Background = new SolidColorBrush(Color.FromRgb(80, 200, 120)); // Matching progress bar color.
                    break;

                case NotificationType.Warning:
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(255, 107, 53)); // Orange background.
                    IconText.Text = "⚠"; // Warning sign icon.
                    ProgressBar.Background = new SolidColorBrush(Color.FromRgb(255, 107, 53));
                    break;

                case NotificationType.Error:
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(229, 62, 62)); // Red background.
                    IconText.Text = "✕"; // Cross icon.
                    ProgressBar.Background = new SolidColorBrush(Color.FromRgb(229, 62, 62));
                    break;

                case NotificationType.Info:
                default: // Default to Info type if no specific type is matched.
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(74, 144, 226)); // Blue background.
                    IconText.Text = "ℹ"; // Information icon.
                    ProgressBar.Background = new SolidColorBrush(Color.FromRgb(74, 144, 226));
                    break;
            }
        }

        /// <summary>
        /// Displays the notification window.
        /// It first positions the window off-screen and then starts the slide-in animation.
        /// </summary>
        public void ShowNotification()
        {
            PositionWindow(); // Set the initial off-screen position.
            this.Show(); // Make the window visible.
            slideInStoryboard.Begin(this); // Start the slide-in animation.
        }

        /// <summary>
        /// Positions the notification window on the screen, typically in the bottom-right corner.
        /// It also sets up the 'From' and 'To' values for the slide-in and slide-out animations
        /// based on the screen dimensions.
        /// </summary>
        private void PositionWindow()
        {
            // Get the dimensions of the screen's working area (excluding taskbar).
            var workingArea = SystemParameters.WorkArea;
            var screenWidth = workingArea.Width;
            var screenHeight = workingArea.Height;

            // Calculate the target position for the notification (bottom-right with 20px margin).
            var targetLeft = screenWidth - this.Width - 20;
            var targetTop = screenHeight - this.Height - 20;

            // Store the initial off-screen position (right of the screen) in the window's Tag property.
            // This is the 'From' value for the slide-in animation.
            this.Tag = screenWidth;
            this.Left = (double)this.Tag; // Set the window's initial Left position.
            this.Top = targetTop; // Set the window's Top position.

            // Update the 'To' property of the slide-in animation to the calculated targetLeft.
            var slideInAnimation = slideInStoryboard.Children[0] as DoubleAnimation;
            if (slideInAnimation != null)
            {
                slideInAnimation.To = targetLeft;
            }

            // Update the 'To' property of the slide-out animation to move it off-screen to the right.
            var slideOutAnimation = slideOutStoryboard.Children[0] as DoubleAnimation;
            if (slideOutAnimation != null)
            {
                slideOutAnimation.To = screenWidth;
            }
        }

        /// <summary>
        /// Initiates the closing sequence for the notification.
        /// It stops the timers and starts the slide-out animation.
        /// </summary>
        public void CloseNotification()
        {
            StopTimers(); // Stop all active timers.
            slideOutStoryboard.Begin(this); // Start the slide-out animation.
        }

        /// <summary>
        /// Stops both the auto-close timer and the progress update timer.
        /// </summary>
        private void StopTimers()
        {
            autoCloseTimer?.Stop(); // Safely stop the auto-close timer if it's active.
            progressTimer?.Stop(); // Safely stop the progress timer if it's active.
        }

        /// <summary>
        /// Event handler for the <see cref="autoCloseTimer"/>'s Tick event.
        /// When triggered, it means the auto-close duration has expired, so the notification is closed.
        /// </summary>
        /// <param name="sender">The source of the event (autoCloseTimer).</param>
        /// <param name="e">Event arguments.</param>
        private void AutoCloseTimer_Tick(object sender, EventArgs e)
        {
            CloseNotification(); // Close the notification.
        }

        /// <summary>
        /// Event handler for the <see cref="progressTimer"/>'s Tick event.
        /// This method is called repeatedly to update the width of the progress bar,
        /// visually representing the remaining time until the notification closes.
        /// </summary>
        /// <param name="sender">The source of the event (progressTimer).</param>
        /// <param name="e">Event arguments.</param>
        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            remainingTime -= 0.05; // Decrease remaining time by the timer interval (50ms).

            if (remainingTime <= 0)
            {
                ProgressBar.Width = 0; // Set width to 0 when time runs out.
                progressTimer.Stop(); // Stop the progress timer.
            }
            else
            {
                // Calculate the percentage of time remaining and set the progress bar width accordingly.
                var progressPercentage = remainingTime / totalDuration;
                ProgressBar.Width = this.ActualWidth * progressPercentage;
            }
        }

        /// <summary>
        /// Event handler for the Window's Loaded event.
        /// This is called once the window has been loaded and rendered.
        /// It initializes the progress bar's width to the full width of the notification.
        /// </summary>
        /// <param name="sender">The source of the event (the Window itself).</param>
        /// <param name="e">Event arguments.</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the initial width of the progress bar to the full width of the notification window.
            ProgressBar.Width = this.ActualWidth;
        }

        /// <summary>
        /// Event handler for the close button within the notification's UI.
        /// Allows the user to manually close the notification.
        /// </summary>
        /// <param name="sender">The source of the event (CloseButton).</param>
        /// <param name="e">Event arguments.</param>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseNotification(); // Close the notification.
        }

        /// <summary>
        /// Overrides the <see cref="Window.OnMouseEnter"/> method.
        /// When the mouse cursor enters the notification window, the auto-close and progress timers are paused.
        /// This allows the user more time to read the message.
        /// </summary>
        /// <param name="e">Mouse event arguments.</param>
        protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
        {
            autoCloseTimer?.Stop(); // Pause the auto-close timer.
            progressTimer?.Stop(); // Pause the progress timer.
            base.OnMouseEnter(e); // Call the base class method.
        }

        /// <summary>
        /// Overrides the <see cref="Window.OnMouseLeave"/> method.
        /// When the mouse cursor leaves the notification window, the auto-close and progress timers are resumed,
        /// provided there is still remaining time.
        /// </summary>
        /// <param name="e">Mouse event arguments.</param>
        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            // Only resume if there is still time remaining for auto-closure.
            if (remainingTime > 0)
            {
                autoCloseTimer?.Start(); // Resume the auto-close timer.
                progressTimer?.Start(); // Resume the progress timer.
            }
            base.OnMouseLeave(e); // Call the base class method.
        }

        /// <summary>
        /// Overrides the <see cref="Window.OnClosed"/> method.
        /// This is called when the window is completely closed. It ensures that all timers are stopped
        /// and their references are nulled out to prevent memory leaks.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected override void OnClosed(EventArgs e)
        {
            StopTimers(); // Stop all timers.
            autoCloseTimer = null; // Nullify timer references.
            progressTimer = null;
            base.OnClosed(e); // Call the base class method.
        }

        // The following static helper methods provide a convenient way to show different types of notifications
        // without needing to instantiate wndNotification directly. They are typically called from other parts of the application.

        /// <summary>
        /// Static helper method to display an informational toast notification.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message content.</param>
        /// <param name="duration">The display duration in seconds.</param>
        public static void ShowInfo(string title, string message, double duration = 5.0)
        {
            var notification = new wndNotification(title, message, NotificationType.Info, duration);
            notification.ShowNotification();
        }

        /// <summary>
        /// Static helper method to display a success toast notification.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message content.</param>
        /// <param name="duration">The display duration in seconds.</param>
        public static void ShowSuccess(string title, string message, double duration = 5.0)
        {
            var notification = new wndNotification(title, message, NotificationType.Success, duration);
            notification.ShowNotification();
        }

        /// <summary>
        /// Static helper method to display a warning toast notification.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message content.</param>
        /// <param name="duration">The display duration in seconds.</param>
        public static void ShowWarning(string title, string message, double duration = 7.0)
        {
            var notification = new wndNotification(title, message, NotificationType.Warning, duration);
            notification.ShowNotification();
        }

        /// <summary>
        /// Static helper method to display an error toast notification.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message content.</param>
        /// <param name="duration">The display duration in seconds.</param>
        public static void ShowError(string title, string message, double duration = 10.0)
        {
            var notification = new wndNotification(title, message, NotificationType.Error, duration);
            notification.ShowNotification();
        }

        /// <summary>
        /// Static helper method to display a persistent toast notification (does not auto-dismiss).
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message content.</param>
        /// <param name="type">The <see cref="NotificationType"/> for styling.</param>
        public static void ShowPersistent(string title, string message, NotificationType type = NotificationType.Info)
        {
            var notification = new wndNotification(title, message, type, 0); // 0 duration makes it persistent.
            notification.ShowNotification();
        }
    }
}
