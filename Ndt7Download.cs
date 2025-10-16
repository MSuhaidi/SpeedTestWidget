using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SpeedTestWidget
{
    public static class Ndt7Download
    {
        private const int TestDurationSeconds = 10;
        private const int ConnectionTimeoutSeconds = 15;

        public static async Task<(double speed, double ping)> RunWebSocketDownload(
            string url,
            Action<double, double, double> progressCallback)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TestDurationSeconds + ConnectionTimeoutSeconds));
            var token = cts.Token;

            double finalSpeed = 0;
            double pingMs = 0;
            var stopwatch = Stopwatch.StartNew();
            var buffer = new byte[16384];

            using var ws = new ClientWebSocket();

            try
            {
                // Configure WebSocket
                ws.Options.AddSubProtocol("net.measurementlab.ndt.v7");
                ws.Options.SetRequestHeader("User-Agent", "ndt7-client-dotnet/1.0");
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                connectCts.CancelAfter(TimeSpan.FromSeconds(10));
                await ws.ConnectAsync(new Uri(url), connectCts.Token);

                // Test phase
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;

                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        try
                        {
                            // Optional: Log raw JSON messages for debugging
                            // JsonMessageLogger.LogDownloadMessage(message);
                            using var doc = JsonDocument.Parse(message);
                            var root = doc.RootElement;

                            // Extract ping/MinRTT from BBRInfo
                            if (root.TryGetProperty("BBRInfo", out var bbrInfo))
                            {
                                if (bbrInfo.TryGetProperty("MinRTT", out var rtt))
                                {
                                    pingMs = rtt.GetInt64() / 1000.0; // Convert microseconds to milliseconds
                                }
                            }

                            // Primary: AppInfo with NumBytes
                            if (root.TryGetProperty("AppInfo", out var appInfo))
                            {
                                if (appInfo.TryGetProperty("NumBytes", out var numBytes) &&
                                    appInfo.TryGetProperty("ElapsedTime", out var elapsedTime))
                                {
                                    long bytes = numBytes.GetInt64();
                                    long elapsedMicroseconds = elapsedTime.GetInt64();

                                    if (elapsedMicroseconds > 0)
                                    {
                                        double speedMbps = (bytes * 8.0 / 1_000_000.0) / (elapsedMicroseconds / 1_000_000.0);
                                        finalSpeed = speedMbps;

                                        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                        double progress = Math.Min(100, (elapsedSeconds / TestDurationSeconds) * 100);
                                        progressCallback(progress, finalSpeed, pingMs);
                                    }
                                }
                            }
                            // Fallback: TCPInfo
                            else if (root.TryGetProperty("TCPInfo", out var tcpInfo))
                            {
                                if (tcpInfo.TryGetProperty("BytesSent", out var bytesSent) &&
                                        tcpInfo.TryGetProperty("ElapsedTime", out var elapsedTime))
                                {
                                    long bytes = bytesSent.GetInt64();
                                    long elapsedMicroseconds = elapsedTime.GetInt64();

                                    if (elapsedMicroseconds > 0)
                                    {
                                        double speedMbps = (bytes * 8.0 / 1_000_000.0) / (elapsedMicroseconds / 1_000_000.0);
                                        finalSpeed = speedMbps;

                                        double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                        double progress = Math.Min(100, (elapsedSeconds / TestDurationSeconds) * 100);
                                        progressCallback(progress, finalSpeed, pingMs);
                                    }
                                }
                            }
                        }
                        catch (JsonException) { /* Ignore parse errors */ }
                    }

                    // Auto-close after test duration
                    if (stopwatch.Elapsed.TotalSeconds >= TestDurationSeconds)
                        break;
                }

                if (finalSpeed == 0)
                    throw new InvalidOperationException("No valid speed measurements received from server.");
            }
            finally
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", CancellationToken.None);
                    }
                    catch { /* Ignore close errors */ }
                }

                stopwatch.Stop();
            }
            return (finalSpeed, pingMs);
        }
    }
}