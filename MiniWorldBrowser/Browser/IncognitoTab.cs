using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Browser;

/// <summary>
/// 隐身模式标签页
/// 特点：
/// 1. 使用共享的隐身环境
/// 2. 不保存浏览历史
/// 3. 不保存表单数据
/// 4. Cookie 在窗口关闭后清除
/// </summary>
public class IncognitoTab : IDisposable
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public WebView2 WebView { get; private set; }
    public TabButton? TabButton { get; set; }
    
    public string Title { get; private set; } = "新标签页";
    public string Url { get; private set; } = "about:blank";
    public bool IsLoading { get; private set; }
    public bool IsSecure { get; private set; }
    public string? FaviconUrl { get; private set; }
    public DateTime LastActiveTime { get; set; } = DateTime.Now;
    
    public bool CanGoBack => _isInitialized && WebView.CoreWebView2?.CanGoBack == true;
    public bool CanGoForward => _isInitialized && WebView.CoreWebView2?.CanGoForward == true;
    
    // 事件
    public event Action<IncognitoTab>? TitleChanged;
    public event Action<IncognitoTab>? UrlChanged;
    public event Action<IncognitoTab>? LoadingStateChanged;
    public event Action<IncognitoTab, string>? NewWindowRequested;
    public event Action<IncognitoTab>? FaviconChanged;
    public event Action<IncognitoTab>? SecurityStateChanged;
    public event Action<IncognitoTab, string>? StatusTextChanged;
    public event Action<IncognitoTab, CoreWebView2DownloadStartingEventArgs>? DownloadStarting;
    public event Action<IncognitoTab, double>? ZoomChanged;
    
    private readonly Panel _container;
    private readonly ISettingsService _settingsService;
    private readonly CoreWebView2Environment _environment;
    private readonly IHistoryService? _mainHistoryService;
    private bool _isInitialized;
    private string _pendingUrl = "";
    private bool _pendingShow = false;  // 标记是否需要在内容加载后显示
    private bool _hasShownOnce = false; // 标记是否已经显示过一次
    
    public IncognitoTab(Panel container, ISettingsService settingsService, CoreWebView2Environment environment, IHistoryService? mainHistoryService = null)
    {
        _container = container;
        _settingsService = settingsService;
        _environment = environment;
        _mainHistoryService = mainHistoryService;
        
        WebView = new WebView2
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        _container.Controls.Add(WebView);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            await WebView.EnsureCoreWebView2Async(_environment);
            
            if (WebView.CoreWebView2 == null)
                throw new Exception("WebView2 CoreWebView2 初始化失败");
            
            _isInitialized = true;
            
            // 配置隐身模式设置
            var settings = WebView.CoreWebView2.Settings;
            if (settings != null)
            {
                settings.IsBuiltInErrorPageEnabled = true;
                settings.AreDefaultContextMenusEnabled = true;
                settings.AreDevToolsEnabled = true;
                
                // 设置标准 User-Agent
                settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";
                
                // 禁用密码保存提示
                settings.IsPasswordAutosaveEnabled = false;
                // 禁用表单自动填充
                settings.IsGeneralAutofillEnabled = false;
                
                // 启用必要功能
                settings.IsWebMessageEnabled = true;
                settings.AreHostObjectsAllowed = true;
            }

            try
            {
                WebView.KeyDown += (_, e) =>
                {
                    try
                    {
                        if (e.KeyCode == System.Windows.Forms.Keys.F12)
                        {
                            WebView.CoreWebView2?.OpenDevToolsWindow();
                            e.Handled = true;
                        }
                    }
                    catch { }
                };
            }
            catch { }

            try
            {
                WebView.CoreWebView2.WebMessageReceived += (_, e) =>
                {
                    try
                    {
                        var rawMsg = e.WebMessageAsJson;
                        var msg = System.Text.Json.JsonDocument.Parse(rawMsg);
                        var action = msg.RootElement.TryGetProperty("action", out var a) ? a.GetString() : null;
                        if (action == "console")
                        {
                            var level = msg.RootElement.TryGetProperty("level", out var l) ? l.GetString() : "";
                            var message = msg.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "";
                            System.Diagnostics.Debug.WriteLine($"[WebView2][Incognito][Console][{level}] {message}");
                        }
                    }
                    catch { }
                };
            }
            catch { }

            try
            {
                await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"(function(){
  if (window.__mw_console_bridge_installed) return;
  window.__mw_console_bridge_installed = true;

  function safeStringify(v){
    try {
      if (typeof v === 'string') return v;
      return JSON.stringify(v);
    } catch (e) {
      try { return String(v); } catch (e2) { return '[unprintable]'; }
    }
  }

  function post(level, args){
    try {
      if (window.chrome && window.chrome.webview) {
        var msg = Array.prototype.slice.call(args).map(safeStringify).join(' ');
        window.chrome.webview.postMessage({ action: 'console', level: level, message: msg });
      }
    } catch (e) {}
  }

  var originalError = console.error;
  var originalWarn = console.warn;
  console.error = function(){ post('error', arguments); try { return originalError.apply(console, arguments); } catch (e) {} };
  console.warn = function(){ post('warn', arguments); try { return originalWarn.apply(console, arguments); } catch (e) {} };

  window.addEventListener('error', function(ev){
    try {
      var msg = ev && ev.message ? ev.message : 'window.onerror';
      post('error', [msg, ev && ev.filename ? ev.filename : '', ev && ev.lineno ? ev.lineno : 0, ev && ev.colno ? ev.colno : 0]);
    } catch (e) {}
  });

  window.addEventListener('unhandledrejection', function(ev){
    try {
      post('error', ['unhandledrejection', ev && ev.reason ? ev.reason : '']);
    } catch (e) {}
  });
})();");
            }
            catch { }
            
            // 绑定事件
            WebView.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
            WebView.CoreWebView2.SourceChanged += OnSourceChanged;
            WebView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            WebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            
            try { WebView.CoreWebView2.FaviconChanged += OnFaviconChanged; } catch { }
            try { WebView.CoreWebView2.StatusBarTextChanged += OnStatusBarTextChanged; } catch { }
            try { WebView.CoreWebView2.ContextMenuRequested += OnContextMenuRequested; } catch { }
            try { WebView.CoreWebView2.DownloadStarting += OnDownloadStarting; } catch { }
            try { WebView.ZoomFactorChanged += OnZoomFactorChanged; } catch { }
            
            // 设置下载对话框位置 - 右上角，紧贴工具栏
            try 
            { 
                WebView.CoreWebView2.DefaultDownloadDialogCornerAlignment = Microsoft.Web.WebView2.Core.CoreWebView2DefaultDownloadDialogCornerAlignment.TopRight;
                WebView.CoreWebView2.DefaultDownloadDialogMargin = new System.Drawing.Point(8, 0);
            } 
            catch { }
            
            // 注入鼠标手势脚本
            if (_settingsService?.Settings?.EnableMouseGesture == true)
            {
                try { await InjectMouseGestureScript(); } catch { }
            }
            
            // 处理待导航的 URL
            if (!string.IsNullOrEmpty(_pendingUrl))
            {
                var url = _pendingUrl;
                _pendingUrl = "";
                Navigate(url);
            }
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            throw new Exception($"隐身模式 WebView2 初始化失败: {ex.Message}", ex);
        }
    }
    
    #region 事件处理
    
    private void OnDocumentTitleChanged(object? sender, object e)
    {
        try
        {
            Title = WebView.CoreWebView2?.DocumentTitle ?? "新标签页";
            if (string.IsNullOrEmpty(Title)) Title = "新标签页";
            TitleChanged?.Invoke(this);
        }
        catch { }
    }
    
    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        try
        {
            Url = WebView.CoreWebView2?.Source ?? "about:blank";
            UrlChanged?.Invoke(this);
        }
        catch { }
    }
    
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        try
        {
            IsLoading = true;
            LoadingStateChanged?.Invoke(this);
        }
        catch { }
    }
    
    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            IsLoading = false;
            LoadingStateChanged?.Invoke(this);
        }
        catch { }
    }
    
    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        try
        {
            e.Handled = true;
            NewWindowRequested?.Invoke(this, e.Uri);
        }
        catch { }
    }
    
    private void OnFaviconChanged(object? sender, object e)
    {
        try
        {
            FaviconUrl = WebView.CoreWebView2?.FaviconUri;
            FaviconChanged?.Invoke(this);
        }
        catch { }
    }
    
    private void OnStatusBarTextChanged(object? sender, object e)
    {
        try
        {
            var text = WebView.CoreWebView2?.StatusBarText ?? "";
            StatusTextChanged?.Invoke(this, text);
        }
        catch { }
    }
    
    private void OnZoomFactorChanged(object? sender, EventArgs e)
    {
        try
        {
            var zoomFactor = WebView?.ZoomFactor ?? 1.0;
            ZoomChanged?.Invoke(this, zoomFactor);
        }
        catch { }
    }
    
    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        try
        {
            DownloadStarting?.Invoke(this, e);
        }
        catch { }
    }

    private void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        try
        {
            if (WebView.CoreWebView2?.Environment == null) return;
            
            var menuItems = e.MenuItems;
            
            if (e.ContextMenuTarget.HasLinkUri)
            {
                var linkUri = e.ContextMenuTarget.LinkUri;
                
                var openInNewTab = WebView.CoreWebView2.Environment.CreateContextMenuItem(
                    "在新标签页中打开", null, CoreWebView2ContextMenuItemKind.Command);
                openInNewTab.CustomItemSelected += (s, args) => NewWindowRequested?.Invoke(this, linkUri);
                menuItems.Insert(0, openInNewTab);
                
                var copyLink = WebView.CoreWebView2.Environment.CreateContextMenuItem(
                    "复制链接地址", null, CoreWebView2ContextMenuItemKind.Command);
                copyLink.CustomItemSelected += (s, args) => { try { Clipboard.SetText(linkUri); } catch { } };
                menuItems.Insert(1, copyLink);
                
                var separator = WebView.CoreWebView2.Environment.CreateContextMenuItem(
                    "", null, CoreWebView2ContextMenuItemKind.Separator);
                menuItems.Insert(2, separator);
            }
            
            if (e.ContextMenuTarget.HasSelection)
            {
                var selectedText = e.ContextMenuTarget.SelectionText;
                if (!string.IsNullOrEmpty(selectedText) && selectedText.Length < 50)
                {
                    var displayText = selectedText.Length > 20 ? selectedText[..20] + "..." : selectedText;
                    var searchEngine = _settingsService?.Settings?.SearchEngine ?? Constants.AppConstants.DefaultSearchEngine;
                    var searchItem = WebView.CoreWebView2.Environment.CreateContextMenuItem(
                        $"搜索 \"{displayText}\"", null, CoreWebView2ContextMenuItemKind.Command);
                    searchItem.CustomItemSelected += (s, args) =>
                    {
                        NewWindowRequested?.Invoke(this, searchEngine + Uri.EscapeDataString(selectedText));
                    };
                    menuItems.Insert(0, searchItem);
                    
                    var separator = WebView.CoreWebView2.Environment.CreateContextMenuItem(
                        "", null, CoreWebView2ContextMenuItemKind.Separator);
                    menuItems.Insert(1, separator);
                }
            }
        }
        catch { }
    }
    
    #endregion
    
    #region 导航方法
    
    public void Navigate(string url)
    {
        if (!_isInitialized || WebView.CoreWebView2 == null)
        {
            _pendingUrl = url;
            return;
        }
        
        // 隐身模式新标签页
        if (UrlHelper.IsNewTabPage(url))
        {
            try
            {
                var newTabHtml = GenerateIncognitoNewTabPage();
                Url = "about:newtab";
                Title = "新标签页 - 隐身模式";
                IsSecure = true;
                WebView.CoreWebView2.NavigateToString(newTabHtml);
                TitleChanged?.Invoke(this);
            }
            catch { }
            return;
        }
        
        // 设置页面
        if (url == "about:settings")
        {
            try
            {
                var settings = _settingsService?.Settings ?? new Models.BrowserSettings();
                var settingsHtml = HtmlGenerator.GenerateSettingsPage(settings);
                Url = "about:settings";
                Title = "设置";
                IsSecure = true;
                WebView.CoreWebView2.NavigateToString(settingsHtml);
                TitleChanged?.Invoke(this);
            }
            catch { }
            return;
        }
        
        var searchEngine = _settingsService?.Settings?.SearchEngine ?? Constants.AppConstants.DefaultSearchEngine;
        url = UrlHelper.Normalize(url, searchEngine);
        
        if (!UrlHelper.IsValid(url))
        {
            try
            {
                var errorHtml = HtmlGenerator.GenerateInvalidUrlPage(url);
                WebView.CoreWebView2.NavigateToString(errorHtml);
            }
            catch { }
            return;
        }
        
        Url = url;
        IsSecure = UrlHelper.IsSecure(url);
        SecurityStateChanged?.Invoke(this);
        
        try { WebView.CoreWebView2.Navigate(url); }
        catch { }
    }
    
    public void GoBack() { if (CanGoBack) WebView.CoreWebView2?.GoBack(); }
    public void GoForward() { if (CanGoForward) WebView.CoreWebView2?.GoForward(); }
    public void Refresh() => WebView.CoreWebView2?.Reload();
    public void Stop() => WebView.CoreWebView2?.Stop();
    
    #endregion
    
    #region 显示控制
    
    public void Show()
    {
        LastActiveTime = DateTime.Now;

        // 如果已经显示过一次，直接显示（标签页切换时）
        if (_hasShownOnce)
        {
            WebView.Visible = true;
            WebView.BringToFront();
            return;
        }
        
        WebView.BringToFront();
        
        // 第一次显示时，标记为待显示，等待内容渲染后再显示
        _pendingShow = true;
        
        // 订阅 NavigationCompleted 事件，在页面加载完成后显示
        if (WebView.CoreWebView2 != null && !_navigationCompletedSubscribed)
        {
            _navigationCompletedSubscribed = true;
            WebView.CoreWebView2.NavigationCompleted += OnNavigationCompletedForShow;
        }
        
        // 设置一个较长的延迟后显示作为后备，避免无限等待
        Task.Delay(800).ContinueWith(_ =>
        {
            if (_pendingShow && !WebView.IsDisposed)
            {
                try
                {
                    WebView.BeginInvoke(() =>
                    {
                        if (!WebView.IsDisposed && _pendingShow)
                        {
                            WebView.Visible = true;
                            _hasShownOnce = true;
                            _pendingShow = false;
                        }
                    });
                }
                catch { }
            }
        });
    }
    
    private bool _navigationCompletedSubscribed = false;
    
    private void OnNavigationCompletedForShow(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        // 页面加载完成后显示 WebView
        if (_pendingShow && !WebView.IsDisposed)
        {
            try
            {
                // 延迟一小段时间让页面渲染完成
                Task.Delay(50).ContinueWith(_ =>
                {
                    if (!WebView.IsDisposed && _pendingShow)
                    {
                        try
                        {
                            WebView.BeginInvoke(() =>
                            {
                                if (!WebView.IsDisposed && _pendingShow)
                                {
                                    WebView.Visible = true;
                                    _hasShownOnce = true;
                                    _pendingShow = false;
                                }
                            });
                        }
                        catch { }
                    }
                });
            }
            catch { }
        }
        
        // 取消订阅，只需要第一次
        try
        {
            if (WebView.CoreWebView2 != null)
            {
                WebView.CoreWebView2.NavigationCompleted -= OnNavigationCompletedForShow;
            }
        }
        catch { }
    }
    
    public void Hide() => WebView.Visible = false;
    
    /// <summary>
    /// 标记标签页为已渲染状态，下次显示时直接显示
    /// </summary>
    public void MarkAsRendered()
    {
        _hasShownOnce = true;
        _pendingShow = false;
    }
    
    #endregion

    #region 隐身模式新标签页
    
    /// <summary>
    /// 生成隐身模式专用的新标签页（深色主题，参考 Edge InPrivate）
    /// </summary>
    private string GenerateIncognitoNewTabPage()
    {
        var searchEngineName = GetSearchEngineName(_settingsService?.Settings?.SearchEngine ?? "");
        var searchEngine = _settingsService?.Settings?.SearchEngine ?? Constants.AppConstants.DefaultSearchEngine;
        var shortcutsHtml = GenerateIncognitoShortcutsHtml();
        
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>新标签页 - 隐身模式</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif;
            background: #202124;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            color: #e8eaed;
            padding-top: 60px;
        }}
        .container {{ text-align: center; padding: 30px; max-width: 700px; }}
        .icon {{ font-size: 56px; margin-bottom: 16px; }}
        h1 {{ font-size: 26px; font-weight: 400; margin-bottom: 12px; color: #e8eaed; }}
        .subtitle {{ font-size: 14px; color: #9aa0a6; margin-bottom: 30px; }}
        .search-box {{ position: relative; width: 100%; max-width: 560px; margin: 0 auto 30px; }}
        .search-input {{
            width: 100%; padding: 14px 50px 14px 20px; font-size: 16px;
            border: 1px solid #5f6368; border-radius: 24px; outline: none;
            background: #303134; color: #e8eaed;
            transition: border-color 0.2s, box-shadow 0.2s;
        }}
        .search-input:focus {{
            border-color: #8ab4f8;
            box-shadow: 0 0 0 1px #8ab4f8;
        }}
        .search-input::placeholder {{ color: #9aa0a6; }}
        .search-btn {{
            position: absolute; right: 8px; top: 50%; transform: translateY(-50%);
            width: 36px; height: 36px; border: none; border-radius: 50%;
            background: #8ab4f8; color: #202124; font-size: 18px;
            cursor: pointer; transition: background 0.2s;
        }}
        .search-btn:hover {{ background: #aecbfa; }}
        .shortcuts {{ display: flex; flex-wrap: wrap; justify-content: center; gap: 16px; max-width: 560px; margin: 0 auto 30px; }}
        .shortcut {{ width: 72px; text-decoration: none; color: #e8eaed; text-align: center; transition: transform 0.2s; }}
        .shortcut:hover {{ transform: scale(1.1); }}
        .shortcut-icon {{
            width: 48px; height: 48px; background: #303134;
            border-radius: 10px; display: flex; align-items: center;
            justify-content: center; margin: 0 auto 6px; overflow: hidden;
        }}
        .shortcut-icon img {{ width: 28px; height: 28px; object-fit: contain; }}
        .shortcut-icon .letter {{ 
            font-size: 20px; font-weight: bold; color: #8ab4f8; 
            width: 48px; height: 48px; display: flex; align-items: center; justify-content: center;
        }}
        .shortcut-name {{ font-size: 11px; color: #9aa0a6; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }}
        .info-cards {{ display: flex; gap: 16px; margin-top: 20px; }}
        .info-card {{
            flex: 1; background: #303134; border-radius: 8px; padding: 16px;
            text-align: left;
        }}
        .info-card h3 {{ font-size: 13px; font-weight: 500; margin-bottom: 10px; color: #8ab4f8; }}
        .info-card ul {{ font-size: 12px; color: #9aa0a6; line-height: 1.7; padding-left: 18px; }}
        .info-card li {{ margin-bottom: 3px; }}
        .footer {{ position: fixed; bottom: 16px; font-size: 11px; color: #5f6368; }}
        .footer a {{ color: #8ab4f8; text-decoration: none; }}
        .footer a:hover {{ text-decoration: underline; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>🕵️</div>
        <h1>无痕浏览</h1>
        <p class='subtitle'>使用 {searchEngineName} 进行 InPrivate 搜索</p>
        
        <div class='search-box'>
            <input type='text' class='search-input' id='searchInput' 
                   placeholder='搜索或输入网址' autofocus>
            <button class='search-btn' onclick='doSearch()'>🔍</button>
        </div>
        
        <div class='shortcuts'>
            {shortcutsHtml}
        </div>
        
        <div class='info-cards'>
            <div class='info-card'>
                <h3>✓ 无痕浏览的功能</h3>
                <ul>
                    <li>关闭窗口时删除浏览数据</li>
                    <li>保存书签和下载的文件</li>
                </ul>
            </div>
            <div class='info-card'>
                <h3>✗ 无痕浏览不会</h3>
                <ul>
                    <li>对网络管理员隐藏活动</li>
                    <li>对网站隐藏您的 IP</li>
                </ul>
            </div>
        </div>
    </div>
    
    <div class='footer'>
        <a href='#' onclick='showMoreInfo(); return false;'>更多详细信息</a>
    </div>
    
    <script>
        const searchInput = document.getElementById('searchInput');
        const searchEngine = '{searchEngine}';
        
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
        
        function showMoreInfo() {{
            alert('隐身浏览模式\\n\\n在此模式下：\\n• 浏览历史不会被保存\\n• Cookie 和网站数据在关闭窗口后清除\\n• 表单数据不会被保存\\n\\n注意：下载的文件和书签会保留。');
        }}
    </script>
</body>
</html>";
    }
    
    private static string GetSearchEngineName(string searchEngine)
    {
        if (searchEngine.Contains("baidu")) return "百度";
        if (searchEngine.Contains("bing")) return "必应";
        if (searchEngine.Contains("google")) return "Google";
        return "搜索引擎";
    }
    
    /// <summary>
    /// 生成隐身模式快捷方式 HTML
    /// </summary>
    private string GenerateIncognitoShortcutsHtml()
    {
        var frequentSites = _mainHistoryService?.GetFrequentSites(6);
        
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
            var title = System.Net.WebUtility.HtmlEncode(site.Title);
            var url = System.Net.WebUtility.HtmlEncode(site.Url);
            var firstChar = GetFirstChar(site.Title, site.Domain);
            var faviconUrl = $"https://{System.Net.WebUtility.HtmlEncode(site.Domain)}/favicon.ico";
            
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
    
    #endregion

    #region 鼠标手势脚本
    
    private async Task InjectMouseGestureScript()
    {
        if (WebView.CoreWebView2 == null) return;
        
        string script = @"(function() {
            let isGesturing = false;
            let gesturePoints = [];
            let startX = 0, startY = 0;
            let canvas = null;
            let ctx = null;
            let tipBox = null;
            let currentGesture = '';
            
            const gestureNames = {
                'L': '后退', 'R': '前进', 'U': '滚动到顶部', 'D': '滚动到底部',
                'UD': '重新载入', 'DU': '新建标签页', 'DR': '关闭标签页', 'RD': '关闭标签页'
            };
            const gestureArrows = { 'L': '←', 'R': '→', 'U': '↑', 'D': '↓' };
            
            function createOverlay() {
                if (canvas) return;
                canvas = document.createElement('canvas');
                canvas.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:999998;pointer-events:none;';
                canvas.width = window.innerWidth;
                canvas.height = window.innerHeight;
                document.body.appendChild(canvas);
                ctx = canvas.getContext('2d');
                ctx.strokeStyle = '#8ab4f8';
                ctx.lineWidth = 3;
                ctx.lineCap = 'round';
                
                tipBox = document.createElement('div');
                tipBox.style.cssText = 'position:fixed;bottom:80px;right:40px;background:rgba(50,50,50,0.95);color:#fff;padding:15px 25px;border-radius:8px;z-index:999999;font-family:Microsoft YaHei UI;display:none;text-align:center;min-width:120px;';
                document.body.appendChild(tipBox);
            }
            
            function removeOverlay() {
                if (canvas) { canvas.remove(); canvas = null; ctx = null; }
                if (tipBox) { tipBox.remove(); tipBox = null; }
            }
            
            function drawLine(x1, y1, x2, y2) {
                if (!ctx) return;
                ctx.beginPath();
                ctx.moveTo(x1, y1);
                ctx.lineTo(x2, y2);
                ctx.stroke();
            }
            
            function updateTip(gesture) {
                if (!tipBox || !gesture) return;
                let arrows = gesture.split('').map(d => gestureArrows[d] || d).join(' ');
                let name = gestureNames[gesture] || '';
                tipBox.innerHTML = '<div style=""font-size:32px;margin-bottom:8px"">' + arrows + '</div>' + (name ? '<div style=""font-size:14px;color:#ccc"">' + name + '</div>' : '');
                tipBox.style.display = 'block';
            }
            
            document.addEventListener('mousedown', function(e) {
                // 左键点击时通知关闭弹出窗口
                if (e.button === 0) {
                    window.chrome.webview.postMessage({action: 'click'});
                }
                // 右键开始手势
                if (e.button === 2) {
                    isGesturing = true;
                    gesturePoints = [];
                    currentGesture = '';
                    startX = e.clientX;
                    startY = e.clientY;
                    gesturePoints.push({x: e.clientX, y: e.clientY});
                    createOverlay();
                }
            });
            
            document.addEventListener('mousemove', function(e) {
                if (isGesturing && gesturePoints.length > 0) {
                    let last = gesturePoints[gesturePoints.length - 1];
                    gesturePoints.push({x: e.clientX, y: e.clientY});
                    drawLine(last.x, last.y, e.clientX, e.clientY);
                    let newGesture = recognizeGesture(gesturePoints);
                    if (newGesture && newGesture !== currentGesture) {
                        currentGesture = newGesture;
                        updateTip(currentGesture);
                    }
                }
            });
            
            document.addEventListener('mouseup', function(e) {
                if (e.button === 2 && isGesturing) {
                    isGesturing = false;
                    let gesture = recognizeGesture(gesturePoints);
                    removeOverlay();
                    if (gesture) {
                        e.preventDefault();
                        window.chrome.webview.postMessage({action: 'gesture', gesture: gesture});
                    }
                    gesturePoints = [];
                    currentGesture = '';
                }
            });
            
            document.addEventListener('contextmenu', function(e) {
                let dx = Math.abs(e.clientX - startX);
                let dy = Math.abs(e.clientY - startY);
                if (dx > 30 || dy > 30) e.preventDefault();
            });
            
            function recognizeGesture(points) {
                if (points.length < 2) return null;
                let directions = [];
                let segmentStart = points[0];
                let lastDir = null;
                let minDist = 25;
                
                for (let i = 1; i < points.length; i++) {
                    let p = points[i];
                    let dx = p.x - segmentStart.x;
                    let dy = p.y - segmentStart.y;
                    let dist = Math.sqrt(dx*dx + dy*dy);
                    
                    if (dist > minDist) {
                        let dir = Math.abs(dx) > Math.abs(dy) ? (dx > 0 ? 'R' : 'L') : (dy > 0 ? 'D' : 'U');
                        if (dir !== lastDir) {
                            if (lastDir !== null) directions.push(lastDir);
                            lastDir = dir;
                            segmentStart = p;
                        }
                    }
                }
                if (lastDir !== null && (directions.length === 0 || directions[directions.length-1] !== lastDir))
                    directions.push(lastDir);
                return directions.length > 0 ? directions.join('') : null;
            }
        })();";
        
        await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }
    
    #endregion
    
    public void Dispose()
    {
        try
        {
            _isInitialized = false;
            
            if (WebView != null)
            {
                try
                {
                    // 取消事件订阅
                    if (WebView.CoreWebView2 != null)
                    {
                        try { WebView.CoreWebView2.DocumentTitleChanged -= OnDocumentTitleChanged; } catch { }
                        try { WebView.CoreWebView2.SourceChanged -= OnSourceChanged; } catch { }
                        try { WebView.CoreWebView2.NavigationStarting -= OnNavigationStarting; } catch { }
                        try { WebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted; } catch { }
                        try { WebView.CoreWebView2.NewWindowRequested -= OnNewWindowRequested; } catch { }
                        try { WebView.CoreWebView2.Stop(); } catch { }
                    }
                }
                catch { }
                
                try { WebView.Dispose(); } catch { }
            }
        }
        catch { }
    }
}
