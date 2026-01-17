using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Forms;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Services.Interfaces;
using Microsoft.Web.WebView2.Core;

namespace MiniWorldBrowser.Browser;

/// <summary>
/// 隐身模式标签页管理器
/// 特点：
/// 1. 使用独立的用户数据目录
/// 2. 不记录浏览历史
/// 3. 窗口关闭后清除所有数据
/// </summary>
public class IncognitoTabManager : IDisposable
{
    private readonly List<IncognitoTab> _tabs = new();
    private readonly Panel _browserContainer;
    private readonly FlowLayoutPanel _tabContainer;
    private readonly Control _newTabButton;
    private readonly Control? _tabOverflowButton;
    private TabOverflowPanel? _tabOverflowPanel;
    private readonly ISettingsService _settingsService;
    private readonly IAdBlockService _adBlockService;
    private readonly string _incognitoDataFolder;
    private readonly IHistoryService? _mainHistoryService;
    private readonly PasswordService _passwordService = new();
    private CoreWebView2Environment? _incognitoEnvironment;
    
    private IncognitoTab? _activeTab;
    
    public IncognitoTab? ActiveTab => _activeTab;
    public IReadOnlyList<IncognitoTab> Tabs => _tabs.AsReadOnly();
    public int TabCount => _tabs.Count;
    
    // 事件
    public event Action<IncognitoTab>? TabCreated;
    public event Action<IncognitoTab>? TabClosed;
    public event Action<IncognitoTab>? ActiveTabChanged;
    public event Action<IncognitoTab>? TabTitleChanged;
    public event Action<IncognitoTab>? TabUrlChanged;
    public event Action<IncognitoTab>? TabLoadingStateChanged;
    public event Action<IncognitoTab>? TabSecurityStateChanged;
    public event Action<IncognitoTab, string>? TabStatusTextChanged;
    public event Action<IncognitoTab, double>? TabZoomChanged;
    public event Action<string>? NewWindowRequested;
    public event Action? AllTabsClosed;
    public event Action<MiniWorldBrowser.Models.DownloadItem>? DownloadStarted;
    public event Action? WebViewClicked;
    public event Action? BookmarkAllTabsRequested;
    public event Action<string, string, string>? PasswordKeyButtonRequested; // host, username, password
    public event Action<string, object>? SettingChanged;
    
    private readonly List<MiniWorldBrowser.Models.DownloadItem> _downloads = new();
    private readonly Stack<string> _closedTabUrls = new();
    
    // 预加载缓存
    private IncognitoTab? _cachedTab;
    private TabButton? _cachedTabButton;
    private bool _isPreloadingTab = false;
    private readonly object _cacheLock = new();

    private const int NormalTabMaxWidth = 200;
    private const int NormalTabMinWidth = 100;
    private const int PinnedTabWidth = 40;
    private const int OverflowButtonWidth = 32;
    private const int NewTabButtonWidth = 32;
    private const int TabBarPadding = 4;
    
    public IncognitoTabManager(
        Panel browserContainer,
        FlowLayoutPanel tabContainer,
        Control newTabButton,
        Control? tabOverflowButton,
        ISettingsService settingsService,
        IAdBlockService adBlockService,
        string incognitoDataFolder,
        IHistoryService? mainHistoryService = null)
    {
        _browserContainer = browserContainer;
        _tabContainer = tabContainer;
        _newTabButton = newTabButton;
        _tabOverflowButton = tabOverflowButton;
        _settingsService = settingsService;
        _adBlockService = adBlockService;
        _incognitoDataFolder = incognitoDataFolder;
        _mainHistoryService = mainHistoryService;

        _tabContainer.SizeChanged += (_, __) => UpdateTabLayout();
        _newTabButton.SizeChanged += (_, __) => UpdateTabLayout();
        _tabContainer.MouseWheel += (_, e) => OnTabContainerMouseWheel(e);
        
        if (_tabOverflowButton != null)
        {
            _tabOverflowButton.Click += (s, e) => ToggleOverflowPanel();
        }
        
        // 延迟启动预加载
        Task.Delay(1000).ContinueWith(_ => PreloadTabAsync());
    }

    public void SetOverflowPanel(TabOverflowPanel panel)
    {
        _tabOverflowPanel = panel;
        _tabOverflowPanel.TabClicked += t => 
        {
            if (t is IncognitoTab incognitoTab)
                SwitchToTab(incognitoTab);
        };
        _tabOverflowPanel.CloseClicked += t => 
        {
            if (t is IncognitoTab incognitoTab)
                CloseTab(incognitoTab);
        };
        _tabOverflowPanel.PanelClosed += () => 
        {
            if (_tabOverflowButton != null)
                _tabOverflowButton.Invalidate();
        };
    }

    private void ToggleOverflowPanel()
    {
        if (_tabOverflowPanel == null) return;
        
        if (_tabOverflowPanel.Visible)
        {
            _tabOverflowPanel.HidePanel();
        }
        else
        {
            ShowOverflowPanel();
        }
    }

    private void ShowOverflowPanel()
    {
        if (_tabOverflowPanel == null || _tabOverflowButton == null) return;
        
        var overflowTabs = GetOverflowTabs();
        _tabOverflowPanel.UpdateTheme(true);
        _tabOverflowPanel.SetTabs(overflowTabs, _activeTab);
        
        var parent = _tabOverflowPanel.Parent;
        if (parent == null) return;

        var screenPos = _tabOverflowButton.PointToScreen(new Point(0, _tabOverflowButton.Height));
        var clientPos = parent.PointToClient(screenPos);
        
        // 确保面板不会超出窗口右边界
        if (clientPos.X + _tabOverflowPanel.Width > parent.ClientSize.Width)
        {
            clientPos.X = parent.ClientSize.Width - _tabOverflowPanel.Width - 4;
        }
        
        _tabOverflowPanel.Show(clientPos);
    }

