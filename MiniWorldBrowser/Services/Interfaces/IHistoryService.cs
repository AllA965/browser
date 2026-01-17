using MiniWorldBrowser.Models;

namespace MiniWorldBrowser.Services.Interfaces;

/// <summary>
/// 历史记录服务接口
/// </summary>
public interface IHistoryService
{
    /// <summary>
    /// 添加历史记录
    /// </summary>
    void Add(string url, string title, string? faviconUrl = null);
    
    /// <summary>
    /// 是否可以后退
    /// </summary>
    bool CanGoBack { get; }
    
    /// <summary>
    /// 是否可以前进
    /// </summary>
    bool CanGoForward { get; }
    
    /// <summary>
    /// 后退
    /// </summary>
    string? GoBack();
    
    /// <summary>
    /// 前进
    /// </summary>
    string? GoForward();
    
    /// <summary>
    /// 获取历史记录列表
    /// </summary>
    List<HistoryItem> GetHistory(int limit = 50);
    
    /// <summary>
    /// 搜索历史记录
    /// </summary>
    List<HistoryItem> Search(string keyword, int limit = 20);
    
    /// <summary>
    /// 清除历史记录
    /// </summary>
    void Clear();
    
    /// <summary>
    /// 获取经常访问的网址（按访问次数排序）
    /// </summary>
    List<FrequentSite> GetFrequentSites(int limit = 6);
    
    /// <summary>
    /// 历史记录变更事件
    /// </summary>
    event Action? HistoryChanged;
}
