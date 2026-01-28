using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Browser;

/// <summary>
/// 浏览器标签页封装
/// </summary>
public class BrowserTab : IDisposable
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public WebView2 WebView { get; private set; }
    public TabButton? TabButton { get; set; }
    
    public string Title { get; private set; } = "新标签页";
    public string Url { get; private set; } = "about:blank";
    public bool IsLoading { get; private set; }
    public bool IsSecure { get; private set; }
    public bool IsTranslated { get; set; }
    public string? FaviconUrl { get; private set; }
    public DateTime LastActiveTime { get; set; } = DateTime.Now;
    public bool IsSuspended { get; private set; }
    public bool BlockFind { get; set; }
    public bool IsIncognito => !string.IsNullOrEmpty(_incognitoUserDataFolder);
    
    public bool CanGoBack => _isInitialized && WebView.CoreWebView2?.CanGoBack == true;
    public bool CanGoForward => _isInitialized && WebView.CoreWebView2?.CanGoForward == true;
    
    // 事件
    public event Action<BrowserTab>? TitleChanged;
    public event Action<BrowserTab>? UrlChanged;
    public event Action<BrowserTab>? LoadingStateChanged;
    public event Action<BrowserTab, string>? NewWindowRequested;
    public event Action<BrowserTab>? FaviconChanged;
    public event Action<BrowserTab>? SecurityStateChanged;
    public event Action<BrowserTab, string>? StatusTextChanged;
    public event Action<BrowserTab, CoreWebView2DownloadStartingEventArgs>? DownloadStarting;
    public event Action<BrowserTab, string, string, string>? PasswordDetected; // host, username, password
    public event Action<BrowserTab, double>? ZoomChanged; // 缩放变化事件
    public event Action<BrowserTab>? TranslationRequested; // 翻译请求事件
    
    private readonly Panel _container;
    private readonly ISettingsService _settingsService;
    private readonly IHistoryService? _historyService;
    private readonly string _resourceLogPath;
    private bool _isInitialized;
    private readonly string? _incognitoUserDataFolder;
    private string _pendingUrl = "";
    private bool _pendingShow = false;  // 标记是否需要在内容加载后显示
    private bool _hasShownOnce = false; // 标记是否已经显示过一次
    
    // 静态环境缓存，防止并发初始化冲突 (0x8007139F)
    private static readonly Dictionary<string, CoreWebView2Environment> _environments = new();
    private static readonly SemaphoreSlim _envSemaphore = new(1, 1);
    
    // 待处理的密码凭据（用于在页面导航后显示保存弹窗）
    private (string host, string username, string password, DateTime timestamp)? _pendingCredentials;
    
    // 用于存储从 POST 请求中捕获的凭据
    private (string host, string username, string password, DateTime timestamp)? _capturedCredentialsFromPost;
    
    // 用于延迟检查凭据的定时器
    private System.Windows.Forms.Timer? _credentialCheckTimer;
    
    /// <summary>
    /// 获取用户数据目录（支持自定义缓存路径）
    /// </summary>
    public string GetUserDataFolder()
    {
        return GetUserDataFolder(_incognitoUserDataFolder, _settingsService);
    }

    /// <summary>
    /// 静态获取用户数据目录方法
    /// </summary>
    public static string GetUserDataFolder(string? incognitoFolder, ISettingsService? settingsService)
    {
        // 如果是隐身模式，使用传入的临时目录
        if (!string.IsNullOrEmpty(incognitoFolder))
        {
            return incognitoFolder;
        }

        // 检查是否使用自定义缓存路径
        if (settingsService?.Settings?.UseCustomCachePath == true && 
            !string.IsNullOrEmpty(settingsService.Settings.CustomCachePath))
        {
            return settingsService.Settings.CustomCachePath;
        }
        
        // 使用默认路径
        var userDataFolder = AppConstants.UserDataFolder;
        if (string.IsNullOrEmpty(userDataFolder))
        {
            userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MiniWorldBrowser", "UserData");
        }
        
        return userDataFolder;
    }

    /// <summary>
    /// 获取或创建共享的 WebView2 环境
    /// </summary>
    public static async Task<CoreWebView2Environment> GetSharedEnvironmentAsync(string userDataFolder, ISettingsService? settingsService)
    {
        await _envSemaphore.WaitAsync();
        try
        {
            if (_environments.TryGetValue(userDataFolder, out var existingEnv))
            {
                return existingEnv;
            }

            string? browserExecutableFolder = FindWebView2Runtime();
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--allow-running-insecure-content " +
                                           "--disable-blink-features=AutomationControlled " +
                                           "--ignore-gpu-blocklist " +
                                           "--enable-gpu-rasterization " +
                                           "--enable-zero-copy " +
                                           "--enable-features=SharedArrayBuffer,Canvas2dLayers " +
                                           "--use-angle=d3d11 " +
                                           "--enable-accelerated-2d-canvas " +
                                           "--enable-accelerated-video-decode"
            };

            // 加载 AI 扩展
            var extensionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "AiExtension");
            if (Directory.Exists(extensionPath))
            {
                options.AdditionalBrowserArguments += $" --load-extension=\"{extensionPath}\"";
            }
            else
            {
                // 开发环境下尝试查找源码目录
                var sourceExtensionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "AiExtension");
                if (Directory.Exists(sourceExtensionPath))
                {
                    options.AdditionalBrowserArguments += $" --load-extension=\"{Path.GetFullPath(sourceExtensionPath)}\"";
                }
            }

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: browserExecutableFolder,
                userDataFolder: userDataFolder,
                options: options);

            _environments[userDataFolder] = env;
            return env;
        }
        finally
        {
            _envSemaphore.Release();
        }
    }
    
    /// <summary>
    /// 查找打包的 WebView2 Runtime 路径
    /// </summary>
    public static string? FindWebView2Runtime()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // 检查 WebView2.Runtime.X64 包的路径 (WebView2 目录)
        var webView2Dir = Path.Combine(appDir, "WebView2");
        if (Directory.Exists(webView2Dir) && File.Exists(Path.Combine(webView2Dir, "msedgewebview2.exe")))
            return webView2Dir;
        
        // 检查 runtimes 目录
        var runtimeDir = Path.Combine(appDir, "runtimes", "win-x64", "native");
        if (Directory.Exists(runtimeDir) && File.Exists(Path.Combine(runtimeDir, "msedgewebview2.exe")))
            return runtimeDir;
        
        // 检查上级目录的 WebView2 (开发环境)
        var parentWebView2 = Path.Combine(appDir, "..", "..", "..", "..", "publish", "WebView2");
        if (Directory.Exists(parentWebView2) && File.Exists(Path.Combine(parentWebView2, "msedgewebview2.exe")))
            return Path.GetFullPath(parentWebView2);
        
        return null; // 使用系统安装的 WebView2
    }
    
    public BrowserTab(Panel container, ISettingsService settingsService, IHistoryService? historyService = null, string? incognitoUserDataFolder = null)
    {
        _container = container;
        _settingsService = settingsService;
        _historyService = historyService;
        _incognitoUserDataFolder = incognitoUserDataFolder;

        try
        {
            var logDir = GetUserDataFolder();
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            _resourceLogPath = Path.Combine(logDir, "webview2_resource_log.txt");
        }
        catch
        {
            _resourceLogPath = Path.Combine(Path.GetTempPath(), "MiniWorldBrowser_webview2_resource_log.txt");
        }
        
        WebView = new WebView2
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        _container.Controls.Add(WebView);
    }

    private void AppendResourceLog(string line)
    {
        try
        {
            File.AppendAllText(_resourceLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch { }
    }

    private void SetupResourceDiagnostics()
    {
        try
        {
            if (WebView?.CoreWebView2 == null) return;

            try
            {
                WebView.CoreWebView2.ServerCertificateErrorDetected += (sender, e) =>
                {
                    try
                    {
                        AppendResourceLog($"[CertError] url={e.RequestUri} status={e.ErrorStatus}");

                        try
                        {
                            var host = new Uri(e.RequestUri).Host;
                            if (!string.IsNullOrEmpty(host) && System.Net.IPAddress.TryParse(host, out var ip))
                            {
                                e.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
                                AppendResourceLog($"[CertError] action=AlwaysAllow host={host}");
                            }
                        }
                        catch { }
                    }
                    catch { }
                };
            }
            catch { }

            try
            {
                WebView.CoreWebView2.WebResourceResponseReceived += (_, e) =>
                {
                    try
                    {
                        var status = 0;
                        try { status = e.Response.StatusCode; } catch { }

                        if (status >= 400 || status == 0)
                        {
                            var contentType = "";
                            try { contentType = e.Response.Headers.GetHeader("Content-Type") ?? ""; } catch { }
                            AppendResourceLog($"[HTTP] {status} {e.Request.Uri} ct={contentType}");
                        }
                    }
                    catch { }
                };
            }
            catch { }
        }
        catch { }
    }
    
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            // 确保用户数据目录存在
            // 优先使用自定义缓存路径
            var userDataFolder = GetUserDataFolder();
            
            if (!Directory.Exists(userDataFolder))
                Directory.CreateDirectory(userDataFolder);
            
            // 使用共享环境初始化，防止并发冲突
            CoreWebView2Environment env = await GetSharedEnvironmentAsync(userDataFolder, _settingsService);

            // 初始化 WebView2
            await WebView.EnsureCoreWebView2Async(env);
            
            if (WebView.CoreWebView2 == null)
                throw new Exception("WebView2 CoreWebView2 初始化失败");
            
            _isInitialized = true;
            
            // 注册右键菜单事件
            WebView.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;
            
            // 配置设置
            var settings = WebView.CoreWebView2.Settings;
            if (settings != null)
            {
                settings.IsBuiltInErrorPageEnabled = true;
                settings.AreDefaultContextMenusEnabled = true;
                settings.AreDevToolsEnabled = true;
                
                // 设置标准 User-Agent，避免部分网站因识别为 WebView 而减少样式支持
                // 使用 Chrome 130 的标准 UA
                settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";
                
                // 启用更多功能支持
                settings.IsPasswordAutosaveEnabled = !IsIncognito; // 隐身模式不自动保存密码
                settings.IsGeneralAutofillEnabled = !IsIncognito; // 隐身模式不自动填充
                settings.IsWebMessageEnabled = true;
                settings.AreHostObjectsAllowed = true;
            }

            SetupResourceDiagnostics();

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
                            System.Diagnostics.Debug.WriteLine($"[WebView2][Console][{level}] {message}");
                            AppendResourceLog($"[Console][{level}] {message}");
                        }
                        else if (action == "resourceError")
                        {
                            var tag = msg.RootElement.TryGetProperty("tag", out var t) ? t.GetString() : "";
                            var url = msg.RootElement.TryGetProperty("url", out var u) ? u.GetString() : "";
                            var detail = msg.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : "";
                            var line = $"[ResourceError] tag={tag} url={url} {detail}";
                            System.Diagnostics.Debug.WriteLine($"[WebView2]{line}");
                            AppendResourceLog(line);
                        }
                        else if (action == "resourceDiag")
                        {
                            var message = msg.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "";
                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                AppendResourceLog($"[Diag] {message}");
                            }
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

  window.addEventListener('error', function(ev){
    try {
      var t = ev && ev.target;
      if (!t) return;
      var tag = (t.tagName || '').toLowerCase();
      if (tag === 'script' || tag === 'link' || tag === 'img') {
        var url = t.src || t.href || '';
        if (url) {
          window.chrome.webview.postMessage({ action: 'resourceError', tag: tag, url: url, detail: 'load error' });
        }
      }
    } catch (e) {}
  }, true);

  try {
    if (document.fonts && document.fonts.addEventListener) {
      document.fonts.addEventListener('loadingerror', function(){
        try { window.chrome.webview.postMessage({ action: 'resourceError', tag: 'font', url: '', detail: 'font loadingerror' }); } catch (e) {}
      });
    }
  } catch (e) {}

  try {
    function sendDiag(payload) {
      try {
        if (window.chrome && window.chrome.webview) {
          window.chrome.webview.postMessage({ action: 'resourceDiag', message: JSON.stringify(payload) });
        }
      } catch (e) {}
    }

    function diagOnce() {
      try {
        var links = Array.prototype.slice.call(document.querySelectorAll('link[rel~=""stylesheet""]')).map(function(l){
          return { href: l.href || '', hasSheet: !!l.sheet, media: l.media || '', disabled: !!l.disabled };
        });
        var scripts = Array.prototype.slice.call(document.querySelectorAll('script[src]')).map(function(s){ return s.src || ''; }).filter(Boolean);
        var uses = Array.prototype.slice.call(document.querySelectorAll('use')).map(function(u){
          return u.getAttribute('href') || u.getAttribute('xlink:href') || '';
        }).filter(Boolean);
        sendDiag({ type: 'dom', href: location.href, stylesheetCount: links.length, scriptCount: scripts.length, svgUseCount: uses.length, links: links.slice(0, 30), scripts: scripts.slice(0, 30), uses: uses.slice(0, 30) });
      } catch (e) {
        sendDiag({ type: 'dom', href: location.href, error: String(e && e.message ? e.message : e) });
      }
    }

    if (document.readyState === 'complete') {
      setTimeout(diagOnce, 800);
    } else {
      window.addEventListener('load', function(){ setTimeout(diagOnce, 800); }, { once: true });
    }
  } catch (e) {}

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
            try { WebView.CoreWebView2.DownloadStarting += OnDownloadStarting; } catch { }
            try { WebView.ZoomFactorChanged += OnZoomFactorChanged; } catch { }
            
            // 设置下载对话框位置 - 右上角，紧贴工具栏
            try 
            { 
                WebView.CoreWebView2.DefaultDownloadDialogCornerAlignment = CoreWebView2DefaultDownloadDialogCornerAlignment.TopRight;
                WebView.CoreWebView2.DefaultDownloadDialogMargin = new System.Drawing.Point(8, 0);
            } 
            catch { }
            
            // 注入超级拖拽脚本（根据设置决定是否启用）
            if (_settingsService?.Settings?.EnableSuperDrag == true)
            {
                try { await InjectSuperDragScript(); } catch { }
            }
            
            // 注入鼠标手势脚本（根据设置决定是否启用）
            if (_settingsService?.Settings?.EnableMouseGesture == true)
            {
                try { await InjectMouseGestureScript(); } catch { }
            }
            
            // 注入密码检测脚本（根据设置决定是否启用）
            if (_settingsService?.Settings?.SavePasswords == true)
            {
                try 
                { 
                    await InjectPasswordDetectionScript();
                    
                    // 设置 POST 请求拦截器来捕获登录凭据
                    SetupPostRequestInterceptor();
                    
                    // 监听 iframe 创建事件，在 iframe 中也注入脚本
                    try
                    {
                        WebView.CoreWebView2.FrameCreated += OnFrameCreated;
                    }
                    catch { }
                } 
                catch { }
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
            throw new Exception($"WebView2 初始化失败: {ex.Message}", ex);
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
    
    private async void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        try
        {
            IsTranslated = false;
            IsLoading = true;
            LoadingStateChanged?.Invoke(this);
            
            // 对于 about: 页面，跳过密码捕获
            if (e.Uri.StartsWith("about:") || e.Uri.StartsWith("data:"))
            {
                return;
            }
            
            // 在页面导航前尝试捕获密码
            var savePasswordsEnabled = _settingsService?.Settings?.SavePasswords;
            
            if (savePasswordsEnabled == true && WebView?.CoreWebView2 != null)
            {
                try
                {
                    // 执行脚本获取当前页面的凭据（包括从 localStorage 读取）
                    var script = @"
                        (function() {
                            var result = { username: '', password: '', host: window.location.hostname, source: 'input' };
                            
                            // 首先尝试从 localStorage 读取（由密码检测脚本保存）
                            try {
                                var savedData = localStorage.getItem('_miniworld_pwd_data');
                                if (savedData) {
                                    var data = JSON.parse(savedData);
                                    // 检查是否是最近 30 秒内保存的
                                    if (data && data.timestamp && (Date.now() - data.timestamp) < 30000) {
                                        if (data.username && data.password) {
                                            result.username = data.username;
                                            result.password = data.password;
                                            result.host = data.host || result.host;
                                            result.source = 'localStorage';
                                            // 清除已使用的数据
                                            localStorage.removeItem('_miniworld_pwd_data');
                                            return JSON.stringify(result);
                                        }
                                    } else {
                                        // 数据过期，清除
                                        localStorage.removeItem('_miniworld_pwd_data');
                                    }
                                }
                            } catch (e) {}
                            
                            // 如果 localStorage 没有数据，从输入框读取
                            var inputs = document.querySelectorAll('input');
                            for (var i = 0; i < inputs.length; i++) {
                                var input = inputs[i];
                                if (!input.value) continue;
                                if (input.type === 'password') {
                                    result.password = input.value;
                                } else if (input.type === 'text' || input.type === 'email' || input.type === 'tel') {
                                    if (!result.username) result.username = input.value;
                                }
                            }
                            return JSON.stringify(result);
                        })();
                    ";
                    
                    var resultJson = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                    
                    if (!string.IsNullOrEmpty(resultJson) && resultJson != "null")
                    {
                        // 移除外层引号并解析
                        var unescaped = System.Text.Json.JsonSerializer.Deserialize<string>(resultJson);
                        if (!string.IsNullOrEmpty(unescaped))
                        {
                            var result = System.Text.Json.JsonDocument.Parse(unescaped);
                            var username = result.RootElement.GetProperty("username").GetString() ?? "";
                            var password = result.RootElement.GetProperty("password").GetString() ?? "";
                            var host = result.RootElement.GetProperty("host").GetString() ?? "";
                            
                            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(host))
                            {
                                // 存储待处理的凭据，在 NavigationCompleted 中触发事件
                                _pendingCredentials = (host, username, password, DateTime.Now);
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略错误
                }
            }
        }
        catch
        {
            // 忽略错误
        }
    }
    
    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            IsLoading = false;

            try
            {
                AppendResourceLog($"[NavCompleted] success={e.IsSuccess} status={e.WebErrorStatus} url={Url}");
            }
            catch { }
            
            // 对于 about: 页面，跳过密码检测
            if (Url.StartsWith("about:") || Url.StartsWith("data:"))
            {
                LoadingStateChanged?.Invoke(this);
                return;
            }

            try
            {
                var script = "(function(){try{var links=[].slice.call(document.querySelectorAll('link[rel~=stylesheet]')).map(function(l){return {href:l.href||'',hasSheet:!!l.sheet,media:l.media||'',disabled:!!l.disabled};});var scripts=[].slice.call(document.querySelectorAll('script[src]')).map(function(s){return s.src||'';}).filter(Boolean);var uses=[].slice.call(document.querySelectorAll('use')).map(function(u){return u.getAttribute('href')||u.getAttribute('xlink:href')||'';}).filter(Boolean);if(window.chrome&&window.chrome.webview){window.chrome.webview.postMessage({action:'resourceDiag',message:JSON.stringify({type:'dom2',href:location.href,stylesheetCount:links.length,scriptCount:scripts.length,svgUseCount:uses.length,links:links.slice(0,30),scripts:scripts.slice(0,30),uses:uses.slice(0,30)})});}}catch(e){}})();";
                _ = WebView?.CoreWebView2?.ExecuteScriptAsync(script);
            }
            catch { }
            
            // 优先检查从 POST 请求捕获的凭据（最可靠的方式）
            // 注意：POST 中的密码可能是加密的，需要结合 JS 端捕获的原始密码
            if (_capturedCredentialsFromPost.HasValue)
            {
                var creds = _capturedCredentialsFromPost.Value;
                // 检查是否是最近 30 秒内保存的
                if ((DateTime.Now - creds.timestamp).TotalSeconds < 30)
                {
                    // 尝试从 localStorage 获取原始密码（JS 端保存的）
                    var originalCreds = await TryGetCredentialsFromLocalStorage();
                    if (originalCreds.HasValue)
                    {
                        PasswordDetected?.Invoke(this, originalCreds.Value.host, originalCreds.Value.username, originalCreds.Value.password);
                    }
                    else
                    {
                        // 如果 localStorage 没有，使用 POST 捕获的（可能是加密密码）
                        PasswordDetected?.Invoke(this, creds.host, creds.username, creds.password);
                    }
                    _capturedCredentialsFromPost = null;
                    _pendingCredentials = null;
                    LoadingStateChanged?.Invoke(this);
                    return;
                }
                _capturedCredentialsFromPost = null;
            }
            
            // 检查是否有待处理的凭据（在 NavigationStarting 中保存的）
            if (_pendingCredentials.HasValue)
            {
                var creds = _pendingCredentials.Value;
                // 检查是否是最近 30 秒内保存的
                if ((DateTime.Now - creds.timestamp).TotalSeconds < 30)
                {
                    PasswordDetected?.Invoke(this, creds.host, creds.username, creds.password);
                }
                _pendingCredentials = null;
            }
            
            // 在页面加载完成后检查 localStorage 中是否有保存的凭据
            if (_settingsService?.Settings?.SavePasswords == true && WebView?.CoreWebView2 != null)
            {
                var localCreds = await TryGetCredentialsFromLocalStorage();
                if (localCreds.HasValue)
                {
                    PasswordDetected?.Invoke(this, localCreds.Value.host, localCreds.Value.username, localCreds.Value.password);
                }
            }
            
            LoadingStateChanged?.Invoke(this);
        }
        catch { }
    }
    
    /// <summary>
    /// 尝试从 localStorage 或 window.name 获取凭据
    /// </summary>
    private async Task<(string host, string username, string password)?> TryGetCredentialsFromLocalStorage()
    {
        if (WebView?.CoreWebView2 == null) return null;
        
        try
        {
            // 检查 localStorage 和 window.name
            var script = @"
                (function() {
                    try {
                        console.log('[MiniWorld] TryGetCredentials - checking storage...');
                        console.log('[MiniWorld] window.name:', window.name ? window.name.substring(0, 100) : 'empty');
                        
                        // 首先检查 window.name（跨域也能保持）
                        if (window.name && window.name.startsWith('{')) {
                            try {
                                var nameData = JSON.parse(window.name);
                                if (nameData._miniworld_pwd) {
                                    var data = typeof nameData._miniworld_pwd === 'string' 
                                        ? JSON.parse(nameData._miniworld_pwd) 
                                        : nameData._miniworld_pwd;
                                    console.log('[MiniWorld] Found data in window.name, timestamp:', data.timestamp, 'age:', Date.now() - data.timestamp);
                                    if (data && data.timestamp && (Date.now() - data.timestamp) < 60000) {
                                        // 清除已使用的数据
                                        delete nameData._miniworld_pwd;
                                        window.name = Object.keys(nameData).length > 0 ? JSON.stringify(nameData) : '';
                                        console.log('[MiniWorld] Returning credentials from window.name');
                                        return JSON.stringify(data);
                                    }
                                }
                            } catch (e) {
                                console.log('[MiniWorld] Error parsing window.name:', e);
                            }
                        }
                        
                        // 检查主页面的 localStorage（两个键都检查）
                        var keys = ['_miniworld_pwd_data', '_miniworld_cred'];
                        for (var k = 0; k < keys.length; k++) {
                            var savedData = localStorage.getItem(keys[k]);
                            console.log('[MiniWorld] localStorage[' + keys[k] + ']:', savedData ? 'found' : 'not found');
                            if (savedData) {
                                try {
                                    var data = JSON.parse(savedData);
                                    console.log('[MiniWorld] localStorage timestamp:', data.timestamp, 'age:', Date.now() - data.timestamp);
                                    if (data && data.timestamp && (Date.now() - data.timestamp) < 60000) {
                                        localStorage.removeItem(keys[k]);
                                        console.log('[MiniWorld] Returning credentials from localStorage[' + keys[k] + ']');
                                        return savedData;
                                    } else {
                                        localStorage.removeItem(keys[k]);
                                    }
                                } catch (parseErr) {
                                    console.log('[MiniWorld] Error parsing localStorage:', parseErr);
                                }
                            }
                        }
                        
                        // 尝试检查同源 iframe 的 localStorage
                        var iframes = document.querySelectorAll('iframe');
                        console.log('[MiniWorld] Checking', iframes.length, 'iframes');
                        for (var i = 0; i < iframes.length; i++) {
                            try {
                                var iframeWin = iframes[i].contentWindow;
                                if (iframeWin && iframeWin.localStorage) {
                                    for (var k = 0; k < keys.length; k++) {
                                        var iframeData = iframeWin.localStorage.getItem(keys[k]);
                                        if (iframeData) {
                                            var data = JSON.parse(iframeData);
                                            if (data && data.timestamp && (Date.now() - data.timestamp) < 60000) {
                                                iframeWin.localStorage.removeItem(keys[k]);
                                                console.log('[MiniWorld] Returning credentials from iframe localStorage[' + keys[k] + ']');
                                                return iframeData;
                                            } else {
                                                iframeWin.localStorage.removeItem(keys[k]);
                                            }
                                        }
                                    }
                                }
                            } catch (iframeErr) {
                                // 跨域 iframe 无法访问
                                console.log('[MiniWorld] Cannot access iframe', i, '- cross-origin');
                            }
                        }
                        console.log('[MiniWorld] No credentials found in storage');
                    } catch (e) {
                        console.log('[MiniWorld] TryGetCredentials error:', e);
                    }
                    return null;
                })();
            ";
            
            var resultJson = await WebView.CoreWebView2.ExecuteScriptAsync(script);
            
            if (!string.IsNullOrEmpty(resultJson) && resultJson != "null" && resultJson != "\"null\"")
            {
                var unescaped = System.Text.Json.JsonSerializer.Deserialize<string>(resultJson);
                if (!string.IsNullOrEmpty(unescaped))
                {
                    var result = System.Text.Json.JsonDocument.Parse(unescaped);
                    var username = result.RootElement.GetProperty("username").GetString() ?? "";
                    var password = result.RootElement.GetProperty("password").GetString() ?? "";
                    var host = result.RootElement.GetProperty("host").GetString() ?? "";
                    
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(host))
                    {
                        return (host, username, password);
                    }
                }
            }
        }
        catch
        {
            // 忽略错误
        }
        
        return null;
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
    
    private void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        try
        {
            if (WebView.CoreWebView2?.Environment == null) return;
            
            var menuItems = e.MenuItems;
            var coreWebView2 = WebView.CoreWebView2;
            
            // 1. 处理链接相关菜单
            if (e.ContextMenuTarget.HasLinkUri)
            {
                var linkUri = e.ContextMenuTarget.LinkUri;
                
                var openInNewTab = coreWebView2.Environment.CreateContextMenuItem(
                    "在新标签页中打开", null, CoreWebView2ContextMenuItemKind.Command);
                openInNewTab.CustomItemSelected += (s, args) => NewWindowRequested?.Invoke(this, linkUri);
                menuItems.Insert(0, openInNewTab);
                
                var copyLink = coreWebView2.Environment.CreateContextMenuItem(
                    "复制链接地址", null, CoreWebView2ContextMenuItemKind.Command);
                copyLink.CustomItemSelected += (s, args) => { try { Clipboard.SetText(linkUri); } catch { } };
                menuItems.Insert(1, copyLink);
                
                var separator = coreWebView2.Environment.CreateContextMenuItem(
                    "", null, CoreWebView2ContextMenuItemKind.Separator);
                menuItems.Insert(2, separator);
            }
            
            // 2. 处理选中文本相关菜单
            if (e.ContextMenuTarget.HasSelection)
            {
                var selectedText = e.ContextMenuTarget.SelectionText;
                if (!string.IsNullOrEmpty(selectedText) && selectedText.Length < 50)
                {
                    var displayText = selectedText.Length > 20 ? selectedText[..20] + "..." : selectedText;
                    var searchEngine = _settingsService?.Settings?.SearchEngine ?? Constants.AppConstants.DefaultSearchEngine;
                    var searchItem = coreWebView2.Environment.CreateContextMenuItem(
                        $"搜索 \"{displayText}\"", null, CoreWebView2ContextMenuItemKind.Command);
                    searchItem.CustomItemSelected += (s, args) =>
                    {
                        NewWindowRequested?.Invoke(this, searchEngine + Uri.EscapeDataString(selectedText));
                    };
                    menuItems.Insert(0, searchItem);
                    
                    var separator = coreWebView2.Environment.CreateContextMenuItem(
                        "", null, CoreWebView2ContextMenuItemKind.Separator);
                    menuItems.Insert(1, separator);
                }
            }

            // 3. 处理翻译菜单 (Edge 风格)
            var translateItem = coreWebView2.Environment.CreateContextMenuItem(
                "翻译为中文",
                null, 
                CoreWebView2ContextMenuItemKind.Command);

            translateItem.CustomItemSelected += (s, args) =>
            {
                TranslationRequested?.Invoke(this);
            };

            // 寻找“全选”后的位置插入翻译选项
            int insertIndex = menuItems.Count;
            for (int i = 0; i < menuItems.Count; i++)
            {
                if (menuItems[i].Name == "selectAll")
                {
                    insertIndex = i + 1;
                    break;
                }
            }
            
            var transSeparator = coreWebView2.Environment.CreateContextMenuItem(
                string.Empty, null, CoreWebView2ContextMenuItemKind.Separator);
            
            if (insertIndex < menuItems.Count)
            {
                menuItems.Insert(insertIndex, transSeparator);
                menuItems.Insert(insertIndex + 1, translateItem);
            }
            else
            {
                menuItems.Add(transSeparator);
                menuItems.Add(translateItem);
            }
        }
        catch { }
    }
    
    private void OnFrameCreated(object? sender, CoreWebView2FrameCreatedEventArgs e)
    {
        try
        {
            var frameName = e.Frame.Name;
            var frame = e.Frame;
            
            // 检测是否是登录相关的 iframe
            if (frameName.Contains("login") || frameName.Contains("Login"))
            {
                // 尝试在 frame 的 DOMContentLoaded 事件中注入脚本
                try
                {
                    frame.DOMContentLoaded += (s, args) =>
                    {
                        try
                        {
                            // 在 UI 线程上执行脚本注入
                            if (WebView?.InvokeRequired == true)
                            {
                                WebView.Invoke(() => InjectScriptToFrameAsync(frame));
                            }
                            else
                            {
                                _ = InjectScriptToFrameAsync(frame);
                            }
                        }
                        catch { }
                    };
                }
                catch { }
                
                // 同时启动密码轮询作为后备
                StartPasswordPolling(frameName);
            }
        }
        catch { }
    }
    
    /// <summary>
    /// 尝试在 frame 中注入密码捕获脚本
    /// </summary>
    private async Task InjectScriptToFrameAsync(CoreWebView2Frame frame)
    {
        try
        {
            // 这个脚本会：
            // 1. 立即读取当前输入框中的值（捕获已输入的密码）
            // 2. 监听 keydown 事件捕获后续输入
            // 3. 在登录时保存凭据
            var script = @"(function() {
                if (window._mw_pwd_capture) return 'already';
                window._mw_pwd_capture = true;
                
                var host = window.location.hostname;
                console.log('[MiniWorld] [iframe:' + host + '] Password capture script injected');
                
                var rawPassword = '';
                var rawUsername = '';
                var pwdInputs = {};
                
                function isPwdInput(inp) {
                    if (inp.type === 'password') return true;
                    var n = (inp.name || inp.id || '').toLowerCase();
                    return n.indexOf('pass') >= 0 || n.indexOf('pwd') >= 0;
                }
                
                function saveCredentials() {
                    if (!rawUsername || !rawPassword) {
                        console.log('[MiniWorld] [iframe] saveCredentials: missing data, user:', rawUsername, 'pwd len:', rawPassword.length);
                        return;
                    }
                    var data = {
                        host: host,
                        username: rawUsername,
                        password: rawPassword,
                        timestamp: Date.now(),
                        source: 'frame_inject'
                    };
                    console.log('[MiniWorld] [iframe:' + host + '] Saving credentials, pwd len:', rawPassword.length);
                    
                    try { localStorage.setItem('_miniworld_pwd_data', JSON.stringify(data)); } catch(e) { console.log('[MiniWorld] localStorage error:', e); }
                    try { localStorage.setItem('_miniworld_cred', JSON.stringify(data)); } catch(e) {}
                    
                    // 保存到顶层窗口的 name（跨域也能保持）
                    try {
                        var w = window.top || window.parent || window;
                        var d = {};
                        try { if (w.name && w.name[0] === '{') d = JSON.parse(w.name); } catch(e){}
                        d._miniworld_pwd = JSON.stringify(data);
                        w.name = JSON.stringify(d);
                        console.log('[MiniWorld] [iframe] Saved to window.name');
                    } catch(e) { console.log('[MiniWorld] window.name error:', e); }
                    
                    // 发送 postMessage 到父窗口
                    try {
                        window.parent.postMessage({ type: 'miniworld_credentials', data: data }, '*');
                        console.log('[MiniWorld] [iframe] postMessage sent');
                    } catch(e) { console.log('[MiniWorld] postMessage error:', e); }
                }
                
                // 判断是否是用户名输入框
                function isUsernameInput(inp) {
                    // 排除隐藏字段
                    if (inp.type === 'hidden') return false;
                    // 排除密码字段
                    if (isPwdInput(inp)) return false;
                    // 必须是文本类型
                    if (inp.type !== 'text' && inp.type !== 'email' && inp.type !== 'tel') return false;
                    // 必须可见
                    if (inp.offsetParent === null) return false;
                    
                    var n = (inp.name || inp.id || '').toLowerCase();
                    // 排除明显不是用户名的字段
                    var excludes = ['captcha', 'code', 'verify', 'search', 'from', 'redirect', 'url', 'css', 'handler', 'mode', 'layout', 'bizid', 'appid', 'gameid', 'sessionid', 'divid', 'level', 'label', 'tip'];
                    for (var i = 0; i < excludes.length; i++) {
                        if (n.indexOf(excludes[i]) >= 0) return false;
                    }
                    // 优先匹配用户名相关字段
                    var includes = ['user', 'name', 'account', 'login', 'email', 'phone', 'mobile', 'tel'];
                    for (var i = 0; i < includes.length; i++) {
                        if (n.indexOf(includes[i]) >= 0) return true;
                    }
                    // 如果没有明确匹配，但是可见的文本输入框，也可能是用户名
                    return true;
                }
                
                // 立即读取当前输入框中的值（捕获已输入的内容）
                function captureExistingInputs() {
                    var inputs = document.querySelectorAll('input');
                    console.log('[MiniWorld] [iframe] Checking', inputs.length, 'existing inputs');
                    for (var i = 0; i < inputs.length; i++) {
                        var inp = inputs[i];
                        if (!inp.value) continue;
                        
                        var n = (inp.name || inp.id || '').toLowerCase();
                        console.log('[MiniWorld] [iframe] Input:', inp.type, n, 'value len:', inp.value.length, 'visible:', inp.offsetParent !== null);
                        
                        if (isPwdInput(inp)) {
                            // 注意：这里获取的可能是加密后的值，但我们仍然记录
                            // 真正的原始密码需要通过 keydown 捕获
                            console.log('[MiniWorld] [iframe] Found password input with value, len:', inp.value.length);
                        } else if (isUsernameInput(inp)) {
                            if (!rawUsername) {
                                rawUsername = inp.value;
                                console.log('[MiniWorld] [iframe] Found existing username:', rawUsername);
                            }
                        }
                    }
                }
                
                // keydown 捕获原始按键（在网站加密之前）
                document.addEventListener('keydown', function(e) {
                    var t = e.target;
                    if (!t || t.tagName !== 'INPUT') return;
                    var inputId = t.name || t.id || 'unknown';
                    
                    if (isPwdInput(t)) {
                        if (!pwdInputs[inputId]) pwdInputs[inputId] = '';
                        if (e.key === 'Backspace') {
                            pwdInputs[inputId] = pwdInputs[inputId].slice(0, -1);
                        } else if (e.key === 'Delete') {
                            pwdInputs[inputId] = '';
                        } else if (e.key.length === 1 && !e.ctrlKey && !e.altKey && !e.metaKey) {
                            pwdInputs[inputId] += e.key;
                        }
                        rawPassword = pwdInputs[inputId];
                        console.log('[MiniWorld] [iframe] keydown pwd len:', rawPassword.length);
                        // 每次按键都保存（确保在提交前保存）
                        if (rawUsername) saveCredentials();
                    }
                }, true);
                
                // input 事件捕获用户名
                document.addEventListener('input', function(e) {
                    var t = e.target;
                    if (!t || t.tagName !== 'INPUT') return;
                    if (isUsernameInput(t) && t.value) {
                        rawUsername = t.value;
                        console.log('[MiniWorld] [iframe] input user:', rawUsername);
                        if (rawPassword) saveCredentials();
                    }
                }, true);
                
                // 提交时保存
                document.addEventListener('submit', function(e) { 
                    console.log('[MiniWorld] [iframe] form submit');
                    saveCredentials(); 
                }, true);
                
                // 点击登录按钮时保存
                document.addEventListener('click', function(e) {
                    var t = e.target;
                    while (t && t !== document.body) {
                        if (t.tagName === 'BUTTON' || t.tagName === 'A' || (t.tagName === 'INPUT' && (t.type === 'submit' || t.type === 'button'))) {
                            var txt = (t.textContent || t.value || '').toLowerCase();
                            if (/登录|登陆|login|sign/i.test(txt)) { 
                                console.log('[MiniWorld] [iframe] login button click');
                                saveCredentials(); 
                                break; 
                            }
                        }
                        t = t.parentElement;
                    }
                }, true);
                
                // Enter 键提交
                document.addEventListener('keydown', function(e) {
                    if (e.key === 'Enter' && rawPassword) {
                        console.log('[MiniWorld] [iframe] Enter key');
                        setTimeout(saveCredentials, 10);
                    }
                }, true);
                
                // 立即捕获已存在的输入
                captureExistingInputs();
                
                // ========== iframe 密码填充功能 ==========
                var savedPasswords = [];
                var passwordDropdown = null;
                var currentUsernameInput = null;
                var dropdownClosed = false; // 标记是否被用户手动关闭
                
                function createPasswordDropdown() {
                    // 如果已存在，先移除
                    var existing = document.getElementById('_miniworld_pwd_dropdown');
                    if (existing) existing.remove();
                    
                    passwordDropdown = document.createElement('div');
                    passwordDropdown.id = '_miniworld_pwd_dropdown';
                    passwordDropdown.style.cssText = 'position:absolute;background:white;border:1px solid #ccc;border-radius:4px;box-shadow:0 2px 10px rgba(0,0,0,0.2);z-index:999999;max-height:200px;overflow-y:auto;display:none;min-width:200px;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;font-size:14px;';
                    
                    var header = document.createElement('div');
                    header.style.cssText = 'padding:8px 12px;background:#f5f5f5;border-bottom:1px solid #eee;font-weight:500;color:#333;display:flex;justify-content:space-between;align-items:center;';
                    header.innerHTML = '<span>保存的数据</span>';
                    
                    var closeBtn = document.createElement('span');
                    closeBtn.style.cssText = 'cursor:pointer;color:#999;font-size:16px;padding:0 4px;';
                    closeBtn.textContent = '×';
                    closeBtn.onclick = function(e) {
                        e.preventDefault();
                        e.stopPropagation();
                        passwordDropdown.style.display = 'none';
                        dropdownClosed = true;
                        console.log('[MiniWorld] [iframe] Dropdown closed by user');
                    };
                    header.appendChild(closeBtn);
                    passwordDropdown.appendChild(header);
                    
                    document.body.appendChild(passwordDropdown);
                    return passwordDropdown;
                }
                
                function showPasswordDropdown(input) {
                    if (savedPasswords.length === 0) {
                        console.log('[MiniWorld] [iframe] No saved passwords to show');
                        return;
                    }
                    if (dropdownClosed) {
                        console.log('[MiniWorld] [iframe] Dropdown was closed by user, not showing');
                        return;
                    }
                    
                    currentUsernameInput = input;
                    var dropdown = createPasswordDropdown();
                    
                    // 添加密码选项
                    savedPasswords.forEach(function(pwd) {
                        var item = document.createElement('div');
                        item.style.cssText = 'padding:10px 12px;cursor:pointer;border-bottom:1px solid #f0f0f0;background:white;';
                        item.innerHTML = '<div style=""color:#333;"">' + pwd.username + '</div>';
                        item.onmouseover = function() { item.style.background = '#f0f7ff'; };
                        item.onmouseout = function() { item.style.background = 'white'; };
                        item.onclick = function(e) {
                            e.preventDefault();
                            e.stopPropagation();
                            console.log('[MiniWorld] [iframe] Password item clicked:', pwd.username);
                            // 先隐藏下拉框
                            dropdown.style.display = 'none';
                            dropdownClosed = true; // 防止立即重新打开
                            // 然后填充密码
                            fillPassword(pwd);
                        };
                        dropdown.appendChild(item);
                    });
                    
                    var rect = input.getBoundingClientRect();
                    dropdown.style.left = (rect.left + window.scrollX) + 'px';
                    dropdown.style.top = (rect.bottom + window.scrollY + 2) + 'px';
                    dropdown.style.minWidth = Math.max(rect.width, 200) + 'px';
                    dropdown.style.display = 'block';
                    console.log('[MiniWorld] [iframe] Dropdown shown with', savedPasswords.length, 'passwords');
                }
                
                function hidePasswordDropdown() {
                    if (passwordDropdown) passwordDropdown.style.display = 'none';
                }
                
                function fillPassword(pwd) {
                    console.log('[MiniWorld] [iframe] fillPassword called:', pwd.username, 'pwd:', pwd.password ? ('yes, len:' + pwd.password.length) : 'no');
                    
                    // 查找用户名输入框
                    var usernameInput = currentUsernameInput;
                    if (!usernameInput) {
                        usernameInput = document.querySelector('input[type=""text""]:not([type=""hidden""]), input[type=""email""], input[type=""tel""]');
                    }
                    
                    // 查找密码输入框
                    var passwordInput = document.querySelector('input[type=""password""]');
                    
                    console.log('[MiniWorld] [iframe] usernameInput:', usernameInput ? 'found' : 'not found');
                    console.log('[MiniWorld] [iframe] passwordInput:', passwordInput ? 'found' : 'not found');
                    
                    // 填充用户名（不触发focus，避免重新打开下拉框）
                    if (usernameInput) {
                        usernameInput.value = pwd.username;
                        usernameInput.dispatchEvent(new Event('input', { bubbles: true }));
                        usernameInput.dispatchEvent(new Event('change', { bubbles: true }));
                        console.log('[MiniWorld] [iframe] Username filled:', pwd.username);
                    }
                    
                    // 填充密码
                    if (passwordInput && pwd.password) {
                        passwordInput.value = pwd.password;
                        passwordInput.dispatchEvent(new Event('input', { bubbles: true }));
                        passwordInput.dispatchEvent(new Event('change', { bubbles: true }));
                        // 最后聚焦到密码框
                        passwordInput.focus();
                        console.log('[MiniWorld] [iframe] Password filled, length:', pwd.password.length);
                    } else {
                        console.log('[MiniWorld] [iframe] Password NOT filled - input:', !!passwordInput, 'pwd:', !!pwd.password);
                    }
                }
                
                // 监听来自父窗口的消息
                window.addEventListener('message', function(e) {
                    if (e.data && e.data.type === 'miniworld_fill_passwords') {
                        savedPasswords = e.data.passwords || [];
                        console.log('[MiniWorld] [iframe] Received', savedPasswords.length, 'passwords');
                        if (savedPasswords.length > 0) {
                            console.log('[MiniWorld] [iframe] First password:', savedPasswords[0].username, 'has pwd:', !!savedPasswords[0].password);
                        }
                        if (savedPasswords.length > 0 && currentUsernameInput && !dropdownClosed) {
                            showPasswordDropdown(currentUsernameInput);
                        }
                    }
                }, false);
                
                // 请求密码
                function requestPasswords() {
                    try {
                        window.parent.postMessage({ type: 'miniworld_request_passwords', host: host }, '*');
                        console.log('[MiniWorld] [iframe] Requested passwords for:', host);
                    } catch(e) {}
                }
                
                // 设置自动填充
                function setupAutofill() {
                    var inputs = document.querySelectorAll('input[type=""text""], input[type=""email""], input[type=""tel""]');
                    console.log('[MiniWorld] [iframe] setupAutofill: found', inputs.length, 'inputs');
                    
                    for (var i = 0; i < inputs.length; i++) {
                        var input = inputs[i];
                        if (input._mw_autofill) continue;
                        if (input.type === 'hidden') continue;
                        input._mw_autofill = true;
                        
                        input.addEventListener('focus', function(e) {
                            console.log('[MiniWorld] [iframe] Input focused');
                            currentUsernameInput = e.target;
                            dropdownClosed = false; // 重新聚焦时重置关闭状态
                            if (savedPasswords.length > 0) {
                                showPasswordDropdown(e.target);
                            } else {
                                requestPasswords();
                            }
                        });
                        
                        input.addEventListener('click', function(e) {
                            console.log('[MiniWorld] [iframe] Input clicked');
                            currentUsernameInput = e.target;
                            dropdownClosed = false; // 点击时重置关闭状态
                            if (savedPasswords.length > 0) {
                                showPasswordDropdown(e.target);
                            } else {
                                requestPasswords();
                            }
                        });
                    }
                    
                    document.addEventListener('click', function(e) {
                        if (passwordDropdown && !passwordDropdown.contains(e.target) && e.target !== currentUsernameInput) {
                            hidePasswordDropdown();
                        }
                    });
                }
                
                setTimeout(function() {
                    requestPasswords();
                    setupAutofill();
                }, 300);
                
                console.log('[MiniWorld] [iframe:' + host + '] Capture script ready');
                return 'loaded';
            })();";
            
            await frame.ExecuteScriptAsync(script);
        }
        catch { }
    }
    
    private System.Windows.Forms.Timer? _passwordPollingTimer;
    private string _polledPassword = "";
    private string _polledUsername = "";
    
    /// <summary>
    /// 启动密码轮询（用于跨域 iframe 场景）
    /// 通过主页面脚本定期检查 iframe 中的输入框
    /// </summary>
    private void StartPasswordPolling(string frameName)
    {
        // 在 UI 线程上操作
        if (WebView?.InvokeRequired == true)
        {
            WebView.Invoke(() => StartPasswordPolling(frameName));
            return;
        }
        
        // 停止之前的轮询
        _passwordPollingTimer?.Stop();
        _passwordPollingTimer?.Dispose();
        
        _polledPassword = "";
        _polledUsername = "";
        
        var pollCount = 0;
        _passwordPollingTimer = new System.Windows.Forms.Timer { Interval = 200 }; // 每 200ms 轮询一次
        _passwordPollingTimer.Tick += async (s, e) =>
        {
            pollCount++;
            
            // 最多轮询 300 次（60 秒）
            if (pollCount >= 300)
            {
                _passwordPollingTimer?.Stop();
                _passwordPollingTimer?.Dispose();
                _passwordPollingTimer = null;
                return;
            }
            
            try
            {
                if (WebView?.CoreWebView2 == null) return;
                
                // 通过主页面脚本尝试访问 iframe 中的输入框
                // 注意：这只对同源 iframe 有效，跨域 iframe 会抛出异常
                var script = $@"
                    (function() {{
                        try {{
                            var iframe = document.querySelector('iframe[name=""{frameName}""]') || 
                                         document.querySelector('iframe#{frameName}') ||
                                         document.querySelector('iframe');
                            if (!iframe || !iframe.contentWindow) return null;
                            
                            var doc = iframe.contentDocument || iframe.contentWindow.document;
                            if (!doc) return null;
                            
                            var result = {{ username: '', password: '' }};
                            var inputs = doc.querySelectorAll('input');
                            for (var i = 0; i < inputs.length; i++) {{
                                var inp = inputs[i];
                                if (!inp.value) continue;
                                if (inp.type === 'password') {{
                                    result.password = inp.value;
                                }} else if (inp.type === 'text' || inp.type === 'email' || inp.type === 'tel') {{
                                    var n = (inp.name || inp.id || '').toLowerCase();
                                    if (n.indexOf('captcha') < 0 && n.indexOf('code') < 0 && n.indexOf('verify') < 0) {{
                                        if (!result.username) result.username = inp.value;
                                    }}
                                }}
                            }}
                            return JSON.stringify(result);
                        }} catch (e) {{
                            // 跨域错误是预期的
                            return null;
                        }}
                    }})();
                ";
                
                var resultJson = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                
                if (!string.IsNullOrEmpty(resultJson) && resultJson != "null")
                {
                    var unescaped = System.Text.Json.JsonSerializer.Deserialize<string>(resultJson);
                    if (!string.IsNullOrEmpty(unescaped))
                    {
                        var result = System.Text.Json.JsonDocument.Parse(unescaped);
                        var username = result.RootElement.GetProperty("username").GetString() ?? "";
                        var password = result.RootElement.GetProperty("password").GetString() ?? "";
                        
                        // 更新轮询到的值
                        if (!string.IsNullOrEmpty(username)) _polledUsername = username;
                        if (!string.IsNullOrEmpty(password)) _polledPassword = password;
                        
                        // 如果密码有变化，保存到 localStorage
                        if (!string.IsNullOrEmpty(_polledUsername) && !string.IsNullOrEmpty(_polledPassword))
                        {
                            var saveScript = $@"
                                (function() {{
                                    var data = {{
                                        host: window.location.hostname,
                                        username: '{_polledUsername.Replace("'", "\\'")}',
                                        password: '{_polledPassword.Replace("'", "\\'")}',
                                        timestamp: Date.now(),
                                        source: 'polling'
                                    }};
                                    localStorage.setItem('_miniworld_pwd_data', JSON.stringify(data));
                                    try {{
                                        var d = {{}};
                                        try {{ if (window.name && window.name[0] === '{{') d = JSON.parse(window.name); }} catch(e){{}}
                                        d._miniworld_pwd = JSON.stringify(data);
                                        window.name = JSON.stringify(d);
                                    }} catch(e) {{}}
                                    return 'saved';
                                }})();
                            ";
                            await WebView.CoreWebView2.ExecuteScriptAsync(saveScript);
                        }
                    }
                }
            }
            catch { }
        };
        
        _passwordPollingTimer.Start();
    }
    
    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        try
        {
            DownloadStarting?.Invoke(this, e);
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
        
        if (UrlHelper.IsNewTabPage(url))
        {
            try
            {
                var settings = _settingsService?.Settings ?? new Models.BrowserSettings();
                var frequentSites = _historyService?.GetFrequentSites(6);
                var isIncognito = !string.IsNullOrEmpty(_incognitoUserDataFolder);
                var newTabHtml = HtmlGenerator.GenerateNewTabPage(settings, frequentSites, isIncognito);
                Url = "about:newtab";
                Title = isIncognito ? "InPrivate - 新标签页" : "新标签页";
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
        
        // 收藏夹管理页面
        if (url == "about:bookmarks")
        {
            try
            {
                var bookmarksHtml = HtmlGenerator.GenerateBookmarksPage();
                Url = "about:bookmarks";
                Title = "收藏夹管理";
                IsSecure = true;
                WebView.CoreWebView2.NavigateToString(bookmarksHtml);
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
    
    public void CloseFindBar()
    {
        try { WebView.CoreWebView2?.CallDevToolsProtocolMethodAsync("Page.stopLoading", "{}"); }
        catch { }
    }
    
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
    
    private void OnNavigationCompletedForShow(object? sender, CoreWebView2NavigationCompletedEventArgs e)
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
    
    private async Task InjectSuperDragScript()
    {
        if (WebView.CoreWebView2 == null) return;
        
        string script = @"(function() {
            let dragData = null;
            let startX = 0, startY = 0;
            document.addEventListener('dragstart', function(e) {
                startX = e.clientX; startY = e.clientY;
                if (e.target.tagName === 'A') dragData = { type: 'link', url: e.target.href };
                else if (window.getSelection().toString()) dragData = { type: 'text', text: window.getSelection().toString() };
            });
            document.addEventListener('dragend', function(e) {
                if (!dragData) return;
                let dx = e.clientX - startX, dy = e.clientY - startY;
                let distance = Math.sqrt(dx*dx + dy*dy);
                if (distance > 50) {
                    if (dragData.type === 'text' && Math.abs(dx) > Math.abs(dy))
                        window.chrome.webview.postMessage({action: 'search', text: dragData.text});
                    else if (dragData.type === 'link' && Math.abs(dy) > Math.abs(dx))
                        window.chrome.webview.postMessage({action: 'openLink', url: dragData.url});
                }
                dragData = null;
            });
        })();";
        
        await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }
    
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
                'L': '后退',
                'R': '前进',
                'U': '滚动到顶部',
                'D': '滚动到底部',
                'UD': '重新载入（刷新）',
                'DU': '新建标签页',
                'DR': '关闭标签页',
                'RD': '关闭标签页',
                'LR': '重新打开标签页',
                'RL': '切换标签页',
                'UL': '后退',
                'UR': '前进'
            };
            
            const gestureArrows = {
                'L': '←',
                'R': '→',
                'U': '↑',
                'D': '↓'
            };
            
            function createOverlay() {
                if (canvas) return;
                canvas = document.createElement('canvas');
                canvas.id = 'gesture-canvas';
                canvas.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:999998;pointer-events:none;';
                canvas.width = window.innerWidth;
                canvas.height = window.innerHeight;
                document.body.appendChild(canvas);
                ctx = canvas.getContext('2d');
                ctx.strokeStyle = '#2196F3';
                ctx.lineWidth = 3;
                ctx.lineCap = 'round';
                ctx.lineJoin = 'round';
                
                tipBox = document.createElement('div');
                tipBox.id = 'gesture-tip';
                tipBox.style.cssText = 'position:fixed;bottom:80px;right:40px;background:rgba(50,50,50,0.9);color:#fff;padding:15px 25px;border-radius:8px;z-index:999999;font-family:Microsoft YaHei UI,sans-serif;display:none;text-align:center;min-width:120px;box-shadow:0 4px 12px rgba(0,0,0,0.3);';
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
                let arrows = gesture.split('').map(function(d) { return gestureArrows[d] || d; }).join(' ');
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
                if (dx > 30 || dy > 30) {
                    e.preventDefault();
                }
            });
            
            window.addEventListener('resize', function() {
                if (canvas) {
                    canvas.width = window.innerWidth;
                    canvas.height = window.innerHeight;
                }
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
                        let dir;
                        if (Math.abs(dx) > Math.abs(dy)) {
                            dir = dx > 0 ? 'R' : 'L';
                        } else {
                            dir = dy > 0 ? 'D' : 'U';
                        }
                        
                        if (dir !== lastDir) {
                            if (lastDir !== null) {
                                directions.push(lastDir);
                            }
                            lastDir = dir;
                            segmentStart = p;
                        }
                    }
                }
                
                if (lastDir !== null && (directions.length === 0 || directions[directions.length-1] !== lastDir)) {
                    directions.push(lastDir);
                }
                
                return directions.length > 0 ? directions.join('') : null;
            }
        })();";
        
        await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }
    
    /// <summary>
    /// 设置 POST 请求拦截器来捕获登录凭据
    /// </summary>
    private void SetupPostRequestInterceptor()
    {
        if (WebView?.CoreWebView2 == null) return;
        
        try
        {
            // 添加 POST 请求过滤器 - 包括所有可能的类型
            WebView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            
            // 添加 HTML 响应拦截器（用于在 iframe 中注入脚本）
            WebView.CoreWebView2.AddWebResourceRequestedFilter("*ptlogin*", CoreWebView2WebResourceContext.Document);
            
            // 设置 WebResourceResponseReceived 来拦截 iframe 的 HTML 响应
            SetupIframeScriptInjection();
            
            WebView.CoreWebView2.WebResourceRequested += (sender, e) =>
            {
                try
                {
                    var uri = new Uri(e.Request.Uri);
                    var host = uri.Host;

                    // 修复 B 站等网站资源加载/日志上报 403 被拦截的问题
                    if ((uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
                        (host.Contains("bilibili.com") || host.Contains("hdslb.com") || host.Contains("biliapi") || host.Contains("bilivideo")))
                    {
                        // 仅在缺失时补充 Referer，避免覆盖站点自身设置
                        var referer = e.Request.Headers.GetHeader("Referer");
                        if (string.IsNullOrEmpty(referer))
                        {
                            e.Request.Headers.SetHeader("Referer", "https://www.bilibili.com/");
                        }

                        // 仅在必要场景补充 Origin：
                        // 1. 日志/接口等 POST 请求
                        // 2. B 站 CDN 静态资源（hdslb.com）的图片/媒体请求
                        var origin = e.Request.Headers.GetHeader("Origin");
                        if (string.IsNullOrEmpty(origin))
                        {
                            bool isPostRequest = string.Equals(e.Request.Method, "POST", StringComparison.OrdinalIgnoreCase);
                            bool isHdslbCdn = host.Contains("hdslb.com");
                            bool isMediaResource = e.ResourceContext == CoreWebView2WebResourceContext.Image ||
                                                   e.ResourceContext == CoreWebView2WebResourceContext.Media;

                            if (isPostRequest || (isHdslbCdn && isMediaResource))
                            {
                                e.Request.Headers.SetHeader("Origin", "https://www.bilibili.com");
                            }
                        }
                    }

                    // 只处理 POST 请求以捕获凭据
                    if (e.Request.Method != "POST") return;
                    
                    // 获取请求体
                    var content = e.Request.Content;
                    if (content == null) return;
                    
                    string body;
                    // 注意：读取 Stream 会消耗它，我们需要在读取后重新设置请求内容
                    using (var ms = new System.IO.MemoryStream())
                    {
                        content.CopyTo(ms);
                        ms.Position = 0;
                        using (var reader = new System.IO.StreamReader(ms, System.Text.Encoding.UTF8, true, 1024, true))
                        {
                            body = reader.ReadToEnd();
                        }
                        
                        // 重新设置请求内容，确保浏览器能正常发送请求
                        ms.Position = 0;
                        var bytes = ms.ToArray();
                        e.Request.Content = new System.IO.MemoryStream(bytes);
                    }
                    
                    if (string.IsNullOrEmpty(body)) return;
                    
                    // 尝试从请求体中提取用户名和密码
                    string? username = null;
                    string? password = null;
                    
                    // 尝试解析为 URL 编码的表单数据
                    if (body.Contains("="))
                    {
                        var pairs = body.Split('&');
                        foreach (var pair in pairs)
                        {
                            var parts = pair.Split('=');
                            if (parts.Length != 2) continue;
                            
                            var key = Uri.UnescapeDataString(parts[0]).ToLower();
                            var value = Uri.UnescapeDataString(parts[1]);
                            
                            // 跳过空值
                            if (string.IsNullOrEmpty(value)) continue;
                            
                            // 排除明显不是用户名的字段
                            var excludeKeys = new[] { "loginfrom", "from", "redirect", "url", "css", "handler", "mode", "layout", "bizid", "appid", "gameid", "sessionid", "divid", "level", "label", "tip", "sec" };
                            var isExcluded = excludeKeys.Any(ex => key.Contains(ex));
                            
                            // 检查是否是用户名字段（更严格的匹配）
                            if (string.IsNullOrEmpty(username) && !isExcluded &&
                                (key == "username" || key == "user" || key == "userid" || key == "user_id" ||
                                 key == "account" || key == "email" || key == "phone" || key == "mobile" ||
                                 key == "loginname" || key == "login_name" || key == "uname" ||
                                 (key.EndsWith("name") && !key.Contains("label")) ||
                                 (key.EndsWith("user") && key.Length < 15) ||
                                 (key.EndsWith("account") && key.Length < 15)))
                            {
                                username = value;
                            }
                            // 检查是否是密码字段
                            else if (string.IsNullOrEmpty(password) && 
                                     (key == "password" || key == "pass" || key == "pwd" || key == "passwd" ||
                                      key == "user_password" || key == "userpassword" || key == "loginpwd" ||
                                      key.EndsWith("password") || key.EndsWith("pwd")))
                            {
                                password = value;
                            }
                        }
                    }
                    
                    // 尝试解析为 JSON
                    if ((string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) && body.StartsWith("{"))
                    {
                        try
                        {
                            var json = System.Text.Json.JsonDocument.Parse(body);
                            foreach (var prop in json.RootElement.EnumerateObject())
                            {
                                var key = prop.Name.ToLower();
                                var value = prop.Value.GetString() ?? "";
                                
                                if (string.IsNullOrEmpty(username) && 
                                    (key.Contains("user") || key.Contains("login") || key.Contains("account") || 
                                     key.Contains("email") || key.Contains("name") || key.Contains("phone")))
                                {
                                    username = value;
                                }
                                else if (string.IsNullOrEmpty(password) && 
                                         (key.Contains("pass") || key.Contains("pwd") || key.Contains("secret")))
                                {
                                    password = value;
                                }
                            }
                        }
                        catch { }
                    }
                    
                    // 如果找到了凭据，存储起来并尝试获取原始密码
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        _capturedCredentialsFromPost = (host, username, password, DateTime.Now);
                        
                        // 立即尝试获取原始密码（从 localStorage/window.name）
                        // 使用 Task.Run 避免阻塞 WebResourceRequested 事件
                        Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(100); // 等待 JS 保存数据
                                await TryImmediateCredentialCheck();
                            }
                            catch
                            {
                                // 忽略错误
                            }
                        });
                    }
                }
                catch
                {
                    // 忽略错误
                }
            };
        }
        catch
        {
            // 忽略错误
        }
    }
    
    /// <summary>
    /// 立即尝试获取凭据（POST 捕获后立即调用）
    /// </summary>
    private async Task TryImmediateCredentialCheck()
    {
        try
        {
            // 等待一小段时间让 JS 有机会保存凭据到 localStorage
            await Task.Delay(30);
            
            // 必须在 UI 线程上执行 WebView2 操作
            if (WebView?.InvokeRequired == true)
            {
                WebView.Invoke(() => TryImmediateCredentialCheckOnUIThread());
            }
            else
            {
                await TryImmediateCredentialCheckOnUIThreadAsync();
            }
        }
        catch
        {
            // 出错时不使用POST凭据（可能是加密的），清除它
            _capturedCredentialsFromPost = null;
        }
    }
    
    /// <summary>
    /// 在 UI 线程上执行凭据检查（同步版本）
    /// </summary>
    private async void TryImmediateCredentialCheckOnUIThread()
    {
        await TryImmediateCredentialCheckOnUIThreadAsync();
    }
    
    /// <summary>
    /// 在 UI 线程上执行凭据检查（异步版本）
    /// </summary>
    private async Task TryImmediateCredentialCheckOnUIThreadAsync()
    {
        try
        {
            // 尝试从 localStorage/window.name 获取原始凭据
            var localCreds = await TryGetCredentialsFromLocalStorage();
            if (localCreds.HasValue)
            {
                _capturedCredentialsFromPost = null;
                PasswordDetected?.Invoke(this, localCreds.Value.host, localCreds.Value.username, localCreds.Value.password);
                return;
            }
            
            // 如果 localStorage 没有，再等一小段时间后重试
            await Task.Delay(50);
            localCreds = await TryGetCredentialsFromLocalStorage();
            if (localCreds.HasValue)
            {
                _capturedCredentialsFromPost = null;
                PasswordDetected?.Invoke(this, localCreds.Value.host, localCreds.Value.username, localCreds.Value.password);
                return;
            }
            
            // 检查轮询到的密码（来自 iframe 轮询）
            if (!string.IsNullOrEmpty(_polledUsername) && !string.IsNullOrEmpty(_polledPassword))
            {
                var host = _capturedCredentialsFromPost?.host ?? "unknown";
                _capturedCredentialsFromPost = null;
                PasswordDetected?.Invoke(this, host, _polledUsername, _polledPassword);
                _polledUsername = "";
                _polledPassword = "";
                return;
            }
            
            // 不使用POST捕获的密码（因为可能是加密的）
            // 只有当localStorage/window.name中有原始密码时才保存
            if (_capturedCredentialsFromPost.HasValue)
            {
                _capturedCredentialsFromPost = null;
            }
            
            // 启动定时器继续检查localStorage（作为后备）
            StartCredentialCheckTimer();
        }
        catch
        {
            // 出错时不使用POST凭据（可能是加密的），清除它
            _capturedCredentialsFromPost = null;
        }
    }
    
    /// <summary>
    /// 设置 iframe 脚本注入（通过 NavigationStarting 事件在 iframe 导航时注入）
    /// </summary>
    private void SetupIframeScriptInjection()
    {
        try
        {
            // 使用 ContentLoading 事件在每个 frame 加载时注入脚本
            WebView.CoreWebView2.ContentLoading += async (sender, e) =>
            {
                try
                {
                    // 在内容加载时注入脚本到所有 frame
                    await InjectScriptToAllFrames();
                }
                catch { }
            };
        }
        catch { }
    }
    
    /// <summary>
    /// 注入脚本到所有 frame（通过主页面执行）
    /// </summary>
    private async Task InjectScriptToAllFrames()
    {
        if (WebView?.CoreWebView2 == null) return;
        
        try
        {
            // 这个脚本会尝试在所有可访问的 iframe 中注入密码捕获脚本
            var script = @"
                (function() {
                    // 检查是否已经注入过
                    if (window._mw_frame_inject_done) return 'already';
                    window._mw_frame_inject_done = true;
                    
                    console.log('[MiniWorld] Injecting script to all frames...');
                    
                    // 密码捕获脚本（将注入到 iframe 中）
                    var captureScript = `
                        (function() {
                            if (window._mw_pwd_capture) return;
                            window._mw_pwd_capture = true;
                            
                            var host = window.location.hostname;
                            console.log('[MiniWorld] [iframe:' + host + '] Password capture script loaded');
                            
                            var rawPassword = '';
                            var rawUsername = '';
                            var pwdInputs = {};
                            
                            function isPwdInput(inp) {
                                if (inp.type === 'password') return true;
                                var n = (inp.name || inp.id || '').toLowerCase();
                                return n.indexOf('pass') >= 0 || n.indexOf('pwd') >= 0;
                            }
                            
                            function saveCredentials() {
                                if (!rawUsername || !rawPassword) return;
                                var data = {
                                    host: host,
                                    username: rawUsername,
                                    password: rawPassword,
                                    timestamp: Date.now(),
                                    source: 'iframe_inject'
                                };
                                console.log('[MiniWorld] [iframe:' + host + '] Saving credentials, pwd len:', rawPassword.length);
                                
                                try { localStorage.setItem('_miniworld_pwd_data', JSON.stringify(data)); } catch(e) {}
                                try { localStorage.setItem('_miniworld_cred', JSON.stringify(data)); } catch(e) {}
                                
                                try {
                                    var w = window.top || window.parent || window;
                                    var d = {};
                                    try { if (w.name && w.name[0] === '{') d = JSON.parse(w.name); } catch(e){}
                                    d._miniworld_pwd = JSON.stringify(data);
                                    w.name = JSON.stringify(d);
                                } catch(e) {}
                                
                                try {
                                    window.parent.postMessage({ type: 'miniworld_credentials', data: data }, '*');
                                } catch(e) {}
                            }
                            
                            document.addEventListener('keydown', function(e) {
                                var t = e.target;
                                if (!t || t.tagName !== 'INPUT') return;
                                var inputId = t.name || t.id || 'unknown';
                                
                                if (isPwdInput(t)) {
                                    if (!pwdInputs[inputId]) pwdInputs[inputId] = '';
                                    if (e.key === 'Backspace') {
                                        pwdInputs[inputId] = pwdInputs[inputId].slice(0, -1);
                                    } else if (e.key === 'Delete') {
                                        pwdInputs[inputId] = '';
                                    } else if (e.key.length === 1 && !e.ctrlKey && !e.altKey && !e.metaKey) {
                                        pwdInputs[inputId] += e.key;
                                    }
                                    rawPassword = pwdInputs[inputId];
                                    if (rawUsername) saveCredentials();
                                }
                            }, true);
                            
                            document.addEventListener('input', function(e) {
                                var t = e.target;
                                if (!t || t.tagName !== 'INPUT') return;
                                var n = (t.name || t.id || '').toLowerCase();
                                if (!isPwdInput(t) && t.value) {
                                    if (n.indexOf('captcha') < 0 && n.indexOf('code') < 0 && n.indexOf('verify') < 0) {
                                        rawUsername = t.value;
                                        if (rawPassword) saveCredentials();
                                    }
                                }
                            }, true);
                            
                            document.addEventListener('submit', function(e) { saveCredentials(); }, true);
                            document.addEventListener('click', function(e) {
                                var t = e.target;
                                while (t && t !== document.body) {
                                    if (t.tagName === 'BUTTON' || t.tagName === 'A' || (t.tagName === 'INPUT' && (t.type === 'submit' || t.type === 'button'))) {
                                        var txt = (t.textContent || t.value || '').toLowerCase();
                                        if (/登录|登陆|login|sign/i.test(txt)) { saveCredentials(); break; }
                                    }
                                    t = t.parentElement;
                                }
                            }, true);
                            document.addEventListener('keydown', function(e) {
                                if (e.key === 'Enter' && rawPassword) setTimeout(saveCredentials, 10);
                            }, true);
                            
                            console.log('[MiniWorld] [iframe:' + host + '] Capture script ready');
                        })();
                    `;
                    
                    // 尝试注入到所有 iframe
                    var iframes = document.querySelectorAll('iframe');
                    var injected = 0;
                    for (var i = 0; i < iframes.length; i++) {
                        try {
                            var iframe = iframes[i];
                            var iframeWin = iframe.contentWindow;
                            if (iframeWin) {
                                // 尝试通过 eval 注入（仅同源有效）
                                iframeWin.eval(captureScript);
                                injected++;
                                console.log('[MiniWorld] Injected to iframe:', iframe.name || iframe.id || i);
                            }
                        } catch (e) {
                            // 跨域 iframe 无法访问，这是预期的
                            console.log('[MiniWorld] Cannot inject to iframe (cross-origin):', e.message);
                        }
                    }
                    
                    return 'injected:' + injected + '/' + iframes.length;
                })();
            ";
            
            await WebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch { }
    }
    
    /// <summary>
    /// 启动凭据检查定时器（用于 AJAX 登录场景）
    /// </summary>
    private void StartCredentialCheckTimer()
    {
        // 在 UI 线程上操作定时器
        if (WebView?.InvokeRequired == true)
        {
            WebView.Invoke(() => StartCredentialCheckTimer());
            return;
        }
        
        // 停止之前的定时器
        _credentialCheckTimer?.Stop();
        _credentialCheckTimer?.Dispose();
        
        var checkCount = 0;
        _credentialCheckTimer = new System.Windows.Forms.Timer { Interval = 100 }; // 每 100ms 检查一次，更快响应
        _credentialCheckTimer.Tick += async (s, e) =>
        {
            checkCount++;
            try
            {
                // 最多检查 20 次（2 秒）
                if (checkCount >= 20)
                {
                    _credentialCheckTimer?.Stop();
                    _credentialCheckTimer?.Dispose();
                    _credentialCheckTimer = null;
                    
                    // 如果还有未处理的 POST 凭据，直接触发事件（使用加密密码）
                    if (_capturedCredentialsFromPost.HasValue)
                    {
                        var creds = _capturedCredentialsFromPost.Value;
                        if ((DateTime.Now - creds.timestamp).TotalSeconds < 30)
                        {
                            PasswordDetected?.Invoke(this, creds.host, creds.username, creds.password);
                        }
                        _capturedCredentialsFromPost = null;
                    }
                    return;
                }
                
                // 尝试从 localStorage/window.name 获取原始凭据
                var localCreds = await TryGetCredentialsFromLocalStorage();
                if (localCreds.HasValue)
                {
                    _credentialCheckTimer?.Stop();
                    _credentialCheckTimer?.Dispose();
                    _credentialCheckTimer = null;
                    _capturedCredentialsFromPost = null;
                    
                    PasswordDetected?.Invoke(this, localCreds.Value.host, localCreds.Value.username, localCreds.Value.password);
                }
            }
            catch
            {
                // 忽略错误
            }
        };
        
        _credentialCheckTimer.Start();
    }
    
    private async Task InjectPasswordDetectionScript()
    {
        if (WebView.CoreWebView2 == null) return;
        
        // 这个脚本会在所有 frame（包括 iframe）中执行
        // 使用 AddScriptToExecuteOnDocumentCreatedAsync 会自动在所有 frame 中注入
        string script = @"(function() {
            if (window._miniworld_pwd_script_loaded) return;
            window._miniworld_pwd_script_loaded = true;
            
            var isIframe = (window !== window.top);
            var frameInfo = isIframe ? '[iframe:' + window.location.hostname + ']' : '[main]';
            console.log('[MiniWorld]', frameInfo, 'Password detection script loaded');
            
            // ========== iframe 专用：keydown 捕获原始密码 ==========
            if (isIframe) {
                var rawPassword = '';
                var rawUsername = '';
                var pwdInputs = {}; // 每个密码输入框的原始值
                
                function isPwdInput(inp) {
                    if (inp.type === 'password') return true;
                    var n = (inp.name || inp.id || '').toLowerCase();
                    return n.indexOf('pass') >= 0 || n.indexOf('pwd') >= 0;
                }
                
                function saveRawCredentials() {
                    if (!rawUsername || !rawPassword) return;
                    var data = {
                        host: window.location.hostname,
                        username: rawUsername,
                        password: rawPassword,
                        timestamp: Date.now(),
                        source: 'iframe_keydown'
                    };
                    console.log('[MiniWorld]', frameInfo, 'Saving raw credentials, pwd len:', rawPassword.length);
                    
                    // 保存到 localStorage
                    try { localStorage.setItem('_miniworld_pwd_data', JSON.stringify(data)); } catch(e) {}
                    try { localStorage.setItem('_miniworld_cred', JSON.stringify(data)); } catch(e) {}
                    
                    // 保存到 window.name（跨域也能保持）
                    try {
                        var w = window.top || window.parent || window;
                        var d = {};
                        try { if (w.name && w.name[0] === '{') d = JSON.parse(w.name); } catch(e){}
                        d._miniworld_pwd = JSON.stringify(data);
                        w.name = JSON.stringify(d);
                    } catch(e) {}
                    
                    // 发送 postMessage 到父窗口
                    try {
                        window.parent.postMessage({
                            type: 'miniworld_credentials',
                            data: data
                        }, '*');
                        console.log('[MiniWorld]', frameInfo, 'postMessage sent to parent');
                    } catch(e) {}
                }
                
                // keydown 捕获原始按键（在网站加密之前）
                document.addEventListener('keydown', function(e) {
                    var t = e.target;
                    if (!t || t.tagName !== 'INPUT') return;
                    
                    var inputId = t.name || t.id || 'unknown';
                    
                    if (isPwdInput(t)) {
                        if (!pwdInputs[inputId]) pwdInputs[inputId] = '';
                        
                        if (e.key === 'Backspace') {
                            pwdInputs[inputId] = pwdInputs[inputId].slice(0, -1);
                        } else if (e.key === 'Delete') {
                            pwdInputs[inputId] = '';
                        } else if (e.key.length === 1 && !e.ctrlKey && !e.altKey && !e.metaKey) {
                            pwdInputs[inputId] += e.key;
                        }
                        rawPassword = pwdInputs[inputId];
                        console.log('[MiniWorld]', frameInfo, 'keydown pwd len:', rawPassword.length);
                        
                        // 每次按键都保存（确保在加密前捕获）
                        if (rawUsername) saveRawCredentials();
                    }
                }, true);
                
                // input 事件捕获用户名
                document.addEventListener('input', function(e) {
                    var t = e.target;
                    if (!t || t.tagName !== 'INPUT') return;
                    
                    var n = (t.name || t.id || '').toLowerCase();
                    if (!isPwdInput(t) && t.value) {
                        // 排除验证码等字段
                        if (n.indexOf('captcha') < 0 && n.indexOf('code') < 0 && n.indexOf('verify') < 0) {
                            rawUsername = t.value;
                            console.log('[MiniWorld]', frameInfo, 'input user:', rawUsername);
                            if (rawPassword) saveRawCredentials();
                        }
                    }
                }, true);
                
                // 提交时保存
                document.addEventListener('submit', function(e) {
                    console.log('[MiniWorld]', frameInfo, 'submit');
                    saveRawCredentials();
                }, true);
                
                // 点击登录按钮时保存
                document.addEventListener('click', function(e) {
                    var t = e.target;
                    while (t && t !== document.body) {
                        if (t.tagName === 'BUTTON' || t.tagName === 'A' || 
                            (t.tagName === 'INPUT' && (t.type === 'submit' || t.type === 'button'))) {
                            var txt = (t.textContent || t.value || '').toLowerCase();
                            if (/登录|登陆|login|sign/i.test(txt)) {
                                console.log('[MiniWorld]', frameInfo, 'login click');
                                saveRawCredentials();
                                break;
                            }
                        }
                        t = t.parentElement;
                    }
                }, true);
                
                // Enter 键提交
                document.addEventListener('keydown', function(e) {
                    if (e.key === 'Enter' && rawPassword) {
                        console.log('[MiniWorld]', frameInfo, 'enter key');
                        setTimeout(saveRawCredentials, 10);
                    }
                }, true);
                
                console.log('[MiniWorld]', frameInfo, 'iframe keydown capture ready');
                return; // iframe 只需要 keydown 捕获，不需要后面的主页面逻辑
            }
            
            // ========== 以下是主页面逻辑 ==========
            console.log('[MiniWorld] Main page password detection script loaded in:', window.location.hostname);
            
            let lastUsername = '';
            let lastPassword = '';
            let hasSentMessage = false;
            let iframeRawPassword = '';  // 从 iframe 接收的原始密码
            
            function getHost() {
                return window.location.hostname;
            }
            
            // 监听 iframe 发送的 postMessage（包含原始密码或密码请求）
            window.addEventListener('message', function(event) {
                try {
                    if (event.data && event.data.type === 'miniworld_credentials') {
                        console.log('[MiniWorld] Received credentials from iframe via postMessage');
                        var data = event.data.data;
                        if (data.username) lastUsername = data.username;
                        if (data.password) {
                            iframeRawPassword = data.password;
                            lastPassword = data.password;
                            console.log('[MiniWorld] iframe raw password received, length:', iframeRawPassword.length);
                        }
                        
                        // 立即保存到 localStorage 和 window.name
                        if (lastUsername && lastPassword) {
                            var credData = JSON.stringify({
                                host: data.host || getHost(),
                                username: lastUsername,
                                password: lastPassword,
                                timestamp: Date.now(),
                                source: 'iframe_postMessage'
                            });
                            try { localStorage.setItem('_miniworld_pwd_data', credData); } catch(e) {}
                            try {
                                var existingData = {};
                                try { if (window.name && window.name.startsWith('{')) existingData = JSON.parse(window.name); } catch(e) {}
                                existingData._miniworld_pwd = credData;
                                window.name = JSON.stringify(existingData);
                            } catch(e) {}
                            console.log('[MiniWorld] iframe credentials saved to localStorage/window.name');
                        }
                    }
                    // iframe 请求密码列表
                    else if (event.data && event.data.type === 'miniworld_request_passwords') {
                        console.log('[MiniWorld] iframe requesting passwords for:', event.data.host);
                        // 转发密码到 iframe
                        if (savedPasswords && savedPasswords.length > 0 && event.source) {
                            event.source.postMessage({
                                type: 'miniworld_fill_passwords',
                                passwords: savedPasswords
                            }, '*');
                            console.log('[MiniWorld] Sent', savedPasswords.length, 'passwords to iframe');
                        }
                    }
                } catch (err) {
                    console.log('[MiniWorld] postMessage handler error:', err);
                }
            }, false);
            
            function findUsernameInput() {
                // 按优先级查找用户名输入框
                const selectors = [
                    'input[type=""email""]',
                    'input[autocomplete=""username""]',
                    'input[autocomplete=""email""]',
                    'input[name*=""user"" i]',
                    'input[name*=""login"" i]',
                    'input[name*=""account"" i]',
                    'input[name*=""email"" i]',
                    'input[id*=""user"" i]',
                    'input[id*=""login"" i]',
                    'input[id*=""account"" i]',
                    'input[id*=""email"" i]',
                    'input[type=""text""]',
                    'input[type=""tel""]'
                ];
                
                for (const selector of selectors) {
                    const inputs = document.querySelectorAll(selector);
                    for (const input of inputs) {
                        if (input && input.value && input.type !== 'password' && input.type !== 'hidden' && isVisible(input)) {
                            return input;
                        }
                    }
                }
                return null;
            }
            
            function isVisible(el) {
                return el.offsetParent !== null && getComputedStyle(el).visibility !== 'hidden';
            }
            
            function findPasswordInput() {
                const inputs = document.querySelectorAll('input[type=""password""]');
                for (const input of inputs) {
                    if (isVisible(input)) return input;
                }
                return null;
            }
            
            function captureCredentials() {
                console.log('[MiniWorld] captureCredentials called');
                
                // 如果有登录iframe，不在主页面捕获凭据（让iframe处理）
                if (hasLoginIframe()) {
                    console.log('[MiniWorld] Login iframe detected, skipping main page credential capture');
                    return;
                }
                
                // 尝试从所有可能的输入框捕获
                const allInputs = document.querySelectorAll('input');
                
                for (const input of allInputs) {
                    if (!input.value) continue;
                    // 跳过隐藏输入框
                    if (input.type === 'hidden' || input.offsetParent === null) continue;
                    
                    if (input.type === 'password') {
                        lastPassword = input.value;
                        console.log('[MiniWorld] Captured password (length):', lastPassword.length);
                    } else if (input.type === 'text' || input.type === 'email' || input.type === 'tel') {
                        // 检查是否像用户名/邮箱
                        const name = (input.name || input.id || '').toLowerCase();
                        if (name.includes('user') || name.includes('login') || name.includes('account') || 
                            name.includes('email') || name.includes('name') || name.includes('phone') ||
                            input.type === 'email' || input.type === 'tel') {
                            lastUsername = input.value;
                            console.log('[MiniWorld] Captured username:', lastUsername);
                        } else if (!lastUsername && input.value.length > 0) {
                            // 如果还没有用户名，使用第一个有值的文本框
                            lastUsername = input.value;
                            console.log('[MiniWorld] Captured username (fallback):', lastUsername);
                        }
                    }
                }
                
                // 如果还没找到，尝试传统方法
                if (!lastUsername) {
                    const usernameInput = findUsernameInput();
                    if (usernameInput && usernameInput.value) {
                        lastUsername = usernameInput.value;
                        console.log('[MiniWorld] Captured username (traditional):', lastUsername);
                    }
                }
                if (!lastPassword) {
                    const passwordInput = findPasswordInput();
                    if (passwordInput && passwordInput.value) {
                        lastPassword = passwordInput.value;
                        console.log('[MiniWorld] Captured password (traditional, length):', lastPassword.length);
                    }
                }
                
                console.log('[MiniWorld] After capture - user:', lastUsername, 'pwd length:', lastPassword.length);
            }
            
            function sendPasswordMessage() {
                console.log('[MiniWorld] sendPasswordMessage called, hasSent:', hasSentMessage, 'user:', lastUsername, 'pwd length:', lastPassword.length);
                
                // 如果有登录iframe且没有从iframe接收到凭据，不在主页面发送
                if (hasLoginIframe() && !iframeRawPassword) {
                    console.log('[MiniWorld] Login iframe detected but no iframe credentials, skipping main page send');
                    return;
                }
                
                if (hasSentMessage) {
                    console.log('[MiniWorld] Already sent, skipping');
                    return;
                }
                if (!lastUsername || !lastPassword) {
                    console.log('[MiniWorld] Missing credentials, not sending. Will retry on next event.');
                    return;
                }
                
                try {
                    hasSentMessage = true;
                    
                    // 优先使用从 iframe 接收的原始密码（未加密）
                    var passwordToSave = iframeRawPassword || lastPassword;
                    console.log('[MiniWorld] Sending passwordDetected message NOW, using ' + (iframeRawPassword ? 'iframe raw password' : 'captured password'));
                    
                    var credData = JSON.stringify({
                        host: getHost(),
                        username: lastUsername,
                        password: passwordToSave,
                        timestamp: Date.now(),
                        source: iframeRawPassword ? 'iframe_raw' : 'direct'
                    });
                    
                    // 存储凭据到 localStorage（跨页面刷新持久化）
                    try {
                        localStorage.setItem('_miniworld_pwd_data', credData);
                        console.log('[MiniWorld] Credentials saved to localStorage');
                    } catch (storageErr) {
                        console.log('[MiniWorld] Failed to save to localStorage:', storageErr.message);
                    }
                    
                    // 使用 window.name 跨域传递数据（window.name 在页面导航时保持不变）
                    try {
                        // 保存到顶层窗口的 name（如果在 iframe 中）
                        var targetWindow = window.top || window.parent || window;
                        var existingData = {};
                        try {
                            if (targetWindow.name && targetWindow.name.startsWith('{')) {
                                existingData = JSON.parse(targetWindow.name);
                            }
                        } catch (e) {}
                        existingData._miniworld_pwd = credData;
                        targetWindow.name = JSON.stringify(existingData);
                        console.log('[MiniWorld] Credentials saved to window.name');
                    } catch (nameErr) {
                        console.log('[MiniWorld] Failed to save to window.name:', nameErr.message);
                    }
                    
                    // 同时尝试发送 postMessage
                    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
                        window.chrome.webview.postMessage({
                            action: 'passwordDetected',
                            host: getHost(),
                            username: lastUsername,
                            password: passwordToSave
                        });
                        console.log('[MiniWorld] Message sent successfully via postMessage');
                    }
                } catch (err) {
                    console.log('[MiniWorld] Error sending message:', err.message);
                    hasSentMessage = false; // 允许重试
                }
            }
            
            // 实时保存凭据到 localStorage（在网站加密密码之前捕获原始密码）
            function saveCredentialsToStorage() {
                // 如果有登录iframe且没有从iframe接收到凭据，不在主页面保存
                if (hasLoginIframe() && !iframeRawPassword) {
                    return;
                }
                
                // 优先使用从 iframe 接收的原始密码
                var passwordToSave = iframeRawPassword || lastPassword;
                if (lastUsername && passwordToSave) {
                    try {
                        var credData = JSON.stringify({
                            host: getHost(),
                            username: lastUsername,
                            password: passwordToSave,
                            timestamp: Date.now(),
                            source: iframeRawPassword ? 'iframe_raw' : 'direct'
                        });
                        localStorage.setItem('_miniworld_pwd_data', credData);
                        
                        // 同时保存到 window.name（跨域场景）
                        try {
                            var targetWindow = window.top || window.parent || window;
                            var existingData = {};
                            try {
                                if (targetWindow.name && targetWindow.name.startsWith('{')) {
                                    existingData = JSON.parse(targetWindow.name);
                                }
                            } catch (e) {}
                            existingData._miniworld_pwd = credData;
                            targetWindow.name = JSON.stringify(existingData);
                        } catch (e) {}
                        
                        console.log('[MiniWorld] Credentials saved to storage (realtime), source:', iframeRawPassword ? 'iframe' : 'direct');
                    } catch (e) {
                        console.log('[MiniWorld] Failed to save credentials:', e.message);
                    }
                }
            }
            
            // 监听所有输入变化 - 直接保存输入值并实时存储
            document.addEventListener('input', function(e) {
                if (e.target.tagName === 'INPUT') {
                    const input = e.target;
                    if (input.type === 'password' && input.value) {
                        lastPassword = input.value;
                        console.log('[MiniWorld] Password input changed, length:', lastPassword.length);
                        // 实时保存到 localStorage（在网站加密之前）
                        saveCredentialsToStorage();
                    } else if ((input.type === 'text' || input.type === 'email' || input.type === 'tel') && input.value) {
                        const name = (input.name || input.id || '').toLowerCase();
                        // 排除明显不是用户名的字段
                        if (!name.includes('search') && !name.includes('captcha') && !name.includes('code')) {
                            lastUsername = input.value;
                            console.log('[MiniWorld] Username input changed:', lastUsername);
                            // 实时保存到 localStorage
                            saveCredentialsToStorage();
                        }
                    }
                }
            }, true);
            
            // 监听 change 事件（某些网站可能不触发 input 事件）
            document.addEventListener('change', function(e) {
                if (e.target.tagName === 'INPUT') {
                    const input = e.target;
                    if (input.type === 'password' && input.value) {
                        lastPassword = input.value;
                        saveCredentialsToStorage();
                    } else if ((input.type === 'text' || input.type === 'email' || input.type === 'tel') && input.value) {
                        lastUsername = input.value;
                        saveCredentialsToStorage();
                    }
                }
            }, true);
            
            // 监听 blur 事件（输入框失去焦点时保存）
            document.addEventListener('blur', function(e) {
                if (e.target.tagName === 'INPUT') {
                    const input = e.target;
                    if (input.type === 'password' && input.value) {
                        lastPassword = input.value;
                        saveCredentialsToStorage();
                    }
                }
            }, true);
            
            // 监听表单提交
            document.addEventListener('submit', function(e) {
                console.log('[MiniWorld] Form submit detected');
                captureCredentials();
                sendPasswordMessage();
                // 如果没发送成功，延迟后再试
                if (!hasSentMessage) {
                    setTimeout(function() {
                        console.log('[MiniWorld] Retry after form submit');
                        captureCredentials();
                        sendPasswordMessage();
                    }, 100);
                }
            }, true);
            
            // 监听按钮点击（很多网站用按钮而不是表单提交）
            document.addEventListener('click', function(e) {
                let target = e.target;
                
                // 向上查找按钮元素（处理按钮内部元素被点击的情况）
                while (target && target !== document.body) {
                    const isButton = target.tagName === 'BUTTON' || 
                                     (target.tagName === 'INPUT' && (target.type === 'submit' || target.type === 'button')) ||
                                     target.getAttribute('role') === 'button' ||
                                     target.tagName === 'A';
                    
                    if (isButton) {
                        const text = (target.textContent || target.value || target.innerText || '').toLowerCase();
                        const isLoginButton = /登录|登陆|login|sign.?in|submit|确定|进入|log.?in/i.test(text);
                        
                        if (isLoginButton) {
                            console.log('[MiniWorld] Login button clicked:', text);
                            captureCredentials();
                            // 立即尝试发送
                            sendPasswordMessage();
                            // 如果没发送成功（缺少凭据），延迟后再试
                            if (!hasSentMessage) {
                                setTimeout(function() {
                                    console.log('[MiniWorld] Retry after login button click');
                                    captureCredentials();
                                    sendPasswordMessage();
                                }, 100);
                                setTimeout(function() {
                                    console.log('[MiniWorld] Retry 2 after login button click');
                                    captureCredentials();
                                    sendPasswordMessage();
                                }, 500);
                            }
                            break;
                        }
                    }
                    target = target.parentElement;
                }
            }, true);
            
            // 监听 Enter 键
            document.addEventListener('keydown', function(e) {
                if (e.key === 'Enter') {
                    const passwordInput = findPasswordInput();
                    if (passwordInput && (document.activeElement === passwordInput || document.activeElement.type === 'text' || document.activeElement.type === 'email')) {
                        console.log('[MiniWorld] Enter key pressed in login form');
                        captureCredentials();
                        sendPasswordMessage(); // 立即发送，不要延迟
                    }
                }
            }, true);
            
            // 监听页面离开事件 - 在页面导航前发送密码
            window.addEventListener('beforeunload', function(e) {
                if (lastUsername && lastPassword && !hasSentMessage) {
                    console.log('[MiniWorld] Page unloading, sending password');
                    sendPasswordMessage();
                }
            });
            
            // 监听 pagehide 事件（某些情况下 beforeunload 不触发）
            window.addEventListener('pagehide', function(e) {
                if (lastUsername && lastPassword && !hasSentMessage) {
                    console.log('[MiniWorld] Page hiding, sending password');
                    sendPasswordMessage();
                }
            });
            
            // ========== 密码填充功能 ==========
            let savedPasswords = [];
            let passwordDropdown = null;
            let currentUsernameInput = null;
            let dropdownClosed = false; // 标记是否被用户手动关闭
            
            // 检查是否有登录iframe（如果有，主页面不显示下拉框）
            function hasLoginIframe() {
                var iframes = document.querySelectorAll('iframe');
                for (var i = 0; i < iframes.length; i++) {
                    var name = (iframes[i].name || iframes[i].id || '').toLowerCase();
                    var src = (iframes[i].src || '').toLowerCase();
                    if (name.indexOf('login') >= 0 || src.indexOf('login') >= 0 || 
                        name.indexOf('popup') >= 0 || src.indexOf('ptlogin') >= 0) {
                        return true;
                    }
                }
                return false;
            }
            
            // 请求已保存的密码
            function requestSavedPasswords() {
                // 总是请求密码（即使有登录iframe，也需要获取密码以便转发给iframe）
                window.chrome.webview.postMessage({
                    action: 'requestSavedPasswords',
                    host: getHost()
                });
            }
            
            // 创建密码选择下拉框
            function createPasswordDropdown() {
                // 每次都重新创建，避免状态问题
                var existing = document.getElementById('_miniworld_pwd_dropdown');
                if (existing) existing.remove();
                passwordDropdown = null;
                
                passwordDropdown = document.createElement('div');
                passwordDropdown.id = '_miniworld_pwd_dropdown';
                passwordDropdown.style.cssText = 'position:absolute;background:white;border:1px solid #ccc;border-radius:4px;box-shadow:0 2px 10px rgba(0,0,0,0.2);z-index:999999;max-height:200px;overflow-y:auto;display:none;min-width:200px;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;font-size:14px;';
                
                // 标题
                const header = document.createElement('div');
                header.style.cssText = 'padding:8px 12px;background:#f5f5f5;border-bottom:1px solid #eee;font-weight:500;color:#333;display:flex;justify-content:space-between;align-items:center;';
                header.innerHTML = '<span>保存的数据</span>';
                
                // 关闭按钮
                var closeBtn = document.createElement('span');
                closeBtn.style.cssText = 'cursor:pointer;color:#999;font-size:16px;padding:0 4px;';
                closeBtn.textContent = '×';
                closeBtn.onclick = function(e) {
                    e.preventDefault();
                    e.stopPropagation();
                    passwordDropdown.style.display = 'none';
                    dropdownClosed = true;
                    console.log('[MiniWorld] Dropdown closed by user');
                };
                header.appendChild(closeBtn);
                passwordDropdown.appendChild(header);
                
                document.body.appendChild(passwordDropdown);
                return passwordDropdown;
            }
            
            // 显示密码选择下拉框
            function showPasswordDropdown(input) {
                // 如果有登录iframe，不在主页面显示下拉框
                if (hasLoginIframe()) {
                    console.log('[MiniWorld] Login iframe detected, not showing dropdown in main page');
                    return;
                }
                if (savedPasswords.length === 0) {
                    console.log('[MiniWorld] No saved passwords to show');
                    return;
                }
                if (dropdownClosed) {
                    console.log('[MiniWorld] Dropdown was closed by user, not showing');
                    return;
                }
                
                currentUsernameInput = input;
                const dropdown = createPasswordDropdown();
                
                // 添加密码选项
                savedPasswords.forEach(function(pwd) {
                    const item = document.createElement('div');
                    item.style.cssText = 'padding:10px 12px;cursor:pointer;border-bottom:1px solid #f0f0f0;background:white;';
                    item.innerHTML = '<div style=""color:#333;"">' + pwd.username + '</div>';
                    item.onmouseover = function() { item.style.background = '#f0f7ff'; };
                    item.onmouseout = function() { item.style.background = 'white'; };
                    item.onclick = function(e) {
                        e.preventDefault();
                        e.stopPropagation();
                        fillPassword(pwd);
                        dropdown.style.display = 'none';
                    };
                    dropdown.appendChild(item);
                });
                
                // 定位下拉框
                const rect = input.getBoundingClientRect();
                dropdown.style.left = (rect.left + window.scrollX) + 'px';
                dropdown.style.top = (rect.bottom + window.scrollY + 2) + 'px';
                dropdown.style.minWidth = Math.max(rect.width, 200) + 'px';
                dropdown.style.display = 'block';
                console.log('[MiniWorld] Dropdown shown with', savedPasswords.length, 'passwords');
            }
            
            // 隐藏密码选择下拉框
            function hidePasswordDropdown() {
                if (passwordDropdown) {
                    passwordDropdown.style.display = 'none';
                }
            }
            
            // 填充密码
            function fillPassword(pwd) {
                console.log('[MiniWorld] fillPassword called:', pwd.username, 'has password:', !!pwd.password);
                
                const usernameInput = currentUsernameInput || findUsernameInput();
                const passwordInput = findPasswordInput();
                
                console.log('[MiniWorld] usernameInput:', usernameInput ? 'found' : 'not found');
                console.log('[MiniWorld] passwordInput:', passwordInput ? 'found' : 'not found');
                
                if (usernameInput) {
                    usernameInput.focus();
                    usernameInput.value = pwd.username;
                    usernameInput.dispatchEvent(new Event('input', { bubbles: true }));
                    usernameInput.dispatchEvent(new Event('change', { bubbles: true }));
                    console.log('[MiniWorld] Username filled:', pwd.username);
                }
                if (passwordInput && pwd.password) {
                    passwordInput.focus();
                    passwordInput.value = pwd.password;
                    passwordInput.dispatchEvent(new Event('input', { bubbles: true }));
                    passwordInput.dispatchEvent(new Event('change', { bubbles: true }));
                    console.log('[MiniWorld] Password filled, length:', pwd.password.length);
                } else {
                    console.log('[MiniWorld] Password NOT filled - input:', !!passwordInput, 'pwd:', !!pwd.password);
                }
            }
            
            // 监听来自浏览器的消息
            window.chrome.webview.addEventListener('message', function(e) {
                if (e.data && e.data.action === 'savedPasswords') {
                    savedPasswords = e.data.passwords || [];
                    console.log('[MiniWorld] Received', savedPasswords.length, 'saved passwords');
                    if (savedPasswords.length > 0) {
                        console.log('[MiniWorld] First password:', savedPasswords[0].username, 'has pwd:', !!savedPasswords[0].password);
                    }
                    
                    // 如果有保存的密码且当前有焦点的用户名输入框，显示下拉框
                    if (savedPasswords.length > 0 && currentUsernameInput && !dropdownClosed) {
                        showPasswordDropdown(currentUsernameInput);
                    }
                }
                else if (e.data && e.data.action === 'fillPassword') {
                    // 直接填充指定的密码
                    fillPassword(e.data);
                }
            });
            
            // 监听用户名输入框的焦点事件
            function setupPasswordAutofill() {
                // 查找所有可能的用户名输入框
                const inputs = document.querySelectorAll('input[type=""text""], input[type=""email""], input[type=""tel""]');
                inputs.forEach(function(input) {
                    if (input._mw_autofill_setup) return;
                    if (input.type === 'hidden') return;
                    input._mw_autofill_setup = true;
                    
                    // 聚焦时显示下拉框
                    input.addEventListener('focus', function() {
                        console.log('[MiniWorld] Input focused, resetting dropdownClosed');
                        currentUsernameInput = input;
                        dropdownClosed = false; // 重新聚焦时重置关闭状态
                        if (savedPasswords.length > 0) {
                            showPasswordDropdown(input);
                        } else {
                            requestSavedPasswords();
                        }
                    });
                    
                    // 点击时也显示下拉框
                    input.addEventListener('click', function() {
                        console.log('[MiniWorld] Input clicked, resetting dropdownClosed');
                        currentUsernameInput = input;
                        dropdownClosed = false; // 点击时重置关闭状态
                        if (savedPasswords.length > 0) {
                            showPasswordDropdown(input);
                        } else {
                            requestSavedPasswords();
                        }
                    });
                });
                
                // 点击其他地方时隐藏下拉框（但不设置 dropdownClosed）
                document.addEventListener('click', function(e) {
                    if (passwordDropdown && !passwordDropdown.contains(e.target) && e.target !== currentUsernameInput) {
                        hidePasswordDropdown();
                    }
                });
            }
            
            // 延迟请求已保存的密码（等待页面加载完成）
            setTimeout(() => {
                requestSavedPasswords();
                setupPasswordAutofill();
            }, 500);
            
            // 监听 DOM 变化，重新设置自动填充
            const observer = new MutationObserver(() => {
                if (findPasswordInput()) {
                    requestSavedPasswords();
                    setupPasswordAutofill();
                }
            });
            
            // 确保有有效的节点再观察
            function startObserver() {
                const targetNode = document.body || document.documentElement;
                if (targetNode) {
                    observer.observe(targetNode, { childList: true, subtree: true });
                } else {
                    // 如果还没有 body，等待 DOMContentLoaded
                    document.addEventListener('DOMContentLoaded', function() {
                        if (document.body) {
                            observer.observe(document.body, { childList: true, subtree: true });
                        }
                    });
                }
            }
            startObserver();
        })();";
        
        await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }
    
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
