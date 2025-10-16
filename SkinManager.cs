using System.IO;
using System.Reflection;
using System.Windows;

namespace SpeedTestWidget
{
    public class SkinManager
    {
        // Skins folder next to the executable
        private static readonly string AppSkinsFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Skins");

        // Skins folder in AppData
        private static readonly string AppDataSkinsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpeedTest", "Skins");

        /// <summary>
        /// Discover all available skins from app directory and AppData
        /// </summary>
        public static List<SkinInfo> DiscoverSkins()
        {
            var skins = new List<SkinInfo>();

            // 1. Check Skins folder next to executable
            if (Directory.Exists(AppSkinsFolder))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(AppSkinsFolder, "*.xaml"))
                    {
                        var skinName = GetSkinNameFromFile(file);
                        skins.Add(new SkinInfo
                        {
                            Name = skinName,
                            FilePath = file,
                            IsCustom = false,
                            Location = "App Directory"
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading app skins folder: {ex.Message}");
                }
            }

            // 2. Check AppData skins folder
            if (Directory.Exists(AppDataSkinsFolder))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(AppDataSkinsFolder, "*.xaml"))
                    {
                        var skinName = GetSkinNameFromFile(file);

                        // Don't add duplicate names (app directory takes priority)
                        if (!skins.Any(s => s.Name.Equals(skinName, StringComparison.OrdinalIgnoreCase)))
                        {
                            skins.Add(new SkinInfo
                            {
                                Name = skinName,
                                FilePath = file,
                                IsCustom = true,
                                Location = "AppData"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading AppData skins folder: {ex.Message}");
                }
            }

            return [.. skins.OrderBy(s => s.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(s => s.Name)];
        }

        /// <summary>
        /// Extract skin name from filename (removes "Skin" suffix if present)
        /// </summary>
        private static string GetSkinNameFromFile(string filePath)
        {
            var skinName = Path.GetFileNameWithoutExtension(filePath);
            if (skinName.EndsWith("Skin", StringComparison.OrdinalIgnoreCase) && skinName.Length > 4)
            {
                skinName = skinName[..^4];
            }
            return skinName;
        }

        /// <summary>
        /// Load a skin by name
        /// Falls back to embedded default if skin not found or loading fails
        /// </summary>
        public static bool LoadSkin(string skinName)
        {
            try
            {
                var skins = DiscoverSkins();
                var skin = skins.FirstOrDefault(s => s.Name.Equals(skinName, StringComparison.OrdinalIgnoreCase));

                // If requested skin not found, try to load it anyway (might be embedded default)
                if (skin == null)
                {
                    // Try to load embedded default skin
                    if (skinName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                    {
                        return LoadEmbeddedDefaultSkin();
                    }

                    // Fall back to any available skin
                    skin = skins.FirstOrDefault();
                    if (skin == null)
                    {
                        return LoadEmbeddedDefaultSkin();
                    }
                }

                // Verify file exists
                if (!File.Exists(skin.FilePath))
                {
                    return LoadEmbeddedDefaultSkin();
                }

                // Load the skin
                var skinDict = new ResourceDictionary
                {
                    Source = new Uri(skin.FilePath, UriKind.Absolute)
                };

                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(skinDict);

                return true;
            }
            catch (Exception)
            {
                return LoadEmbeddedDefaultSkin();
            }
        }

        /// <summary>
        /// Load the embedded default skin as fallback
        /// </summary>
        private static bool LoadEmbeddedDefaultSkin()
        {
            try
            {
                // Try to load from embedded resource
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(r => r.EndsWith("DefaultSkin.xaml", StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        MessageBox.Show("Loading embedded default skin");
                        var skinDict = (ResourceDictionary)System.Windows.Markup.XamlReader.Load(stream);

                        Application.Current.Resources.MergedDictionaries.Clear();
                        Application.Current.Resources.MergedDictionaries.Add(skinDict);

                        return true;
                    }
                }

                // If embedded resource not found, create a minimal default skin
                var defaultDict = CreateMinimalDefaultSkin();
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(defaultDict);
                return true;
            }
            catch (Exception)
            {
                // Last resort: create minimal skin in code
                try
                {
                    var defaultDict = CreateMinimalDefaultSkin();
                    Application.Current.Resources.MergedDictionaries.Clear();
                    Application.Current.Resources.MergedDictionaries.Add(defaultDict);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Create a minimal default skin programmatically
        /// </summary>
        private static ResourceDictionary CreateMinimalDefaultSkin()
        {
            var dict = new ResourceDictionary();

            // Widget Background
            var bgBrush = new System.Windows.Media.LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            bgBrush.GradientStops.Add(new System.Windows.Media.GradientStop(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"), 0));
            bgBrush.GradientStops.Add(new System.Windows.Media.GradientStop(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D30"), 1));
            dict["WidgetBackground"] = bgBrush;

            // Other brushes
            dict["WidgetBorder"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3F3F46"));
            dict["TitleBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E0E0E0"));
            dict["DownloadBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EC9B0"));
            dict["UploadBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CE9178"));
            dict["PingBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DCDCAA"));
            dict["SubtextBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#808080"));

            return dict;
        }

        /// <summary>
        /// Copy default skins from app directory to AppData on first run
        /// This populates AppData with editable copies
        /// </summary>
        public static void CopyDefaultSkinsToAppData()
        {
            try
            {
                // Ensure AppData Skins folder exists
                Directory.CreateDirectory(AppDataSkinsFolder);

                // Check if AppData already has skins (don't overwrite on subsequent runs)
                var appDataHasSkins = Directory.Exists(AppDataSkinsFolder) &&
                    Directory.GetFiles(AppDataSkinsFolder, "*.xaml").Length > 0;

                if (appDataHasSkins)
                {
                    return;
                }

                // Copy all skins from app directory to AppData
                if (Directory.Exists(AppSkinsFolder))
                {
                    var skinFiles = Directory.GetFiles(AppSkinsFolder, "*.xaml");
                    foreach (var skinFile in skinFiles)
                    {
                        var fileName = Path.GetFileName(skinFile);
                        var destPath = Path.Combine(AppDataSkinsFolder, fileName);

                        File.Copy(skinFile, destPath, overwrite: false);
                    }
                }
                else
                {
                    MessageBox.Show("App Skins folder not found, cannot copy to AppData");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying skins to AppData: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensure custom skins folder exists in AppData
        /// </summary>
        public static void EnsureCustomSkinsFolder()
        {
            if (!Directory.Exists(AppDataSkinsFolder))
            {
                Directory.CreateDirectory(AppDataSkinsFolder);
            }
        }

        /// <summary>
        /// Get the custom skins folder path (AppData)
        /// </summary>
        public static string GetCustomSkinsFolder() => AppDataSkinsFolder;

        /// <summary>
        /// Get the app skins folder path (next to executable)
        /// </summary>
        public static string GetAppSkinsFolder() => AppSkinsFolder;

        /// <summary>
        /// Install a custom skin file to AppData
        /// </summary>
        public static bool InstallCustomSkin(string sourceFilePath)
        {
            try
            {
                EnsureCustomSkinsFolder();
                var fileName = Path.GetFileName(sourceFilePath);
                var destPath = Path.Combine(AppDataSkinsFolder, fileName);
                File.Copy(sourceFilePath, destPath, overwrite: true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public class SkinInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool IsCustom { get; set; }
        public string Location { get; set; } = string.Empty;
    }
}