    private void UpdateTabLayout()
    {
        if (_tabContainer.IsDisposed || _newTabButton.IsDisposed) return;

        var tabButtons = _tabs.Select(t => t.TabButton).Where(b => b != null).Cast<TabButton>().ToList();
        if (tabButtons.Count == 0)
        {
            if (_tabOverflowButton != null) _tabOverflowButton.Visible = false;
            return;
        }

        var pinned = tabButtons.Where(b => b.IsPinned).ToList();
        var normal = tabButtons.Where(b => !b.IsPinned).ToList();

        var available = _tabContainer.ClientSize.Width;
        if (available <= 0) return;

        var padding = _tabContainer.Padding.Left + _tabContainer.Padding.Right;
        var pinnedTaken = pinned.Sum(b => PinnedTabWidth + b.Margin.Left + b.Margin.Right);
        var newTabWidth = _newTabButton.Visible ? NewTabButtonWidth + _newTabButton.Margin.Left + _newTabButton.Margin.Right : 0;
        var overflowWidth = OverflowButtonWidth;
        
        var spaceForNormalTabs = available - padding - pinnedTaken - newTabWidth - overflowWidth - TabBarPadding;
        if (spaceForNormalTabs < 0) spaceForNormalTabs = 0;

        var normalTargetWidth = NormalTabMaxWidth;
        var tabsToShow = normal.Count;

        if (normal.Count > 0)
        {
            var minWidthNeeded = normal.Count * NormalTabMinWidth;
            
            if (spaceForNormalTabs >= minWidthNeeded)
            {
                normalTargetWidth = Math.Min(spaceForNormalTabs / normal.Count, NormalTabMaxWidth);
                if (normalTargetWidth < NormalTabMinWidth) normalTargetWidth = NormalTabMinWidth;
                tabsToShow = normal.Count;
            }
            else
            {
                normalTargetWidth = NormalTabMinWidth;
                tabsToShow = Math.Max(1, spaceForNormalTabs / NormalTabMinWidth);
            }
        }

        foreach (var btn in tabButtons)
        {
            btn.Visible = false;
        }

        foreach (var b in pinned)
        {
            b.Visible = true;
            b.PreferredWidth = PinnedTabWidth;
            if (!b.IsDisposed && b.Width != PinnedTabWidth) b.Width = PinnedTabWidth;
        }

        var visibleNormalTabs = normal.Take(tabsToShow).ToList();
        foreach (var b in visibleNormalTabs)
        {
            b.Visible = true;
            b.PreferredWidth = normalTargetWidth;
            if (!b.IsDisposed && b.Width != normalTargetWidth) b.Width = normalTargetWidth;
        }

        var hasOverflow = normal.Count > tabsToShow;
        if (_tabOverflowButton != null)
        {
            _tabOverflowButton.Visible = hasOverflow;
        }

        if (_tabOverflowPanel != null && _tabOverflowPanel.Visible)
        {
            var overflowTabs = GetOverflowTabs();
            _tabOverflowPanel.SetTabs(overflowTabs, _activeTab);
        }

        if (_tabContainer.Controls.Contains(_newTabButton))
        {
            _newTabButton.SendToBack();
        }

        try { _tabContainer.PerformLayout(); } catch { }
    }

    private List<IncognitoTab> GetOverflowTabs()
    {
        var tabButtons = _tabs.Select(t => t.TabButton).Where(b => b != null).Cast<TabButton>().ToList();
        var pinned = tabButtons.Where(b => b.IsPinned).ToList();
        var normal = tabButtons.Where(b => !b.IsPinned).ToList();

        var available = _tabContainer.ClientSize.Width;
        if (available <= 0) return _tabs.ToList();

        var padding = _tabContainer.Padding.Left + _tabContainer.Padding.Right;
        var pinnedTaken = pinned.Sum(b => PinnedTabWidth + b.Margin.Left + b.Margin.Right);
        var newTabWidth = _newTabButton.Visible ? NewTabButtonWidth + _newTabButton.Margin.Left + _newTabButton.Margin.Right : 0;
        var overflowWidth = OverflowButtonWidth;
        
        var spaceForNormalTabs = available - padding - pinnedTaken - newTabWidth - overflowWidth - TabBarPadding;
        if (spaceForNormalTabs < 0) spaceForNormalTabs = 0;

        var tabsToShow = 0;
        if (normal.Count > 0)
        {
            var minWidthNeeded = normal.Count * NormalTabMinWidth;
            if (spaceForNormalTabs >= minWidthNeeded)
            {
                tabsToShow = normal.Count;
            }
            else
            {
                tabsToShow = Math.Max(0, spaceForNormalTabs / NormalTabMinWidth);
            }
        }

        var visibleTabIds = pinned.Select(b => b.TabId).ToHashSet();
        visibleTabIds.UnionWith(normal.Take(tabsToShow).Select(b => b.TabId));

        return _tabs.Where(t => !visibleTabIds.Contains(t.Id)).ToList();
    }

    private void OnTabContainerMouseWheel(MouseEventArgs e)
    {
        if (_tabContainer.IsDisposed) return;
        if (!_tabContainer.AutoScroll) return;

        try
        {
            var scroll = _tabContainer.HorizontalScroll;
            var max = Math.Max(0, scroll.Maximum - scroll.LargeChange + 1);
            var step = 80;
            var next = scroll.Value + (e.Delta < 0 ? step : -step);
            if (next < 0) next = 0;
            if (next > max) next = max;
            scroll.Value = next;
            _tabContainer.PerformLayout();
        }
        catch { }
    }
    
