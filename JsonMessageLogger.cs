using System.IO;

namespace SpeedTestWidget
{
    /// <summary>
    /// Optional logger for debugging NDT7 JSON messages
    /// Logs download and upload messages to separate files
    /// </summary>
    public static class JsonMessageLogger
    {
        private static StreamWriter? _downloadWriter;
        private static StreamWriter? _uploadWriter;
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                _downloadWriter = new StreamWriter("ndt7_download_messages.json", false) { AutoFlush = true };
                _uploadWriter = new StreamWriter("ndt7_upload_messages.json", false) { AutoFlush = true };

                _downloadWriter.WriteLine("// NDT7 Download Messages");
                _uploadWriter.WriteLine("// NDT7 Upload Messages");

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize JSON logger: {ex.Message}");
            }
        }

        public static void LogDownloadMessage(string message)
        {
            try
            {
                _downloadWriter?.WriteLine(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log download message: {ex.Message}");
            }
        }

        public static void LogUploadMessage(string message)
        {
            try
            {
                _uploadWriter?.WriteLine(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log upload message: {ex.Message}");
            }
        }

        public static void Close()
        {
            try
            {
                _downloadWriter?.Dispose();
                _uploadWriter?.Dispose();
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to close JSON logger: {ex.Message}");
            }
        }
    }
}