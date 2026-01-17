namespace MiniWorldBrowser.Models;

/// <summary>
/// 浏览历史记录项
/// </summary>
public class HistoryItem
{
    /// <summary>
    /// 访问的 URL
    /// </summary>
    public string Url { get; set; } = "";
    
    /// <summary>
    /// 页面标题
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// 访问时间
    /// </summary>
    public DateTime VisitTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 网站图标 URL
    /// </summary>
    public string? FaviconUrl { get; set; }
}

/// <summary>
/// 经常访问的网站
/// </summary>
public class FrequentSite
{
    /// <summary>
    /// 网站 URL
    /// </summary>
    public string Url { get; set; } = "";
    
    /// <summary>
    /// 网站标题
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// 网站域名
    /// </summary>
    public string Domain { get; set; } = "";
    
    /// <summary>
    /// 访问次数
    /// </summary>
    public int VisitCount { get; set; }
    
    /// <summary>
    /// 网站图标 URL
    /// </summary>
    public string? FaviconUrl { get; set; }
}
