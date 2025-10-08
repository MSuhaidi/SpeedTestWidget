namespace SpeedTestWidget
{
    public interface IHistoryItem
    {
        double DownloadMbps { get; set; }
        string Location { get; set; }
        string Server { get; set; }
        string Timestamp { get; set; }
        double UploadMbps { get; set; }
    }
}