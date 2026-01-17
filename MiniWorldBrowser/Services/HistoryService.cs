using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;
using System.Text.Json;

namespace MiniWorldBrowser.Services;

/// <summary>
/// 历史记录服务实现 - 支持持久化存储
/// </summary>
public class HistoryService : IHistoryService, IDisposable
{
    private readonly List<HistoryItem> _items = new();
    private int _currentIndex = -1;
    private readonly string _historyFilePath;
    private readonly object _lock = new();
    private bool _isDirty = false;
    private System.Windows.Forms.Timer? _saveTimer;
    private bool _disposed = false;
    
    public event Action? HistoryChanged;
    
    public bool CanGoBack => _currentIndex > 0;
    public bool CanGoForward => _currentIndex < _items.Count - 1;
    
    public HistoryService()
    {
        // 历史记录文件路径
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiniWorld");
        
        try
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
        }
        catch { }
        
        _historyFilePath = Path.Combine(appDataPath, "history.json");
        
        // 加载历史记录
        Load();
        
        // 使用 Windows Forms Timer（在 UI 线程执行，更安全）
        _saveTimer = new System.Windows.Forms.Timer { Interval = 60000 }; // 每分钟保存一次
        _saveTimer.Tick += OnSaveTimerTick;
        _saveTimer.Start();
        
