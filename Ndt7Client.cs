using System.Net.Http;
using System.Text.Json;

namespace SpeedTestWidget
{
    // NDT7 Client for running speed tests against M-Lab servers
    public static class Ndt7Client
    {
        private static readonly HttpClient _http = new();

        // Discovers the nearest NDT7 server using M-Lab's locate API
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

                var firstServer = results[0];

                return new ServerInfo
                {
                    Hostname = firstServer.GetProperty("machine").GetString()!,
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

        // Runs a complete NDT7 speed test (download and upload)
        public static async Task<TestResult> RunTestAsync(
            Action<string, double, double?, double?, double?, double?> progressCallback)
        {
            ServerInfo server;

            // Server discovery
            progressCallback("Locating server", 0, null, null, null, null);
            server = await DiscoverServerAsync();

            double downloadMbps = 0;
            double uploadMbps = 0;
            double downloadPingMs = 0;
            double uploadPingMs = 0;

            // Run Download Test
            progressCallback("Download test", 0, null, null, null, null);
            (downloadMbps, downloadPingMs) = await Ndt7Download.RunWebSocketDownload(
                server.DownloadUrl,
                (progress, speed, ping) => progressCallback("Download test", progress, speed, null, ping, null));

            // Small delay between tests
            await Task.Delay(1000);

            // Run Upload Test
            progressCallback("Upload test", 0, downloadMbps, null, downloadPingMs, null);
            (uploadMbps, uploadPingMs) = await Ndt7Upload.RunWebSocketUpload(
                server.UploadUrl,
                (progress, speed, ping) => progressCallback("Upload test", progress, downloadMbps, speed, downloadPingMs, ping));

            return new TestResult
            {
                DownloadMbps = downloadMbps,
                UploadMbps = uploadMbps,
                DownloadPingMs = downloadPingMs,
                UploadPingMs = uploadPingMs,
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
        public double DownloadPingMs { get; set; }
        public double UploadPingMs { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        public void Deconstruct(out double downloadMbps, out double uploadMbps,
            out double downloadPingMs, out double uploadPingMs,
            out string hostname, out string city, out string country)
        {
            downloadMbps = DownloadMbps;
            uploadMbps = UploadMbps;
            downloadPingMs = DownloadPingMs;
            uploadPingMs = UploadPingMs;
            hostname = Hostname;
            city = City;
            country = Country;
        }
    }
}