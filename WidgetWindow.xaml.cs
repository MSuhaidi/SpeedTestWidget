using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace SpeedTestWidget
{
    public partial class WidgetWindow : Window
    {
        private readonly SecureStorage _secureStorage;
        private DispatcherTimer? _timer;
        private List<SkinInfo> _availableSkins = [];

        public WidgetWindow()
        {
            InitializeComponent();

            // Fade in animation
            this.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
            this.BeginAnimation(Window.OpacityProperty, fadeIn);

            // Ensure database is initialized
            DatabaseHelper.InitDatabase();

            // Initialize secure storage
            _secureStorage = new SecureStorage();

            // Copy default skins to AppData on first run (so users can edit them)
            SkinManager.CopyDefaultSkinsToAppData();

            // Initialize JSON logger (optional, for debugging)
            // JsonMessageLogger.Initialize();

            // Load last test result from secure storage
            LoadLastResult();

            // Start timer to update "time since last test"
            StartTimeSinceTimer();

            // Show app version
            ShowAppInfo();

            // Apply saved skin (default to "Default")
            ApplySkin(LoadSavedSkin());

            // Populate Skin Menu
            RefreshSkins();
        }

        private void LoadLastResult()
        {
            // Try loading from secure storage first
            var secureResult = _secureStorage.LoadResult();

            if (secureResult != null)
            {
                DownloadText.Text = $"{secureResult.DownloadMbps:F2} Mbps";
                UploadText.Text = $"{secureResult.UploadMbps:F2} Mbps";
                DownloadPingText.Text = $"{secureResult.DownloadPingMs:F0} ms";
                UploadPingText.Text = $"{secureResult.UploadPingMs:F0} ms";
                UpdateTimeSince(secureResult.Timestamp);

                TamperWarning.Visibility = secureResult.IsValid ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                // Fallback to database if secure storage is empty
                var lastResult = DatabaseHelper.GetLastResult();

                if (lastResult != null)
                {
                    DownloadText.Text = $"{lastResult.DownloadMbps:F2} Mbps";
                    UploadText.Text = $"{lastResult.UploadMbps:F2} Mbps";
                    DownloadPingText.Text = $"{lastResult.DownloadPingMs:F0} ms";
                    UploadPingText.Text = $"{lastResult.UploadPingMs:F0} ms";
                    UpdateTimeSince(lastResult.Timestamp);
                }
                else
                {
                    DownloadText.Text = "-- Mbps";
                    UploadText.Text = "-- Mbps";
                    DownloadPingText.Text = "-- ms";
                    UploadPingText.Text = "-- ms";
                    TimeSinceText.Text = "No tests yet";
                }
            }
        }

        private void StartTimeSinceTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
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
            // Clean up JSON logger
            // JsonMessageLogger.Close();
        }

        private async void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            StartTestButton.IsEnabled = false;
            contextruntest.IsEnabled = false;
            StartTestButton.Content = "Testing...";

            DownloadText.Text = "…";
            UploadText.Text = "…";
            DownloadPingText.Text = "…";
            UploadPingText.Text = "…";
            TimeSinceText.Text = "Running test...";
            TamperWarning.Visibility = Visibility.Collapsed;

            try
            {
                var result = await Ndt7Client.RunTestAsync(UpdateProgress);

                DownloadText.Text = $"{result.DownloadMbps:F2} Mbps";
                UploadText.Text = $"{result.UploadMbps:F2} Mbps";
                DownloadPingText.Text = $"{result.DownloadPingMs:F0} ms";
                UploadPingText.Text = $"{result.UploadPingMs:F0} ms";
                TimeSinceText.Text = "Last: Just now";

                _secureStorage.SaveResult(result.DownloadMbps, result.UploadMbps,
                    result.DownloadPingMs, result.UploadPingMs,
                    result.Hostname, result.City, result.Country,
                SecureStorage.GetOptions());

                DatabaseHelper.SaveResult(result.DownloadMbps, result.UploadMbps,
                    result.DownloadPingMs, result.UploadPingMs,
                    result.Hostname, result.City, result.Country);
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
                contextruntest.IsEnabled = true;
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

        private void UpdateProgress(string phase, double progress, double? downloadMbps = null,
            double? uploadMbps = null, double? downloadPing = null, double? uploadPing = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (progress > 0)
                    TimeSinceText.Text = $"{phase} {progress:F0}%";
                else
                    TimeSinceText.Text = $"{phase}";

                if (downloadMbps.HasValue)
                    DownloadText.Text = $"{downloadMbps.Value:F2} Mbps";
                if (uploadMbps.HasValue)
                    UploadText.Text = $"{uploadMbps.Value:F2} Mbps";
                if (downloadPing.HasValue)
                    DownloadPingText.Text = $"{downloadPing.Value:F0} ms";
                if (uploadPing.HasValue)
                    UploadPingText.Text = $"{uploadPing.Value:F0} ms";
            });
        }

        private void HandleTestError(string title, string message, Exception ex)
        {
            DownloadText.Text = "Error";
            UploadText.Text = "Error";

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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowAppInfo()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
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

        public void RefreshSkins()
        {
            _availableSkins = SkinManager.DiscoverSkins();
            RebuildSkinMenu();
        }

        private void RebuildSkinMenu()
        {
            if (this.contextMenu == null)
                return;

            var skinMenuItem = this.contextMenu.Items.OfType<MenuItem>()
                .FirstOrDefault(m => m.Header?.ToString() == "Skin");

            if (skinMenuItem == null)
                return;

            skinMenuItem.Items.Clear();

            var currentSkin = LoadSavedSkin();

            // Add skin menu items
            foreach (var skin in _availableSkins)
            {
                var displayName = skin.IsCustom ? $"{skin.Name} ⭐" : skin.Name;
                var item = new MenuItem
                {
                    Header = displayName,
                    Tag = skin.Name,
                    IsCheckable = true,
                    IsChecked = skin.Name.Equals(currentSkin, StringComparison.OrdinalIgnoreCase)
                };

                item.Click += (s, e) =>
                {
                    ApplySkin(skin.Name);

                    foreach (var menuItem in skinMenuItem.Items.OfType<MenuItem>())
                    {
                        if (menuItem.Tag is string tag)
                        {
                            menuItem.IsChecked = tag.Equals(skin.Name, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                };

                skinMenuItem.Items.Add(item);
            }

            skinMenuItem.Items.Add(new Separator());

            var refreshItem = new MenuItem { Header = "🔄 Refresh Skins" };
            refreshItem.Click += (s, e) => RefreshSkins();
            skinMenuItem.Items.Add(refreshItem);

            var openFolderItem = new MenuItem { Header = "📂 Open Skins Folder" };
            openFolderItem.Click += OpenSkinsFolder_Click;
            skinMenuItem.Items.Add(openFolderItem);
        }

        public void ApplySkin(string skinName)
        {
            try
            {
                if (SkinManager.LoadSkin(skinName))
                {
                    if (this.contextMenu?.Items.OfType<MenuItem>()
                        .FirstOrDefault(m => m.Header?.ToString() == "Skin") is MenuItem skinMenuItem)
                    {
                        foreach (var item in skinMenuItem.Items.OfType<MenuItem>().Where(m => m.Tag is string))
                        {
                            item.IsChecked = item.Tag.ToString()!.Equals(skinName, StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    SaveSkinPreference(skinName);
                }
                else
                {
                    MessageBox.Show($"Failed to load skin: {skinName}", "Skin Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load skin: {ex.Message}", "Skin Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void SaveSkinPreference(string skinName)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var prefFile = Path.Combine(appData, "SpeedTest", "skin_preference.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(prefFile)!);
                File.WriteAllText(prefFile, skinName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}");
            }
        }

        private static string LoadSavedSkin()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var prefFile = Path.Combine(appData, "SpeedTest", "skin_preference.txt");
                if (File.Exists(prefFile))
                {
                    var skin = File.ReadAllText(prefFile).Trim();
                    return skin;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load failed: {ex.Message}");
            }

            return "Default";
        }

        private void OpenSkinsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SkinManager.EnsureCustomSkinsFolder();
                var folder = SkinManager.GetCustomSkinsFolder();
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open skins folder: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        public void ClearData_Click(object sender, RoutedEventArgs e)
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
                    DownloadPingText.Text = "-- ms";
                    UploadPingText.Text = "-- ms";
                    TimeSinceText.Text = "No tests yet";
                    TamperWarning.Visibility = Visibility.Collapsed;

                    MessageBox.Show("All data cleared successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clear data: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}