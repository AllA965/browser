using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using Microsoft.Web.WebView2.Core;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Constants;

namespace MiniWorldBrowser.Helpers;

/// <summary>
/// HTML 页面生成器 - 生成新标签页和错误页面
/// </summary>
public static class HtmlGenerator
{
    #region 新标签页
    
    private static string? _cachedIconBase64;

    private static readonly object _iconCacheLock = new();
    private static readonly Dictionary<string, string> _cachedIconPngByKey = new();

    private static string ResolveIconPath(string? preferredName = null)
    {
        var names = preferredName != null 
            ? new[] { preferredName } 
            : new[] { "鲲穹_.png", "鲲穹01.ico", "鲲穹AI浏览器.ico" };

        var baseDirs = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
        };

        foreach (var name in names)
        {
            foreach (var dir in baseDirs)
            {
                try
                {
                    var p = Path.Combine(dir, name);
                    if (File.Exists(p))
                        return p;
                }
                catch { }
            }
        }

        return "";
    }

    private static string GetIconBase64()
    {
        if (_cachedIconBase64 != null) return _cachedIconBase64;
        try
        {
            var iconPath = ResolveIconPath();
            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            {
                var bytes = File.ReadAllBytes(iconPath);
                _cachedIconBase64 = Convert.ToBase64String(bytes);
                return _cachedIconBase64;
            }
        }
        catch { }
        return "";
    }

    private static string GetIconPngBase64(int size, string? iconName = null)
    {
        var cacheKey = $"{(iconName ?? "default")}_{size}";
        lock (_iconCacheLock)
        {
            if (_cachedIconPngByKey.TryGetValue(cacheKey, out var cached))
                return cached;
        }

        try
        {
            var iconPath = ResolveIconPath(iconName);
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
                return "";

            Image src;
            bool isIcon = iconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);
            
            if (isIcon)
            {
                using var icon = new Icon(iconPath);
                src = icon.ToBitmap();
            }
            else
            {
                src = Image.FromFile(iconPath);
            }

            try 
            {
                using var dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(dst))
                {
                    g.Clear(Color.Transparent);
                    g.CompositingMode = CompositingMode.SourceOver;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new Rectangle(0, 0, size, size));
                }

                using var ms = new MemoryStream();
                dst.Save(ms, ImageFormat.Png);
                var base64 = Convert.ToBase64String(ms.ToArray());
                
                lock (_iconCacheLock)
                {
                    _cachedIconPngByKey[cacheKey] = base64;
                }
                return base64;
            }
            finally
            {
                src.Dispose();
            }
        }
        catch
        {
            return "";
        }
    }
    
    /// <summary>
    /// 生成新标签页 HTML
    /// </summary>
    public static string GenerateNewTabPage(BrowserSettings settings, List<FrequentSite>? frequentSites = null, bool isIncognito = false)
    {
        if (isIncognito)
        {
            return GenerateIncognitoPage(settings);
        }

        var shortcutsHtml = GenerateShortcutsHtml(frequentSites);
        var watermarkPngBase64 = GetIconPngBase64(1024, "鲲穹_.png"); // 使用用户指定的 PNG 水印
        var logoPngBase64 = GetIconPngBase64(144, "鲲穹AI浏览器.ico"); // Logo 保持原样
        
        var backgroundColor = "#ffffff";
        var textColor = "#1e293b";
        var inputBackground = "#ffffff";
        var inputColor = "#1e293b";
        var inputBorder = "rgba(0, 0, 0, 0.1)";
        var searchBtnBackground = "#2563eb";
        var searchBtnColor = "white";
        
        var watermarkStyle = string.IsNullOrEmpty(watermarkPngBase64) ? "" : $@"
         .watermark-container {{
             position: fixed;
             top: 50%;
             left: 50%;
             width: 800px;
             height: 800px;
             transform: translate(-50%, -50%) rotate(-5deg);
                 pointer-events: none;
                 z-index: -1;
                 opacity: 0.04;
                background-image: url('data:image/png;base64,{watermarkPngBase64}');
                background-size: contain;
                background-repeat: no-repeat;
                background-position: center;
                filter: grayscale(1) brightness(1.1);
        }}";

        var logoHtml = string.IsNullOrEmpty(logoPngBase64)
            ? "<div class='logo'>🌐</div>"
            : $"<div class='logo'><img class='logo-img' src='data:image/png;base64,{logoPngBase64}' alt='logo'></div>";
        
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>新标签页</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif;
            background: {backgroundColor};
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            color: {textColor};
            position: relative;
            overflow: hidden;
        }}
        {watermarkStyle}
        .container {{
            text-align: center;
            padding: 48px;
            position: relative;
            z-index: 1;
            width: 90%;
            max-width: 850px;
            transition: all 0.5s cubic-bezier(0.4, 0, 0.2, 1);
        }}
        .logo {{ margin-bottom: 12px; }}
        .logo-img {{ width: 72px; height: 72px; object-fit: contain; filter: drop-shadow(0 4px 8px rgba(0,0,0,0.1)); }}
        h1 {{ font-size: 32px; font-weight: 600; margin-bottom: 28px; letter-spacing: 0.5px; color: {textColor}; }}
        .search-box {{ position: relative; width: 100%; max-width: 600px; margin: 0 auto 40px; }}
        .search-input {{
            width: 100%; padding: 18px 60px 18px 24px; font-size: 16px;
            border: 1px solid {inputBorder}; border-radius: 30px; outline: none;
            box-shadow: 0 4px 15px rgba(0,0,0,0.05);
            background: {inputBackground};
            color: {inputColor};
            transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
        }}
        .search-input:focus {{
            box-shadow: 0 10px 40px rgba(0,0,0,0.08);
            border-color: rgba(37, 99, 235, 0.3);
            transform: translateY(-1px);
        }}
        .search-btn {{
            position: absolute; right: 10px; top: 50%; transform: translateY(-50%);
            width: 42px; height: 42px; border: none; border-radius: 50%;
            background: {searchBtnBackground}; color: {searchBtnColor};
            cursor: pointer; transition: all 0.2s;
            box-shadow: 0 4px 12px rgba(37, 99, 235, 0.2);
            display: flex; align-items: center; justify-content: center;
        }}
        .search-btn:hover {{ background: {searchBtnBackground}; opacity: 0.9; transform: translateY(-50%) scale(1.05); }}
        .search-btn svg {{ width: 20px; height: 20px; fill: none; stroke: currentColor; stroke-width: 2.5; stroke-linecap: round; stroke-linejoin: round; }}
        .shortcuts {{ display: flex; flex-wrap: wrap; justify-content: center; gap: 24px; }}
        .shortcut {{ width: 88px; text-decoration: none; color: {textColor}; text-align: center; transition: all 0.2s; }}
        .shortcut:hover {{ transform: translateY(-4px); color: #2563eb; }}
        .shortcut-icon {{
            width: 64px; height: 64px;
            background: {inputBackground};
            border: 1px solid rgba(0, 0, 0, 0.05);
            border-radius: 20px;
            display: flex; align-items: center;
            justify-content: center;
            margin: 0 auto 12px;
            overflow: hidden;
            box-shadow: 0 4px 12px rgba(0,0,0,0.03);
            transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
        }}
        .shortcut:hover .shortcut-icon {{
            box-shadow: 0 12px 24px rgba(0,0,0,0.06);
            border-color: rgba(37, 99, 235, 0.1);
        }}
        .shortcut-icon img {{ width: 32px; height: 32px; object-fit: contain; }}
        .shortcut-icon .letter {{ 
            font-size: 24px; font-weight: bold; color: #64748b; 
            width: 60px; height: 60px; display: flex; align-items: center; justify-content: center;
        }}
        .shortcut-name {{ font-size: 13px; font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }}
        .footer {{ position: fixed; bottom: 20px; font-size: 12px; color: #94a3b8; font-weight: 500; }}
    </style>
</head>
  <body>
     <div class='watermark-container'></div>
     <div class='container'>
        {logoHtml}
        <h1>鲲穹AI浏览器</h1>
        <div class='search-box'>
            <input type='text' class='search-input' id='searchInput' 
                   placeholder='搜索或输入网址'>
            <button class='search-btn' onclick='doSearch()'>
                <svg viewBox='0 0 24 24'><circle cx='11' cy='11' r='8'></circle><line x1='21' y1='21' x2='16.65' y2='16.65'></line></svg>
            </button>
        </div>
        <div class='shortcuts'>
            {shortcutsHtml}
        </div>
    </div>
    <div class='footer'>轻量 · 快速 · 简洁</div>
    <script>
        const searchInput = document.getElementById('searchInput');
        const searchEngine = '{settings.SearchEngine}';
        searchInput.addEventListener('keydown', e => {{ if (e.key === 'Enter') doSearch(); }});
        function doSearch() {{
            const query = searchInput.value.trim();
            if (!query) return;
            if (query.includes('.') && !query.includes(' ')) {{
                window.location.href = query.startsWith('http') ? query : 'https://' + query;
            }} else {{
                window.location.href = searchEngine + encodeURIComponent(query);
            }}
        }}
    </script>
</body>
</html>";
    }

    private static string GenerateIncognitoPage(BrowserSettings settings)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>InPrivate 浏览</title>
    <style>
        body {{
            background-color: #202124;
            color: #fff;
            font-family: 'Segoe UI', 'Microsoft YaHei UI', sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
        }}
        .container {{
            max-width: 720px;
            padding: 20px;
        }}
        .header {{
            display: flex;
            align-items: center;
            margin-bottom: 24px;
        }}
        .icon {{
            font-size: 48px;
            margin-right: 20px;
        }}
        h1 {{
            font-size: 24px;
            font-weight: 400;
            margin: 0;
        }}
        p {{
            color: #bdc1c6;
            line-height: 1.6;
            margin-bottom: 30px;
        }}
        .cards {{
            display: flex;
            gap: 20px;
        }}
        .card {{
            flex: 1;
            background: rgba(255, 255, 255, 0.05);
            padding: 20px;
            border-radius: 8px;
        }}
        .card h3 {{
            font-size: 16px;
            margin-top: 0;
            margin-bottom: 16px;
            color: #fff;
        }}
        ul {{
            margin: 0;
            padding-left: 20px;
            color: #9aa0a6;
        }}
        li {{
            margin-bottom: 8px;
            font-size: 13px;
        }}
        .search-box {{
            margin-top: 40px;
            position: relative;
        }}
        .search-input {{
            width: 100%;
            padding: 14px 20px;
            border-radius: 24px;
            border: 1px solid #5f6368;
            background: #303134;
            color: #fff;
            font-size: 16px;
            outline: none;
        }}
        .search-input:focus {{
            background: #202124;
            border-color: #8ab4f8;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='icon'>🕶️</div>
            <div>
                <h1>您已进入 InPrivate 浏览模式</h1>
            </div>
        </div>
        <p>现在，您可以私密地浏览网页，其他人使用此设备时将不会看到您的活动。不过，您下载的内容和添加的书签仍会保存在此设备上。</p>
        
        <div class='cards'>
            <div class='card'>
                <h3>鲲穹AI浏览器 不会保存以下信息：</h3>
                <ul>
                    <li>您的浏览历史记录</li>
                    <li>Cookie 和网站数据</li>
                    <li>表单中输入的信息</li>
                </ul>
            </div>
            <div class='card'>
                <h3>以下主体可能仍会看到您的活动：</h3>
                <ul>
                    <li>您访问的网站</li>
                    <li>您的雇主或您所在的学校</li>
                    <li>您的互联网服务提供商</li>
                </ul>
            </div>
        </div>

        <div class='search-box'>
            <input type='text' class='search-input' id='searchInput' placeholder='搜索或输入网址'>
        </div>
    </div>
    <script>
        const searchInput = document.getElementById('searchInput');
        const searchEngine = '{settings.SearchEngine}';
        searchInput.addEventListener('keydown', e => {{ if (e.key === 'Enter') doSearch(); }});
        function doSearch() {{
            const query = searchInput.value.trim();
            if (!query) return;
            if (query.includes('.') && !query.includes(' ')) {{
                window.location.href = query.startsWith('http') ? query : 'https://' + query;
            }} else {{
                window.location.href = searchEngine + encodeURIComponent(query);
            }}
        }}
    </script>
</body>
</html>";
    }
    
    /// <summary>
    /// 生成快捷方式 HTML
    /// </summary>
    private static string GenerateShortcutsHtml(List<FrequentSite>? frequentSites)
    {
        // 如果没有经常访问的网站，显示默认快捷方式
        if (frequentSites == null || frequentSites.Count == 0)
        {
            return @"
            <a href='https://www.baidu.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.baidu.com/favicon.ico' onerror=""this.onerror=null;this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2280%22>B</text></svg>'""><span class='letter'>B</span></div><div class='shortcut-name'>百度</div></a>
            <a href='https://www.bing.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.bing.com/favicon.ico' onerror=""this.onerror=null;this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2280%22>B</text></svg>'""><span class='letter'>B</span></div><div class='shortcut-name'>必应</div></a>
            <a href='https://www.google.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.google.com/favicon.ico' onerror=""this.onerror=null;this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2280%22>G</text></svg>'""><span class='letter'>G</span></div><div class='shortcut-name'>Google</div></a>
            <a href='https://www.bilibili.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.bilibili.com/favicon.ico' onerror=""this.onerror=null;this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2280%22>B</text></svg>'""><span class='letter'>B</span></div><div class='shortcut-name'>哔哩哔哩</div></a>
            <a href='https://www.zhihu.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.zhihu.com/favicon.ico' onerror=""this.onerror=null;this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2280%22>知</text></svg>'""><span class='letter'>知</span></div><div class='shortcut-name'>知乎</div></a>
            <a href='https://github.com' class='shortcut'><div class='shortcut-icon'><img src='https://github.com/favicon.ico' onerror=""this.onerror=null;this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2280%22>G</text></svg>'""><span class='letter'>G</span></div><div class='shortcut-name'>GitHub</div></a>";
        }
        
        var sb = new System.Text.StringBuilder();
        foreach (var site in frequentSites)
        {
            var title = Escape(site.Title);
            var url = Escape(site.Url);
            var firstChar = GetFirstChar(site.Title, site.Domain);
            // 直接使用网站的 favicon.ico
            var faviconUrl = $"https://{Escape(site.Domain)}/favicon.ico";
            
            sb.AppendLine($@"
            <a href='{url}' class='shortcut'>
                <div class='shortcut-icon'>
                    <img src='{faviconUrl}' onerror=""this.onerror=null;this.style.display='none';this.nextElementSibling.style.display='flex'"">
                    <span class='letter' style='display:none'>{firstChar}</span>
                </div>
                <div class='shortcut-name'>{title}</div>
            </a>");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 获取网站首字母用于显示
    /// </summary>
    private static string GetFirstChar(string title, string domain)
    {
        if (!string.IsNullOrEmpty(title))
        {
            var c = title[0];
            if (char.IsLetter(c))
                return char.ToUpper(c).ToString();
            if (c >= 0x4e00 && c <= 0x9fff) // 中文字符
                return c.ToString();
        }
        
        // 使用域名首字母
        var d = domain.StartsWith("www.") ? domain[4..] : domain;
        return d.Length > 0 ? char.ToUpper(d[0]).ToString() : "?";
    }
    
    private static string GetSearchEngineName(string searchEngine)
    {
        if (searchEngine.Contains("baidu")) return "百度";
        if (searchEngine.Contains("bing")) return "必应";
        if (searchEngine.Contains("google")) return "Google";
        return "搜索引擎";
    }
    
    #endregion
    
    #region 设置页面
    
    /// <summary>
    /// 生成设置页面 HTML
    /// </summary>
    public static string GenerateSettingsPage(BrowserSettings settings)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>设置</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif; background: #f5f5f5; color: #333; }}
        .container {{ display: flex; min-height: 100vh; }}
        .sidebar {{ 
            width: 200px; 
            background: #fff; 
            border-right: 1px solid #e0e0e0; 
            padding: 20px 0;
            position: sticky;
            top: 0;
            height: 100vh;
            flex-shrink: 0;
        }}
        .sidebar h2 {{ padding: 10px 20px; font-size: 18px; color: #333; margin-bottom: 10px; }}
        .nav-item {{ padding: 12px 20px; cursor: pointer; color: #666; transition: all 0.2s; }}
        .nav-item:hover {{ background: #f0f0f0; }}
        .nav-item.active {{ background: #e8f0fe; color: #1a73e8; border-left: 3px solid #1a73e8; }}
        .content {{ flex: 1; padding: 30px 40px; max-width: 800px; }}
        .content h1 {{ font-size: 24px; margin-bottom: 30px; font-weight: normal; }}
        .section {{ background: #fff; border-radius: 8px; padding: 20px; margin-bottom: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
        .section h3 {{ font-size: 14px; color: #666; margin-bottom: 15px; text-transform: uppercase; }}
        .setting-item {{ display: flex; justify-content: space-between; align-items: center; padding: 12px 0; border-bottom: 1px solid #f0f0f0; }}
        .setting-item:last-child {{ border-bottom: none; }}
        .setting-label {{ font-size: 14px; }}
        .setting-desc {{ font-size: 12px; color: #888; margin-top: 4px; }}
        .toggle {{ position: relative; width: 44px; height: 24px; }}
        .toggle input {{ opacity: 0; width: 0; height: 0; }}
        .toggle .slider {{ position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; background: #ccc; border-radius: 24px; transition: 0.3s; }}
        .toggle .slider:before {{ position: absolute; content: ''; height: 18px; width: 18px; left: 3px; bottom: 3px; background: white; border-radius: 50%; transition: 0.3s; }}
        .toggle input:checked + .slider {{ background: #1a73e8; }}
        .toggle input:checked + .slider:before {{ transform: translateX(20px); }}
        select, input[type='text'] {{ padding: 8px 12px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px; }}
        select {{ min-width: 150px; }}
        input[type='text'] {{ width: 300px; }}
        .btn {{ 
            padding: 10px 20px; 
            border: none; 
            border-radius: 4px; 
            cursor: pointer; 
            font-size: 14px; 
            min-height: 44px; 
            min-width: 44px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            transition: background 0.2s, transform 0.1s;
            user-select: none;
        }}
        .btn:active {{
            transform: scale(0.98);
            filter: brightness(0.9);
        }}
        .btn-primary {{ background: #1a73e8; color: white; }}
        .btn-primary:hover {{ background: #1557b0; }}
        .btn-secondary {{ background: #f0f0f0; color: #333; }}
        .btn-secondary:hover {{ background: #e0e0e0; }}
        .header-row {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 30px; }}
        .header-row h1 {{ margin-bottom: 0; }}
        .search-box {{ position: relative; }}
        .search-box input {{ width: 200px; padding: 8px 30px 8px 12px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px; }}
        .search-box input:focus {{ outline: none; border-color: #1a73e8; }}
        .search-box .clear-btn {{ position: absolute; right: 8px; top: 50%; transform: translateY(-50%); background: none; border: none; cursor: pointer; color: #999; font-size: 14px; display: none; }}
        .search-box .clear-btn:hover {{ color: #666; }}
        .highlight {{ background-color: #ffeb3b; padding: 0 2px; }}
        .section.hidden {{ display: none; }}
        .no-results {{ text-align: center; padding: 40px; color: #888; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='sidebar'>
            <h2>{AppConstants.AppName}</h2>
            <div class='nav-item' onclick='showSection(""history"")'>历史记录</div>
            <div class='nav-item active' onclick='showSection(""settings"")'>设置</div>
            <div class='nav-item' onclick='showSection(""ai"")'>AI 设置</div>
        </div>
        <div class='content'>
            <div class='header-row'>
                <h1>设置</h1>
                <div class='search-box'>
                    <input type='text' id='settingsSearch' placeholder='在设置中搜索' oninput='searchSettings(this.value)'>
                    <button class='clear-btn' id='clearSearchBtn' onclick='clearSearch()'>✕</button>
                </div>
            </div>
            <div id='noResults' class='no-results' style='display:none;'>没有找到匹配的设置项</div>
            
            <div class='section' id='ai-section'>
                <h3>AI 设置</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>服务模式</div>
                        <div class='setting-desc'>选择 AI 助手的工作模式</div>
                    </div>
                    <select onchange='updateSetting(""aimode"", this.value)'>
                        <option value='0' {(settings.AiServiceMode == 0 ? "selected" : "")}>内置网页模式 (DeepSeek)</option>
                        <option value='1' {(settings.AiServiceMode == 1 ? "selected" : "")}>自定义 API 模式 (支持 OpenAI 兼容接口)</option>
                    </select>
                </div>
                
                <div id='ai-api-settings' style='display:{(settings.AiServiceMode == 1 ? "block" : "none")};'>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>服务商预设</div>
                            <div class='setting-desc'>选择常见的 AI 服务商自动配置</div>
                        </div>
                        <select id='aiProviderSelect' onchange='applyAiProviderPreset(this.value)'>
                            <option value='custom'>自定义</option>
                            <option value='deepseek' {(settings.AiApiBaseUrl?.Contains("deepseek") == true ? "selected" : "")}>DeepSeek</option>
                            <option value='openai' {(settings.AiApiBaseUrl?.Contains("openai") == true ? "selected" : "")}>OpenAI</option>
                            <option value='anthropic' {(settings.AiApiBaseUrl?.Contains("anthropic") == true ? "selected" : "")}>Anthropic (Claude)</option>
                            <option value='groq' {(settings.AiApiBaseUrl?.Contains("groq") == true ? "selected" : "")}>Groq</option>
                            <option value='minimax' {(settings.AiApiBaseUrl?.Contains("minimax") == true || settings.AiApiBaseUrl?.Contains("minimaxi") == true ? "selected" : "")}>MiniMax</option>
                            <option value='dashscope' {(settings.AiApiBaseUrl?.Contains("dashscope") == true || settings.AiApiBaseUrl?.Contains("aliyuncs") == true ? "selected" : "")}>阿里百炼 (DashScope)</option>
                            <option value='ollama' {(settings.AiApiBaseUrl?.Contains("localhost") == true ? "selected" : "")}>Ollama (本地)</option>
                        </select>
                    </div>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>API Key</div>
                            <div class='setting-desc'>您的 API 密钥（将加密保存）</div>
                        </div>
                        <input type='password' id='aiApiKey' value='{settings.AiApiKey}' onchange='updateSetting(""aiapikey"", this.value)' style='padding:8px 12px; border:1px solid #ddd; border-radius:4px; font-size:14px; width:300px;'>
                    </div>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>API Proxy URL</div>
                            <div class='setting-desc'>接口代理地址 (如 https://api.deepseek.com/v1)</div>
                        </div>
                        <input type='text' id='aiApiBaseUrl' value='{settings.AiApiBaseUrl}' onchange='updateSetting(""aiapibaseurl"", this.value)'>
                    </div>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>模型名称</div>
                            <div class='setting-desc'>手动输入或从下方预设选择</div>
                        </div>
                        <div style='display:flex; flex-direction:column; gap:8px;'>
                            <input type='text' id='aiModelName' value='{settings.AiModelName}' onchange='updateSetting(""aimodelname"", this.value)'>
                            <select id='aiModelPreset' onchange='applyAiModelPreset(this.value)' style='width:300px;'>
                                <option value=''>选择预设模型...</option>
                            </select>
                        </div>
                    </div>
                </div>
                
                <div id='ai-web-settings' style='display:{(settings.AiServiceMode == 0 ? "block" : "none")};'>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>AI 网页地址</div>
                            <div class='setting-desc'>内置网页模式使用的 URL</div>
                        </div>
                        <input type='text' value='{settings.AiCustomWebUrl}' onchange='updateSetting(""aicustomweburl"", this.value)'>
                    </div>
                </div>
            </div>

            <div class='section'>
                <h3>启动时</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='startupBehavior' value='0' {(settings.StartupBehavior == 0 ? "checked" : "")} onchange='updateSetting(""startup"", ""0"")' style='margin-right:8px;'>
                        <span>打开新标签页</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='startupBehavior' value='1' {(settings.StartupBehavior == 1 ? "checked" : "")} onchange='updateSetting(""startup"", ""1"")' style='margin-right:8px;'>
                        <span>继续浏览上次关闭浏览器时在看的网页</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='startupBehavior' value='2' {(settings.StartupBehavior == 2 ? "checked" : "")} onchange='updateSetting(""startup"", ""2"")' style='margin-right:8px;'>
                        <span>打开特定网页或一组网页</span>
                        <a href='javascript:void(0)' onclick='openHomePageDialog()' style='color:#0066cc;margin-left:8px;text-decoration:none;'>设置网页</a>
                    </label>
                    <div id='startupPagesDisplay' style='display:{(settings.StartupBehavior == 2 && !string.IsNullOrEmpty(settings.HomePage) && settings.HomePage != "about:newtab" ? "block" : "none")};padding:6px 0 6px 24px;color:#666;font-size:13px;'>
                        当前设置: {Escape(settings.HomePage != "about:newtab" ? settings.HomePage : "")}
                    </div>
                </div>
            </div>
            
            <div class='section'>
                <h3>广告过滤</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='adblockmode' value='0' {(settings.AdBlockMode == 0 ? "checked" : "")} onchange='updateSetting(""adblockmode"", ""0"")' style='margin-right:8px;'>
                        <span>不过滤任何广告</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='adblockmode' value='1' {(settings.AdBlockMode == 1 ? "checked" : "")} onchange='updateSetting(""adblockmode"", ""1"")' style='margin-right:8px;'>
                        <span>仅拦截弹出窗口</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='adblockmode' value='2' {(settings.AdBlockMode == 2 ? "checked" : "")} onchange='updateSetting(""adblockmode"", ""2"")' style='margin-right:8px;'>
                        <span>强力拦截页面广告</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='adblockmode' value='3' {(settings.AdBlockMode == 3 ? "checked" : "")} onchange='updateSetting(""adblockmode"", ""3"")' style='margin-right:8px;'>
                        <span>自定义过滤规则</span>
                    </label>
                </div>
                <div style='display:flex;gap:10px;padding-top:10px;border-top:1px solid #f0f0f0;margin-top:8px;'>
                    <button onclick='openAdBlockExceptions()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>管理例外网站...</button>
                    <button onclick='openAdBlockRulesFolder()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>打开规则文件夹</button>
                </div>
            </div>
            
            <div class='section'>
                <h3>标签</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' {(settings.RightClickCloseTab ? "checked" : "")} onchange='updateSetting(""rightclickclosetab"", this.checked)' style='margin-right:8px;'>
                        <span>右击关闭对应标签（按住Shift右击可显示菜单）</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' {(settings.OpenLinksInBackground ? "checked" : "")} onchange='updateSetting(""openlinksbackground"", this.checked)' style='margin-right:8px;'>
                        <span>点击链接在后台标签打开</span>
                    </label>
                </div>
                <div class='setting-item' style='border-top:1px solid #f0f0f0;margin-top:8px;padding-top:12px;'>
                    <div class='setting-label'>地址栏输入内容时：</div>
                    <select onchange='updateSetting(""addressbarinput"", this.value)' style='padding:6px 10px;border:1px solid #ddd;border-radius:4px;'>
                        <option value='0' {(settings.AddressBarInputMode == 0 ? "selected" : "")}>智能选择打开方式（推荐）</option>
                        <option value='1' {(settings.AddressBarInputMode == 1 ? "selected" : "")}>在当前标签打开</option>
                        <option value='2' {(settings.AddressBarInputMode == 2 ? "selected" : "")}>在新标签打开</option>
                    </select>
                </div>
                <div class='setting-item'>
                    <div class='setting-label'>新打开网页时：</div>
                    <select onchange='updateSetting(""newtabposition"", this.value)' style='padding:6px 10px;border:1px solid #ddd;border-radius:4px;'>
                        <option value='0' {(settings.NewTabPosition == 0 ? "selected" : "")}>当前标签右侧打开</option>
                        <option value='1' {(settings.NewTabPosition == 1 ? "selected" : "")}>所有标签右侧打开</option>
                    </select>
                </div>
            </div>
            
            <div class='section'>
                <h3>搜索引擎</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>默认搜索引擎</div>
                        <div class='setting-desc'>地址栏搜索使用的搜索引擎</div>
                    </div>
                    <div style='display:flex;align-items:center;gap:10px;'>
                        <select id='searchEngine' onchange='updateSetting(""search"", this.value)'>
                            <option value='0' {(settings.AddressBarSearchEngine == 0 ? "selected" : "")}>360</option>
                            <option value='1' {(settings.AddressBarSearchEngine == 1 ? "selected" : "")}>百度</option>
                            <option value='2' {(settings.AddressBarSearchEngine == 2 ? "selected" : "")}>必应</option>
                            <option value='3' {(settings.AddressBarSearchEngine == 3 ? "selected" : "")}>Google</option>
                        </select>
                        <button onclick='openSearchEngineManager()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>管理搜索引擎</button>
                    </div>
                </div>
            </div>
            
            <div class='section'>
                <h3>外观</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='showHomeButton' {(settings.ShowHomeButton ? "checked" : "")} onchange='updateSetting(""homebutton"", this.checked)' style='margin-right:8px;'>
                        <span>显示&quot;主页&quot;按钮</span>
                    </label>
                    <div id='homePageSetting' style='display:{(settings.ShowHomeButton ? "flex" : "none")};align-items:center;padding:6px 0 6px 24px;'>
                        <span style='color:#666;font-size:13px;margin-right:8px;'>{Escape(Forms.HomePageDialog.GetHomePageDisplayText(settings.HomePage))}</span>
                        <a href='#' onclick='openHomePageDialog();return false;' style='color:#1a73e8;text-decoration:none;font-size:13px;'>更改</a>
                    </div>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='showBookmarkBar' {(settings.AlwaysShowBookmarkBar ? "checked" : "")} onchange='updateSetting(""bookmarkbar"", this.checked)' style='margin-right:8px;'>
                        <span>总是显示收藏栏</span>
                    </label>
                </div>
            </div>
            
            <div class='section'>
                <h3>功能</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>鼠标手势</div>
                        <div class='setting-desc'>使用鼠标手势快速执行操作</div>
                    </div>
                    <label class='toggle'>
                        <input type='checkbox' id='mouseGesture' {(settings.EnableMouseGesture ? "checked" : "")} onchange='updateSetting(""gesture"", this.checked)'>
                        <span class='slider'></span>
                    </label>
                </div>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>超级拖拽</div>
                        <div class='setting-desc'>拖拽文字或链接快速搜索或打开</div>
                    </div>
                    <label class='toggle'>
                        <input type='checkbox' id='superDrag' {(settings.EnableSuperDrag ? "checked" : "")} onchange='updateSetting(""superdrag"", this.checked)'>
                        <span class='slider'></span>
                    </label>
                </div>
            </div>
            
            <div class='section'>
                <h3>下载</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>下载位置</div>
                        <div class='setting-desc'>文件下载的默认保存位置</div>
                    </div>
                    <div style='display:flex;gap:8px;align-items:center'>
                        <input type='text' id='downloadPath' value='{Escape(settings.DownloadPath)}' onchange='updateSetting(""downloadpath"", this.value)' style='flex:1'>
                        <button class='btn btn-secondary' onclick='browseDownloadPath()'>浏览...</button>
                    </div>
                </div>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>下载前询问保存位置</div>
                        <div class='setting-desc'>每次下载前询问文件保存位置</div>
                    </div>
                    <label class='toggle'>
                        <input type='checkbox' id='askDownload' {(settings.AskDownloadLocation ? "checked" : "")} onchange='updateSetting(""askdownload"", this.checked)'>
                        <span class='slider'></span>
                    </label>
                </div>
            </div>
            
            <div class='section'>
                <h3>用户数据</h3>
                <div style='padding:12px 0;'>
                    <button onclick='openImportData()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>导入收藏和设置...</button>
                </div>
            </div>
            
            <div class='section'>
                <h3>网页设置</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='smoothScrolling' {(settings.EnableSmoothScrolling ? "checked" : "")} onchange='updateSetting(""smoothscrolling"", this.checked)' style='margin-right:8px;'>
                        <span>启用网页平滑滚动效果（重启浏览器后生效）</span>
                    </label>
                </div>
            </div>
            
            <div class='section'>
                <h3>自定义缓存</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>自定义缓存目录位置:</div>
                    </div>
                    <div style='display:flex;gap:8px;align-items:center'>
                        <input type='text' id='cachePath' value='{Escape(GetCachePath(settings))}' readonly style='flex:1;background:#f9f9f9;'>
                        <button class='btn btn-secondary' onclick='changeCachePath()'>更改...</button>
                    </div>
                </div>
                <div style='padding:8px 0;color:#888;font-size:13px;'>
                    更改后，将清空现有缓存，重启生效
                    <a href='#' onclick='openCacheDir();return false;' style='color:#1a73e8;text-decoration:none;margin-left:10px;'>打开缓存目录</a>
                    <a href='#' onclick='resetCachePath();return false;' style='color:#1a73e8;text-decoration:none;margin-left:10px;'>设回默认</a>
                </div>
            </div>
            
            <div class='section'>
                <h3>默认浏览器</h3>
                <div style='padding:12px 0;'>
                    <button onclick='setAsDefaultBrowser()' style='padding:10px 20px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>将鲲穹AI浏览器设置为默认浏览器</button>
                </div>
                <div id='defaultBrowserStatus' style='font-size:13px;color:#666;'></div>
            </div>
            
            <div class='section'>
                <h3>隐私设置</h3>
                <div style='display:flex;gap:10px;padding:12px 0;'>
                    <button onclick='openContentSettings()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>内容设置...</button>
                    <button onclick='openClearBrowsingData()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>清除浏览数据...</button>
                </div>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='crashUpload' {(settings.EnableCrashUpload ? "checked" : "")} onchange='updateSetting(""crashupload"", this.checked)' style='margin-right:8px;'>
                        <span>开启崩溃上传</span>
                    </label>
                    <div style='font-size:12px;color:#888;margin-left:24px;'>发生崩溃时自动上传错误报告以帮助改进浏览器</div>
                </div>
            </div>
            
            <div class='section'>
                <h3>密码和表单</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='enableAutofill' {(settings.EnableAutofill ? "checked" : "")} onchange='updateSetting(""enableautofill"", this.checked)' style='margin-right:8px;'>
                        <span>启用自动填充功能后，只需点击一次即可填写网络表单。</span>
                        <a href='#' onclick='openAutofillSettings();return false;' style='color:#1a73e8;text-decoration:none;margin-left:8px;'>管理自动填充设置</a>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='savePasswords' {(settings.SavePasswords ? "checked" : "")} onchange='updateSetting(""savepasswords"", this.checked)' style='margin-right:8px;'>
                        <span>提示我保存在网页上输入的密码。</span>
                        <a href='#' onclick='openPasswordManager();return false;' style='color:#1a73e8;text-decoration:none;margin-left:8px;'>管理已保存的密码</a>
                    </label>
                </div>
            </div>
            
            <div class='section'>
                <h3>网络内容</h3>
                <div class='setting-item'>
                    <div class='setting-label'>字号:</div>
                    <div style='display:flex;align-items:center;gap:10px;'>
                        <select id='fontSize' onchange='updateSetting(""fontsize"", this.value)' style='padding:6px 10px;border:1px solid #ddd;border-radius:4px;min-width:100px;'>
                            <option value='0' {(settings.FontSize == 0 ? "selected" : "")}>极小</option>
                            <option value='1' {(settings.FontSize == 1 ? "selected" : "")}>小</option>
                            <option value='2' {(settings.FontSize == 2 ? "selected" : "")}>中</option>
                            <option value='3' {(settings.FontSize == 3 ? "selected" : "")}>大</option>
                            <option value='4' {(settings.FontSize == 4 ? "selected" : "")}>极大</option>
                        </select>
                        <button onclick='openFontSettings()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>自定义字体...</button>
                    </div>
                </div>
                <div class='setting-item'>
                    <div class='setting-label'>网页缩放:</div>
                    <select id='pageZoom' onchange='updateSetting(""pagezoom"", this.value)' style='padding:6px 10px;border:1px solid #ddd;border-radius:4px;min-width:100px;'>
                        <option value='50' {(settings.PageZoom == 50 ? "selected" : "")}>50%</option>
                        <option value='75' {(settings.PageZoom == 75 ? "selected" : "")}>75%</option>
                        <option value='90' {(settings.PageZoom == 90 ? "selected" : "")}>90%</option>
                        <option value='100' {(settings.PageZoom == 100 ? "selected" : "")}>100%</option>
                        <option value='110' {(settings.PageZoom == 110 ? "selected" : "")}>110%</option>
                        <option value='125' {(settings.PageZoom == 125 ? "selected" : "")}>125%</option>
                        <option value='150' {(settings.PageZoom == 150 ? "selected" : "")}>150%</option>
                        <option value='175' {(settings.PageZoom == 175 ? "selected" : "")}>175%</option>
                        <option value='200' {(settings.PageZoom == 200 ? "selected" : "")}>200%</option>
                    </select>
                </div>
            </div>
            
            <div class='section'>
                <h3>网络</h3>
                <div style='padding:8px 0;'>
                    <div style='font-size:13px;color:#666;margin-bottom:10px;'>鲲穹AI浏览器会使用您计算机的系统代理设置连接到网络。</div>
                    <button onclick='openProxySettings()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>更改代理服务器设置...</button>
                </div>
            </div>
            
            <div class='section'>
                <h3>HTTPS/SSL</h3>
                <div style='padding:8px 0;'>
                    <button onclick='openCertificateManager()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>管理证书...</button>
                </div>
            </div>
            
            <div style='margin-top: 20px;'>
                <button class='btn btn-secondary' onclick='resetSettings()'>恢复默认设置</button>
            </div>
        </div>
    </div>
    <script>
        function updateSetting(key, value) {{
            window.chrome.webview.postMessage({{ action: 'updateSetting', key: key, value: value }});

            // AI 模式切换时动态显示/隐藏配置项
            if (key === 'aimode') {{
                var apiSettings = document.getElementById('ai-api-settings');
                var webSettings = document.getElementById('ai-web-settings');
                if (apiSettings) apiSettings.style.display = (value == '1') ? 'block' : 'none';
                if (webSettings) webSettings.style.display = (value == '0') ? 'block' : 'none';
            }}
        }}

        var aiPresets = {{
            'deepseek': {{
                baseUrl: 'https://api.deepseek.com/v1',
                models: ['deepseek-chat', 'deepseek-reasoner']
            }},
            'openai': {{
                baseUrl: 'https://api.openai.com/v1',
                models: ['gpt-4o', 'gpt-4-turbo', 'gpt-3.5-turbo']
            }},
            'anthropic': {{
                baseUrl: 'https://api.anthropic.com/v1',
                models: ['claude-3-5-sonnet-20240620', 'claude-3-opus-20240229']
            }},
            'groq': {{
                baseUrl: 'https://api.groq.com/openai/v1',
                models: ['llama3-70b-8192', 'mixtral-8x7b-32768']
            }},
            'minimax': {{
                baseUrl: 'https://api.minimaxi.com/v1',
                models: ['MiniMax-M2.1', 'MiniMax-M2.1-lightning', 'MiniMax-M2']
            }},
            'dashscope': {{
                baseUrl: 'https://dashscope.aliyuncs.com/compatible-mode/v1',
                models: [
                    {{ id: 'qwen3-max', name: '通义千问 3-Max (qwen3-max)' }},
                    {{ id: 'qwen3-max-latest', name: '通义千问 3-Max 最新版 (qwen3-max-latest)' }},
                    {{ id: 'qwen-max', name: '通义千问 Max (qwen-max)' }},
                    {{ id: 'qwen-max-latest', name: '通义千问 Max 最新版 (qwen-max-latest)' }},
                    {{ id: 'qwen-plus', name: '通义千问 Plus (qwen-plus)' }},
                    {{ id: 'qwen-plus-latest', name: '通义千问 Plus 最新版 (qwen-plus-latest)' }},
                    {{ id: 'qwen-turbo', name: '通义千问 Turbo (qwen-turbo)' }},
                    {{ id: 'qwen-turbo-latest', name: '通义千问 Turbo 最新版 (qwen-turbo-latest)' }},
                    {{ id: 'qwen-long', name: '通义千问 Long (qwen-long)' }},
                    {{ id: 'qwen-long-latest', name: '通义千问 Long 最新版 (qwen-long-latest)' }},
                    {{ id: 'qwen-flash', name: '通义千问 Flash (qwen-flash)' }},
                    {{ id: 'qwen-coder-plus', name: '通义千问 Coder Plus (qwen-coder-plus)' }},
                    {{ id: 'qwen-coder-turbo', name: '通义千问 Coder Turbo (qwen-coder-turbo)' }},
                    {{ id: 'qwq-plus', name: 'QwQ Plus (qwq-plus)' }},
                    {{ id: 'qwq-plus-latest', name: 'QwQ Plus 最新版 (qwq-plus-latest)' }}
                ]
            }},
            'ollama': {{
                baseUrl: 'http://localhost:11434/v1',
                models: ['llama3', 'qwen2', 'gemma']
            }}
        }};

        function applyAiProviderPreset(provider) {{
            var baseUrlInput = document.getElementById('aiApiBaseUrl');
            var modelPresetSelect = document.getElementById('aiModelPreset');
            
            if (provider === 'custom') {{
                modelPresetSelect.innerHTML = '<option value="">选择预设模型...</option>';
                return;
            }}

            var preset = aiPresets[provider];
            if (preset) {{
                baseUrlInput.value = preset.baseUrl;
                updateSetting('aiapibaseurl', preset.baseUrl);

                modelPresetSelect.innerHTML = '<option value="">选择预设模型...</option>';
                preset.models.forEach(function(model) {{
                    var option = document.createElement('option');
                    if (typeof model === 'object') {{
                        option.value = model.id;
                        option.textContent = model.name;
                    }} else {{
                        option.value = model;
                        option.textContent = model;
                    }}
                    modelPresetSelect.appendChild(option);
                }});
                
                // 默认选择第一个模型
                if (preset.models.length > 0) {{
                    var firstModel = preset.models[0];
                    var firstModelId = typeof firstModel === 'object' ? firstModel.id : firstModel;
                    applyAiModelPreset(firstModelId);
                    modelPresetSelect.value = firstModelId;
                }}
            }}
        }}

        function applyAiModelPreset(model) {{
            if (!model) return;
            var modelInput = document.getElementById('aiModelName');
            modelInput.value = model;
            updateSetting('aimodelname', model);
        }}

        // 初始化 AI 模型下拉框
        function initAiPresets() {{
            var provider = document.getElementById('aiProviderSelect')?.value;
            if (provider && provider !== 'custom') {{
                var preset = aiPresets[provider];
                var modelPresetSelect = document.getElementById('aiModelPreset');
                var currentModel = document.getElementById('aiModelName')?.value;
                
                if (preset && modelPresetSelect) {{
                    modelPresetSelect.innerHTML = '<option value="">选择预设模型...</option>';
                    preset.models.forEach(function(m) {{
                        var option = document.createElement('option');
                        var modelId = typeof m === 'object' ? m.id : m;
                        var modelName = typeof m === 'object' ? m.name : m;
                        
                        option.value = modelId;
                        option.textContent = modelName;
                        if (modelId === currentModel) option.selected = true;
                        modelPresetSelect.appendChild(option);
                    }});
                }}
            }}
        }}
        setTimeout(initAiPresets, 500);
        function resetSettings() {{
            if (confirm('确定要恢复所有设置为默认值吗？')) {{
                window.chrome.webview.postMessage({{ action: 'resetSettings' }});
            }}
        }}
        function browseDownloadPath() {{
            window.chrome.webview.postMessage({{ action: 'browseDownloadPath' }});
        }}
        function openSearchEngineManager() {{
            window.chrome.webview.postMessage({{ action: 'openSearchEngineManager' }});
        }}
        function openAdBlockExceptions() {{
            window.chrome.webview.postMessage({{ action: 'openAdBlockExceptions' }});
        }}
        function openAdBlockRulesFolder() {{
            window.chrome.webview.postMessage({{ action: 'openAdBlockRulesFolder' }});
        }}
        function openContentSettings() {{
            window.chrome.webview.postMessage({{ action: 'openContentSettings' }});
        }}
        function openClearBrowsingData() {{
            window.chrome.webview.postMessage({{ action: 'openClearBrowsingData' }});
        }}
        function openImportData() {{
            window.chrome.webview.postMessage({{ action: 'openImportData' }});
        }}
        function openAutofillSettings() {{
            window.chrome.webview.postMessage({{ action: 'openAutofillSettings' }});
        }}
        function openPasswordManager() {{
            window.chrome.webview.postMessage({{ action: 'openPasswordManager' }});
        }}
        function changeCachePath() {{
            window.chrome.webview.postMessage({{ action: 'changeCachePath' }});
        }}
        function openCacheDir() {{
            window.chrome.webview.postMessage({{ action: 'openCacheDir' }});
        }}
        function resetCachePath() {{
            if (confirm('确定要将缓存目录设回默认位置吗？\\n\\n这将清空现有缓存，需要重启浏览器后生效。')) {{
                window.chrome.webview.postMessage({{ action: 'resetCachePath' }});
            }}
        }}
        function openHomePageDialog() {{
            window.chrome.webview.postMessage({{ action: 'openHomePageDialog' }});
        }}
        function setAsDefaultBrowser() {{
            window.chrome.webview.postMessage({{ action: 'setAsDefaultBrowser' }});
        }}
        function checkDefaultBrowser() {{
            window.chrome.webview.postMessage({{ action: 'checkDefaultBrowser' }});
        }}
        function openFontSettings() {{
            window.chrome.webview.postMessage({{ action: 'openFontSettings' }});
        }}
        function openProxySettings() {{
            window.chrome.webview.postMessage({{ action: 'openProxySettings' }});
        }}
        function openCertificateManager() {{
            window.chrome.webview.postMessage({{ action: 'openCertificateManager' }});
        }}
        // 页面加载时检查默认浏览器状态（延迟执行避免阻塞）
        setTimeout(function() {{ checkDefaultBrowser(); }}, 100);
        // 监听主页按钮复选框变化，显示/隐藏主页设置
        document.getElementById('showHomeButton')?.addEventListener('change', function() {{
            var homePageSetting = document.getElementById('homePageSetting');
            if (homePageSetting) {{
                homePageSetting.style.display = this.checked ? 'flex' : 'none';
            }}
        }});
        
        // 搜索功能
        var originalSections = [];
        function initSearch() {{
            var sections = document.querySelectorAll('.section');
            sections.forEach(function(section) {{
                originalSections.push({{
                    element: section,
                    html: section.innerHTML,
                    text: section.textContent.toLowerCase()
                }});
            }});
        }}
        initSearch();
        
        function searchSettings(keyword) {{
            var clearBtn = document.getElementById('clearSearchBtn');
            var noResults = document.getElementById('noResults');
            
            if (!keyword || keyword.trim() === '') {{
                clearBtn.style.display = 'none';
                // 恢复所有 section
                originalSections.forEach(function(item) {{
                    item.element.innerHTML = item.html;
                    item.element.classList.remove('hidden');
                }});
                noResults.style.display = 'none';
                return;
            }}
            
            clearBtn.style.display = 'block';
            keyword = keyword.toLowerCase().trim();
            var hasResults = false;
            
            originalSections.forEach(function(item) {{
                if (item.text.includes(keyword)) {{
                    item.element.classList.remove('hidden');
                    // 高亮匹配的文本
                    var html = item.html;
                    var regex = new RegExp('(' + escapeRegex(keyword) + ')', 'gi');
                    // 只高亮文本节点中的内容，避免破坏 HTML 标签
                    html = highlightText(html, keyword);
                    item.element.innerHTML = html;
                    hasResults = true;
                }} else {{
                    item.element.classList.add('hidden');
                }}
            }});
            
            noResults.style.display = hasResults ? 'none' : 'block';
        }}
        
        function highlightText(html, keyword) {{
            // 简单的高亮实现：只高亮可见文本
            var tempDiv = document.createElement('div');
            tempDiv.innerHTML = html;
            highlightNode(tempDiv, keyword);
            return tempDiv.innerHTML;
        }}
        
        function highlightNode(node, keyword) {{
            if (node.nodeType === 3) {{ // 文本节点
                var text = node.textContent;
                var lowerText = text.toLowerCase();
                var index = lowerText.indexOf(keyword.toLowerCase());
                if (index >= 0) {{
                    var before = text.substring(0, index);
                    var match = text.substring(index, index + keyword.length);
                    var after = text.substring(index + keyword.length);
                    
                    var span = document.createElement('span');
                    span.className = 'highlight';
                    span.textContent = match;
                    
                    var parent = node.parentNode;
                    var beforeNode = document.createTextNode(before);
                    var afterNode = document.createTextNode(after);
                    
                    parent.insertBefore(beforeNode, node);
                    parent.insertBefore(span, node);
                    parent.insertBefore(afterNode, node);
                    parent.removeChild(node);
                    
                    // 继续处理剩余文本
                    highlightNode(afterNode, keyword);
                }}
            }} else if (node.nodeType === 1 && node.tagName !== 'SCRIPT' && node.tagName !== 'STYLE') {{
                // 元素节点，递归处理子节点
                var children = Array.from(node.childNodes);
                children.forEach(function(child) {{
                    highlightNode(child, keyword);
                }});
            }}
        }}
        
        function escapeRegex(str) {{
            return str.replace(/[.*+?^${{}}()|[\\]\\\\]/g, '\\\\$&');
        }}
        
        function clearSearch() {{
            var searchInput = document.getElementById('settingsSearch');
            searchInput.value = '';
            searchSettings('');
            searchInput.focus();
        }}
        
        var settingsContent = document.querySelector('.content').innerHTML;
        function showSection(section) {{
            document.querySelectorAll('.nav-item').forEach(function(el) {{ el.classList.remove('active'); }});
            event.target.classList.add('active');
            
            var content = document.querySelector('.content');
            if (section === 'history') {{
                window.chrome.webview.postMessage({{ action: 'getHistory' }});
            }} else if (section === 'settings') {{
                content.innerHTML = settingsContent;
            }} else if (section === 'ai') {{
                content.innerHTML = settingsContent;
                var aiSection = document.getElementById('ai-section');
                if (aiSection) {{
                    aiSection.scrollIntoView({{ behavior: 'smooth' }});
                }}
            }}
        }}
        
        window.chrome.webview.addEventListener('message', function(e) {{
            if (e.data && e.data.action === 'historyData') {{
                showHistoryContent(e.data.items);
            }} else if (e.data && e.data.action === 'downloadPathSelected') {{
                var input = document.getElementById('downloadPath');
                if (input && e.data.path) {{
                    input.value = e.data.path;
                    updateSetting('downloadpath', e.data.path);
                }}
            }} else if (e.data && e.data.action === 'cachePathChanged') {{
                var cacheInput = document.getElementById('cachePath');
                if (cacheInput && e.data.path) {{
                    cacheInput.value = e.data.path;
                }}
            }} else if (e.data && e.data.action === 'defaultBrowserStatus') {{
                var statusDiv = document.getElementById('defaultBrowserStatus');
                if (statusDiv) {{
                    if (e.data.isDefault) {{
                        statusDiv.innerHTML = '<span style=color:#0a0>鲲穹AI浏览器目前是默认浏览器。</span>';
                    }} else {{
                        statusDiv.innerHTML = '鲲穹AI浏览器目前不是默认浏览器。';
                    }}
                }}
            }}
        }});
        
        function showHistoryContent(items) {{
            var content = document.querySelector('.content');
            var html = '<h1>历史记录</h1><div class=section><div style=display:flex;justify-content:space-between;align-items:center;margin-bottom:15px><input type=text id=historySearch placeholder=搜索... style=flex:1;margin-right:10px onkeyup=searchHistory(this.value)><button class=btn onclick=clearHistory()>清除所有</button></div><div id=historyList>';
            if (items && items.length > 0) {{
                for (var i = 0; i < items.length; i++) {{
                    var item = items[i];
                    html += buildHistoryItem(item);
                }}
            }} else {{
                html += '<p style=color:#888;padding:20px;text-align:center>暂无浏览历史记录</p>';
            }}
            html += '</div></div>';
            content.innerHTML = html;
        }}
        
        function buildHistoryItem(item) {{
            var div = document.createElement('div');
            div.className = 'history-item';
            div.style.cssText = 'display:flex;align-items:center;padding:10px 0;border-bottom:1px solid #f0f0f0;cursor:pointer';
            div.setAttribute('data-url', item.url);
            div.onclick = function() {{ navigateTo(this.getAttribute('data-url')); }};
            var faviconHtml = item.favicon ? '<img src=' + item.favicon + ' style=width:16px;height:16px;margin-right:10px;flex-shrink:0 onerror=this.style.display=none>' : '<span style=width:16px;height:16px;margin-right:10px;display:inline-block;background:#ddd;border-radius:2px></span>';
            div.innerHTML = faviconHtml + '<div style=flex:1;overflow:hidden><div style=font-size:14px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis>' + escapeHtml(item.title || item.url) + '</div><div style=font-size:12px;color:#888;white-space:nowrap;overflow:hidden;text-overflow:ellipsis>' + escapeHtml(item.url) + '</div></div><div style=font-size:12px;color:#888;margin-left:10px;white-space:nowrap>' + formatTime(item.visitTime) + '</div>';
            var container = document.createElement('div');
            container.appendChild(div);
            return container.innerHTML;
        }}
        
        function escapeHtml(text) {{
            if (!text) return '';
            var div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }}
        
        function formatTime(timeStr) {{
            var date = new Date(timeStr);
            var now = new Date();
            if (date.toDateString() === now.toDateString()) {{
                return date.getHours() + ':' + String(date.getMinutes()).padStart(2, '0');
            }}
            return (date.getMonth()+1) + '/' + date.getDate() + ' ' + date.getHours() + ':' + String(date.getMinutes()).padStart(2, '0');
        }}
        
        function navigateTo(url) {{
            window.chrome.webview.postMessage({{ action: 'navigate', url: url }});
        }}
        
        function searchHistory(keyword) {{
            window.chrome.webview.postMessage({{ action: 'searchHistory', keyword: keyword }});
        }}
        
        function clearHistory() {{
            if (confirm('确定要清除所有历史记录吗？')) {{
                window.chrome.webview.postMessage({{ action: 'clearHistory' }});
            }}
        }}
    </script>
</body>
</html>";
    }
    
    #endregion
    
    #region 收藏夹管理页面
    
    /// <summary>
    /// 生成收藏夹管理页面
    /// </summary>
    public static string GenerateBookmarksPage()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>收藏夹管理</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif; background: #f5f5f5; color: #333; }
        .container { max-width: 900px; margin: 0 auto; padding: 30px; }
        h1 { font-size: 24px; font-weight: normal; margin-bottom: 20px; color: #333; }
        .toolbar { display: flex; gap: 10px; margin-bottom: 20px; align-items: center; }
        .search-box { flex: 1; position: relative; }
        .search-box input { width: 100%; padding: 10px 40px 10px 15px; border: 1px solid #ddd; border-radius: 6px; font-size: 14px; }
        .search-box input:focus { outline: none; border-color: #1a73e8; }
        .btn { padding: 10px 16px; border: none; border-radius: 6px; cursor: pointer; font-size: 14px; transition: background 0.2s; }
        .btn-primary { background: #1a73e8; color: white; }
        .btn-primary:hover { background: #1557b0; }
        .btn-secondary { background: #f0f0f0; color: #333; }
        .btn-secondary:hover { background: #e0e0e0; }
        .bookmark-list { background: white; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        .bookmark-item { display: flex; align-items: center; padding: 12px 16px; border-bottom: 1px solid #f0f0f0; cursor: pointer; transition: background 0.2s; }
        .bookmark-item:hover { background: #f8f9fa; }
        .bookmark-item:last-child { border-bottom: none; }
        .bookmark-icon { width: 20px; height: 20px; margin-right: 12px; flex-shrink: 0; }
        .bookmark-icon img { width: 16px; height: 16px; }
        .folder-icon { font-size: 18px; }
        .bookmark-info { flex: 1; min-width: 0; }
        .bookmark-title { font-size: 14px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .bookmark-url { font-size: 12px; color: #888; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin-top: 2px; }
        .bookmark-actions { display: none; gap: 8px; }
        .bookmark-item:hover .bookmark-actions { display: flex; }
        .action-btn { padding: 4px 8px; font-size: 12px; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
        .action-btn:hover { background: #f0f0f0; }
        .action-btn.delete:hover { background: #fee; color: #c00; border-color: #fcc; }
        .empty-state { text-align: center; padding: 60px 20px; color: #888; }
        .empty-state .icon { font-size: 48px; margin-bottom: 16px; }
        .breadcrumb { display: flex; align-items: center; gap: 8px; margin-bottom: 15px; font-size: 14px; }
        .breadcrumb a { color: #1a73e8; text-decoration: none; }
        .breadcrumb a:hover { text-decoration: underline; }
        .breadcrumb span { color: #888; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>📚 收藏夹管理</h1>
        
        <div class='toolbar'>
            <div class='search-box'>
                <input type='text' id='searchInput' placeholder='搜索收藏...' onkeyup='searchBookmarks(this.value)'>
            </div>
            <button class='btn btn-primary' onclick='addFolder()'>新建文件夹</button>
            <button class='btn btn-secondary' onclick='exportBookmarks()'>导出收藏</button>
        </div>
        
        <div class='breadcrumb' id='breadcrumb'>
            <a href='#' onclick='loadBookmarks(); return false;'>收藏夹</a>
        </div>
        
        <div class='bookmark-list' id='bookmarkList'>
            <div class='empty-state'>
                <div class='icon'>⏳</div>
                <div>正在加载...</div>
            </div>
        </div>
    </div>
    
    <script>
        var currentFolderId = null;
        var folderStack = [];
        
        function loadBookmarks(folderId) {
            currentFolderId = folderId || null;
            window.chrome.webview.postMessage({ action: 'getBookmarks', folderId: currentFolderId });
        }
        
        function searchBookmarks(keyword) {
            if (keyword.trim()) {
                window.chrome.webview.postMessage({ action: 'searchBookmarks', keyword: keyword });
            } else {
                loadBookmarks(currentFolderId);
            }
        }
        
        function openFolder(id, title) {
            folderStack.push({ id: currentFolderId, title: currentFolderId ? '...' : '收藏夹' });
            loadBookmarks(id);
            updateBreadcrumb(title);
        }
        
        function updateBreadcrumb(title) {
            var html = '<a href=""#"" onclick=""goToRoot(); return false;"">收藏夹</a>';
            if (title) {
                html += ' <span>›</span> <span>' + escapeHtml(title) + '</span>';
            }
            document.getElementById('breadcrumb').innerHTML = html;
        }
        
        function goToRoot() {
            folderStack = [];
            loadBookmarks(null);
            updateBreadcrumb(null);
        }
        
        function navigateTo(url) {
            window.chrome.webview.postMessage({ action: 'navigate', url: url });
        }
        
        function editBookmark(id, title, url) {
            var newTitle = prompt('编辑名称:', title);
            if (newTitle !== null && newTitle.trim()) {
                window.chrome.webview.postMessage({ action: 'updateBookmark', id: id, title: newTitle.trim() });
            }
        }
        
        function deleteBookmark(id, title) {
            if (confirm('确定要删除 ""' + title + '"" 吗？')) {
                window.chrome.webview.postMessage({ action: 'deleteBookmark', id: id });
            }
        }
        
        function addFolder() {
            var name = prompt('输入文件夹名称:');
            if (name && name.trim()) {
                window.chrome.webview.postMessage({ action: 'addFolder', title: name.trim(), parentId: currentFolderId });
            }
        }
        
        function exportBookmarks() {
            window.chrome.webview.postMessage({ action: 'exportBookmarks' });
        }
        
        function escapeHtml(text) {
            if (!text) return '';
            var div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }
        
        function renderBookmarks(items) {
            var list = document.getElementById('bookmarkList');
            if (!items || items.length === 0) {
                list.innerHTML = '<div class=""empty-state""><div class=""icon"">📭</div><div>暂无收藏</div></div>';
                return;
            }
            
            var html = '';
            for (var i = 0; i < items.length; i++) {
                var item = items[i];
                if (item.isFolder) {
                    html += '<div class=""bookmark-item"" ondblclick=""openFolder(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\')"">' +
                        '<div class=""bookmark-icon folder-icon"">📁</div>' +
                        '<div class=""bookmark-info""><div class=""bookmark-title"">' + escapeHtml(item.title) + '</div></div>' +
                        '<div class=""bookmark-actions"">' +
                        '<button class=""action-btn"" onclick=""event.stopPropagation(); editBookmark(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\', \'\')"">编辑</button>' +
                        '<button class=""action-btn delete"" onclick=""event.stopPropagation(); deleteBookmark(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\')"">删除</button>' +
                        '</div></div>';
                } else {
                    var favicon = item.favicon || 'https://www.google.com/s2/favicons?domain=' + encodeURIComponent(new URL(item.url).hostname) + '&sz=16';
                    html += '<div class=""bookmark-item"" ondblclick=""navigateTo(\'' + escapeHtml(item.url) + '\')"">' +
                        '<div class=""bookmark-icon""><img src=""' + favicon + '"" onerror=""this.style.display=\'none\'""></div>' +
                        '<div class=""bookmark-info""><div class=""bookmark-title"">' + escapeHtml(item.title) + '</div>' +
                        '<div class=""bookmark-url"">' + escapeHtml(item.url) + '</div></div>' +
                        '<div class=""bookmark-actions"">' +
                        '<button class=""action-btn"" onclick=""event.stopPropagation(); editBookmark(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\', \'' + escapeHtml(item.url) + '\')"">编辑</button>' +
                        '<button class=""action-btn delete"" onclick=""event.stopPropagation(); deleteBookmark(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\')"">删除</button>' +
                        '</div></div>';
                }
            }
            list.innerHTML = html;
        }
        
        window.chrome.webview.addEventListener('message', function(e) {
            if (e.data && e.data.action === 'bookmarksData') {
                renderBookmarks(e.data.items);
            }
        });
        
        // 初始加载
        loadBookmarks();
    </script>
</body>
</html>";
    }
    
    #endregion
    
    #region 错误页面
    
    public static string GenerateInvalidUrlPage(string url) =>
        GenerateErrorPage("无法访问此网址", "ERR_INVALID_URL",
            $"网址 <strong>{Escape(url)}</strong> 无效或无法解析。",
            new[] { "请检查网址是否拼写正确", "确保网址包含正确的协议（如 https://）", "尝试搜索该网站名称" });
    
    public static string GenerateNetworkErrorPage(string url) =>
        GenerateErrorPage("无法连接到网络", "ERR_NETWORK_DISCONNECTED",
            "无法建立网络连接，请检查您的网络设置。",
            new[] { "检查网络电缆、调制解调器和路由器", "重新连接到 Wi-Fi", "检查防火墙和代理设置" });
    
    public static string GenerateTimeoutPage(string url) =>
        GenerateErrorPage("连接超时", "ERR_CONNECTION_TIMED_OUT",
            $"连接 <strong>{Escape(UrlHelper.GetHost(url))}</strong> 时超时。",
            new[] { "网站可能暂时无法访问或太忙", "请稍后重试", "检查您的网络连接" });
    
    public static string GenerateDnsErrorPage(string url) =>
        GenerateErrorPage("找不到服务器", "ERR_NAME_NOT_RESOLVED",
            $"找不到 <strong>{Escape(UrlHelper.GetHost(url))}</strong> 的服务器 DNS 地址。",
            new[] { "检查网址是否正确", "尝试运行网络诊断", "检查 DNS 设置" });
    
    public static string GenerateConnectionRefusedPage(string url) =>
        GenerateErrorPage("连接被拒绝", "ERR_CONNECTION_REFUSED",
            $"<strong>{Escape(UrlHelper.GetHost(url))}</strong> 拒绝了连接请求。",
            new[] { "网站可能暂时关闭或已永久移动", "检查防火墙和代理设置", "如果您使用代理服务器，请检查代理设置" });
    
    public static string GenerateSslErrorPage(string url) =>
        GenerateErrorPage("您的连接不是私密连接", "ERR_CERT_AUTHORITY_INVALID",
            $"攻击者可能正在试图从 <strong>{Escape(UrlHelper.GetHost(url))}</strong> 窃取您的信息。",
            new[] { "此网站的安全证书存在问题", "建议不要继续访问此网站", "如果您了解风险，可以选择继续" }, true);
    
    public static string GenerateGenericErrorPage(string url, int errorCode, string errorMessage) =>
        GenerateErrorPage("无法访问此网站", $"ERR_FAILED ({errorCode})",
            $"访问 <strong>{Escape(UrlHelper.GetHost(url))}</strong> 时出错：{Escape(errorMessage)}",
            new[] { "请稍后重试", "检查您的网络连接", "检查防火墙和代理设置" });
    
    public static string GenerateFromWebErrorStatus(string url, CoreWebView2WebErrorStatus status) =>
        status switch
        {
            CoreWebView2WebErrorStatus.ConnectionAborted or
            CoreWebView2WebErrorStatus.ConnectionReset or
            CoreWebView2WebErrorStatus.Disconnected => GenerateNetworkErrorPage(url),
            CoreWebView2WebErrorStatus.Timeout => GenerateTimeoutPage(url),
            CoreWebView2WebErrorStatus.HostNameNotResolved => GenerateDnsErrorPage(url),
            CoreWebView2WebErrorStatus.CannotConnect => GenerateConnectionRefusedPage(url),
            CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect or
            CoreWebView2WebErrorStatus.CertificateExpired or
            CoreWebView2WebErrorStatus.CertificateIsInvalid or
            CoreWebView2WebErrorStatus.CertificateRevoked => GenerateSslErrorPage(url),
            _ => GenerateGenericErrorPage(url, (int)status, status.ToString())
        };
    
    private static string GenerateErrorPage(string title, string errorCode, string description, 
        string[] suggestions, bool isWarning = false)
    {
        var color = isWarning ? "#c53929" : "#5f6368";
        var icon = isWarning ? "⚠️" : "😕";
        var suggestionsHtml = string.Join("", suggestions.Select(s => $"<li>{s}</li>"));
        
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>{Escape(title)}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif;
            background: #f8f9fa; color: #202124;
            display: flex; justify-content: center; align-items: center;
            min-height: 100vh; padding: 20px;
        }}
        .container {{ max-width: 600px; text-align: center; }}
        .icon {{ font-size: 72px; margin-bottom: 20px; }}
        h1 {{ font-size: 24px; font-weight: 400; color: {color}; margin-bottom: 16px; }}
        .error-code {{ font-size: 12px; color: #5f6368; margin-bottom: 24px; }}
        .description {{ font-size: 14px; color: #5f6368; line-height: 1.6; margin-bottom: 24px; }}
        .suggestions {{
            text-align: left; background: white; border-radius: 8px;
            padding: 20px 20px 20px 40px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }}
        .suggestions h3 {{ font-size: 14px; font-weight: 500; margin-bottom: 12px; margin-left: -20px; }}
        .suggestions ul {{ font-size: 13px; color: #5f6368; line-height: 1.8; }}
        .retry-btn {{
            margin-top: 24px; padding: 10px 24px; font-size: 14px;
            color: white; background: #1a73e8; border: none; border-radius: 4px;
            cursor: pointer; transition: background 0.2s;
        }}
        .retry-btn:hover {{ background: #1557b0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>{icon}</div>
        <h1>{Escape(title)}</h1>
        <div class='error-code'>{Escape(errorCode)}</div>
        <div class='description'>{description}</div>
        <div class='suggestions'>
            <h3>请尝试以下操作：</h3>
            <ul>{suggestionsHtml}</ul>
        </div>
        <button class='retry-btn' onclick='location.reload()'>重新加载</button>
    </div>
</body>
</html>";
    }
    
    private static string Escape(string text) => WebUtility.HtmlEncode(text ?? "");
    
    private static string GetCachePath(BrowserSettings settings)
    {
        if (settings.UseCustomCachePath && !string.IsNullOrEmpty(settings.CustomCachePath))
        {
            return settings.CustomCachePath;
        }
        return Constants.AppConstants.DefaultCacheFolder;
    }
    
    #endregion
}
