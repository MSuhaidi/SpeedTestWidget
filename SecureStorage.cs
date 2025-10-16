using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpeedTestWidget
{
    /// <summary>
    /// Secure storage with tamper detection for last speed test result
    /// Uses SHA256 signatures to detect manual data modification
    /// </summary>
    public class SecureStorage
    {
        private readonly string _dataFile;
        private readonly string _secretKey;

        public SecureStorage()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "SpeedTest");
            Directory.CreateDirectory(appFolder);

            _dataFile = Path.Combine(appFolder, "last_result_secure.dat");

            // Generate or load a secret key for this installation
            var keyFile = Path.Combine(appFolder, ".signature_key");
            if (File.Exists(keyFile))
            {
                _secretKey = File.ReadAllText(keyFile);
            }
            else
            {
                _secretKey = Guid.NewGuid().ToString("N");
                File.WriteAllText(keyFile, _secretKey);
                File.SetAttributes(keyFile, FileAttributes.Hidden);
            }
        }

        public static JsonSerializerOptions GetOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }

        /// <summary>
        /// Save speed test result with tamper protection signature
        /// </summary>
        public void SaveResult(double downloadMbps, double uploadMbps, double downloadPingMs, double uploadPingMs,
            string server, string city, string country, JsonSerializerOptions options)
        {
            var result = new SecureTestResult
            {
                DownloadMbps = downloadMbps,
                UploadMbps = uploadMbps,
                DownloadPingMs = downloadPingMs,
                UploadPingMs = uploadPingMs,
                Server = server,
                City = city,
                Country = country,
                Timestamp = DateTime.Now
            };

            // Generate signature
            result.Signature = GenerateSignature(result);

            var json = JsonSerializer.Serialize(result, options);

            File.WriteAllText(_dataFile, json);
        }

        /// <summary>
        /// Load last result and verify signature
        /// Returns null if no data exists
        /// Sets IsValid=false if data was tampered with
        /// </summary>
        public SecureTestResult? LoadResult()
        {
            if (!File.Exists(_dataFile))
                return null;

            try
            {
                var json = File.ReadAllText(_dataFile);
                var result = JsonSerializer.Deserialize<SecureTestResult>(json);

                if (result != null)
                {
                    // Verify signature
                    var expectedSignature = GenerateSignature(result);
                    result.IsValid = result.Signature == expectedSignature;
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clear all stored data (for fresh start)
        /// </summary>
        public void ClearAllData()
        {
            if (File.Exists(_dataFile))
                File.Delete(_dataFile);
        }

        /// <summary>
        /// Generate SHA256 signature for tamper detection
        /// </summary>
        private string GenerateSignature(SecureTestResult result)
        {
            // Create signature from all critical data + secret key
            var data = $"{result.DownloadMbps:F10}|{result.UploadMbps:F10}|" +
                      $"{result.Timestamp:O}|{result.Server}|{result.City}|{result.Country}|{_secretKey}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }
    }

    /// <summary>
    /// Speed test result with tamper detection
    /// </summary>
    public class SecureTestResult
    {
        public double DownloadMbps { get; set; }
        public double UploadMbps { get; set; }
        public double DownloadPingMs { get; set; }
        public double UploadPingMs { get; set; }
        public DateTime Timestamp { get; set; }
        public string Server { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid { get; set; } = true;
    }
}