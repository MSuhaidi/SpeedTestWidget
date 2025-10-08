using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SpeedTestWidget
{
    public static class Ndt7Download
    {
        private const int TestDurationSeconds = 10;
        private const int ConnectionTimeoutSeconds = 15;

        public static async Task<double> RunWebSocketDownload(
            string url,
            ProgressBar progressBar,
            Action<string> updateDebugText)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TestDurationSeconds + ConnectionTimeoutSeconds));
            var token = cts.Token;

            double finalSpeed = 0;
            var stopwatch = Stopwatch.StartNew();
            var buffer = new byte[16384];

            using var ws = new ClientWebSocket();

            try
            {
                // Configure WebSocket
                ws.Options.AddSubProtocol("net.measurementlab.ndt.v7");
                ws.Options.SetRequestHeader("User-Agent", "ndt7-client-dotnet/1.0");
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                // Connection phase with timeout
                updateDebugText("Connecting to download server...");

                try
                {
                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    connectCts.CancelAfter(TimeSpan.FromSeconds(10));

                    await ws.ConnectAsync(new Uri(url), connectCts.Token);
                    updateDebugText("Download test connected");
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("Connection to download server timed out after 10 seconds.");
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    throw new WebSocketException("Server closed connection during handshake. The server may be overloaded.");
                }

                // Test phase
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;

                    try
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    }
                    catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    {
                        updateDebugText("Server closed connection unexpectedly");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        updateDebugText("Download test cancelled (timeout)");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        updateDebugText("Server closed connection normally");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        try
                        {
                            JsonMessageLogger.LogDownloadMessage(message);
                            using var doc = JsonDocument.Parse(message);
                            var root = doc.RootElement;

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

                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            progressBar.Value = progress;
                                            updateDebugText($"Download: {speedMbps:F2} Mbps ({bytes:N0} bytes)");
                                        });
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

                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            progressBar.Value = progress;
                                            updateDebugText($"Download: {speedMbps:F2} Mbps (TCP)");
                                        });
                                    }
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                                updateDebugText($"JSON parse warning: {ex.Message}"));
                        }
                    }

                    // Auto-close after test duration
                    if (stopwatch.Elapsed.TotalSeconds >= TestDurationSeconds)
                    {
                        break;
                    }
                }

                Application.Current.Dispatcher.Invoke(() => progressBar.Value = 100);

                if (finalSpeed == 0)
                {
                    throw new InvalidOperationException("No valid speed measurements received from server.");
                }
            }
            catch (OperationCanceledException)
            {
                if (finalSpeed == 0)
                {
                    throw new TimeoutException("Download test timed out with no measurements.");
                }
                updateDebugText($"Download test timed out (got {finalSpeed:F2} Mbps before timeout)");
            }
            catch (WebSocketException ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    updateDebugText($"WebSocket error: {ex.Message}"));
                throw;
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    updateDebugText($"Download error: {ex.Message}"));
                throw;
            }
            finally
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error closing WebSocket: {ex.Message}");
                    }
                }

                stopwatch.Stop();
                Application.Current.Dispatcher.Invoke(() =>
                    updateDebugText($"Download test finished: {finalSpeed:F2} Mbps"));
            }

            return finalSpeed;
        }
    }
}