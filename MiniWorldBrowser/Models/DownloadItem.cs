namespace MiniWorldBrowser.Models;

/// <summary>
/// 下载项
/// </summary>
public class DownloadItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Url { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long TotalBytes { get; set; }
    public long ReceivedBytes { get; set; }
    public double Progress => TotalBytes > 0 ? (double)ReceivedBytes / TotalBytes * 100 : 0;
    public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 下载状态
/// </summary>
public enum DownloadStatus
{
    Pending,
    Downloading,
    Completed,
    Cancelled,
    Failed
}
