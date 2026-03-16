using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Text;
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
    private static readonly string[] LanguageOptions =
    {
        "zh-CN", "zh_TW", "en", "hi", "es", "fr", "ar", "bn", "pt_BR", "pt",
        "ru", "id", "ur", "de", "ja", "tr", "vi", "ko", "th", "it",
        "fa", "sw", "tl", "ta", "jv", "ms", "ha",
    };

    private static readonly Dictionary<string, string> LanguageLabelFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-CN"] = "Chinese (Simplified)",
        ["zh_TW"] = "Chinese (Traditional)",
        ["en"] = "English",
        ["hi"] = "Hindi",
        ["es"] = "Spanish",
        ["fr"] = "French",
        ["ar"] = "Arabic",
        ["bn"] = "Bengali",
        ["pt_BR"] = "Portuguese (Brazil)",
        ["pt"] = "Portuguese",
        ["ru"] = "Russian",
        ["id"] = "Indonesian",
        ["ur"] = "Urdu",
        ["de"] = "German",
        ["ja"] = "Japanese",
        ["tr"] = "Turkish",
        ["vi"] = "Vietnamese",
        ["ko"] = "Korean",
        ["th"] = "Thai",
        ["it"] = "Italian",
        ["fa"] = "Persian",
        ["sw"] = "Swahili",
        ["tl"] = "Filipino",
        ["ta"] = "Tamil",
        ["jv"] = "Javanese",
        ["ms"] = "Malay",
        ["ha"] = "Hausa",
    };

    private static string BuildLanguageOptionsHtml(string selectedLanguageCode)
    {
        var sb = new StringBuilder();
        var selected = string.IsNullOrWhiteSpace(selectedLanguageCode) ? "auto" : selectedLanguageCode;
        sb.Append($"<option value='auto' {(string.Equals(selected, "auto", StringComparison.OrdinalIgnoreCase) ? "selected" : "")}>{Escape(Localization.T("settings.appearance.language_auto"))}</option>");
        foreach (var code in LanguageOptions)
        {
            var label = ResolveLanguageLabel(code);
            sb.Append($"<option value='{code}' {(string.Equals(selected, code, StringComparison.OrdinalIgnoreCase) ? "selected" : "")}>{Escape(label)}</option>");
        }
        return sb.ToString();
    }

    private static string ResolveLanguageLabel(string languageCode)
    {
        var keySuffix = languageCode.Replace("-", "_", StringComparison.Ordinal);
        var key = $"settings.appearance.language_names.{keySuffix}";
        var localized = Localization.T(key);
        if (!string.Equals(localized, key, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(localized))
            return localized;

        return LanguageLabelFallbacks.TryGetValue(languageCode, out var fallback) ? fallback : languageCode;
    }

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
            <a href='https://www.baidu.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.baidu.com/favicon.ico' onerror=""this.onerror=null;this.style.display='none';this.nextElementSibling.style.display='flex'""><span class='letter' style='display:none'>B</span></div><div class='shortcut-name'>百度</div></a>
            <a href='https://www.bing.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.bing.com/favicon.ico' onerror=""this.onerror=null;this.style.display='none';this.nextElementSibling.style.display='flex'""><span class='letter' style='display:none'>B</span></div><div class='shortcut-name'>必应</div></a>
            <a href='https://www.google.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.google.com/favicon.ico' onerror=""this.onerror=null;this.style.display='none';this.nextElementSibling.style.display='flex'""><span class='letter' style='display:none'>G</span></div><div class='shortcut-name'>Google</div></a>
            <a href='https://www.bilibili.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.bilibili.com/favicon.ico' onerror=""this.onerror=null;this.style.display='none';this.nextElementSibling.style.display='flex'""><span class='letter' style='display:none'>B</span></div><div class='shortcut-name'>哔哩哔哩</div></a>
            <a href='https://www.zhihu.com' class='shortcut'><div class='shortcut-icon'><img src='https://www.zhihu.com/favicon.ico' onerror=""this.onerror=null;this.style.display='none';this.nextElementSibling.style.display='flex'""><span class='letter' style='display:none'>知</span></div><div class='shortcut-name'>知乎</div></a>
            <a href='https://github.com' class='shortcut'><div class='shortcut-icon'><img src='https://github.com/favicon.ico' onerror=""this.onerror=null;this.style.display='none';this.nextElementSibling.style.display='flex'""><span class='letter' style='display:none'>G</span></div><div class='shortcut-name'>GitHub</div></a>";
        }
        
        var sb = new System.Text.StringBuilder();
        foreach (var site in frequentSites)
        {
            var title = Escape(site.Title);
            var url = Escape(site.Url);
            var firstChar = GetFirstChar(site.Title, site.Domain);
            // 鐩存帴浣跨敤缃戠珯鐨?favicon.ico
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
            if (c >= 0x4e00 && c <= 0x9fff) // 涓枃瀛楃
                return c.ToString();
        }
        
        // 浣跨敤鍩熷悕棣栧瓧姣?
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
    
    #region 璁剧疆椤甸潰
    
    /// <summary>
    /// 鐢熸垚璁剧疆椤甸潰 HTML
    /// </summary>
    public static string GenerateSettingsPage(BrowserSettings settings)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>{Localization.T("settings.header.title")}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        :root {{
            --page-bg: radial-gradient(1200px 700px at 15% -10%, #e9f2ff 0%, #f5f8ff 45%, #f3f5f8 100%);
            --card-bg: rgba(255, 255, 255, 0.92);
            --line-color: #dbe4f0;
            --text-main: #1f2a37;
            --text-sub: #6e7c8f;
            --primary: #1d63d8;
            --primary-strong: #1550b6;
            --primary-soft: rgba(29, 99, 216, 0.14);
        }}
        body {{ font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif; background: var(--page-bg); color: var(--text-main); }}
        .container {{ display: flex; min-height: 100vh; position: relative; }}
        .sidebar {{ 
            width: 220px; 
            background: rgba(255, 255, 255, 0.86); 
            border-right: 1px solid var(--line-color); 
            padding: 20px 0;
            position: sticky;
            top: 0;
            height: 100vh;
            flex-shrink: 0;
            backdrop-filter: blur(8px);
        }}
        .sidebar h2 {{ padding: 10px 20px; font-size: 18px; color: var(--text-main); margin-bottom: 10px; }}
        .nav-item {{ padding: 12px 20px; cursor: pointer; color: var(--text-sub); transition: all 0.2s ease; border-left: 3px solid transparent; }}
        .nav-item:hover {{ background: #eef3fb; color: #2f4a6d; }}
        .nav-item.active {{ background: linear-gradient(90deg, var(--primary-soft) 0%, rgba(255,255,255,0) 100%); color: var(--primary); border-left: 3px solid var(--primary); }}
        .content {{ flex: 1; padding: 30px 40px 130px; max-width: 900px; }}
        .content h1 {{ font-size: 24px; margin-bottom: 30px; font-weight: normal; }}
        .section {{ background: var(--card-bg); border-radius: 14px; padding: 20px; margin-bottom: 20px; border: 1px solid var(--line-color); box-shadow: 0 10px 26px rgba(31, 57, 99, 0.08); }}
        .section h3 {{ font-size: 14px; color: #5b6f89; margin-bottom: 15px; text-transform: uppercase; letter-spacing: 0.6px; }}
        .setting-item {{ display: flex; justify-content: space-between; align-items: center; padding: 12px 0; border-bottom: 1px solid #edf2f8; }}
        .setting-item:last-child {{ border-bottom: none; }}
        .setting-label {{ font-size: 14px; }}
        .setting-desc {{ font-size: 12px; color: var(--text-sub); margin-top: 4px; }}
        .toggle {{ position: relative; width: 44px; height: 24px; }}
        .toggle input {{ opacity: 0; width: 0; height: 0; }}
        .toggle .slider {{ position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; background: #ccc; border-radius: 24px; transition: 0.3s; }}
        .toggle .slider:before {{ position: absolute; content: ''; height: 18px; width: 18px; left: 3px; bottom: 3px; background: white; border-radius: 50%; transition: 0.3s; }}
        .toggle input:checked + .slider {{ background: var(--primary); }}
        .toggle input:checked + .slider:before {{ transform: translateX(20px); }}
        select, input[type='text'] {{ padding: 8px 12px; border: 1px solid #cfdae8; border-radius: 8px; font-size: 14px; background: #fff; }}
        select {{ min-width: 150px; }}
        input[type='text'] {{ width: 300px; }}
        .btn {{ 
            padding: 10px 20px; 
            border: none; 
            border-radius: 10px; 
            cursor: pointer; 
            font-size: 14px; 
            min-height: 44px; 
            min-width: 44px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            transition: all 0.2s ease, transform 0.1s;
            user-select: none;
        }}
        .btn:active {{
            transform: scale(0.98);
            filter: brightness(0.9);
        }}
        .btn-primary {{ background: linear-gradient(135deg, var(--primary) 0%, #2f7be9 100%); color: white; box-shadow: 0 8px 18px rgba(29, 99, 216, 0.28); }}
        .btn-primary:hover {{ background: linear-gradient(135deg, #2c72e0 0%, #1d63d8 100%); transform: translateY(-1px); }}
        .btn-primary:disabled {{ opacity: 0.55; cursor: not-allowed; box-shadow: none; transform: none; }}
        .btn-secondary {{ background: #eef2f7; color: #2d3748; border: 1px solid #d8e1ee; }}
        .btn-secondary:hover {{ background: #e2e9f3; }}
        .header-row {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 30px; }}
        .header-row h1 {{ margin-bottom: 0; }}
        .search-box {{ position: relative; }}
        .search-box input {{ width: 220px; padding: 8px 30px 8px 12px; border: 1px solid #cfdae8; border-radius: 8px; font-size: 14px; }}
        .search-box input:focus {{ outline: none; border-color: var(--primary); box-shadow: 0 0 0 3px var(--primary-soft); }}
        .search-box .clear-btn {{ position: absolute; right: 8px; top: 50%; transform: translateY(-50%); background: none; border: none; cursor: pointer; color: #999; font-size: 14px; display: none; }}
        .search-box .clear-btn:hover {{ color: #666; }}
        .highlight {{ background-color: #ffeb3b; padding: 0 2px; }}
        .section.hidden {{ display: none; }}
        .no-results {{ text-align: center; padding: 40px; color: var(--text-sub); }}
        .settings-action-bar {{
            position: fixed;
            left: 236px;
            right: 24px;
            bottom: 16px;
            background: rgba(255, 255, 255, 0.88);
            border: 1px solid #d4dfef;
            border-radius: 14px;
            box-shadow: 0 14px 30px rgba(27, 52, 90, 0.18);
            backdrop-filter: blur(10px);
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 10px;
            padding: 12px 16px;
            z-index: 999;
        }}
        .save-status {{ font-size: 13px; color: #617389; }}
        .save-status.dirty {{ color: var(--primary); font-weight: 600; }}
        .save-btn {{ min-width: 150px; }}
        @media (max-width: 960px) {{
            .sidebar {{ width: 0; overflow: hidden; padding: 0; border-right: none; }}
            .content {{ max-width: none; padding: 22px 16px 130px; }}
            .settings-action-bar {{ left: 16px; right: 16px; }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='sidebar'>
            <h2>{AppConstants.AppName}</h2>
            <div class='nav-item' onclick='showSection(""history"")'>{Localization.T("settings.sidebar.history")}</div>
            <div class='nav-item active' onclick='showSection(""settings"")'>{Localization.T("settings.sidebar.settings")}</div>
            <div class='nav-item' onclick='showSection(""ai"")'>{Localization.T("settings.sidebar.ai")}</div>
        </div>
        <div class='content'>
            <div class='header-row'>
                <h1>{Localization.T("settings.header.title")}</h1>
                <div class='search-box'>
                    <input type='text' id='settingsSearch' placeholder='{Localization.T("settings.header.search_placeholder")}' oninput='searchSettings(this.value)'>
                    <button class='clear-btn' id='clearSearchBtn' onclick='clearSearch()'>&times;</button>
                </div>
            </div>
            <div id='noResults' class='no-results' style='display:none;'>{Localization.T("settings.no_results")}</div>
            
            <div class='section' id='ai-section'>
                <h3>{Localization.T("ai.settings.title")}</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>{Localization.T("ai.mode.title")}</div>
                        <div class='setting-desc'>{Localization.T("ai.settings.subtitle")}</div>
                    </div>
                    <select onchange='updateSetting(""aimode"", this.value)'>
                        <option value='0' {(settings.AiServiceMode == 0 ? "selected" : "")}>{Localization.T("ai.mode.web")}</option>
                        <option value='1' {(settings.AiServiceMode == 1 ? "selected" : "")}>{Localization.T("ai.mode.api")}</option>
                    </select>
                </div>

                <div id='ai-api-settings' style='display:{(settings.AiServiceMode == 1 ? "block" : "none")};'>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>{Localization.T("ai.api.provider")}</div>
                            <div class='setting-desc'>{Localization.T("ai.tip.compatible")}</div>
                        </div>
                        <select id='aiProviderSelect' onchange='applyAiProviderPreset(this.value)'>
                            <option value='custom'>{Localization.T("ai.provider.custom")}</option>
                            <option value='deepseek' {(settings.AiApiBaseUrl?.Contains("deepseek") == true ? "selected" : "")}>{Localization.T("ai.provider.deepseek")}</option>
                            <option value='volcengine' {(settings.AiApiBaseUrl?.Contains("volces.com") == true || settings.AiApiBaseUrl?.Contains("volcengine") == true ? "selected" : "")}>{Localization.T("ai.provider.volcengine")}</option>
                            <option value='openai' {(settings.AiApiBaseUrl?.Contains("openai") == true ? "selected" : "")}>{Localization.T("ai.provider.openai")}</option>
                            <option value='anthropic' {(settings.AiApiBaseUrl?.Contains("anthropic") == true ? "selected" : "")}>{Localization.T("ai.provider.anthropic")}</option>
                            <option value='groq' {(settings.AiApiBaseUrl?.Contains("groq") == true ? "selected" : "")}>{Localization.T("ai.provider.groq")}</option>
                            <option value='minimax' {(settings.AiApiBaseUrl?.Contains("minimax") == true || settings.AiApiBaseUrl?.Contains("minimaxi") == true ? "selected" : "")}>{Localization.T("ai.provider.minimax")}</option>
                            <option value='dashscope' {(settings.AiApiBaseUrl?.Contains("dashscope") == true || settings.AiApiBaseUrl?.Contains("aliyuncs") == true ? "selected" : "")}>{Localization.T("ai.provider.dashscope")}</option>
                            <option value='ollama' {(settings.AiApiBaseUrl?.Contains("localhost") == true ? "selected" : "")}>{Localization.T("ai.provider.ollama_local")}</option>
                        </select>
                    </div>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>{Localization.T("ai.api.key")}</div>
                            <div class='setting-desc'>{Localization.T("ai.api.key_stored_local")}</div>
                        </div>
                        <input type='password' id='aiApiKey' value='{settings.AiApiKey}' onchange='updateSetting(""aiapikey"", this.value)' style='padding:8px 12px; border:1px solid #ddd; border-radius:4px; font-size:14px; width:300px;'>
                    </div>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>{Localization.T("ai.api.base")}</div>
                            <div class='setting-desc'>{Localization.T("ai.api.base_example")}</div>
                        </div>
                        <input type='text' id='aiApiBaseUrl' value='{settings.AiApiBaseUrl}' onchange='updateSetting(""aiapibaseurl"", this.value)'>
                    </div>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>{Localization.T("ai.api.model_name")}</div>
                            <div class='setting-desc'>{Localization.T("ai.api.model_desc")}</div>
                        </div>
                        <div style='display:flex; flex-direction:column; gap:8px;'>
                            <input type='text' id='aiModelName' value='{settings.AiModelName}' onchange='updateSetting(""aimodelname"", this.value)'>
                            <select id='aiModelPreset' onchange='applyAiModelPreset(this.value)' style='width:300px;'>
                                <option value=''>{Localization.T("ai.api.select_preset_model")}</option>
                            </select>
                        </div>
                    </div>
                </div>

                <div id='ai-web-settings' style='display:{(settings.AiServiceMode == 0 ? "block" : "none")};'>
                    <div class='setting-item'>
                        <div>
                            <div class='setting-label'>{Localization.T("ai.web.url")}</div>
                            <div class='setting-desc'>{Localization.T("ai.web.title")}</div>
                        </div>
                        <input type='text' value='{settings.AiCustomWebUrl}' onchange='updateSetting(""aicustomweburl"", this.value)'>
                    </div>
                </div>
            </div>

            <div class='section'>
                <h3>{Localization.T("settings.startup.title")}</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='startupBehavior' value='0' {(settings.StartupBehavior == 0 ? "checked" : "")} onchange='updateSetting(""startup"", ""0"")' style='margin-right:8px;'>
                        <span>{Localization.T("settings.startup.newtab")}</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='startupBehavior' value='1' {(settings.StartupBehavior == 1 ? "checked" : "")} onchange='updateSetting(""startup"", ""1"")' style='margin-right:8px;'>
                        <span>{Localization.T("settings.startup.continue_last")}</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='startupBehavior' value='2' {(settings.StartupBehavior == 2 ? "checked" : "")} onchange='updateSetting(""startup"", ""2"")' style='margin-right:8px;'>
                        <span>{Localization.T("settings.startup.specific_pages")}</span>
                        <a href='javascript:void(0)' onclick='openHomePageDialog()' style='color:#0066cc;margin-left:8px;text-decoration:none;'>{Localization.T("settings.startup.manage_pages")}</a>
                    </label>
                    <div id='startupPagesDisplay' style='display:{(settings.StartupBehavior == 2 && !string.IsNullOrEmpty(settings.HomePage) && settings.HomePage != "about:newtab" ? "block" : "none")};padding:6px 0 6px 24px;color:#666;font-size:13px;'>
                        {Localization.T("settings.startup.current_setting")} {Escape(settings.HomePage != "about:newtab" ? settings.HomePage : "")}
                    </div>
                </div>
            </div>

            <div class='section'>
                <h3>{Localization.T("settings.adblock.title")}</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='adblockmode' value='0' {(settings.AdBlockMode == 0 ? "checked" : "")} onchange='updateSetting(""adblockmode"", ""0"")' style='margin-right:8px;'>
                        <span>{Localization.T("settings.adblock.none")}</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='adblockmode' value='1' {(settings.AdBlockMode == 1 ? "checked" : "")} onchange='updateSetting(""adblockmode"", ""1"")' style='margin-right:8px;'>
                        <span>{Localization.T("settings.adblock.popup")}</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='adblockmode' value='2' {(settings.AdBlockMode == 2 ? "checked" : "")} onchange='updateSetting(""adblockmode"", ""2"")' style='margin-right:8px;'>
                        <span>{Localization.T("settings.adblock.aggressive")}</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='radio' name='adblockmode' value='3' {(settings.AdBlockMode == 3 ? "checked" : "")} onchange='updateSetting(""adblockmode"", ""3"")' style='margin-right:8px;'>
                        <span>{Localization.T("settings.adblock.custom")}</span>
                    </label>
                </div>
                <div style='display:flex;gap:10px;padding-top:10px;border-top:1px solid #f0f0f0;margin-top:8px;'>
                    <button onclick='openAdBlockExceptions()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.adblock.custom_link")}...</button>
                    <button onclick='openAdBlockRulesFolder()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.adblock.manage_rules")}</button>
                </div>
            </div>

            <div class='section'>
                <h3>{Localization.T("settings.tabs.title")}</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' {(settings.RightClickCloseTab ? "checked" : "")} onchange='updateSetting(""rightclickclosetab"", this.checked)' style='margin-right:8px;'>
                        <span>{Localization.T("settings.tabs.right_click_close_desc")}</span>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' {(settings.OpenLinksInBackground ? "checked" : "")} onchange='updateSetting(""openlinksbackground"", this.checked)' style='margin-right:8px;'>
                        <span>{Localization.T("settings.tabs.open_links_background")}</span>
                    </label>
                </div>
                <div class='setting-item' style='border-top:1px solid #f0f0f0;margin-top:8px;padding-top:12px;'>
                    <div class='setting-label'>{Localization.T("settings.tabs.input_mode_label")}</div>
                    <select onchange='updateSetting(""addressbarinput"", this.value)' style='padding:6px 10px;border:1px solid #ddd;border-radius:4px;'>
                        <option value='0' {(settings.AddressBarInputMode == 0 ? "selected" : "")}>{Localization.T("settings.tabs.input_mode.instant")}</option>
                        <option value='1' {(settings.AddressBarInputMode == 1 ? "selected" : "")}>{Localization.T("settings.tabs.input_mode.enter")}</option>
                        <option value='2' {(settings.AddressBarInputMode == 2 ? "selected" : "")}>{Localization.T("settings.tabs.input_mode.new_tab")}</option>
                    </select>
                </div>
                <div class='setting-item'>
                    <div class='setting-label'>{Localization.T("settings.tabs.open_new")}</div>
                    <select onchange='updateSetting(""newtabposition"", this.value)' style='padding:6px 10px;border:1px solid #ddd;border-radius:4px;'>
                        <option value='0' {(settings.NewTabPosition == 0 ? "selected" : "")}>{Localization.T("settings.tabs.new_tab_position.right")}</option>
                        <option value='1' {(settings.NewTabPosition == 1 ? "selected" : "")}>{Localization.T("settings.tabs.new_tab_position.last")}</option>
                    </select>
                </div>
            </div>

            <div class='section'>
                <h3>{Localization.T("settings.search.title")}</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>{Localization.T("settings.search.default_label")}</div>
                        <div class='setting-desc'>{Localization.T("settings.search.desc")}</div>
                    </div>
                    <div style='display:flex;align-items:center;gap:10px;'>
                        <select id='searchEngine' onchange='updateSetting(""search"", this.value)'>
                            <option value='0' {(settings.AddressBarSearchEngine == 0 ? "selected" : "")}>{Localization.T("settings.search.options.360")}</option>
                            <option value='1' {(settings.AddressBarSearchEngine == 1 ? "selected" : "")}>{Localization.T("settings.search.options.baidu")}</option>
                            <option value='2' {(settings.AddressBarSearchEngine == 2 ? "selected" : "")}>{Localization.T("settings.search.options.bing")}</option>
                            <option value='3' {(settings.AddressBarSearchEngine == 3 ? "selected" : "")}>{Localization.T("settings.search.options.google")}</option>
                        </select>
                        <button onclick='openSearchEngineManager()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.search.manage_button")}</button>
                    </div>
                </div>
            </div>
            
            <div class='section'>
                <h3>{Localization.T("settings.appearance.title")}</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='showHomeButton' {(settings.ShowHomeButton ? "checked" : "")} onchange='updateSetting(""homebutton"", this.checked)' style='margin-right:8px;'>
                        <span>{Localization.T("settings.appearance.show_home_button")}</span>
                    </label>
                    <div id='homePageSetting' style='display:{(settings.ShowHomeButton ? "flex" : "none")};align-items:center;padding:6px 0 6px 24px;'>
                        <span style='color:#666;font-size:13px;margin-right:8px;'>{Escape(Forms.HomePageDialog.GetHomePageDisplayText(settings.HomePage))}</span>
                        <a href='#' onclick='openHomePageDialog();return false;' style='color:#1a73e8;text-decoration:none;font-size:13px;'>{Localization.T("settings.appearance.change")}</a>
                    </div>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='showBookmarkBar' {(settings.AlwaysShowBookmarkBar ? "checked" : "")} onchange='updateSetting(""bookmarkbar"", this.checked)' style='margin-right:8px;'>
                        <span>{Localization.T("settings.appearance.show_bookmark_bar")}</span>
                    </label>
                    <div class='setting-item' style='margin-top:8px;'>
                        <div class='setting-label'>{Localization.T("settings.appearance.language")}</div>
                        <div>
                            <select id='languageSelect' onchange='updateSetting(""language"", this.value)' oninput='updateSetting(""language"", this.value)'>
                                {BuildLanguageOptionsHtml(settings.LanguageCode)}
                            </select>
                        </div>
                    </div>
                </div>
            </div>
            
            <div class='section'>
                <h3>{Localization.T("settings.features.title")}</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>{Localization.T("settings.features.mouse_gesture_label")}</div>
                        <div class='setting-desc'>{Localization.T("settings.features.mouse_gesture_desc")}</div>
                    </div>
                    <label class='toggle'>
                        <input type='checkbox' id='mouseGesture' {(settings.EnableMouseGesture ? "checked" : "")} onchange='updateSetting(""gesture"", this.checked)'>
                        <span class='slider'></span>
                    </label>
                </div>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>{Localization.T("settings.features.super_drag_label")}</div>
                        <div class='setting-desc'>{Localization.T("settings.features.super_drag_desc")}</div>
                    </div>
                    <label class='toggle'>
                        <input type='checkbox' id='superDrag' {(settings.EnableSuperDrag ? "checked" : "")} onchange='updateSetting(""superdrag"", this.checked)'>
                        <span class='slider'></span>
                    </label>
                </div>
            </div>
             
            <div class='section'>
                <h3>{Localization.T("settings.downloads.title")}</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>{Localization.T("settings.downloads.location_label")}</div>
                        <div class='setting-desc'>{Localization.T("settings.downloads.location_desc")}</div>
                    </div>
                    <div style='display:flex;gap:8px;align-items:center'>
                        <input type='text' id='downloadPath' value='{Escape(settings.DownloadPath)}' onchange='updateSetting(""downloadpath"", this.value)' style='flex:1'>
                        <button class='btn btn-secondary' onclick='browseDownloadPath()'>{Localization.T("settings.downloads.browse")}</button>
                    </div>
                </div>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>{Localization.T("settings.downloads.ask_location_label")}</div>
                        <div class='setting-desc'>{LocalizeOrFallback("settings.downloads.ask_location_desc", "settings.downloads.ask_location_label")}</div>
                    </div>
                    <label class='toggle'>
                        <input type='checkbox' id='askDownload' {(settings.AskDownloadLocation ? "checked" : "")} onchange='updateSetting(""askdownload"", this.checked)'>
                        <span class='slider'></span>
                    </label>
                </div>
            </div>
             
            <div class='section'>
                <h3>{Localization.T("settings.user_data.title")}</h3>
                <div style='padding:12px 0;'>
                    <button onclick='openImportData()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.user_data.import")}</button>
                </div>
            </div>
             
            <div class='section'>
                <h3>{Localization.T("settings.web.title")}</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='smoothScrolling' {(settings.EnableSmoothScrolling ? "checked" : "")} onchange='updateSetting(""smoothscrolling"", this.checked)' style='margin-right:8px;'>
                        <span>{Localization.T("settings.web.smooth_scrolling")}</span>
                    </label>
                </div>
            </div>
             
            <div class='section'>
                <h3>{Localization.T("settings.cache.title")}</h3>
                <div class='setting-item'>
                    <div>
                        <div class='setting-label'>{Localization.T("settings.cache.directory_label")}</div>
                    </div>
                    <div style='display:flex;gap:8px;align-items:center'>
                        <input type='text' id='cachePath' value='{Escape(GetCachePath(settings))}' readonly style='flex:1;background:#f9f9f9;'>
                        <button class='btn btn-secondary' onclick='changeCachePath()'>{Localization.T("settings.cache.change_button")}</button>
                    </div>
                </div>
                <div style='padding:8px 0;color:#888;font-size:13px;'>
                    {Localization.T("settings.cache.notice")}
                    <a href='#' onclick='openCacheDir();return false;' style='color:#1a73e8;text-decoration:none;margin-left:10px;'>{Localization.T("settings.cache.open_dir")}</a>
                    <a href='#' onclick='resetCachePath();return false;' style='color:#1a73e8;text-decoration:none;margin-left:10px;'>{Localization.T("settings.cache.reset_default")}</a>
                </div>
            </div>
             
            <div class='section'>
                <h3>{Localization.T("settings.default_browser.title")}</h3>
                <div style='padding:12px 0;'>
                    <button onclick='setAsDefaultBrowser()' style='padding:10px 20px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.default_browser.set_default")}</button>
                </div>
                <div id='defaultBrowserStatus' style='font-size:13px;color:#666;'></div>
            </div>
             
            <div class='section'>
                <h3>{Localization.T("settings.privacy.title")}</h3>
                <div style='display:flex;gap:10px;padding:12px 0;'>
                    <button onclick='openContentSettings()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.privacy.content_settings")}</button>
                    <button onclick='openClearBrowsingData()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.privacy.clear_browsing_data")}</button>
                </div>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='crashUpload' {(settings.EnableCrashUpload ? "checked" : "")} onchange='updateSetting(""crashupload"", this.checked)' style='margin-right:8px;'>
                        <span>{Localization.T("settings.privacy.crash_upload")}</span>
                    </label>
                    <div style='font-size:12px;color:#888;margin-left:24px;'>{Localization.T("settings.privacy.crash_upload_desc")}</div>
                </div>
            </div>
             
            <div class='section'>
                <h3>{Localization.T("settings.passwords.title")}</h3>
                <div style='padding:8px 0;'>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='enableAutofill' {(settings.EnableAutofill ? "checked" : "")} onchange='updateSetting(""enableautofill"", this.checked)' style='margin-right:8px;'>
                        <span>{Localization.T("settings.passwords.autofill_desc")}</span>
                        <a href='#' onclick='openAutofillSettings();return false;' style='color:#1a73e8;text-decoration:none;margin-left:8px;'>{Localization.T("settings.passwords.manage_autofill")}</a>
                    </label>
                    <label style='display:flex;align-items:center;padding:6px 0;cursor:pointer;'>
                        <input type='checkbox' id='savePasswords' {(settings.SavePasswords ? "checked" : "")} onchange='updateSetting(""savepasswords"", this.checked)' style='margin-right:8px;'>
                        <span>{Localization.T("settings.passwords.save_passwords_desc")}</span>
                        <a href='#' onclick='openPasswordManager();return false;' style='color:#1a73e8;text-decoration:none;margin-left:8px;'>{Localization.T("settings.passwords.manage_saved_passwords")}</a>
                    </label>
                </div>
            </div>
             
            <div class='section'>
                <h3>{Localization.T("settings.web_content.title")}</h3>
                <div class='setting-item'>
                    <div class='setting-label'>{Localization.T("settings.web_content.font_size")}</div>
                    <div style='display:flex;align-items:center;gap:10px;'>
                        <select id='fontSize' onchange='updateSetting(""fontsize"", this.value)' style='padding:6px 10px;border:1px solid #ddd;border-radius:4px;min-width:100px;'>
                            <option value='0' {(settings.FontSize == 0 ? "selected" : "")}>{Localization.T("settings.web_content.font_size_very_small")}</option>
                            <option value='1' {(settings.FontSize == 1 ? "selected" : "")}>{Localization.T("settings.web_content.font_size_small")}</option>
                            <option value='2' {(settings.FontSize == 2 ? "selected" : "")}>{Localization.T("settings.web_content.font_size_medium")}</option>
                            <option value='3' {(settings.FontSize == 3 ? "selected" : "")}>{Localization.T("settings.web_content.font_size_large")}</option>
                            <option value='4' {(settings.FontSize == 4 ? "selected" : "")}>{Localization.T("settings.web_content.font_size_very_large")}</option>
                        </select>
                        <button onclick='openFontSettings()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.web_content.customize_fonts")}</button>
                    </div>
                </div>
                <div class='setting-item'>
                    <div class='setting-label'>{Localization.T("settings.web_content.page_zoom")}</div>
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
                <h3>{Localization.T("settings.network.title")}</h3>
                <div style='padding:8px 0;'>
                    <div style='font-size:13px;color:#666;margin-bottom:10px;'>{Localization.T("settings.network.system_proxy_desc")}</div>
                    <button onclick='openProxySettings()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.network.proxy_change")}</button>
                </div>
            </div>
            
            <div class='section'>
                <h3>{Localization.T("settings.https.title")}</h3>
                <div style='padding:8px 0;'>
                    <button onclick='openCertificateManager()' style='padding:8px 16px;border:1px solid #ddd;border-radius:4px;background:#fff;cursor:pointer;font-size:14px;'>{Localization.T("settings.https.manage_certificates")}</button>
                </div>
            </div>
            
            <div style='margin-top: 20px;'>
                <button class='btn btn-secondary' onclick='resetSettings()'>{Localization.T("settings.reset_button")}</button>
            </div>
            <div class='settings-action-bar' id='settingsActionBar'>
                <div class='save-status' id='saveStatusText'>{Escape(Localization.T("settings.save.no_pending_changes"))}</div>
                <button id='saveSettingsBtn' class='btn btn-primary save-btn' onclick='savePendingSettings()'>{Escape(Localization.T("settings.save.apply_button"))}</button>
            </div>
        </div>
    </div>
    <script>
        var saveStatusTimer = null;

        function postSettingUpdate(key, value) {{
            try {{
                if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {{
                    window.chrome.webview.postMessage({{ action: 'updateSetting', key: key, value: value }});
                    return true;
                }}
            }} catch (err) {{
                console.error('postSettingUpdate failed', key, err);
            }}
            return false;
        }}

        function setSaveBarState(text, isDirty) {{
            var saveBtn = document.getElementById('saveSettingsBtn');
            var statusEl = document.getElementById('saveStatusText');
            if (saveBtn) {{
                saveBtn.disabled = false;
            }}
            if (statusEl) {{
                statusEl.className = 'save-status' + (isDirty ? ' dirty' : '');
                statusEl.textContent = text;
            }}
        }}

        function collectSettingsFromPage() {{
            var collected = {{}};
            var elements = document.querySelectorAll('input[onchange], select[onchange], textarea[onchange]');
            elements.forEach(function(el) {{
                var onchange = el.getAttribute('onchange') || '';
                var firstDoubleQuote = onchange.indexOf(String.fromCharCode(34));
                var firstSingleQuote = onchange.indexOf(String.fromCharCode(39));
                var start = firstDoubleQuote >= 0 ? firstDoubleQuote : firstSingleQuote;
                if (start < 0) return;
                var quoteChar = onchange[start];
                var end = onchange.indexOf(quoteChar, start + 1);
                if (end < 0) return;

                var key = onchange.substring(start + 1, end);
                if (!key) return;

                var tagName = (el.tagName || '').toLowerCase();
                var type = (el.type || '').toLowerCase();

                if (type === 'radio') {{
                    if (!el.checked) return;
                    collected[key] = el.value;
                    return;
                }}

                if (type === 'checkbox') {{
                    collected[key] = !!el.checked;
                    return;
                }}

                if (tagName === 'select' || tagName === 'textarea' || type === 'text' || type === 'password' || type === 'number' || type === 'search' || type === 'url') {{
                    collected[key] = el.value;
                }}
            }});
            return collected;
        }}

        function updateSetting(key, value) {{
            // Restore original behavior: save immediately on every change.
            var ok = postSettingUpdate(key, value);

            if (key === 'aimode') {{
                var apiSettings = document.getElementById('ai-api-settings');
                var webSettings = document.getElementById('ai-web-settings');
                if (apiSettings) apiSettings.style.display = (value == '1') ? 'block' : 'none';
                if (webSettings) webSettings.style.display = (value == '0') ? 'block' : 'none';
            }}

            if (!ok) {{
                setSaveBarState('{EscapeJsString(Localization.T("settings.save.failed_channel"))}', true);
                return;
            }}

            setSaveBarState('{EscapeJsString(Localization.T("settings.save.auto_saved"))}', false);
            if (saveStatusTimer) clearTimeout(saveStatusTimer);
            saveStatusTimer = setTimeout(function() {{
                setSaveBarState('{EscapeJsString(Localization.T("settings.save.auto_hint"))}', false);
            }}, 1200);
        }}

        function savePendingSettings() {{
            var allCurrentSettings = collectSettingsFromPage();
            var entries = Object.entries(allCurrentSettings);
            if (entries.length === 0) {{
                setSaveBarState('{EscapeJsString(Localization.T("settings.save.nothing_to_save"))}', false);
                return;
            }}

            var languageValue = undefined;
            var failedCount = 0;
            entries.forEach(function(pair) {{
                var key = pair[0];
                var value = pair[1];
                if (key === 'language') {{
                    languageValue = value;
                    return;
                }}
                if (!postSettingUpdate(key, value)) failedCount++;
            }});

            if (languageValue !== undefined) {{
                if (!postSettingUpdate('language', languageValue)) failedCount++;
            }}

            if (failedCount > 0) {{
                setSaveBarState('{EscapeJsString(Localization.T("settings.save.partial_failed"))}', true);
                return;
            }}

            setSaveBarState('{EscapeJsString(Localization.T("settings.save.manual_applied"))}', false);
        }}

        var aiPresets = {{
            'deepseek': {{
                baseUrl: 'https://api.deepseek.com/v1',
                models: ['deepseek-chat', 'deepseek-reasoner']
            }},
            'volcengine': {{
                baseUrl: 'https://ark.cn-beijing.volces.com/api/v3',
                models: [
                    {{ id: 'doubao-seed-2-0-pro-260215', name: 'Doubao-Seed-2-0-pro (flagship)' }},
                    {{ id: 'doubao-seed-2-0-lite-260215', name: 'Doubao-Seed-2-0-lite (balanced)' }},
                    {{ id: 'doubao-seed-2-0-mini-260215', name: 'Doubao-Seed-2-0-mini (low latency)' }},
                    {{ id: 'doubao-seed-2-0-code-preview-260215', name: 'Doubao-Seed-2-0-code (coding enhanced)' }},
                    {{ id: 'doubao-seed-1-8-251228', name: 'Doubao-Seed-1.8' }},
                    {{ id: 'doubao-seed-1-6-251015', name: 'Doubao-Seed-1.6' }},
                    {{ id: 'doubao-seed-1-6-vision-250815', name: 'Doubao-Seed-1.6-vision (vision)' }},
                    {{ id: 'glm-4-7-251222', name: 'GLM-4.7 (Zhipu)' }},
                    {{ id: 'deepseek-r1', name: 'DeepSeek-R1 (Ark)' }},
                    {{ id: 'deepseek-v3', name: 'DeepSeek-V3 (Ark)' }},
                    {{ id: 'ep-2024xxxxxxxx-xxxxx', name: 'Manual endpoint ID (ep-xxx)' }}
                ]
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
                    {{ id: 'qwen3-max', name: 'Qwen 3-Max (qwen3-max)' }},
                    {{ id: 'qwen3-max-latest', name: 'Qwen 3-Max latest (qwen3-max-latest)' }},
                    {{ id: 'qwen-max', name: 'Qwen Max (qwen-max)' }},
                    {{ id: 'qwen-max-latest', name: 'Qwen Max latest (qwen-max-latest)' }},
                    {{ id: 'qwen-plus', name: 'Qwen Plus (qwen-plus)' }},
                    {{ id: 'qwen-plus-latest', name: 'Qwen Plus latest (qwen-plus-latest)' }},
                    {{ id: 'qwen-turbo', name: 'Qwen Turbo (qwen-turbo)' }},
                    {{ id: 'qwen-turbo-latest', name: 'Qwen Turbo latest (qwen-turbo-latest)' }},
                    {{ id: 'qwen-long', name: 'Qwen Long (qwen-long)' }},
                    {{ id: 'qwen-long-latest', name: 'Qwen Long latest (qwen-long-latest)' }},
                    {{ id: 'qwen-flash', name: 'Qwen Flash (qwen-flash)' }},
                    {{ id: 'qwen-coder-plus', name: 'Qwen Coder Plus (qwen-coder-plus)' }},
                    {{ id: 'qwen-coder-turbo', name: 'Qwen Coder Turbo (qwen-coder-turbo)' }},
                    {{ id: 'qwq-plus', name: 'QwQ Plus (qwq-plus)' }},
                    {{ id: 'qwq-plus-latest', name: 'QwQ Plus latest (qwq-plus-latest)' }}
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
                modelPresetSelect.innerHTML = '<option value="">{EscapeJsString(Localization.T("ai.api.select_preset_model"))}</option>';
                return;
            }}

            var preset = aiPresets[provider];
            if (preset) {{
                baseUrlInput.value = preset.baseUrl;
                updateSetting('aiapibaseurl', preset.baseUrl);

                modelPresetSelect.innerHTML = '<option value="">{EscapeJsString(Localization.T("ai.api.select_preset_model"))}</option>';
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
                
                // Select the first model by default
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

        // Initialize AI model preset dropdown
        function initAiPresets() {{
            var provider = document.getElementById('aiProviderSelect')?.value;
            if (provider && provider !== 'custom') {{
                var preset = aiPresets[provider];
                var modelPresetSelect = document.getElementById('aiModelPreset');
                var currentModel = document.getElementById('aiModelName')?.value;
                
                if (preset && modelPresetSelect) {{
                    modelPresetSelect.innerHTML = '<option value="">{EscapeJsString(Localization.T("ai.api.select_preset_model"))}</option>';
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
        setSaveBarState('{EscapeJsString(Localization.T("settings.save.auto_hint"))}', false);

        document.addEventListener('keydown', function(e) {{
            if ((e.ctrlKey || e.metaKey) && e.key && e.key.toLowerCase() === 's') {{
                e.preventDefault();
                savePendingSettings();
            }}
        }});

        function resetSettings() {{
            if (confirm('{EscapeJsString(Localization.T("settings.reset.confirm"))}')) {{
                setSaveBarState('{EscapeJsString(Localization.T("settings.save.resetting_defaults"))}', false);
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
            if (confirm('{EscapeJsString(Localization.T("settings.cache.reset_confirm"))}')) {{
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
        // Check default browser status after page load
        setTimeout(function() {{ checkDefaultBrowser(); }}, 100);
        // Listen to home button checkbox changes and show/hide homepage config
        document.getElementById('showHomeButton')?.addEventListener('change', function() {{
            var homePageSetting = document.getElementById('homePageSetting');
            if (homePageSetting) {{
                homePageSetting.style.display = this.checked ? 'flex' : 'none';
            }}
        }});
        
        // Search
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
                // Restore all sections
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
                    // Highlight matched text
                    var html = item.html;
                    var regex = new RegExp('(' + escapeRegex(keyword) + ')', 'gi');
                    // Highlight text nodes only to avoid breaking HTML tags
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
            // Simple highlighter: only highlight visible text
            var tempDiv = document.createElement('div');
            tempDiv.innerHTML = html;
            highlightNode(tempDiv, keyword);
            return tempDiv.innerHTML;
        }}
        
        function highlightNode(node, keyword) {{
            if (node.nodeType === 3) {{ // Text node
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
                    
                    // Continue processing remaining text
                    highlightNode(afterNode, keyword);
                }}
            }} else if (node.nodeType === 1 && node.tagName !== 'SCRIPT' && node.tagName !== 'STYLE') {{
                // Element node, recursively process children
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
                setSaveBarState('{EscapeJsString(Localization.T("settings.save.auto_hint"))}', false);
            }} else if (section === 'ai') {{
                content.innerHTML = settingsContent;
                setSaveBarState('{EscapeJsString(Localization.T("settings.save.auto_hint"))}', false);
                var aiSection = document.getElementById('ai-section');
                if (aiSection) {{
                    aiSection.scrollIntoView({{ behavior: 'smooth' }});
                }}
            }}
        }}
        
        window.chrome.webview.addEventListener('message', function(e) {{
            if (e.data && e.data.action === 'historyData') {{
                showHistoryContent(e.data.items);
            }} else if (e.data && e.data.action === 'settingUpdateResult') {{
                if (!e.data.success) {{
                    var reason = e.data.error ? (' - ' + e.data.error) : '';
                    setSaveBarState('{EscapeJsString(Localization.T("settings.save.failed"))}' + reason, true);
                }}
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
                        statusDiv.innerHTML = '<span style=color:#0a0>{EscapeJsString(Localization.T("settings.default_browser.is_default").Replace("{{app}}", AppConstants.AppName))}</span>';
                    }} else {{
                        statusDiv.innerHTML = '{EscapeJsString(Localization.T("settings.default_browser.not_default").Replace("{{app}}", AppConstants.AppName))}';
                    }}
                }}
            }}
        }});
        
        function showHistoryContent(items) {{
            var content = document.querySelector('.content');
            var html = '<h1>{EscapeJsString(Localization.T("settings.history.title"))}</h1><div class=section><div style=display:flex;justify-content:space-between;align-items:center;margin-bottom:15px><input type=text id=historySearch placeholder=""{EscapeJsString(Localization.T("settings.history.search_placeholder"))}"" style=flex:1;margin-right:10px onkeyup=searchHistory(this.value)><button class=btn onclick=clearHistory()>{EscapeJsString(Localization.T("actions.delete_all"))}</button></div><div id=historyList>';
            if (items && items.length > 0) {{
                for (var i = 0; i < items.length; i++) {{
                    var item = items[i];
                    html += buildHistoryItem(item);
                }}
            }} else {{
                html += '<p style=color:#888;padding:20px;text-align:center>{EscapeJsString(Localization.T("settings.history.empty"))}</p>';
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
            if (confirm('{EscapeJsString(Localization.T("confirm.clear_history_all"))}')) {{
                window.chrome.webview.postMessage({{ action: 'clearHistory' }});
            }}
        }}
    </script>
</body>
</html>";
    }
    
    #endregion
    
    #region 鏀惰棌澶圭鐞嗛〉闈?
    
    /// <summary>
    /// 鐢熸垚鏀惰棌澶圭鐞嗛〉闈?
    /// </summary>
    public static string GenerateBookmarksPage()
    {
        var html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>__L_BOOKMARKS_TITLE__</title>
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
        <h1>&#128218; __L_BOOKMARKS_TITLE__</h1>
        
        <div class='toolbar'>
            <div class='search-box'>
                <input type='text' id='searchInput' placeholder='__L_BOOKMARKS_SEARCH__' onkeyup='searchBookmarks(this.value)'>
            </div>
            <button class='btn btn-primary' onclick='addFolder()'>__L_BOOKMARKS_NEW_FOLDER__</button>
            <button class='btn btn-secondary' onclick='exportBookmarks()'>__L_BOOKMARKS_EXPORT__</button>
        </div>
        
        <div class='breadcrumb' id='breadcrumb'>
            <a href='#' onclick='loadBookmarks(); return false;'>__L_BOOKMARKS_ROOT__</a>
        </div>
        
        <div class='bookmark-list' id='bookmarkList'>
            <div class='empty-state'>
                <div class='icon'>&#128260;</div>
                <div>__L_COMMON_LOADING__</div>
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
            folderStack.push({ id: currentFolderId, title: currentFolderId ? '...' : '__L_BOOKMARKS_ROOT__' });
            loadBookmarks(id);
            updateBreadcrumb(title);
        }
        
        function updateBreadcrumb(title) {
            var html = '<a href=""#"" onclick=""goToRoot(); return false;"">__L_BOOKMARKS_ROOT__</a>';
            if (title) {
                html += ' <span>&gt;</span> <span>' + escapeHtml(title) + '</span>';
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
            var newTitle = prompt('__L_ACTION_RENAME__:', title);
            if (newTitle !== null && newTitle.trim()) {
                window.chrome.webview.postMessage({ action: 'updateBookmark', id: id, title: newTitle.trim() });
            }
        }
        
        function deleteBookmark(id, title) {
            if (confirm('__L_CONFIRM_DELETE_BOOKMARK__: ' + title)) {
                window.chrome.webview.postMessage({ action: 'deleteBookmark', id: id });
            }
        }
        
        function addFolder() {
            var name = prompt('__L_BOOKMARKS_INPUT_FOLDER__');
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
                list.innerHTML = '<div class=""empty-state""><div class=""icon"">&#128230;</div><div>__L_BOOKMARKS_EMPTY__</div></div>';
                return;
            }
            
            var html = '';
            for (var i = 0; i < items.length; i++) {
                var item = items[i];
                if (item.isFolder) {
                    html += '<div class=""bookmark-item"" ondblclick=""openFolder(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\')"">' +
                        '<div class=""bookmark-icon folder-icon"">&#128193;</div>' +
                        '<div class=""bookmark-info""><div class=""bookmark-title"">' + escapeHtml(item.title) + '</div></div>' +
                        '<div class=""bookmark-actions"">' +
                        '<button class=""action-btn"" onclick=""event.stopPropagation(); editBookmark(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\', \'\')"">__L_ACTION_EDIT__</button>' +
                        '<button class=""action-btn delete"" onclick=""event.stopPropagation(); deleteBookmark(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\')"">__L_ACTION_DELETE__</button>' +
                        '</div></div>';
                } else {
                    var favicon = item.favicon || 'https://www.google.com/s2/favicons?domain=' + encodeURIComponent(new URL(item.url).hostname) + '&sz=16';
                    html += '<div class=""bookmark-item"" ondblclick=""navigateTo(\'' + escapeHtml(item.url) + '\')"">' +
                        '<div class=""bookmark-icon""><img src=""' + favicon + '"" onerror=""this.style.display=\'none\'""></div>' +
                        '<div class=""bookmark-info""><div class=""bookmark-title"">' + escapeHtml(item.title) + '</div>' +
                        '<div class=""bookmark-url"">' + escapeHtml(item.url) + '</div></div>' +
                        '<div class=""bookmark-actions"">' +
                        '<button class=""action-btn"" onclick=""event.stopPropagation(); editBookmark(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\', \'' + escapeHtml(item.url) + '\')"">__L_ACTION_EDIT__</button>' +
                        '<button class=""action-btn delete"" onclick=""event.stopPropagation(); deleteBookmark(\'' + item.id + '\', \'' + escapeHtml(item.title) + '\')"">__L_ACTION_DELETE__</button>' +
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
        
        // 鍒濆鍔犺浇
        loadBookmarks();
    </script>
</body>
</html>";
        return html
            .Replace("__L_BOOKMARKS_TITLE__", Localization.T("bookmarks.manager.title"))
            .Replace("__L_BOOKMARKS_SEARCH__", Localization.T("bookmarks.manager.search_placeholder"))
            .Replace("__L_BOOKMARKS_NEW_FOLDER__", Localization.T("bookmarks.manager.new_folder"))
            .Replace("__L_BOOKMARKS_EXPORT__", Localization.T("bookmarks.manager.export"))
            .Replace("__L_BOOKMARKS_ROOT__", Localization.T("bookmarks.manager.root"))
            .Replace("__L_COMMON_LOADING__", Localization.T("common.loading"))
            .Replace("__L_ACTION_RENAME__", Localization.T("actions.rename"))
            .Replace("__L_CONFIRM_DELETE_BOOKMARK__", Localization.T("bookmarks.manager.confirm_delete"))
            .Replace("__L_BOOKMARKS_INPUT_FOLDER__", Localization.T("bookmarks.manager.input_folder_name"))
            .Replace("__L_BOOKMARKS_EMPTY__", Localization.T("bookmarks.manager.empty"))
            .Replace("__L_ACTION_EDIT__", Localization.T("actions.edit"))
            .Replace("__L_ACTION_DELETE__", Localization.T("actions.delete"));
    }
    
    #endregion
    
    #region 閿欒椤甸潰
    
    public static string GenerateInvalidUrlPage(string url) =>
        GenerateErrorPage("This site can't be reached", "ERR_INVALID_URL",
            $"The URL <strong>{Escape(url)}</strong> is invalid or could not be resolved.",
            new[]
            {
                "Check if the URL is spelled correctly",
                "Make sure the URL includes a valid protocol (for example, https://)",
                "Try searching for the website name"
            });
    
    public static string GenerateNetworkErrorPage(string url) =>
        GenerateErrorPage("Unable to connect to the network", "ERR_NETWORK_DISCONNECTED",
            "A network connection could not be established. Please check your network settings.",
            new[]
            {
                "Check network cables, modem, and router",
                "Reconnect to Wi-Fi",
                "Check firewall and proxy settings"
            });
    
    public static string GenerateTimeoutPage(string url) =>
        GenerateErrorPage("Connection timed out", "ERR_CONNECTION_TIMED_OUT",
            $"The connection to <strong>{Escape(UrlHelper.GetHost(url))}</strong> timed out.",
            new[]
            {
                "The website may be temporarily unavailable or too busy",
                "Please try again later",
                "Check your network connection"
            });
    
    public static string GenerateDnsErrorPage(string url) =>
        GenerateErrorPage("Server not found", "ERR_NAME_NOT_RESOLVED",
            $"The DNS address for <strong>{Escape(UrlHelper.GetHost(url))}</strong> could not be found.",
            new[] { "Check if the URL is correct", "Try running network diagnostics", "Check DNS settings" });
    
    public static string GenerateConnectionRefusedPage(string url) =>
        GenerateErrorPage("Connection refused", "ERR_CONNECTION_REFUSED",
            $"<strong>{Escape(UrlHelper.GetHost(url))}</strong> refused the connection request.",
            new[]
            {
                "The site may be temporarily down or has moved permanently",
                "Check firewall and proxy settings",
                "If you are using a proxy server, verify your proxy settings"
            });
    
    public static string GenerateSslErrorPage(string url) =>
        GenerateErrorPage("Your connection is not private", "ERR_CERT_AUTHORITY_INVALID",
            $"Attackers might be trying to steal your information from <strong>{Escape(UrlHelper.GetHost(url))}</strong>.",
            new[]
            {
                "This site has a problem with its security certificate",
                "It is recommended not to continue",
                "If you understand the risk, you can choose to proceed"
            }, true);
    
    public static string GenerateGenericErrorPage(string url, int errorCode, string errorMessage) =>
        GenerateErrorPage("This site can't be reached", $"ERR_FAILED ({errorCode})",
            $"An error occurred while accessing <strong>{Escape(UrlHelper.GetHost(url))}</strong>: {Escape(errorMessage)}",
            new[] { "Please try again later", "Check your network connection", "Check firewall and proxy settings" });
    
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
        var accentColor = isWarning ? "#ef4444" : "#3b82f6";
        var iconColor = isWarning ? "#fee2e2" : "#dbeafe";
        var iconSvg = isWarning 
            ? "<svg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 24 24' stroke='currentColor'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z' /></svg>"
            : "<svg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 24 24' stroke='currentColor'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z' /></svg>";
        
        var suggestionsHtml = string.Join("", suggestions.Select(s => $@"
            <div class='suggestion-item'>
                <svg class='suggestion-icon' viewBox='0 0 20 20' fill='currentColor'>
                    <path fill-rule='evenodd' d='M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z' clip-rule='evenodd' />
                </svg>
                <span>{s}</span>
            </div>"));
        
        return $@"<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>{Escape(title)}</title>
    <style>
        :root {{
            --accent: {accentColor};
            --accent-bg: {iconColor};
            --text-main: #1f2937;
            --text-muted: #6b7280;
            --bg-page: #f9fafb;
            --bg-card: #ffffff;
        }}
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
            background: var(--bg-page);
            color: var(--text-main);
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            line-height: 1.5;
        }}
        .container {{
            max-width: 480px;
            width: 90%;
            text-align: center;
            padding: 40px 20px;
        }}
        .icon-wrapper {{
            width: 80px;
            height: 80px;
            background: var(--accent-bg);
            color: var(--accent);
            border-radius: 24px;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 24px;
        }}
        .icon-wrapper svg {{ width: 40px; height: 40px; }}
        h1 {{
            font-size: 24px;
            font-weight: 700;
            margin-bottom: 12px;
            color: var(--text-main);
        }}
        .error-code {{
            display: inline-block;
            font-family: ui-monospace, monospace;
            font-size: 12px;
            font-weight: 600;
            background: #f3f4f6;
            color: var(--text-muted);
            padding: 4px 12px;
            border-radius: 9999px;
            margin-bottom: 24px;
        }}
        .description {{
            font-size: 16px;
            color: var(--text-muted);
            margin-bottom: 32px;
        }}
        .card {{
            background: var(--bg-card);
            border-radius: 20px;
            padding: 24px;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
            text-align: left;
            margin-bottom: 32px;
        }}
        .card h3 {{
            font-size: 14px;
            font-weight: 600;
            color: var(--text-main);
            margin-bottom: 16px;
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        .suggestion-item {{
            display: flex;
            align-items: flex-start;
            gap: 12px;
            margin-bottom: 12px;
            font-size: 14px;
            color: var(--text-muted);
        }}
        .suggestion-icon {{
            width: 18px;
            height: 18px;
            color: #10b981;
            flex-shrink: 0;
            margin-top: 1px;
        }}
        .actions {{
            display: flex;
            gap: 12px;
            justify-content: center;
        }}
        .btn {{
            padding: 12px 24px;
            font-size: 14px;
            font-weight: 600;
            border-radius: 12px;
            cursor: pointer;
            transition: all 0.2s;
            border: none;
            text-decoration: none;
        }}
        .btn-primary {{
            background: var(--accent);
            color: white;
            box-shadow: 0 4px 14px 0 rgba(59, 130, 246, 0.39);
        }}
        .btn-primary:hover {{
            opacity: 0.9;
            transform: translateY(-1px);
        }}
        .btn-secondary {{
            background: #fff;
            color: var(--text-main);
            border: 1px solid #e5e7eb;
        }}
        .btn-secondary:hover {{
            background: #f9fafb;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon-wrapper'>{iconSvg}</div>
        <h1>{Escape(title)}</h1>
        <div class='error-code'>{Escape(errorCode)}</div>
        <div class='description'>{description}</div>
        
        <div class='card'>
            <h3>
                <svg width='16' height='16' fill='none' stroke='currentColor' viewBox='0 0 24 24'>
                    <path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M9.663 17h4.674M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z' />
                </svg>
                鎮ㄥ彲浠ュ皾璇曚互涓嬫搷浣?
            </h3>
            {suggestionsHtml}
        </div>
        
        <div class='actions'>
            <button class='btn btn-primary' onclick='location.reload()'>閲嶆柊鍔犺浇椤甸潰</button>
            <button class='btn btn-secondary' onclick='history.back()'>杩斿洖涓婁竴椤?/button>
        </div>
    </div>
</body>
</html>";
    }
    
    private static string LocalizeOrFallback(string key, string fallbackKey)
    {
        var value = Localization.T(key);
        return string.Equals(value, key, StringComparison.Ordinal) ? Localization.T(fallbackKey) : value;
    }

    private static string Escape(string text) => WebUtility.HtmlEncode(text ?? "");

    private static string EscapeJsString(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
    
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
