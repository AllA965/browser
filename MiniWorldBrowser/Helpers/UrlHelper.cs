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

    /// <summary>
    /// 判断是否为登录或授权页面
    /// </summary>
    public static bool IsLoginOrAuthUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        
        try
        {
            string lowerUrl = url.ToLower();
            var uri = new Uri(lowerUrl);
            var host = uri.Host;
            var path = uri.AbsolutePath;
            var query = uri.Query;

            // 1. 明确的登录/授权子域名或路径
            string[] loginSpecifics = { 
                "passport.baidu.com", "accounts.google.com", "login.microsoftonline.com", 
                "graph.qq.com", "api.weibo.com", "github.com/login", "weixin.qq.com/connect",
                "open.weixin.qq.com", "passport.weibo.com", "cas.baidu.com", "auth.baidu.com",
                "passport.jd.com", "passport.taobao.com", "login.taobao.com", "canva.cn/login"
            };
            if (loginSpecifics.Any(s => lowerUrl.Contains(s))) return true;

            // 2. 常见的登录关键字，但需要排除普通主页导航
            string[] loginKeywords = { "/login", "/signin", "/authorize", "/oauth", "/auth" };
            if (loginKeywords.Any(p => path.Contains(p))) 
            {
                // 排除一些常见主域名下直接打开的非登录新窗口
                if (host.EndsWith("baidu.com") && !path.Contains("passport") && !path.Contains("auth")) return false;
                if (host.EndsWith("qq.com") && !path.Contains("graph") && !path.Contains("connect")) return false;
                return true;
            }

            // 3. OAuth 参数检测
            if (query.Contains("client_id=") && (query.Contains("redirect_uri=") || query.Contains("response_type=")))
            {
                // 只有在路径包含 auth/login 相关词汇时才判定为弹窗，防止误杀
                if (lowerUrl.Contains("auth") || lowerUrl.Contains("login") || lowerUrl.Contains("authorize"))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