    /// <summary>
    /// 预加载一个标签页到缓存
    /// </summary>
    private async void PreloadTabAsync()
    {
        lock (_cacheLock)
        {
            if (_isPreloadingTab || _cachedTab != null) return;
            _isPreloadingTab = true;
        }
        
        try
        {
            await EnsureEnvironmentAsync();
            
            var tab = new IncognitoTab(_browserContainer, _settingsService, _incognitoEnvironment!, _mainHistoryService);
            var tabBtn = new TabButton(darkTheme: true) { TabId = tab.Id };
            tabBtn.RightClickToClose = _settingsService?.Settings?.RightClickCloseTab ?? false;
            tab.TabButton = tabBtn;
            
            await tab.InitializeAsync();
            
            var homePage = _settingsService?.Settings?.HomePage ?? "about:newtab";
            
            // 使用 TaskCompletionSource 等待页面加载完成
            var tcs = new TaskCompletionSource<bool>();
            void OnNavCompleted(object? s, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
            {
                try { tab.WebView.CoreWebView2.NavigationCompleted -= OnNavCompleted; } catch { }
                tcs.TrySetResult(true);
            }
            
            if (tab.WebView?.CoreWebView2 != null)
            {
                tab.WebView.CoreWebView2.NavigationCompleted += OnNavCompleted;
            }
            
            tab.Navigate(homePage);
            
            // 等待页面加载完成（最多等待 5 秒）
            var timeoutTask = Task.Delay(5000);
            await Task.WhenAny(tcs.Task, timeoutTask);
            
            // 额外等待一小段时间让页面渲染完成
            await Task.Delay(100);
            
            // 隐藏预加载的标签页，但标记为已渲染
            tab.Hide();
            tab.MarkAsRendered(); // 标记为已渲染，下次显示时直接显示
            
            lock (_cacheLock)
            {
                _cachedTab = tab;
                _cachedTabButton = tabBtn;
                _isPreloadingTab = false;
            }
        }
        catch
        {
            lock (_cacheLock)
            {
                _isPreloadingTab = false;
            }
        }
    }
    
    /// <summary>
    /// 设置缓存标签页的事件和配置
    /// </summary>
    private void SetupCachedTab(IncognitoTab tab, TabButton tabBtn)
    {
        tabBtn.TabClicked += OnTabButtonClicked;
        tabBtn.CloseClicked += OnTabButtonCloseClicked;
        tabBtn.NewTabRequested += OnNewTabRequested;
        tabBtn.RefreshRequested += OnRefreshRequested;
        tabBtn.DuplicateRequested += OnDuplicateRequested;
        tabBtn.PinRequested += OnPinRequested;
        tabBtn.CloseOthersRequested += OnCloseOthersRequested;
        tabBtn.CloseLeftRequested += OnCloseLeftRequested;
        tabBtn.CloseRightRequested += OnCloseRightRequested;
        tabBtn.ReopenClosedRequested += OnReopenClosedRequested;
        tabBtn.BookmarkAllRequested += OnBookmarkAllRequested;
        
        tab.TitleChanged += t => OnTabTitleChanged(t);
        tab.UrlChanged += t => OnTabUrlChanged(t);
        tab.LoadingStateChanged += t => OnTabLoadingStateChanged(t);
        tab.NewWindowRequested += (t, newUrl) => NewWindowRequested?.Invoke(newUrl);
        tab.FaviconChanged += t => t.TabButton?.SetFavicon(t.FaviconUrl);
        tab.SecurityStateChanged += t => OnTabSecurityStateChanged(t);
        tab.StatusTextChanged += (t, text) => OnTabStatusTextChanged(t, text);
        tab.ZoomChanged += (t, zoom) => OnTabZoomChanged(t, zoom);
        tab.DownloadStarting += OnDownloadStarting;
        
        if (_adBlockService?.Enabled == true)
            SetupAdBlocker(tab);
        
        if (tab.WebView?.CoreWebView2 != null)
        {
            SetupWebMessageHandler(tab);
            tab.WebView.GotFocus += (s, e) => WebViewClicked?.Invoke();
        }
    }

    /// <summary>
    /// 确保隐身环境已初始化
    /// </summary>
    private async Task EnsureEnvironmentAsync()
    {
        if (_incognitoEnvironment != null) return;
        
        if (!Directory.Exists(_incognitoDataFolder))
            Directory.CreateDirectory(_incognitoDataFolder);
        
        // 使用打包的 WebView2 Runtime
        var browserExecutableFolder = BrowserTab.FindWebView2Runtime();
        
        // 配置环境选项，提高兼容性
        var options = new CoreWebView2EnvironmentOptions
        {
            // 允许加载不安全的内容，解决部分网站样式/图标加载问题
            AdditionalBrowserArguments = "--allow-running-insecure-content --disable-features=BlockInsecurePrivateNetworkRequests"
        };

        _incognitoEnvironment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: browserExecutableFolder,
            userDataFolder: _incognitoDataFolder,
            options: options);
    }
    
    /// <summary>
    /// 创建新的隐身标签页
    /// </summary>
    /// <param name="url">要打开的URL</param>
    /// <param name="openInBackground">是否在后台打开（不切换到新标签）</param>
    public async Task<IncognitoTab> CreateTabAsync(string url, bool openInBackground = false)
    {
        await EnsureEnvironmentAsync();
        
        IncognitoTab? tab = null;
        TabButton? tabBtn = null;
        
        // 检查是否可以使用缓存的标签页
        var homePage = _settingsService?.Settings?.HomePage ?? "about:newtab";
        bool useCached = false;
        
        lock (_cacheLock)
        {
            if (_cachedTab != null && _cachedTabButton != null && url == homePage)
            {
                tab = _cachedTab;
                tabBtn = _cachedTabButton;
                _cachedTab = null;
                _cachedTabButton = null;
                useCached = true;
            }
        }
        
        if (useCached && tab != null && tabBtn != null)
        {
            try
            {
                SetupCachedTab(tab, tabBtn);
                _tabs.Add(tab);
                
                _tabContainer.Controls.Add(tabBtn);

                UpdateTabLayout();
                
                if (!openInBackground)
                    SwitchToTab(tab);
                
                TabCreated?.Invoke(tab);
                _ = Task.Run(() => PreloadTabAsync());
                return tab;
            }
            catch
            {
                try { tab?.Dispose(); } catch { }
                tab = null;
                tabBtn = null;
            }
        }
        
        // 正常创建标签页
        try
        {
            tab = new IncognitoTab(_browserContainer, _settingsService!, _incognitoEnvironment!, _mainHistoryService);
            
            // 创建标签按钮（深色主题）
            tabBtn = new TabButton(darkTheme: true) { TabId = tab.Id };
            tabBtn.RightClickToClose = _settingsService?.Settings?.RightClickCloseTab ?? false;
            tabBtn.TabClicked += OnTabButtonClicked;
            tabBtn.CloseClicked += OnTabButtonCloseClicked;
            tabBtn.NewTabRequested += OnNewTabRequested;
            tabBtn.RefreshRequested += OnRefreshRequested;
            tabBtn.DuplicateRequested += OnDuplicateRequested;
            tabBtn.PinRequested += OnPinRequested;
            tabBtn.CloseOthersRequested += OnCloseOthersRequested;
            tabBtn.CloseLeftRequested += OnCloseLeftRequested;
            tabBtn.CloseRightRequested += OnCloseRightRequested;
            tabBtn.ReopenClosedRequested += OnReopenClosedRequested;
            tabBtn.BookmarkAllRequested += OnBookmarkAllRequested;
            tab.TabButton = tabBtn;
            
            // 绑定事件
            tab.TitleChanged += t => OnTabTitleChanged(t);
            tab.UrlChanged += t => OnTabUrlChanged(t);
            tab.LoadingStateChanged += t => OnTabLoadingStateChanged(t);
            tab.NewWindowRequested += (t, newUrl) => NewWindowRequested?.Invoke(newUrl);
            tab.FaviconChanged += t => t.TabButton?.SetFavicon(t.FaviconUrl);
            tab.SecurityStateChanged += t => OnTabSecurityStateChanged(t);
            tab.StatusTextChanged += (t, text) => OnTabStatusTextChanged(t, text);
            tab.ZoomChanged += (t, zoom) => OnTabZoomChanged(t, zoom);
            tab.DownloadStarting += OnDownloadStarting;
            
            _tabs.Add(tab);
            
            _tabContainer.Controls.Add(tabBtn);

            UpdateTabLayout();
            
            // 初始化 WebView2
            await tab.InitializeAsync();
            
            // 设置广告过滤
            if (_adBlockService?.Enabled == true)
                SetupAdBlocker(tab);
            
            // 设置 WebMessage 处理器
            if (tab.WebView?.CoreWebView2 != null)
            {
                SetupWebMessageHandler(tab);
                
                // WebView 获得焦点时触发事件（用于关闭菜单等）
                tab.WebView.GotFocus += (s, e) => WebViewClicked?.Invoke();
            }
            
            // 先导航，再显示标签页，避免闪烁
            tab.Navigate(url);
            
            // 根据参数决定是否切换到该标签页
            if (!openInBackground)
            {
                SwitchToTab(tab);
            }
            else
            {
                tab.Hide();
            }
            
            TabCreated?.Invoke(tab);
            return tab;
        }
        catch
        {
            // 清理失败的标签页
            if (tab != null)
            {
                try
                {
                    _tabs.Remove(tab);
                    if (tabBtn != null)
                        _tabContainer.Controls.Remove(tabBtn);
                    tab.Dispose();
                }
                catch { }
            }
            
            throw;
        }
    }
    
