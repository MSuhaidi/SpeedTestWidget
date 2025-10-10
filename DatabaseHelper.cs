using Microsoft.Data.Sqlite;
using System.IO;

namespace SpeedTestWidget
{
    public static class DatabaseHelper
    {
        // Use absolute path in user's AppData folder
        private static readonly string DbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpeedTest",
            "speedtest.db"
        );

        public static void InitDatabase()
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(DbPath))
            {
                using var conn = new SqliteConnection($"Data Source={DbPath}");
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Results (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    DownloadMbps REAL NOT NULL,
                    UploadMbps REAL NOT NULL,
                    Hostname TEXT NOT NULL,
                    City TEXT NOT NULL,
                    Country TEXT NOT NULL
                );
                
                CREATE INDEX IF NOT EXISTS idx_timestamp ON Results(Timestamp DESC);";
                cmd.ExecuteNonQuery();
            }
        }

        public static void SaveResult(string hostname, string city, string country, double download, double upload)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={DbPath}");
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                INSERT INTO Results (Timestamp, DownloadMbps, UploadMbps, Hostname, City, Country)
                VALUES (@t, @d, @u, @host, @city, @country)";

                cmd.Parameters.AddWithValue("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@d", download);
                cmd.Parameters.AddWithValue("@u", upload);
                cmd.Parameters.AddWithValue("@host", hostname);
                cmd.Parameters.AddWithValue("@city", city);
                cmd.Parameters.AddWithValue("@country", country);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save result: {ex.Message}");
                throw;
            }
        }

        public static List<HistoryItem> GetHistory(int limit = 50)
        {
            var history = new List<HistoryItem>();

            try
            {
                using var conn = new SqliteConnection($"Data Source={DbPath}");
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                SELECT Timestamp, DownloadMbps, UploadMbps, Hostname, City, Country
                FROM Results
                ORDER BY Timestamp DESC
                LIMIT @limit";
                cmd.Parameters.AddWithValue("@limit", limit);

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    history.Add(new HistoryItem
                    {
                        Timestamp = reader.GetString(0),
                        DownloadMbps = reader.GetDouble(1),
                        UploadMbps = reader.GetDouble(2),
                        Server = reader.GetString(3),
                        Location = $"{reader.GetString(4)}, {reader.GetString(5)}"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load history: {ex.Message}");
            }

            return history;
        }

        public static HistoryItem? GetLastResult()
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={DbPath}");
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                SELECT Timestamp, DownloadMbps, UploadMbps, Hostname, City, Country
                FROM Results
                ORDER BY Timestamp DESC
                LIMIT 1";

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new HistoryItem
                    {
                        Timestamp = reader.GetString(0),
                        DownloadMbps = reader.GetDouble(1),
                        UploadMbps = reader.GetDouble(2),
                        Server = reader.GetString(3),
                        Location = $"{reader.GetString(4)}, {reader.GetString(5)}"
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get last result: {ex.Message}");
            }

            return null;
        }

        public static void ClearHistory()
        {
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Results";
            cmd.ExecuteNonQuery();
        }

        public static int GetResultCount()
        {
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Results";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // Helper method to get database location
        public static string GetDatabasePath() => DbPath;
    }

    public class HistoryItem
    {
        public string Timestamp { get; set; } = string.Empty;
        public double DownloadMbps { get; set; }
        public double UploadMbps { get; set; }
        public string Server { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}