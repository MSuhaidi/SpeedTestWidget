using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SpeedTestWidget
{
    public static class Ndt7Upload
    {
        private const int TestDurationSeconds = 10;
        private const int ConnectionTimeoutSeconds = 15;
        private const int BufferSize = 8192;

        public static async Task<(double speed, double ping)> RunWebSocketUpload(
            string url,
            Action<double, double, double> progressCallback)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TestDurationSeconds + ConnectionTimeoutSeconds));
            var token = cts.Token;

            double finalSpeed = 0;
            double pingMs = 0;
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

                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                connectCts.CancelAfter(TimeSpan.FromSeconds(10));
                await ws.ConnectAsync(new Uri(url), connectCts.Token);

                // Send task: continuously send data
                var sendTask = Task.Run(async () =>
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
                        catch (OperationCanceledException) { break; }
                        catch (WebSocketException) { break; }
                    }
                }, token);

                // Receive task: read measurement messages from server
                var receiveTask = Task.Run(async () =>
                {
                    double localFinalSpeed = 0;

                    while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                    {
                        WebSocketReceiveResult result;

                        try
                        {
                            result = await ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), token);

                            if (result.MessageType == WebSocketMessageType.Close)
                                break;

                            if (result.MessageType == WebSocketMessageType.Text)
                            {
                                var message = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);

                                try
                                {
                                    // Optional: Log raw JSON messages for debugging
                                    // JsonMessageLogger.LogUploadMessage(message);
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
                                                localFinalSpeed = (bytes * 8.0 / 1_000_000.0) / (elapsedMicroseconds / 1_000_000.0);
                                                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                                                double progress = Math.Min(100, (elapsedSeconds / TestDurationSeconds) * 100);
                                                progressCallback(progress, localFinalSpeed, pingMs);
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
                                                progressCallback(progress, localFinalSpeed, pingMs);
                                            }
                                        }
                                    }
                                }
                                catch (JsonException) { /* Ignore parse errors */ }
                            }
                        }
                        catch (OperationCanceledException) { break; }
                        catch (WebSocketException) { break; }
                    }
                    return localFinalSpeed;
                }, token);

                // Wait for both tasks to complete
                await Task.WhenAll(sendTask, receiveTask);
                finalSpeed = await receiveTask;

                if (finalSpeed == 0)
                    throw new InvalidOperationException("No valid speed measurements received from server.");
            }
            finally
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Complete", CancellationToken.None);
                    }
                    catch { /* Ignore close errors */ }
                }

                stopwatch.Stop();
            }

            return (finalSpeed, pingMs);
        }
    }
}