    /// <summary>
    /// 关闭标签页
    /// </summary>
    public void CloseTab(IncognitoTab tab)
    {
        if (tab == null) return;
        
        // 记录关闭的标签页 URL（用于重新打开）
        if (!string.IsNullOrEmpty(tab.Url) && !tab.Url.StartsWith("about:"))
        {
            _closedTabUrls.Push(tab.Url);
        }
        
        if (_tabs.Count == 1)
        {
            // 最后一个标签，先清理资源再触发关闭事件
            try
            {
                _tabs.Remove(tab);
                _tabContainer.Controls.Remove(tab.TabButton);
                tab.Dispose();
            }
            catch { }

            UpdateTabLayout();
            
            try { AllTabsClosed?.Invoke(); } catch { }
            return;
        }
        
        int index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        
        try { _tabContainer.Controls.Remove(tab.TabButton); } catch { }

        UpdateTabLayout();
        
        if (tab == _activeTab)
        {
            var newIndex = Math.Min(index, _tabs.Count - 1);
            if (newIndex >= 0 && newIndex < _tabs.Count)
            {
                SwitchToTab(_tabs[newIndex]);
            }
        }
        
        try { TabClosed?.Invoke(tab); } catch { }
        try { tab.Dispose(); } catch { }

        UpdateTabLayout();
    }
    
    /// <summary>
    /// 切换到指定标签页
    /// </summary>
    public void SwitchToTab(IncognitoTab tab)
    {
        if (tab == null || _activeTab == tab) return;
        
        var oldTab = _activeTab;
        _activeTab = tab;
        
        // 暂停布局更新，避免闪烁
        _browserContainer.SuspendLayout();
        try
        {
            // 先显示新标签页，再隐藏旧标签页，避免闪烁
            _activeTab.Show();
            _activeTab.TabButton?.SetActive(true);
            
            oldTab?.Hide();
            oldTab?.TabButton?.SetActive(false);
        }
        finally
        {
            _browserContainer.ResumeLayout(true);
        }
        
        ActiveTabChanged?.Invoke(_activeTab);
    }
    
    public void SwitchToNextTab()
    {
        if (_tabs.Count <= 1 || _activeTab == null) return;
        int idx = _tabs.IndexOf(_activeTab);
        int next = (idx + 1) % _tabs.Count;
        SwitchToTab(_tabs[next]);
    }
    
    public void SwitchToPreviousTab()
    {
        if (_tabs.Count <= 1 || _activeTab == null) return;
        int idx = _tabs.IndexOf(_activeTab);
        int prev = (idx - 1 + _tabs.Count) % _tabs.Count;
        SwitchToTab(_tabs[prev]);
    }

    #region 私有方法
    
