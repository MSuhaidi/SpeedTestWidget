using SpeedTestWidget;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace Speed_Test
{
    public partial class WidgetWindow : Window
    {
        // Create hidden progress bars for the NDT client
        private readonly ProgressBar _downloadProgress;
        private readonly ProgressBar _uploadProgress;
        private readonly SecureStorage _secureStorage;
        private DispatcherTimer? _timer;

        public WidgetWindow()
        {
            InitializeComponent();

            // Fade in animation
            this.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
            this.BeginAnimation(Window.OpacityProperty, fadeIn);

            // Initialize hidden progress bars (not displayed, just for NDT client)
            _downloadProgress = new ProgressBar { Minimum = 0, Maximum = 100 };
            _uploadProgress = new ProgressBar { Minimum = 0, Maximum = 100 };

            // Initialize secure storage
            _secureStorage = new SecureStorage();

            // Load last test result from secure storage
            LoadLastResult();

            // Start timer to update "time since last test"
            StartTimeSinceTimer();

            // Show app version
            ShowAppInfo();

            // Apply saved skin (default to "Default")
            ApplySkin(LoadSavedSkin());
        }

        private void LoadLastResult()
        {
            // Try loading from secure storage first
            var secureResult = _secureStorage.LoadResult();

            if (secureResult != null)
            {
                DownloadText.Text = $"{secureResult.DownloadMbps:F1} Mbps";
                UploadText.Text = $"{secureResult.UploadMbps:F1} Mbps";
                UpdateTimeSince(secureResult.Timestamp);

                // Show tamper warning if data was modified
                TamperWarning.Visibility = secureResult.IsValid ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                // Fallback to database if secure storage is empty
                var lastResult = DatabaseHelper.GetLastResult();

                if (lastResult != null)
                {
                    DownloadText.Text = $"{lastResult.DownloadMbps:F1} Mbps";
                    UploadText.Text = $"{lastResult.UploadMbps:F1} Mbps";
                    UpdateTimeSince(lastResult.Timestamp);
                }
                else
                {
                    DownloadText.Text = "-- Mbps";
                    UploadText.Text = "-- Mbps";
                    TimeSinceText.Text = "No tests yet";
                }
            }
        }

        private void StartTimeSinceTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10) // Update every 10 seconds
            };
            _timer.Tick += (s, e) =>
            {
                var secureResult = _secureStorage.LoadResult();
                if (secureResult != null)
                {
                    UpdateTimeSince(secureResult.Timestamp);
                }
            };
            _timer.Start();
        }

        private void UpdateTimeSince(string timestampStr)
        {
            if (DateTime.TryParse(timestampStr, out DateTime lastTestTime))
            {
                UpdateTimeSince(lastTestTime);
            }
        }

        private void UpdateTimeSince(DateTime lastTestTime)
        {
            var timeSpan = DateTime.Now - lastTestTime;

            string timeSinceText;
            if (timeSpan.TotalMinutes < 1)
                timeSinceText = "Just now";
            else if (timeSpan.TotalMinutes < 60)
                timeSinceText = $"{(int)timeSpan.TotalMinutes}m ago";
            else if (timeSpan.TotalHours < 24)
                timeSinceText = $"{(int)timeSpan.TotalHours}h ago";
            else if (timeSpan.TotalDays < 7)
                timeSinceText = $"{(int)timeSpan.TotalDays}d ago";
            else
                timeSinceText = lastTestTime.ToString("MMM dd");

            TimeSinceText.Text = $"Last: {timeSinceText}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }

        private async void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            StartTestButton.IsEnabled = false;
            StartTestButton.Content = "Testing...";

            DownloadText.Text = "…";
            UploadText.Text = "…";
            TimeSinceText.Text = "Running test...";
            TamperWarning.Visibility = Visibility.Collapsed;

            try
            {
                var (downloadMbps, uploadMbps, hostname, city, country) =
                    await Ndt7Client.RunTestAsync(
                        _downloadProgress,
                        _uploadProgress,
                        UpdateDebugText);

                // Update display
                DownloadText.Text = $"{downloadMbps:F1} Mbps";
                UploadText.Text = $"{uploadMbps:F1} Mbps";
                TimeSinceText.Text = "Last: Just now";

                // Save to secure storage with tamper protection
                _secureStorage.SaveResult(downloadMbps, uploadMbps, hostname, city, country);

                // Also save to database for backward compatibility
                DatabaseHelper.SaveResult(hostname, city, country, downloadMbps, uploadMbps);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                HandleTestError("Network Error", "Unable to reach M-Lab servers. Check your internet connection.", ex);
            }
            catch (System.Net.WebSockets.WebSocketException ex)
            {
                HandleTestError("Connection Error", "WebSocket connection failed. The server may be unavailable.", ex);
            }
            catch (OperationCanceledException ex)
            {
                HandleTestError("Timeout", "Test took too long and was cancelled.", ex);
            }
            catch (System.Text.Json.JsonException ex)
            {
                HandleTestError("Data Error", "Failed to parse server response.", ex);
            }
            catch (Exception ex)
            {
                HandleTestError("Test Error", $"Unexpected error: {ex.Message}", ex);
            }
            finally
            {
                StartTestButton.IsEnabled = true;
                StartTestButton.Content = "Run Test";
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void HandleTestError(string title, string message, Exception ex)
        {
            DownloadText.Text = "Error";
            UploadText.Text = "Error";

            // Update time since with last successful test
            var secureResult = _secureStorage.LoadResult();
            if (secureResult != null)
            {
                UpdateTimeSince(secureResult.Timestamp);
            }
            else
            {
                TimeSinceText.Text = "Test failed";
            }

            MessageBox.Show($"{message}\n\nDetails: {ex.Message}",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void UpdateDebugText(string msg)
        {
            // Extract speed values from debug messages for real-time updates
            if (msg.StartsWith("Download:"))
            {
                var parts = msg.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    DownloadText.Text = $"{parts[1]} Mbps";
                }
            }
            else if (msg.StartsWith("Upload:"))
            {
                var parts = msg.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    UploadText.Text = $"{parts[1]} Mbps";
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowAppInfo()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            AppVersionLink.Inlines.Clear();
            AppVersionLink.Inlines.Add($"v{version}");
        }


        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open link: {ex.Message}");
            }
            e.Handled = true;
        }

        #region Skin Management

        private void ApplySkin(string skinName)
        {
            try
            {
                var skinDict = new ResourceDictionary
                {
                    Source = new Uri($"Skins/{skinName}Skin.xaml", UriKind.Relative)
                };

                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(skinDict);

                // Update menu checks
                DefaultSkinMenuItem.IsChecked = skinName == "Default";
                DarkSkinMenuItem.IsChecked = skinName == "Dark";
                LightSkinMenuItem.IsChecked = skinName == "Light";
                NeonSkinMenuItem.IsChecked = skinName == "Neon";

                // Save preference
                SaveSkinPreference(skinName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load skin: {ex.Message}", "Skin Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DefaultSkin_Click(object sender, RoutedEventArgs e) => ApplySkin("Default");
        private void DarkSkin_Click(object sender, RoutedEventArgs e) => ApplySkin("Dark");
        private void LightSkin_Click(object sender, RoutedEventArgs e) => ApplySkin("Light");
        private void NeonSkin_Click(object sender, RoutedEventArgs e) => ApplySkin("Neon");

        private void SaveSkinPreference(string skinName)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var prefFile = System.IO.Path.Combine(appData, "SpeedTest", "skin_preference.txt");
                System.IO.File.WriteAllText(prefFile, skinName);
            }
            catch { /* Ignore save errors */ }
        }

        private string LoadSavedSkin()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var prefFile = System.IO.Path.Combine(appData, "SpeedTest", "skin_preference.txt");
                if (System.IO.File.Exists(prefFile))
                {
                    return System.IO.File.ReadAllText(prefFile).Trim();
                }
            }
            catch { /* Ignore load errors */ }

            return "Default";
        }

        #endregion

        private void ClearData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all saved speed test data?\n\nThis will remove:\n• Last secure result\n• All test history from database",
                "Confirm Clear Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _secureStorage.ClearAllData();
                    DatabaseHelper.ClearHistory();

                    DownloadText.Text = "-- Mbps";
                    UploadText.Text = "-- Mbps";
                    TimeSinceText.Text = "No tests yet";
                    TamperWarning.Visibility = Visibility.Collapsed;

                    MessageBox.Show("All data cleared successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clear data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}