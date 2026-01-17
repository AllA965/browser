using System.Collections.Concurrent;
using System.Net.Http;

namespace MiniWorldBrowser.Helpers;

/// <summary>
/// Favicon 获取和缓存辅助类
/// </summary>
public static class FaviconHelper
{
    private static readonly ConcurrentDictionary<string, Image?> _cache = new();
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static Image? _defaultIcon;
    
    /// <summary>
    /// 获取默认图标
    /// </summary>
    public static Image DefaultIcon => _defaultIcon ??= CreateDefaultIcon();
    
    /// <summary>
    /// 获取网站图标（同步，从缓存获取）
    /// </summary>
    public static Image GetCachedFavicon(string? url)
    {
        if (string.IsNullOrEmpty(url)) return DefaultIcon;
        
        var host = GetHost(url);
        if (string.IsNullOrEmpty(host)) return DefaultIcon;
        
        return _cache.TryGetValue(host, out var icon) && icon != null ? icon : DefaultIcon;
    }
    
    /// <summary>
    /// 异步获取网站图标
    /// </summary>
    public static async Task<Image> GetFaviconAsync(string? url, string? directFaviconUrl = null)
    {
        if (string.IsNullOrEmpty(url)) return DefaultIcon;
        
        var host = GetHost(url);
        if (string.IsNullOrEmpty(host)) return DefaultIcon;
        
        if (_cache.TryGetValue(host, out var cached) && cached != null)
            return cached;
        
        // 构建 favicon URL 列表，优先使用直接提供的 URL
        var faviconUrls = new List<string>();
        if (!string.IsNullOrEmpty(directFaviconUrl))
            faviconUrls.Add(directFaviconUrl);
        
        faviconUrls.AddRange(new[]
        {
            $"https://{host}/favicon.ico",
            $"https://favicon.cccyun.cc/{host}",
            $"https://api.iowen.cn/favicon/{host}.png",
            $"http://{host}/favicon.ico"
        });
        
        foreach (var faviconUrl in faviconUrls)
        {
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(faviconUrl);
                if (bytes.Length > 0)
                {
                    using var ms = new MemoryStream(bytes);
                    using var tempImg = Image.FromStream(ms);
                    var icon = new Bitmap(tempImg, 16, 16);
                    _cache[host] = icon;
                    return icon;
                }
            }
            catch
            {
                // 继续尝试下一个来源
            }
        }
        
        _cache[host] = null;
        return DefaultIcon;
    }
    
    /// <summary>
    /// 预加载 favicon
    /// </summary>
    public static void PreloadFavicon(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        _ = GetFaviconAsync(url);
    }
    
    private static string? GetHost(string url)
    {
        try
        {
            if (!url.Contains("://"))
                url = "https://" + url;
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return null;
        }
    }
    
    private static Image CreateDefaultIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 绘制一个简单的地球图标
        g.Clear(Color.Transparent);
        using var pen = new Pen(Color.FromArgb(150, 150, 150), 1.5f);
        g.DrawEllipse(pen, 2, 2, 11, 11);
        g.DrawLine(pen, 8, 2, 8, 13);
        g.DrawLine(pen, 2, 8, 13, 8);
        g.DrawArc(pen, 4, 2, 8, 11, 90, 180);
        g.DrawArc(pen, 4, 2, 8, 11, -90, 180);
        
        return bmp;
    }
}