    private void OnTabButtonClicked(TabButton btn)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == btn.TabId);
        if (tab != null) SwitchToTab(tab);
    }
    
    private void OnTabButtonCloseClicked(TabButton btn)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == btn.TabId);
        if (tab != null) CloseTab(tab);
    }
    
    private void OnNewTabRequested(TabButton btn)
    {
        _ = CreateTabAsync(_settingsService?.Settings?.HomePage ?? "about:newtab");
    }
    
    private void OnRefreshRequested(TabButton btn)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == btn.TabId);
        tab?.Refresh();
    }
    
    private void OnDuplicateRequested(TabButton btn)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == btn.TabId);
        if (tab != null && !string.IsNullOrEmpty(tab.Url))
        {
            _ = CreateTabAsync(tab.Url);
        }
    }
    
    private void OnPinRequested(TabButton btn)
    {
        btn.SetPinned(!btn.IsPinned);
        UpdateTabLayout();
    }
    
    private void OnCloseOthersRequested(TabButton btn)
    {
        var tabsToClose = _tabs.Where(t => t.Id != btn.TabId).ToList();
        foreach (var tab in tabsToClose)
        {
            _tabs.Remove(tab);
            _tabContainer.Controls.Remove(tab.TabButton);
            tab.Dispose();
        }

        UpdateTabLayout();
    }
    
    private void OnCloseLeftRequested(TabButton btn)
    {
        var currentTab = _tabs.FirstOrDefault(t => t.Id == btn.TabId);
        if (currentTab == null) return;
        
        var idx = _tabs.IndexOf(currentTab);
        var tabsToClose = _tabs.Take(idx).ToList();
        foreach (var tab in tabsToClose)
        {
            _tabs.Remove(tab);
            _tabContainer.Controls.Remove(tab.TabButton);
            tab.Dispose();
        }

        UpdateTabLayout();
    }
    
    private void OnCloseRightRequested(TabButton btn)
    {
        var currentTab = _tabs.FirstOrDefault(t => t.Id == btn.TabId);
        if (currentTab == null) return;
        
        var idx = _tabs.IndexOf(currentTab);
        var tabsToClose = _tabs.Skip(idx + 1).ToList();
        foreach (var tab in tabsToClose)
        {
            _tabs.Remove(tab);
            _tabContainer.Controls.Remove(tab.TabButton);
            tab.Dispose();
        }

        UpdateTabLayout();
    }
    
    private void OnReopenClosedRequested(TabButton btn)
    {
        _ = ReopenClosedTabAsync();
    }
    
    private void OnBookmarkAllRequested(TabButton btn)
    {
        BookmarkAllTabsRequested?.Invoke();
    }
    
    private void OnTabTitleChanged(IncognitoTab tab)
    {
        tab.TabButton?.SetTitle(tab.Title);
        TabTitleChanged?.Invoke(tab);
    }
    
    private void OnTabUrlChanged(IncognitoTab tab)
    {
        // 隐身模式不记录历史
        TabUrlChanged?.Invoke(tab);
    }
    
    private void OnTabLoadingStateChanged(IncognitoTab tab)
    {
        tab.TabButton?.SetLoading(tab.IsLoading);
        
        // 页面加载完成后应用字体设置
        if (!tab.IsLoading)
        {
            ApplyFontSettingsToTab(tab);
        }
        
        TabLoadingStateChanged?.Invoke(tab);
    }
    
    /// <summary>
    /// 应用字体设置到单个标签页
    /// </summary>
    private void ApplyFontSettingsToTab(IncognitoTab tab)
    {
        if (_settingsService?.Settings == null) return;
        
        try
        {
            if (tab.WebView?.CoreWebView2 != null && !tab.Url.StartsWith("about:"))
            {
                var settings = _settingsService.Settings;
                var script = $@"
                    (function() {{
                        var style = document.getElementById('miniworld-font-settings');
                        if (!style) {{
                            style = document.createElement('style');
                        style.id = 'miniworld-font-settings';
                        document.head.appendChild(style);
                    }}
                    style.textContent = `
                        html, body {{
                            font-family: '{settings.StandardFont}', '{settings.SerifFont}', '{settings.SansSerifFont}', sans-serif !important;
                        }}
                        body {{
                            font-size: {settings.StandardFontSize}px !important;
                        }}
                        code, pre, kbd, samp, tt {{
                            font-family: '{settings.FixedWidthFont}', monospace !important;
                        }}
                    `;
                }})();";
                _ = tab.WebView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }
        catch { }
    }
    
    private void OnTabSecurityStateChanged(IncognitoTab tab)
    {
        TabSecurityStateChanged?.Invoke(tab);
    }
    
    private void OnTabStatusTextChanged(IncognitoTab tab, string text)
    {
        TabStatusTextChanged?.Invoke(tab, text);
    }
    
    private void OnTabZoomChanged(IncognitoTab tab, double zoomFactor)
    {
        // 只有当前活动标签页的缩放变化才触发事件
        if (tab == _activeTab)
        {
            TabZoomChanged?.Invoke(tab, zoomFactor);
        }
    }
    
    private void SetupAdBlocker(IncognitoTab tab)
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;
            
            tab.WebView.CoreWebView2.AddWebResourceRequestedFilter(
                "*", CoreWebView2WebResourceContext.All);
            
            tab.WebView.CoreWebView2.WebResourceRequested += (s, e) =>
            {
                try
                {
                    // 首方资源不拦截，避免误伤站点自身的样式/图标脚本
                    try
                    {
                        if (Uri.TryCreate(tab.Url, UriKind.Absolute, out var pageUri) &&
                            Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var reqUri) &&
                            string.Equals(pageUri.Host, reqUri.Host, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }
                    catch { }

                    // 避免拦截导致站点样式/字体缺失
                    if (e.ResourceContext == CoreWebView2WebResourceContext.Stylesheet ||
                        e.ResourceContext == CoreWebView2WebResourceContext.Font)
                    {
                        return;
                    }

                    if (_adBlockService?.ShouldBlock(e.Request.Uri) == true)
                    {
                        var env = tab.WebView?.CoreWebView2?.Environment;
                        if (env != null)
                            e.Response = env.CreateWebResourceResponse(null, 403, "Blocked", "");

                        System.Diagnostics.Debug.WriteLine($"[AdBlock][Incognito] Blocked ({e.ResourceContext}): {e.Request.Uri}");
                    }
                }
                catch { }
            };
        }
        catch { }
    }
    
    private void OnDownloadStarting(IncognitoTab tab, CoreWebView2DownloadStartingEventArgs e)
    {
        try
        {
            var downloadOperation = e.DownloadOperation;
            var downloadItem = new MiniWorldBrowser.Models.DownloadItem
            {
                Url = downloadOperation.Uri,
                TotalBytes = (long)(downloadOperation.TotalBytesToReceive ?? 0),
                Status = MiniWorldBrowser.Models.DownloadStatus.Downloading
            };
            
            var resultFilePath = downloadOperation.ResultFilePath;
            if (!string.IsNullOrEmpty(resultFilePath))
            {
                downloadItem.FileName = Path.GetFileName(resultFilePath);
                downloadItem.FilePath = resultFilePath;
            }
            else
            {
                var uri = new Uri(downloadOperation.Uri);
                downloadItem.FileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrEmpty(downloadItem.FileName))
                    downloadItem.FileName = "download";
            }
            
            _downloads.Add(downloadItem);
            
            downloadOperation.BytesReceivedChanged += (s, args) =>
            {
                downloadItem.ReceivedBytes = (long)downloadOperation.BytesReceived;
                downloadItem.TotalBytes = (long)(downloadOperation.TotalBytesToReceive ?? (ulong)downloadItem.TotalBytes);
            };
            
            downloadOperation.StateChanged += (s, args) =>
            {
                switch (downloadOperation.State)
                {
                    case CoreWebView2DownloadState.Completed:
                        downloadItem.Status = MiniWorldBrowser.Models.DownloadStatus.Completed;
                        downloadItem.FilePath = downloadOperation.ResultFilePath;
                        downloadItem.EndTime = DateTime.Now;
                        break;
                    case CoreWebView2DownloadState.Interrupted:
                        downloadItem.Status = downloadOperation.InterruptReason == CoreWebView2DownloadInterruptReason.UserCanceled
                            ? MiniWorldBrowser.Models.DownloadStatus.Cancelled
                            : MiniWorldBrowser.Models.DownloadStatus.Failed;
                        downloadItem.EndTime = DateTime.Now;
                        break;
                }
            };
            
            DownloadStarted?.Invoke(downloadItem);
        }
        catch { }
    }
    
    public List<MiniWorldBrowser.Models.DownloadItem> GetDownloads() => _downloads.ToList();
    
    private void SetupWebMessageHandler(IncognitoTab tab)
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;
            
            tab.WebView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    var rawMsg = e.WebMessageAsJson;
                    var msg = System.Text.Json.JsonDocument.Parse(rawMsg);
                    var action = msg.RootElement.GetProperty("action").GetString();
                    
                    if (action == "click")
                    {
                        WebViewClicked?.Invoke();
                    }
                    else if (action == "passwordDetected")
                    {
                        var host = msg.RootElement.GetProperty("host").GetString() ?? "";
                        var username = msg.RootElement.GetProperty("username").GetString() ?? "";
                        var password = msg.RootElement.GetProperty("password").GetString() ?? "";
                        ShowSavePasswordPrompt(tab, host, username, password);
                    }
                    else if (action == "requestSavedPasswords")
                    {
                        var host = msg.RootElement.GetProperty("host").GetString() ?? "";
                        SendSavedPasswords(tab, host);
                    }
                    else if (action == "search")
                    {
                        var text = msg.RootElement.GetProperty("text").GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            var searchEngine = _settingsService?.Settings?.SearchEngine 
                                ?? MiniWorldBrowser.Constants.AppConstants.DefaultSearchEngine;
                            NewWindowRequested?.Invoke(searchEngine + Uri.EscapeDataString(text));
                        }
                    }
                    else if (action == "openLink")
                    {
                        var linkUrl = msg.RootElement.GetProperty("url").GetString();
                        if (!string.IsNullOrEmpty(linkUrl))
                            NewWindowRequested?.Invoke(linkUrl);
                    }
                    else if (action == "navigate")
                    {
                        var url = msg.RootElement.GetProperty("url").GetString();
                        if (!string.IsNullOrEmpty(url))
                            tab.Navigate(url);
                    }
                    else if (action == "gesture")
                    {
                        var gesture = msg.RootElement.GetProperty("gesture").GetString();
                        HandleGesture(tab, gesture);
                    }
                    else if (action == "updateSetting")
                    {
                        var key = msg.RootElement.GetProperty("key").GetString();
                        var value = msg.RootElement.GetProperty("value");
                        HandleSettingUpdate(tab, key, value);
                    }
                    else if (action == "resetSettings")
                    {
                        _settingsService?.Reset();
                        tab.Navigate("about:settings");
                    }
                    else if (action == "browseDownloadPath")
                    {
                        BrowseDownloadPath(tab);
                    }
                    else if (action == "openSearchEngineManager")
                    {
                        OpenSearchEngineManager(tab);
                    }
                    else if (action == "openAdBlockExceptions")
                    {
                        OpenAdBlockExceptions(tab);
                    }
                    else if (action == "openAdBlockRulesFolder")
                    {
                        OpenAdBlockRulesFolder();
                    }
                    else if (action == "openContentSettings")
                    {
                        OpenContentSettings(tab);
                    }
                    else if (action == "openClearBrowsingData")
                    {
                        OpenClearBrowsingData(tab);
                    }
                    else if (action == "openHomePageDialog")
                    {
                        OpenHomePageDialog(tab);
                    }
                    else if (action == "changeCachePath")
                    {
                        ChangeCachePath(tab);
                    }
                    else if (action == "openCacheDir")
                    {
                        OpenCacheDir();
                    }
                    else if (action == "resetCachePath")
                    {
                        ResetCachePath(tab);
                    }
                    else if (action == "openAutofillSettings")
                    {
                        OpenAutofillSettings(tab);
                    }
                    else if (action == "openPasswordManager")
                    {
                        OpenPasswordManager(tab);
                    }
                    else if (action == "openFontSettings")
                    {
                        OpenFontSettings(tab);
                    }
                    else if (action == "openProxySettings")
                    {
                        OpenProxySettings();
                    }
                    else if (action == "openCertificateManager")
                    {
                        OpenCertificateManager();
                    }
                    else if (action == "setAsDefaultBrowser")
                    {
                        SetAsDefaultBrowser(tab);
                    }
                    else if (action == "checkDefaultBrowser")
                    {
                        CheckDefaultBrowser(tab);
                    }
                    else if (action == "getHistory")
                    {
                        SendHistoryData(tab);
                    }
                    else if (action == "searchHistory")
                    {
                        var keyword = msg.RootElement.GetProperty("keyword").GetString() ?? "";
                        SendHistoryData(tab, keyword);
                    }
                    else if (action == "clearHistory")
                    {
                        // 隐身模式不记录历史，但可以清除主历史
                        _mainHistoryService?.Clear();
                        SendHistoryData(tab);
                    }
                }
                catch { }
            };
        }
        catch { }
    }
    
    private void HandleGesture(IncognitoTab tab, string? gesture)
    {
        switch (gesture)
        {
            case "back": tab.GoBack(); break;
            case "forward": tab.GoForward(); break;
            case "refresh": tab.Refresh(); break;
            case "close": CloseTab(tab); break;
        }
    }
    
    private void HandleSettingUpdate(IncognitoTab tab, string? key, System.Text.Json.JsonElement value)
    {
        if (string.IsNullOrEmpty(key) || _settingsService?.Settings == null) return;
        
        try
        {
            switch (key)
            {
                case "hidebookmarkbar":
                case "bookmarkbar":
                case "homebutton":
                case "homepage":
                case "adblock":
                case "adblockmode":
                case "gesture":
                case "superdrag":
                case "search":
                case "startup":
                case "downloadpath":
                case "askdownload":
                case "crashupload":
                case "rightclickclosetab":
                case "openlinksbackground":
                case "addressbarinput":
                case "newtabposition":
                case "smoothscrolling":
                case "enableautofill":
                case "savepasswords":
                case "fontsize":
                case "pagezoom":
                    // 这些设置直接保存到设置服务
                    SaveSetting(key, value);
                    break;
            }
        }
        catch { }
    }
    
    private void SaveSetting(string key, System.Text.Json.JsonElement value)
    {
        if (_settingsService?.Settings == null) return;
        
        switch (key)
        {
            case "hidebookmarkbar":
                var hideBookmarkBar = value.GetBoolean();
                _settingsService.Settings.AlwaysShowBookmarkBar = !hideBookmarkBar;
                _settingsService.Save();
                SettingChanged?.Invoke("hidebookmarkbar", hideBookmarkBar);
                return;
            case "bookmarkbar":
                var showBookmarkBar = value.GetBoolean();
                _settingsService.Settings.AlwaysShowBookmarkBar = showBookmarkBar;
                _settingsService.Save();
                SettingChanged?.Invoke("bookmarkbar", showBookmarkBar);
                return;
            case "homebutton":
                var showHomeButton = value.GetBoolean();
                _settingsService.Settings.ShowHomeButton = showHomeButton;
                _settingsService.Save();
                SettingChanged?.Invoke("homebutton", showHomeButton);
                return;
            case "homepage":
                _settingsService.Settings.HomePage = value.GetString() ?? "";
                break;
            case "adblock":
                _settingsService.Settings.EnableAdBlock = value.GetBoolean();
                break;
            case "adblockmode":
                var adBlockMode = int.Parse(value.GetString() ?? "2");
                _settingsService.Settings.AdBlockMode = adBlockMode;
                _settingsService.Settings.EnableAdBlock = adBlockMode > 0;
                if (_adBlockService != null)
                {
                    _adBlockService.Mode = adBlockMode;
                    _adBlockService.Enabled = adBlockMode > 0;
                }
                break;
            case "gesture":
                _settingsService.Settings.EnableMouseGesture = value.GetBoolean();
                break;
            case "superdrag":
                _settingsService.Settings.EnableSuperDrag = value.GetBoolean();
                break;
            case "search":
                var searchIndex = int.Parse(value.GetString() ?? "1");
                _settingsService.Settings.AddressBarSearchEngine = searchIndex;
                _settingsService.Settings.SearchEngine = searchIndex switch
                {
                    0 => "https://www.so.com/s?q=",
                    1 => "https://www.baidu.com/s?wd=",
                    2 => "https://www.bing.com/search?q=",
                    3 => "https://www.google.com/search?q=",
                    _ => "https://www.baidu.com/s?wd="
                };
                break;
            case "startup":
                _settingsService.Settings.StartupBehavior = int.Parse(value.GetString() ?? "0");
                break;
            case "downloadpath":
                _settingsService.Settings.DownloadPath = value.GetString() ?? "";
                break;
            case "askdownload":
                _settingsService.Settings.AskDownloadLocation = value.GetBoolean();
                break;
            case "crashupload":
                _settingsService.Settings.EnableCrashUpload = value.GetBoolean();
                break;
            case "rightclickclosetab":
                _settingsService.Settings.RightClickCloseTab = value.GetBoolean();
                break;
            case "openlinksbackground":
                _settingsService.Settings.OpenLinksInBackground = value.GetBoolean();
                break;
            case "addressbarinput":
                _settingsService.Settings.AddressBarInputMode = int.Parse(value.GetString() ?? "0");
                break;
            case "newtabposition":
                _settingsService.Settings.NewTabPosition = int.Parse(value.GetString() ?? "0");
                break;
            case "smoothscrolling":
                _settingsService.Settings.EnableSmoothScrolling = value.GetBoolean();
                break;
            case "enableautofill":
                _settingsService.Settings.EnableAutofill = value.GetBoolean();
                break;
            case "savepasswords":
                _settingsService.Settings.SavePasswords = value.GetBoolean();
                break;
            case "fontsize":
                var fontSize = int.Parse(value.GetString() ?? "2");
                _settingsService.Settings.FontSize = fontSize;
                _settingsService.Save();
                // 应用字体大小到所有标签页
                ApplyFontSizeToAllTabs(fontSize);
                return; // 已保存，直接返回
            case "pagezoom":
                var pageZoom = int.Parse(value.GetString() ?? "100");
                _settingsService.Settings.PageZoom = pageZoom;
                _settingsService.Save();
                // 应用缩放到所有标签页
                ApplyZoomToAllTabs(pageZoom);
                return; // 已保存，直接返回
        }
        _settingsService.Save();
    }
    
    private void ApplyFontSizeToAllTabs(int fontSizeLevel)
    {
        // 字号级别对应的 CSS font-size 百分比
        var fontSizePercent = fontSizeLevel switch
        {
            0 => 75,   // 极小
            1 => 87,   // 小
            2 => 100,  // 中
            3 => 115,  // 大
            4 => 130,  // 极大
            _ => 100
        };
        
        // 使用更强的 CSS 样式来缩放字体
        var script = $@"
            (function() {{
                var style = document.getElementById('miniworld-fontsize');
                if (!style) {{
                    style = document.createElement('style');
                    style.id = 'miniworld-fontsize';
                    document.head.appendChild(style);
                }}
                style.textContent = `
                    html {{ font-size: {fontSizePercent}% !important; }}
                    body {{ font-size: {fontSizePercent}% !important; }}
                `;
            }})();";
        
        foreach (var tab in _tabs)
        {
            try
            {
                if (tab.WebView?.CoreWebView2 != null)
                {
                    _ = tab.WebView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch { }
        }
    }
    
    private void ApplyZoomToAllTabs(int zoomPercent)
    {
        var zoomFactor = zoomPercent / 100.0;
        
        foreach (var tab in _tabs)
        {
            try
            {
                if (tab.WebView?.CoreWebView2 != null)
                {
                    tab.WebView.ZoomFactor = zoomFactor;
                }
            }
            catch { }
        }
    }
    
    private void BrowseDownloadPath(IncognitoTab tab)
    {
        try
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择下载文件夹",
                ShowNewFolderButton = true,
                SelectedPath = _settingsService?.Settings?.DownloadPath ?? ""
            };
            
            if (dialog.ShowDialog() == DialogResult.OK && _settingsService?.Settings != null)
            {
                _settingsService.Settings.DownloadPath = dialog.SelectedPath;
                _settingsService.Save();
                tab.Navigate("about:settings");
            }
        }
        catch { }
    }
    
    private void OpenSearchEngineManager(IncognitoTab tab)
    {
        try
        {
            using var dialog = new Forms.SearchEngineManagerDialog(_settingsService!);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                tab.Navigate("about:settings");
            }
        }
        catch { }
    }
    
    private void OpenAdBlockExceptions(IncognitoTab tab)
    {
        try
        {
            if (_settingsService == null) return;
            
            using var dialog = new Forms.AdBlockExceptionDialog(_settingsService);
            dialog.ShowDialog();
            
            // 更新 AdBlockService 的例外列表
            _adBlockService?.SetExceptions(_settingsService.Settings.AdBlockExceptions);
        }
        catch { }
    }
    
    private void OpenAdBlockRulesFolder()
    {
        try
        {
            var rulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AdBlockRules");
            if (!Directory.Exists(rulesPath))
                Directory.CreateDirectory(rulesPath);
            System.Diagnostics.Process.Start("explorer.exe", rulesPath);
        }
        catch { }
    }
    
    private void OpenContentSettings(IncognitoTab tab)
    {
        try
        {
            using var dialog = new Forms.ContentSettingsDialog(_settingsService!);
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void OpenClearBrowsingData(IncognitoTab tab)
    {
        try
        {
            using var dialog = new Forms.ClearBrowsingDataDialog();
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void OpenHomePageDialog(IncognitoTab tab)
    {
        try
        {
            // 传入当前页面URL，用于"使用当前网页"功能
            var currentUrl = _activeTab?.Url;
            using var dialog = new Forms.HomePageDialog(_settingsService!, currentUrl);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                tab.Navigate("about:settings");
            }
        }
        catch { }
    }
    
    private void ChangeCachePath(IncognitoTab tab)
    {
        try
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择缓存目录",
                ShowNewFolderButton = true
            };
            
            if (dialog.ShowDialog() == DialogResult.OK && _settingsService?.Settings != null)
            {
                _settingsService.Settings.UseCustomCachePath = true;
                _settingsService.Settings.CustomCachePath = dialog.SelectedPath;
                _settingsService.Save();
                MessageBox.Show("缓存目录已更改，需要重启浏览器后生效。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                tab.Navigate("about:settings");
            }
        }
        catch { }
    }
    
    private void OpenCacheDir()
    {
        try
        {
            var cachePath = _settingsService?.Settings?.UseCustomCachePath == true && !string.IsNullOrEmpty(_settingsService.Settings.CustomCachePath)
                ? _settingsService.Settings.CustomCachePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniWorldBrowser", "UserData");
            
            if (Directory.Exists(cachePath))
                System.Diagnostics.Process.Start("explorer.exe", cachePath);
        }
        catch { }
    }
    
    private void ResetCachePath(IncognitoTab tab)
    {
        try
        {
            if (_settingsService?.Settings != null)
            {
                _settingsService.Settings.UseCustomCachePath = false;
                _settingsService.Settings.CustomCachePath = "";
                _settingsService.Save();
                MessageBox.Show("缓存目录已重置为默认位置，需要重启浏览器后生效。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                tab.Navigate("about:settings");
            }
        }
        catch { }
    }
    
    private void OpenAutofillSettings(IncognitoTab tab)
    {
        try
        {
            using var dialog = new Forms.AutofillSettingsDialog();
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void OpenPasswordManager(IncognitoTab tab)
    {
        try
        {
            using var dialog = new Forms.PasswordManagerDialog(_passwordService);
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void OpenFontSettings(IncognitoTab tab)
    {
        try
        {
            using var dialog = new Forms.FontSettingsDialog(_settingsService!);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                tab.Navigate("about:settings");
            }
        }
        catch { }
    }
    
    private void OpenProxySettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:network-proxy",
                UseShellExecute = true
            });
        }
        catch { }
    }
    
    private void OpenCertificateManager()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "certmgr.msc",
                UseShellExecute = true
            });
        }
        catch { }
    }
    
    private void SetAsDefaultBrowser(IncognitoTab tab)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:defaultapps",
                UseShellExecute = true
            });
        }
        catch { }
    }
    
    private void CheckDefaultBrowser(IncognitoTab tab)
    {
        // 隐身模式下不检查默认浏览器状态
    }
    
    private void SendHistoryData(IncognitoTab tab, string keyword = "")
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;
            
            // 使用主历史服务获取历史记录
            List<Models.HistoryItem> history;
            if (!string.IsNullOrEmpty(keyword))
            {
                history = _mainHistoryService?.Search(keyword, 100) ?? new List<Models.HistoryItem>();
            }
            else
            {
                history = _mainHistoryService?.GetHistory(100) ?? new List<Models.HistoryItem>();
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "historyData",
                items = history.Select(h => new
                {
                    url = h.Url,
                    title = h.Title,
                    visitTime = h.VisitTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    favicon = h.FaviconUrl
                })
            });
            
            tab.WebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }
    
    private void ShowSavePasswordPrompt(IncognitoTab tab, string host, string username, string password)
    {
        try
        {
            if (tab.WebView == null || tab.WebView.IsDisposed) return;
            
            var savePasswordsEnabled = _settingsService?.Settings?.SavePasswords;
            if (savePasswordsEnabled != true) return;
            
            if (_passwordService.IsNeverSave(host)) return;
            
            if (_passwordService.IsPasswordAlreadySaved(host, username, password)) return;
            
            PasswordKeyButtonRequested?.Invoke(host, username, password);
        }
        catch { }
    }
    
    /// <summary>
    /// 显示保存密码弹窗（由 IncognitoForm 调用）
    /// </summary>
    public void ShowPasswordPopup(string host, string username, string password, Control parent, Point location, bool showSavedMode = false, Action<bool, bool>? onResult = null)
    {
        try
        {
            var form = parent.FindForm();
            if (form == null || form.IsDisposed) return;
            
            var mode = showSavedMode ? PasswordPopupMode.ShowSaved : PasswordPopupMode.AskToSave;
            var popup = new SavePasswordPopup(host, username, password, mode);
            
            var screenLocation = parent.PointToScreen(location);
            var x = screenLocation.X - popup.Width + 30;
            var y = screenLocation.Y + 5;
            
            var screen = Screen.FromControl(form);
            if (x < screen.WorkingArea.Left) x = screen.WorkingArea.Left + 10;
            if (y < screen.WorkingArea.Top) y = screen.WorkingArea.Top + 10;
            if (x + popup.Width > screen.WorkingArea.Right) x = screen.WorkingArea.Right - popup.Width - 10;
            if (y + popup.Height > screen.WorkingArea.Bottom) y = screen.WorkingArea.Bottom - popup.Height - 10;
            
            popup.Location = new Point(x, y);
            
            popup.FormClosed += (s, e) =>
            {
                try
                {
                    if (popup.ShouldSave)
                    {
                        _passwordService.SavePassword(host, username, password);
                        onResult?.Invoke(true, false);
                    }
                    else if (popup.NeverSave)
                    {
                        _passwordService.AddToNeverSave(host);
                        onResult?.Invoke(false, true);
                    }
                }
                catch { }
            };
            
            popup.ManagePasswordsClicked += (s, e) =>
            {
                try
                {
                    form.BeginInvoke(() =>
                    {
                        using var dialog = new Forms.PasswordManagerDialog(_passwordService);
                        dialog.ShowDialog(form);
                    });
                }
                catch { }
            };
            
            popup.Show(form);
        }
        catch { }
    }
    
    private void SendSavedPasswords(IncognitoTab tab, string host)
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;
            
            var passwords = _passwordService.GetPasswordsForHost(host);
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "savedPasswords",
                passwords = passwords.Select(p => new { username = p.Username, password = p.Password })
            });
            tab.WebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }
    
    /// <summary>
    /// 重新打开最近关闭的标签页
    /// </summary>
    public async Task ReopenClosedTabAsync()
    {
        if (_closedTabUrls.Count > 0)
        {
            var url = _closedTabUrls.Pop();
            await CreateTabAsync(url);
        }
    }
    
    #endregion
    
    public void Dispose()
    {
        foreach (var tab in _tabs.ToList())
        {
            try { tab.Dispose(); } catch { }
        }
        _tabs.Clear();
        _incognitoEnvironment = null;
    }
}
