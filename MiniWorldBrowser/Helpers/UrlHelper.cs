namespace MiniWorldBrowser.Helpers;

/// <summary>
/// URL 处理工具类
/// </summary>
public static class UrlHelper
{
    /// <summary>
    /// 规范化 URL
    /// </summary>
    public static string Normalize(string url, string searchEngine)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Constants.AppConstants.DefaultHomePage;
        
        url = url.Trim();
        
        // 已有协议
        if (url.Contains("://"))
            return url;
        
        // 特殊页面
        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            return url;
        
        // 看起来像域名
        if (LooksLikeDomain(url))
            return "https://" + url;
        
        // 当作搜索词处理
        return searchEngine + Uri.EscapeDataString(url);
    }
    
    /// <summary>
    /// 判断是否看起来像域名
    /// </summary>
    public static bool LooksLikeDomain(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        
        // localhost 特殊处理
        if (text.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // 包含点号且是有效 URI
        if (text.Contains('.') && Uri.IsWellFormedUriString("https://" + text, UriKind.Absolute))
            return true;
        
        return false;
    }
    
    /// <summary>
    /// 验证 URL 是否有效
    /// </summary>
    public static bool IsValid(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || 
                uri.Scheme == Uri.UriSchemeHttps || 
                uri.Scheme == "file" || 
                uri.Scheme == "about");
    }
    
    /// <summary>
    /// 获取 URL 的主机名
    /// </summary>
    public static string GetHost(string url)
    {
        try
        {
            return new Uri(url).Host;
        }
        catch
        {
            return url;
        }
    }
    
    /// <summary>
    /// 判断是否为安全连接
    /// </summary>
    public static bool IsSecure(string url)
    {
        return !string.IsNullOrEmpty(url) && 
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// 判断是否为新标签页
    /// </summary>
    public static bool IsNewTabPage(string url)
    {
        return string.IsNullOrEmpty(url) || 
               url == "about:blank" || 
               url == "about:newtab";
    }
}
