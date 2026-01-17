namespace MiniWorldBrowser.Models;

/// <summary>
/// 标签页信息模型
/// </summary>
public class TabInfo
{
    /// <summary>
    /// 标签页唯一标识
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 页面标题
    /// </summary>
    public string Title { get; set; } = "新标签页";
    
    /// <summary>
    /// 当前 URL
    /// </summary>
    public string Url { get; set; } = "about:blank";
    
    /// <summary>
    /// 是否正在加载
    /// </summary>
    public bool IsLoading { get; set; }
    
    /// <summary>
    /// 是否为安全连接
    /// </summary>
    public bool IsSecure { get; set; }
    
    /// <summary>
    /// 网站图标 URL
    /// </summary>
    public string? FaviconUrl { get; set; }
    
    /// <summary>
    /// 是否正在播放音频
    /// </summary>
    public bool IsPlayingAudio { get; set; }
    
    /// <summary>
    /// 是否已静音
    /// </summary>
    public bool IsMuted { get; set; }
    
    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActiveTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 是否已挂起
    /// </summary>
    public bool IsSuspended { get; set; }
}