        // 注册应用程序退出事件
        Application.ApplicationExit += OnApplicationExit;
    }
    
    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        if (_isDirty && !_disposed)
        {
            SaveInternal();
        }
    }
    
    private void OnApplicationExit(object? sender, EventArgs e)
    {
        // 应用退出时保存
        if (_isDirty && !_disposed)
        {
            SaveInternal();
        }
    }
    
    public void Add(string url, string title, string? faviconUrl = null)
    {
        if (string.IsNullOrEmpty(url) || _disposed)
            return;
        
        lock (_lock)
        {
            // 移除当前位置之后的项（前进历史）
            if (_currentIndex < _items.Count - 1)
                _items.RemoveRange(_currentIndex + 1, _items.Count - _currentIndex - 1);
            
            // 避免重复添加相同 URL
            if (_currentIndex >= 0 && _items[_currentIndex].Url == url)
                return;
            
            _items.Add(new HistoryItem
            {
                Url = url,
                Title = title,
                FaviconUrl = faviconUrl,
                VisitTime = DateTime.Now
            });
            
            _currentIndex = _items.Count - 1;
            
            // 限制历史记录数量
            if (_items.Count > AppConstants.MaxHistoryItems)
            {
                _items.RemoveAt(0);
                _currentIndex--;
            }
            
            _isDirty = true;
        }
        
        try { HistoryChanged?.Invoke(); } catch { }
    }
    
    public string? GoBack()
    {
        lock (_lock)
        {
            if (CanGoBack)
                return _items[--_currentIndex].Url;
            return null;
        }
    }
    
    public string? GoForward()
    {
        lock (_lock)
        {
            if (CanGoForward)
                return _items[++_currentIndex].Url;
            return null;
        }
    }
    
    public List<HistoryItem> GetHistory(int limit = 50)
    {
        lock (_lock)
        {
            return _items.TakeLast(limit).Reverse().ToList();
        }
    }
    
    public List<HistoryItem> Search(string keyword, int limit = 20)
    {
        lock (_lock)
        {
            return _items
                .Where(h => h.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                           h.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.VisitTime)
                .Take(limit)
                .ToList();
        }
    }
    
    public List<FrequentSite> GetFrequentSites(int limit = 6)
    {
        lock (_lock)
        {
            // 按域名分组统计访问次数
            var siteStats = _items
                .Where(h => !string.IsNullOrEmpty(h.Url) && 
                           !h.Url.StartsWith("about:") && 
                           !h.Url.StartsWith("data:") &&
                           !h.Title.Contains("新标签页") &&
                           Uri.TryCreate(h.Url, UriKind.Absolute, out _))
                .Select(h => {
                    try
                    {
                        var uri = new Uri(h.Url);
                        return new { 
                            Domain = uri.Host, 
                            BaseUrl = $"{uri.Scheme}://{uri.Host}/",
                            Item = h 
                        };
                    }
                    catch { return null; }
                })
                .Where(x => x != null)
                .GroupBy(x => x!.Domain)
                .Select(g => {
                    var mostRecent = g.OrderByDescending(x => x!.Item.VisitTime).First()!;
                    return new FrequentSite
                    {
                        Domain = g.Key,
                        Url = mostRecent.BaseUrl,
                        Title = GetSiteTitle(mostRecent.Item.Title, g.Key),
                        VisitCount = g.Count(),
                        FaviconUrl = mostRecent.Item.FaviconUrl
                    };
                })
                .OrderByDescending(s => s.VisitCount)
                .Take(limit)
                .ToList();
            
            return siteStats;
        }
    }
    
    private static string GetSiteTitle(string title, string domain)
    {
        // 如果标题太长或包含特殊字符，使用简化的域名
        if (string.IsNullOrEmpty(title) || title.Length > 20)
        {
            // 移除 www. 前缀
            var simpleDomain = domain.StartsWith("www.") ? domain[4..] : domain;
            // 取第一部分作为名称
            var parts = simpleDomain.Split('.');
            return parts[0].Length > 1 ? char.ToUpper(parts[0][0]) + parts[0][1..] : simpleDomain;
        }
        
        // 截取标题的主要部分（通常在 - 或 | 之前）
        var separators = new[] { " - ", " | ", " – ", " — " };
        foreach (var sep in separators)
        {
            var idx = title.IndexOf(sep);
            if (idx > 0 && idx < 20)
                return title[..idx].Trim();
        }
        
        return title.Length > 12 ? title[..12] + "..." : title;
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
            _currentIndex = -1;
            _isDirty = true;
        }
        
        SaveInternal();
        try { HistoryChanged?.Invoke(); } catch { }
    }
    
    /// <summary>
    /// 从文件加载历史记录
    /// </summary>
    private void Load()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
                return;
            
            var json = File.ReadAllText(_historyFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return;
                
            var data = JsonSerializer.Deserialize<HistoryData>(json);
            
            if (data?.Items != null)
            {
                lock (_lock)
                {
                    _items.Clear();
                    
                    // 只加载最近30天的历史记录
                    var cutoffDate = DateTime.Now.AddDays(-30);
                    foreach (var item in data.Items.Where(i => i.VisitTime > cutoffDate))
                    {
                        _items.Add(item);
                    }
                    
                    _currentIndex = _items.Count - 1;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载历史记录失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存历史记录到文件（公开方法）
    /// </summary>
    public void Save()
    {
        if (!_disposed)
            SaveInternal();
    }
    
    /// <summary>
    /// 内部保存方法
    /// </summary>
    private void SaveInternal()
    {
        try
        {
            List<HistoryItem> itemsCopy;
            lock (_lock)
            {
                itemsCopy = _items.ToList();
                _isDirty = false;
            }
            
            var data = new HistoryData { Items = itemsCopy };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存历史记录失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 清理过期的历史记录（超过30天）
    /// </summary>
    public void CleanupOldHistory()
    {
        lock (_lock)
        {
            var cutoffDate = DateTime.Now.AddDays(-30);
            var removed = _items.RemoveAll(i => i.VisitTime < cutoffDate);
            
            if (removed > 0)
            {
                _currentIndex = Math.Min(_currentIndex, _items.Count - 1);
                _isDirty = true;
            }
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Application.ApplicationExit -= OnApplicationExit;
        
        _saveTimer?.Stop();
        _saveTimer?.Dispose();
        _saveTimer = null;
        
        // 最后保存一次
        if (_isDirty)
        {
            SaveInternal();
        }
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 历史记录数据结构（用于序列化）
/// </summary>
internal class HistoryData
{
    public List<HistoryItem> Items { get; set; } = new();
}
