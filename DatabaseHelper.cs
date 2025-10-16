using Microsoft.Data.Sqlite;
using System.IO;
using System.Windows;

namespace SpeedTestWidget
{
    public static class DatabaseHelper
    {
        private static readonly string DbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpeedTest",
            "speedtest.db"
        );

        public static void InitDatabase()
        {
            using var conn = new SqliteConnection($"Data Source={DbPath}");
            conn.Open();

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(DbPath))
            {


                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Results (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    DownloadMbps REAL NOT NULL,
                    UploadMbps REAL NOT NULL,
                    DownloadPingMs REAL NOT NULL,
                    UploadPingMs REAL NOT NULL,
                    Hostname TEXT NOT NULL,
                    City TEXT NOT NULL,
                    Country TEXT NOT NULL
                );
                
                CREATE INDEX IF NOT EXISTS idx_timestamp ON Results(Timestamp DESC);";
                cmd.ExecuteNonQuery();
            }
            else
            {
                var alterCommands = new[]
                {
                    "ALTER TABLE Results ADD COLUMN DownloadPingMs REAL NOT NULL DEFAULT 0",
                    "ALTER TABLE Results ADD COLUMN UploadPingMs REAL NOT NULL DEFAULT 0"
                };

                foreach (var alterCommand in alterCommands)
                {
                    try
                    {
                        using var alterCmd = conn.CreateCommand();
                        alterCmd.CommandText = alterCommand;
                        alterCmd.ExecuteNonQuery();
                    }
                    catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
                    {
                        // Column already exists, ignore
                    }
                }
            }
        }

        public static void SaveResult(double downloadMbps, double uploadMbps,
            double downloadPingMs, double uploadPingMs,
            string hostname, string city, string country)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={DbPath}");
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                INSERT INTO Results (Timestamp, DownloadMbps, UploadMbps, DownloadPingMs, UploadPingMs, Hostname, City, Country)
                VALUES (@t, @d, @u, @dp, @up, @host, @city, @country)";

                cmd.Parameters.AddWithValue("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@d", downloadMbps);
                cmd.Parameters.AddWithValue("@u", uploadMbps);
                cmd.Parameters.AddWithValue("@dp", downloadPingMs);
                cmd.Parameters.AddWithValue("@up", uploadPingMs);
                cmd.Parameters.AddWithValue("@host", hostname);
                cmd.Parameters.AddWithValue("@city", city);
                cmd.Parameters.AddWithValue("@country", country);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save result: {ex.Message}");
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
                SELECT Timestamp, DownloadMbps, UploadMbps, DownloadPingMs, UploadPingMs, Hostname, City, Country
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
                        DownloadPingMs = reader.GetDouble(3),
                        UploadPingMs = reader.GetDouble(4),
                        Server = reader.GetString(5),
                        Location = $"{reader.GetString(6)}, {reader.GetString(7)}"
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load history: {ex.Message}");
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
                SELECT Timestamp, DownloadMbps, UploadMbps, DownloadPingMs, UploadPingMs, Hostname, City, Country
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
                        DownloadPingMs = reader.GetDouble(3),
                        UploadPingMs = reader.GetDouble(4),
                        Server = reader.GetString(5),
                        Location = $"{reader.GetString(6)}, {reader.GetString(7)}"
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to get last result: {ex.Message}");
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

        public static string GetDatabasePath() => DbPath;
    }

    public class HistoryItem
    {
        public string Timestamp { get; set; } = string.Empty;
        public double DownloadMbps { get; set; }
        public double UploadMbps { get; set; }
        public double DownloadPingMs { get; set; }
        public double UploadPingMs { get; set; }
        public string Server { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}