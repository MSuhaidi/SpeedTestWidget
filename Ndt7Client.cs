using System.Net.Http;
using System.Text.Json;
using System.Windows.Controls;

namespace SpeedTestWidget
{
    /// <summary>
    /// NDT7 Client for running speed tests against M-Lab servers
    /// </summary>
    public static class Ndt7Client
    {
        private static readonly HttpClient _http = new();

        /// <summary>
        /// Discovers the nearest NDT7 server using M-Lab's locate API
        /// </summary>
        private static async Task<ServerInfo> DiscoverServerAsync()
        {
            const string locateUrl = "https://locate.measurementlab.net/v2/nearest/ndt/ndt7";

            try
            {
                string json = await _http.GetStringAsync(locateUrl);
                using var doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");

                if (results.GetArrayLength() == 0)
                    throw new InvalidOperationException("No NDT7 servers available in your region. Please try again later.");

                var firstServer = results[0]; // Use the first server in the list. Can change the index to try others.

                return new ServerInfo
                {
                    Hostname = firstServer.GetProperty("hostname").GetString()!,
                    City = firstServer.GetProperty("location").GetProperty("city").GetString()!,
                    Country = firstServer.GetProperty("location").GetProperty("country").GetString()!,
                    DownloadUrl = firstServer.GetProperty("urls").GetProperty("wss:///ndt/v7/download").GetString()!,
                    UploadUrl = firstServer.GetProperty("urls").GetProperty("wss:///ndt/v7/upload").GetString()!
                };
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException("Failed to locate NDT7 server. Check your internet connection.", ex);
            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to parse server location response. The M-Lab service may be temporarily unavailable.", ex);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException("Failed to discover NDT7 server. Please try again.", ex);
            }
        }

        /// <summary>
        /// Runs a complete NDT7 speed test (download and upload)
        /// </summary>
        public static async Task<TestResult> RunTestAsync(
            ProgressBar downloadBar,
            ProgressBar uploadBar,
            Action<string> updateDebugText)
        {
            ServerInfo server;

            try
            {
                updateDebugText("Locating nearest server...");
                server = await DiscoverServerAsync();
                updateDebugText($"Testing server: {server.Hostname} ({server.City}, {server.Country})");
            }
            catch (Exception ex)
            {
                updateDebugText($"Server discovery failed: {ex.Message}");
                throw;
            }

            double downloadMbps = 0;
            double uploadMbps = 0;

            // Run Download Test
            try
            {
                updateDebugText("Starting download test...");
                downloadMbps = await Ndt7Download.RunWebSocketDownload(
                    server.DownloadUrl,
                    downloadBar,
                    updateDebugText);
            }
            catch (Exception ex)
            {
                updateDebugText($"Download test failed: {ex.Message}");
                throw new InvalidOperationException($"Download test failed: {ex.Message}", ex);
            }

            // Small delay between tests
            await Task.Delay(1000);

            // Run Upload Test
            try
            {
                updateDebugText("Starting upload test...");
                uploadMbps = await Ndt7Upload.RunWebSocketUpload(
                    server.UploadUrl,
                    uploadBar,
                    updateDebugText);
            }
            catch (Exception ex)
            {
                updateDebugText($"Upload test failed: {ex.Message}");
                throw new InvalidOperationException($"Upload test failed: {ex.Message}", ex);
            }

            return new TestResult
            {
                DownloadMbps = downloadMbps,
                UploadMbps = uploadMbps,
                Hostname = server.Hostname,
                City = server.City,
                Country = server.Country
            };
        }

        private class ServerInfo
        {
            public string Hostname { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string Country { get; set; } = string.Empty;
            public string DownloadUrl { get; set; } = string.Empty;
            public string UploadUrl { get; set; } = string.Empty;
        }
    }

    /// <summary>
    /// Result of a speed test
    /// </summary>
    public class TestResult
    {
        public double DownloadMbps { get; set; }
        public double UploadMbps { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        public void Deconstruct(out double downloadMbps, out double uploadMbps,
            out string hostname, out string city, out string country)
        {
            downloadMbps = DownloadMbps;
            uploadMbps = UploadMbps;
            hostname = Hostname;
            city = City;
            country = Country;
        }
    }
}