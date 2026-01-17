using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Services;

/// <summary>
/// 广告过滤服务实现
/// </summary>
public class AdBlockService : IAdBlockService
{
    private readonly HashSet<string> _blockedDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _blockedPatterns = new();
    private readonly List<(string Pattern, bool IsAllow)> _exceptions = new();
    private int _blockedCount;
    
    public bool Enabled { get; set; } = true;
    public int Mode { get; set; } = 2; // 0=不过滤, 1=仅弹窗, 2=强力过滤, 3=自定义
    public int BlockedCount => _blockedCount;
    
    public AdBlockService()
    {
        LoadDefaultRules();
    }
    
    private void LoadDefaultRules()
    {
        // 常见广告域名
        var domains = new[]
        {
            "doubleclick.net", "googlesyndication.com", "googleadservices.com",
            "adservice.google.com", "pagead2.googlesyndication.com",
            "ads.yahoo.com", "ad.doubleclick.net",
            "cpro.baidu.com", "pos.baidu.com", "cbjs.baidu.com",
            "tanx.com", "atanx.alicdn.com", "alimama.com",
            "union.sogou.com", "inte.sogou.com",
            "adsame.com", "mediav.com", "ipinyou.com",
            "adnxs.com", "adsrvr.org", "criteo.com", "taboola.com",
            "outbrain.com", "mgid.com", "revcontent.com"
        };
        
        foreach (var d in domains)
            _blockedDomains.Add(d);
        
        // URL 模式
        _blockedPatterns.AddRange(new[]
        {
            "/ads/", "/ad/", "/advert/", "/banner/", "/adsense/",
            "?ad=", "&ad=", "/popup", "/popunder", "/adframe",
            "/adserver", "/adclick", "/adview", "doubleclick",
            "/pagead/", "/sponsor/", "/affiliate/"
        });
    }
    
    public void SetExceptions(List<string> exceptions)
    {
        _exceptions.Clear();
        foreach (var ex in exceptions)
        {
            var parts = ex.Split('|');
            if (parts.Length >= 1)
            {
                var pattern = parts[0].Trim();
                var isAllow = parts.Length < 2 || parts[1] != "block";
                _exceptions.Add((pattern, isAllow));
            }
        }
    }
    
    public bool IsExcepted(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLower();
            
            foreach (var (pattern, isAllow) in _exceptions)
            {
                if (MatchHost(host, pattern.ToLower()))
                {
                    return isAllow; // 如果是允许规则，返回true表示例外
                }
            }
        }
        catch { }
        
        return false;
    }
    
    private static bool MatchHost(string host, string pattern)
    {
        // 支持 [*.] 通配符，匹配域名及其所有子域名
        if (pattern.StartsWith("[*.]"))
        {
            var baseDomain = pattern[4..];
            return host == baseDomain || host.EndsWith("." + baseDomain);
        }
        
        // 支持 * 通配符
        if (pattern.Contains('*'))
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(host, regex);
        }
        
        return host == pattern || host.EndsWith("." + pattern);
    }
    
    public bool ShouldBlock(string url)
    {
        // 模式0：不过滤任何广告
        if (Mode == 0 || !Enabled || string.IsNullOrEmpty(url))
            return false;
        
        // 模式1：仅拦截弹窗，不拦截页面广告
        if (Mode == 1)
            return false;
        
        // 检查是否在例外列表中
        if (IsExcepted(url))
            return false;
        
        try
        {
            var uri = new Uri(url);
            
            // 检查域名
            if (_blockedDomains.Contains(uri.Host))
            {
                _blockedCount++;
                return true;
            }
            
            // 检查子域名
            var parts = uri.Host.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var domain = string.Join(".", parts.Skip(i));
                if (_blockedDomains.Contains(domain))
                {
                    _blockedCount++;
                    return true;
                }
            }
            
            // 检查 URL 模式
            var lowerUrl = url.ToLower();
            foreach (var pattern in _blockedPatterns)
            {
                if (lowerUrl.Contains(pattern))
                {
                    _blockedCount++;
                    return true;
                }
            }
        }
        catch
        {
            // 忽略解析错误
        }
        
        return false;
    }
    
    public bool ShouldBlockPopup(string url)
    {
        // 模式0：不过滤
        if (Mode == 0 || !Enabled)
            return false;
        
        // 检查是否在例外列表中
        if (IsExcepted(url))
            return false;
        
        // 模式1及以上：拦截弹窗
        return true;
    }
    
    public void AddBlockedDomain(string domain)
    {
        _blockedDomains.Add(domain.Trim().ToLower());
    }
    
    public void AddBlockedPattern(string pattern)
    {
        _blockedPatterns.Add(pattern.ToLower());
    }
    
    public void LoadCustomRules(string filePath)
    {
        if (!File.Exists(filePath))
            return;
        
        foreach (var line in File.ReadAllLines(filePath))
        {
            var rule = line.Trim();
            if (string.IsNullOrEmpty(rule) || rule.StartsWith("#") || rule.StartsWith("!"))
                continue;
            
            if (rule.StartsWith("||"))
                AddBlockedDomain(rule[2..].TrimEnd('^'));
            else if (!rule.StartsWith("@@")) // 忽略白名单规则
                AddBlockedPattern(rule);
        }
    }
}
