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
    public static class Ndt7Upload
    {
        private const int TestDurationSeconds = 10;
        private const int ConnectionTimeoutSeconds = 15;
        private const int BufferSize = 8192;

        public static async Task<double> RunWebSocketUpload(
            string url,
            ProgressBar progressBar,
            Action<string> updateDebugText)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TestDurationSeconds + ConnectionTimeoutSeconds));
            var token = cts.Token;

            double finalSpeed = 0;
            var stopwatch = Stopwatch.StartNew();

            var sendBuffer = new byte[BufferSize];
            var receiveBuffer = new byte[16384];
            new Random().NextBytes(sendBuffer);

            using var ws = new ClientWebSocket();

            try
            {
                // Configure WebSocket
                ws.Options.AddSubProtocol("net.measurementlab.ndt.v7");
                ws.Options.SetRequestHeader("User-Agent", "ndt7-client-dotnet/1.0");
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                // Connection phase with timeout
                updateDebugText("Connecting to upload server...");

                try
                {
                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    connectCts.CancelAfter(TimeSpan.FromSeconds(10));

                    await ws.ConnectAsync(new Uri(url), connectCts.Token);
                    updateDebugText("Upload test connected");
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("Connection to upload server timed out after 10 seconds.");
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    throw new WebSocketException("Server closed connection during handshake. The server may be overloaded.");
                }

                // Send task: continuously send data
                var sendTask = Task.Run(async () =>
                {
                    try
                    {
                        while (ws.State == WebSocketState.Open &&
                               !token.IsCancellationRequested &&
                               stopwatch.Elapsed.TotalSeconds < TestDurationSeconds)
                        {
                            try
                            {
                                await ws.SendAsync(
                                    new ArraySegment<byte>(sendBuffer),
                                    WebSocketMessageType.Binary,
                                    endOfMessage: true,
                                    token);
                            }
                            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                    updateDebugText("Upload connection closed by server"));
                                break;
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Send task error: {ex.Message}");
                        throw;
                    }
                }, token);

                // Receive task: read measurement messages from server
                var receiveTask = Task.Run(async () =>
                {
                    double localFinalSpeed = 0;

                    try
                    {
                        while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                        {
                            WebSocketReceiveResult result;

                            try
                            {
                                result = await ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), token);
                            }
                            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                    updateDebugText("Server closed connection unexpectedly"));
                                break;
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                    updateDebugText("Server closed connection normally"));
                                break;
                            }

                            if (result.MessageType == WebSocketMessageType.Text)
                            {
                                var message = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);

                                try
                                {
                                    JsonMessageLogger.LogUploadMessage(message);
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
                                                localFinalSpeed = (bytes * 8.0 / 1_000_000.0) / (elapsedMicroseconds / 1_000_000.0);

                                                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                                double progress = Math.Min(100, (elapsedSeconds / TestDurationSeconds) * 100);

                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    progressBar.Value = progress;
                                                    updateDebugText($"Upload: {localFinalSpeed:F2} Mbps ({bytes:N0} bytes)");
                                                });
                                            }
                                        }
                                    }
                                    // Fallback: TCPInfo
                                    else
                                    {
                                        JsonElement tcpInfo = default;

                                        if (root.TryGetProperty("LastServerMeasurement", out var serverMeasurement))
                                        {
                                            serverMeasurement.TryGetProperty("TCPInfo", out tcpInfo);
                                        }
                                        else
                                        {
                                            root.TryGetProperty("TCPInfo", out tcpInfo);
                                        }

                                        if (tcpInfo.ValueKind != JsonValueKind.Undefined &&
                                            tcpInfo.TryGetProperty("BytesReceived", out var bytesReceived) &&
                                            tcpInfo.TryGetProperty("ElapsedTime", out var elapsedTime))
                                        {
                                            long bytes = bytesReceived.GetInt64();
                                            long elapsedMicroseconds = elapsedTime.GetInt64();

                                            if (elapsedMicroseconds > 0)
                                            {
                                                localFinalSpeed = (bytes * 8.0 / 1_000_000.0) / (elapsedMicroseconds / 1_000_000.0);

                                                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                                double progress = Math.Min(100, (elapsedSeconds / TestDurationSeconds) * 100);

                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    progressBar.Value = progress;
                                                    updateDebugText($"Upload: {localFinalSpeed:F2} Mbps (TCP)");
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
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Receive task error: {ex.Message}");
                        throw;
                    }

                    return localFinalSpeed;
                }, token);

                // Wait for both tasks to complete
                await Task.WhenAll(sendTask, receiveTask);
                finalSpeed = await receiveTask;

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
                    throw new TimeoutException("Upload test timed out with no measurements.");
                }
                updateDebugText($"Upload test timed out (got {finalSpeed:F2} Mbps before timeout)");
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
                    updateDebugText($"Upload error: {ex.Message}"));
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
                    updateDebugText($"Upload test finished: {finalSpeed:F2} Mbps"));
            }

            return finalSpeed;
        }
    }
}