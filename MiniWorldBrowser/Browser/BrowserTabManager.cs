using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Helpers.Extensions;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Browser;

/// <summary>
/// 浏览器标签页管理器
/// </summary>
public class BrowserTabManager
{
    private readonly List<BrowserTab> _tabs = new();
    private readonly Panel _browserContainer;
    private readonly FlowLayoutPanel _tabContainer;
    private readonly Control _newTabButton;
    private readonly Control? _tabOverflowButton;
    private TabOverflowPanel? _tabOverflowPanel;
    private readonly ISettingsService _settingsService;
    private readonly IAdBlockService _adBlockService;
    private readonly IHistoryService? _historyService;
    private readonly IBookmarkService? _bookmarkService;
    private readonly PasswordService _passwordService = new();
    
    private BrowserTab? _activeTab;
    
    public BrowserTab? ActiveTab => _activeTab;
    public IReadOnlyList<BrowserTab> Tabs => _tabs.AsReadOnly();
    public int TabCount => _tabs.Count;
    
    // 事件
    public event Action<BrowserTab>? TabCreated;
    public event Action<BrowserTab>? TabClosed;
    public event Action<BrowserTab>? ActiveTabChanged;
    public event Action<BrowserTab>? TabTitleChanged;
    public event Action<BrowserTab>? TabUrlChanged;
    public event Action<BrowserTab>? TabLoadingStateChanged;
    public event Action<BrowserTab>? TabSecurityStateChanged;
    public event Action<BrowserTab, string>? TabStatusTextChanged;
    public event Action<BrowserTab, double>? TabZoomChanged;
    public event Action<BrowserTab>? TabTranslationRequested;
    public event Action<string>? NewWindowRequested;
    public event Action<string, object>? SettingChanged;
    public event Action<MiniWorldBrowser.Models.DownloadItem>? DownloadStarted;
    public event Action? WebViewClicked;
    public event Action? BookmarkAllTabsRequested;
    public event Action<string, string, string>? PasswordKeyButtonRequested;
    
    private readonly List<MiniWorldBrowser.Models.DownloadItem> _downloads = new();
    private readonly Stack<string> _closedTabUrls = new();
    public IReadOnlyList<MiniWorldBrowser.Models.DownloadItem> Downloads => _downloads.AsReadOnly();
    
    // 预加载缓存
    private BrowserTab? _cachedTab;
    private TabButton? _cachedTabButton;
    private bool _isPreloadingTab = false;
    private readonly object _cacheLock = new();

    private const int NormalTabMaxWidth = 200;
    private const int NormalTabMinWidth = 100;
    private const int PinnedTabWidth = 40;
    private const int OverflowButtonWidth = 32;
    private const int NewTabButtonWidth = 32;
    private const int TabBarPadding = 4;
    
    public BrowserTabManager(
        Panel browserContainer,
        FlowLayoutPanel tabContainer,
        Control newTabButton,
        Control? tabOverflowButton,
        ISettingsService settingsService,
        IAdBlockService adBlockService,
        IHistoryService? historyService = null,
        IBookmarkService? bookmarkService = null)
    {
        _browserContainer = browserContainer;
        _tabContainer = tabContainer;
        _newTabButton = newTabButton;
        _tabOverflowButton = tabOverflowButton;
        _settingsService = settingsService;
        _adBlockService = adBlockService;
        _historyService = historyService;
        _bookmarkService = bookmarkService;

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
            if (t is BrowserTab browserTab)
                SwitchToTab(browserTab);
        };
        _tabOverflowPanel.CloseClicked += t => 
        {
            if (t is BrowserTab browserTab)
                CloseTab(browserTab);
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
        _tabOverflowPanel.UpdateTheme(IsDarkTheme());
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

    private bool IsDarkTheme()
    {
        if (_tabContainer.BackColor.GetBrightness() < 0.5)
            return true;
        
        var parent = _tabContainer.Parent;
        while (parent != null)
        {
            if (parent.BackColor.GetBrightness() < 0.5)
                return true;
            parent = parent.Parent;
        }
        
        return false;
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

    private List<BrowserTab> GetOverflowTabs()
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
        // 避免重复预加载
        lock (_cacheLock)
        {
            if (_isPreloadingTab || _cachedTab != null) return;
            _isPreloadingTab = true;
        }
        
        try
        {
            var tab = new BrowserTab(_browserContainer, _settingsService, _historyService);
            
            var tabBtn = new TabButton { TabId = tab.Id };
            tabBtn.RightClickToClose = _settingsService?.Settings?.RightClickCloseTab ?? false;
            tab.TabButton = tabBtn;
            
            // 初始化 WebView2（这是最耗时的操作）
            await tab.InitializeAsync();
            
            // 预加载新标签页内容
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
    private void SetupCachedTab(BrowserTab tab, TabButton tabBtn)
    {
        // 设置 TabButton 事件
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
        
        // 设置 Tab 事件
        tab.TitleChanged += t => OnTabTitleChanged(t);
        tab.UrlChanged += t => OnTabUrlChanged(t);
        tab.LoadingStateChanged += t => OnTabLoadingStateChanged(t);
        tab.NewWindowRequested += (t, newUrl) => NewWindowRequested?.Invoke(newUrl);
        tab.FaviconChanged += t => t.TabButton?.SetFavicon(t.FaviconUrl);
        tab.SecurityStateChanged += t => OnTabSecurityStateChanged(t);
        tab.StatusTextChanged += (t, text) => OnTabStatusTextChanged(t, text);
        tab.ZoomChanged += (t, zoom) => OnTabZoomChanged(t, zoom);
        tab.TranslationRequested += t => TabTranslationRequested?.Invoke(t);
        tab.DownloadStarting += OnDownloadStarting;
        tab.PasswordDetected += (t, host, username, password) => ShowSavePasswordPrompt(t, host, username, password);
        
        // 设置广告过滤和消息处理
        if (tab.WebView?.CoreWebView2 != null)
        {
            if (_adBlockService?.Enabled == true)
            {
                SetupAdBlocker(tab);
            }
            SetupWebMessageHandler(tab);
            tab.WebView.GotFocus += (s, e) => WebViewClicked?.Invoke();
        }
    }
    
    /// <summary>
    /// 创建新标签页
    /// </summary>
    /// <param name="url">要打开的URL</param>
    /// <param name="openInBackground">是否在后台打开（不切换到新标签）</param>
    public async Task<BrowserTab> CreateTabAsync(string url, bool openInBackground = false)
    {
        BrowserTab? tab = null;
        TabButton? tabBtn = null;
        
        // 检查是否可以使用缓存的标签页
        var homePage = _settingsService?.Settings?.HomePage ?? "about:newtab";
        bool useCached = false;
        
        lock (_cacheLock)
        {
            // 只有打开主页时才使用缓存
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
                // 使用缓存的标签页
                SetupCachedTab(tab, tabBtn);
                
                _tabs.Add(tab);
                
                _tabContainer.Controls.Add(tabBtn);

                UpdateTabLayout();
                tabBtn.PlayShowAnimation();
                
                if (!openInBackground)
                {
                    SwitchToTab(tab);
                }
                
                TabCreated?.Invoke(tab);
                
                // 在后台预加载下一个标签页
                _ = Task.Run(() => PreloadTabAsync());
                
                return tab;
            }
            catch
            {
                // 缓存使用失败，回退到正常创建
                try { tab?.Dispose(); } catch { }
                tab = null;
                tabBtn = null;
            }
        }
        
        // 正常创建标签页
        try
        {
            tab = new BrowserTab(_browserContainer, _settingsService!, _historyService);
            
            // 创建标签按钮
            tabBtn = new TabButton { TabId = tab.Id };
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
            tab.TranslationRequested += t => TabTranslationRequested?.Invoke(t);
            tab.DownloadStarting += OnDownloadStarting;
            tab.PasswordDetected += (t, host, username, password) => ShowSavePasswordPrompt(t, host, username, password);
            
            _tabs.Add(tab);
            
            _tabContainer.Controls.Add(tabBtn);
            
            UpdateTabLayout();
            // 播放出现动画
            tabBtn.PlayShowAnimation();
            
            // 先初始化 WebView2
            await tab.InitializeAsync();
            
            if (tab.WebView?.CoreWebView2 != null)
            {
                if (_adBlockService?.Enabled == true)
                {
                    SetupAdBlocker(tab);
                }
                
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
                // 后台打开时，隐藏新标签页
                tab.Hide();
            }
            
            TabCreated?.Invoke(tab);
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
            
            // 重新抛出异常，让上层处理
            throw;
        }
        
        return tab;
    }
    
    /// <summary>
    /// 关闭标签页
    /// </summary>
    public void CloseTab(BrowserTab tab)
    {
        if (tab == null) return;
        
        if (_tabs.Count == 1)
        {
            // 最后一个标签，关闭窗口
            // 先清理标签页资源
            try
            {
                _tabs.Remove(tab);
                _tabContainer.Controls.Remove(tab.TabButton);
                tab.Dispose();
            }
            catch { }
            
            // 然后关闭窗口
            var form = _browserContainer.FindForm();
            if (form != null && !form.IsDisposed)
            {
                form.Close();
            }
            return;
        }
        
        // 记录关闭的标签页 URL（用于重新打开）
        if (!string.IsNullOrEmpty(tab.Url) && !tab.Url.StartsWith("about:"))
        {
            _closedTabUrls.Push(tab.Url);
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
    public void SwitchToTab(BrowserTab tab)
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
    
    /// <summary>
    /// 切换到下一个标签页
    /// </summary>
    public void SwitchToNextTab()
    {
        if (_tabs.Count <= 1 || _activeTab == null) return;
        
        int idx = _tabs.IndexOf(_activeTab);
        int next = (idx + 1) % _tabs.Count;
        SwitchToTab(_tabs[next]);
    }
    
    /// <summary>
    /// 切换到上一个标签页
    /// </summary>
    public void SwitchToPreviousTab()
    {
        if (_tabs.Count <= 1 || _activeTab == null) return;
        
        int idx = _tabs.IndexOf(_activeTab);
        int prev = (idx - 1 + _tabs.Count) % _tabs.Count;
        SwitchToTab(_tabs[prev]);
    }
    
    /// <summary>
    /// 根据 ID 获取标签页
    /// </summary>
    public BrowserTab? GetTabById(string id)
    {
        return _tabs.FirstOrDefault(t => t.Id == id);
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
    
    private void OnNewTabRequested(TabButton? btn)
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
        if (_closedTabUrls.Count > 0)
        {
            var url = _closedTabUrls.Pop();
            _ = CreateTabAsync(url);
        }
    }
    
    private void OnBookmarkAllRequested(TabButton btn)
    {
        // 触发为所有标签页添加收藏的事件
        // 这需要在 MainForm 中处理
        BookmarkAllTabsRequested?.Invoke();
    }
    
    private void OnTabTitleChanged(BrowserTab tab)
    {
        tab.TabButton?.SetTitle(tab.Title);
        TabTitleChanged?.Invoke(tab);
    }
    
    private void OnTabUrlChanged(BrowserTab tab)
    {
        TabUrlChanged?.Invoke(tab);
    }
    
    private void OnTabLoadingStateChanged(BrowserTab tab)
    {
        tab.TabButton?.SetLoading(tab.IsLoading);
        
        // 页面加载完成后记录历史（此时标题已更新）
        if (!tab.IsLoading && _historyService != null && 
            !string.IsNullOrEmpty(tab.Url) && 
            !tab.Url.StartsWith("about:") && !tab.Url.StartsWith("data:"))
        {
            _historyService.Add(tab.Url, tab.Title, tab.FaviconUrl);
        }
        
        // 页面加载完成后应用字体设置
        if (!tab.IsLoading)
        {
            ApplyFontSettingsToTab(tab);
        }
        
        TabLoadingStateChanged?.Invoke(tab);
    }
    
    private void OnTabSecurityStateChanged(BrowserTab tab)
    {
        TabSecurityStateChanged?.Invoke(tab);
    }
    
    private void OnTabStatusTextChanged(BrowserTab tab, string text)
    {
        TabStatusTextChanged?.Invoke(tab, text);
    }
    
    private void OnTabZoomChanged(BrowserTab tab, double zoomFactor)
    {
        // 只有当前活动标签页的缩放变化才触发事件
        if (tab == _activeTab)
        {
            TabZoomChanged?.Invoke(tab, zoomFactor);
        }
    }
    
    private void SetupAdBlocker(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;
            
            tab.WebView.CoreWebView2.AddWebResourceRequestedFilter(
                "*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
            
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
                    if (e.ResourceContext == Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Stylesheet ||
                        e.ResourceContext == Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Font)
                    {
                        return;
                    }

                    if (_adBlockService?.ShouldBlock(e.Request.Uri) == true)
                    {
                        var env = tab.WebView?.CoreWebView2?.Environment;
                        if (env != null)
                        {
                            e.Response = env.CreateWebResourceResponse(null, 403, "Blocked", "");
                            System.Diagnostics.Debug.WriteLine($"[AdBlock] Blocked ({e.ResourceContext}): {e.Request.Uri}");
                        }
                    }
                }
                catch { }
            };
        }
        catch { }
    }
    
    private void SetupWebMessageHandler(BrowserTab tab)
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
                    
                    if (action == "search")
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
                    else if (action == "updateSetting")
                    {
                        var key = msg.RootElement.GetProperty("key").GetString();
                        var value = msg.RootElement.GetProperty("value");
                        HandleSettingUpdate(key, value);
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
                        _historyService?.Clear();
                        SendHistoryData(tab);
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
                    else if (action == "click")
                    {
                        // 点击网页内容时触发事件，用于关闭弹出窗口
                        WebViewClicked?.Invoke();
                    }
                    else if (action == "resetSettings")
                    {
                        // 恢复默认设置
                        _settingsService?.Reset();
                        // 重新导航到设置页面以刷新显示
                        tab.Navigate("about:settings");
                    }
                    else if (action == "browseDownloadPath")
                    {
                        // 打开文件夹选择对话框
                        BrowseDownloadPath(tab);
                    }
                    else if (action == "openSearchEngineManager")
                    {
                        // 打开搜索引擎管理对话框
                        OpenSearchEngineManager(tab);
                    }
                    else if (action == "getBookmarks")
                    {
                        var folderId = msg.RootElement.TryGetProperty("folderId", out var folderIdProp) 
                            ? folderIdProp.GetString() : null;
                        SendBookmarksData(tab, folderId);
                    }
                    else if (action == "searchBookmarks")
                    {
                        var keyword = msg.RootElement.GetProperty("keyword").GetString() ?? "";
                        SendBookmarksSearchData(tab, keyword);
                    }
                    else if (action == "updateBookmark")
                    {
                        var id = msg.RootElement.GetProperty("id").GetString();
                        var title = msg.RootElement.GetProperty("title").GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            _bookmarkService?.UpdateBookmark(id, title);
                            SendBookmarksData(tab, null);
                        }
                    }
                    else if (action == "deleteBookmark")
                    {
                        var id = msg.RootElement.GetProperty("id").GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            _bookmarkService?.Delete(id);
                            SendBookmarksData(tab, null);
                        }
                    }
                    else if (action == "addFolder")
                    {
                        var title = msg.RootElement.GetProperty("title").GetString();
                        var parentId = msg.RootElement.TryGetProperty("parentId", out var parentProp) 
                            ? parentProp.GetString() : null;
                        if (!string.IsNullOrEmpty(title))
                        {
                            _bookmarkService?.AddFolder(title, parentId);
                            SendBookmarksData(tab, parentId);
                        }
                    }
                    else if (action == "exportBookmarks")
                    {
                        ExportBookmarks(tab);
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
                    else if (action == "openImportData")
                    {
                        OpenImportData(tab);
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
                    else if (action == "setAsDefaultBrowser")
                    {
                        SetAsDefaultBrowser(tab);
                    }
                    else if (action == "checkDefaultBrowser")
                    {
                        CheckDefaultBrowser(tab);
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
                }
                catch { }
            };
        }
        catch { }
    }
    
    private void OpenImportData(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenImportData(tab));
                return;
            }
            
            if (_bookmarkService == null) return;
            
            using var dialog = new Forms.ImportDataDialog(_bookmarkService);
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void OpenHomePageDialog(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenHomePageDialog(tab));
                return;
            }
            
            if (_settingsService == null) return;
            
            // 传入当前页面URL，用于"使用当前网页"功能
            var currentUrl = _activeTab?.Url;
            using var dialog = new Forms.HomePageDialog(_settingsService, currentUrl);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // 刷新设置页面以显示更新后的主页设置
                tab.Navigate("about:settings");
            }
        }
        catch { }
    }
    
    private void ChangeCachePath(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => ChangeCachePath(tab));
                return;
            }
            
            if (_settingsService == null) return;
            
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择缓存目录位置",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            
            var currentPath = _settingsService.Settings.UseCustomCachePath && !string.IsNullOrEmpty(_settingsService.Settings.CustomCachePath)
                ? _settingsService.Settings.CustomCachePath
                : MiniWorldBrowser.Constants.AppConstants.DefaultCacheFolder;
            
            if (Directory.Exists(currentPath))
            {
                dialog.InitialDirectory = currentPath;
            }
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var result = MessageBox.Show(
                    "更改缓存目录将清空现有缓存，需要重启浏览器后生效。\n\n确定要更改吗？",
                    "更改缓存目录",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    _settingsService.Settings.CustomCachePath = dialog.SelectedPath;
                    _settingsService.Settings.UseCustomCachePath = true;
                    _settingsService.Save();
                    
                    // 更新页面显示
                    var json = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        action = "cachePathChanged",
                        path = dialog.SelectedPath
                    });
                    tab.WebView?.CoreWebView2?.PostWebMessageAsJson(json);
                    
                    MessageBox.Show("缓存目录已更改，请重启浏览器使设置生效。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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
                : MiniWorldBrowser.Constants.AppConstants.DefaultCacheFolder;
            
            if (!Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }
            System.Diagnostics.Process.Start("explorer.exe", cachePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开缓存目录: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    private void ResetCachePath(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => ResetCachePath(tab));
                return;
            }
            
            if (_settingsService == null) return;
            
            _settingsService.Settings.CustomCachePath = "";
            _settingsService.Settings.UseCustomCachePath = false;
            _settingsService.Save();
            
            // 更新页面显示
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "cachePathChanged",
                path = MiniWorldBrowser.Constants.AppConstants.DefaultCacheFolder
            });
            tab.WebView?.CoreWebView2?.PostWebMessageAsJson(json);
            
            MessageBox.Show("缓存目录已设回默认，请重启浏览器使设置生效。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch { }
    }
    
    private void OpenAutofillSettings(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenAutofillSettings(tab));
                return;
            }
            
            using var dialog = new Forms.AutofillSettingsDialog();
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void OpenPasswordManager(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenPasswordManager(tab));
                return;
            }
            
            using var dialog = new Forms.PasswordManagerDialog(_passwordService);
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void OpenFontSettings(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenFontSettings(tab));
                return;
            }
            
            if (_settingsService == null) return;
            
            using var dialog = new Forms.FontSettingsDialog(_settingsService);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // 应用字体设置到所有标签页
                ApplyFontSettingsToAllTabs();
            }
        }
        catch { }
    }
    
    private void OpenProxySettings()
    {
        try
        {
            // 打开 Windows Internet 选项的连接设置
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = "inetcpl.cpl,LaunchConnectionDialog",
                UseShellExecute = true
            });
        }
        catch
        {
            // 如果失败，尝试打开 Windows 代理设置
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
    }
    
    private void OpenCertificateManager()
    {
        try
        {
            // 打开 Windows 证书管理器
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "certmgr.msc",
                UseShellExecute = true
            });
        }
        catch
        {
            // 如果失败，尝试通过 rundll32 打开
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "cryptui.dll,CryptUIStartCertMgr",
                    UseShellExecute = true
                });
            }
            catch { }
        }
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
    
    private void ApplyFontSettingsToAllTabs()
    {
        if (_settingsService?.Settings == null) return;
        
        var settings = _settingsService.Settings;
        var script = GenerateFontSettingsScript(settings);
        
        foreach (var tab in _tabs)
        {
            try
            {
                if (tab.WebView?.CoreWebView2 != null && !tab.Url.StartsWith("about:"))
                {
                    _ = tab.WebView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch { }
        }
    }
    
    private static string GenerateFontSettingsScript(Models.BrowserSettings settings)
    {
        // 强制使用 Chrome 风格字体栈作为后备
        return $@"
            (function() {{
                var style = document.getElementById('miniworld-font-settings');
                if (!style) {{
                    style = document.createElement('style');
                    style.id = 'miniworld-font-settings';
                    document.head.appendChild(style);
                }}
                style.textContent = `
                    html, body {{
                        font-family: '{settings.StandardFont}', 'Segoe UI', 'Microsoft YaHei', sans-serif !important;
                        -webkit-font-smoothing: antialiased !important;
                        text-rendering: optimizeLegibility !important;
                    }}
                    body {{
                        font-size: {settings.StandardFontSize}px !important;
                    }}
                    code, pre, kbd, samp, tt {{
                        font-family: '{settings.FixedWidthFont}', 'Consolas', 'Monaco', monospace !important;
                    }}
                `;
            }})();";
    }
    
    /// <summary>
    /// 应用字体设置到单个标签页（在标签页加载完成后调用）
    /// </summary>
    public void ApplyFontSettingsToTab(BrowserTab tab)
    {
        if (_settingsService?.Settings == null) return;
        
        try
        {
            if (tab.WebView?.CoreWebView2 != null)
            {
                // 应用字号设置（使用 CSS）
                var fontSizeLevel = _settingsService.Settings.FontSize;
                var fontSizePercent = fontSizeLevel switch
                {
                    0 => 75,   // 极小
                    1 => 87,   // 小
                    2 => 100,  // 中
                    3 => 115,  // 大
                    4 => 130,  // 极大
                    _ => 100
                };
                
                var fontSizeScript = $@"
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
                _ = tab.WebView.CoreWebView2.ExecuteScriptAsync(fontSizeScript);
                
                // 对非 about: 页面应用自定义字体设置
                if (!tab.Url.StartsWith("about:"))
                {
                    var script = GenerateFontSettingsScript(_settingsService.Settings);
                    _ = tab.WebView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
        }
        catch { }
    }
    
    private void SetAsDefaultBrowser(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => SetAsDefaultBrowser(tab));
                return;
            }
            
            // 打开Windows默认应用设置
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:defaultapps",
                    UseShellExecute = true
                });
            }
            catch
            {
                // 如果无法打开设置，尝试打开控制面板
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "control.exe",
                        Arguments = "/name Microsoft.DefaultPrograms",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
        catch { }
    }
    
    private void CheckDefaultBrowser(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;
            
            bool isDefault = false;
            try
            {
                // 检查HTTP协议的默认处理程序
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
                if (key != null)
                {
                    var progId = key.GetValue("ProgId")?.ToString() ?? "";
                    // 检查是否是我们的浏览器（通过ProgId判断）
                    isDefault = progId.Contains("MiniWorld", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "defaultBrowserStatus",
                isDefault = isDefault
            });
            tab.WebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }
    
    private void ShowSavePasswordPrompt(BrowserTab tab, string host, string username, string password)
    {
        try
        {
            // 检查 WebView 是否有效
            if (tab.WebView == null || tab.WebView.IsDisposed) return;
            
            // 检查设置是否允许保存密码
            if (_settingsService?.Settings?.SavePasswords != true) return;
            
            // 检查是否在一律不保存列表中
            if (_passwordService.IsNeverSave(host)) return;
            
            // 检查密码是否已经保存过（相同的host、username和password）
            if (_passwordService.IsPasswordAlreadySaved(host, username, password)) return;
            
            // 触发事件，让 MainForm 显示钥匙图标
            PasswordKeyButtonRequested?.Invoke(host, username, password);
        }
        catch { }
    }
    
    /// <summary>
    /// 显示保存密码弹窗（由 MainForm 调用）
    /// </summary>
    /// <param name="showSavedMode">true=显示已保存模式，false=显示询问模式</param>
    /// <param name="onResult">结果回调：saved=true表示已保存, neverSave=true表示选择了一律不</param>
    public void ShowPasswordPopup(string host, string username, string password, Control parent, Point location, bool showSavedMode = false, Action<bool, bool>? onResult = null)
    {
        try
        {
            // 获取父窗体
            var form = parent.FindForm();
            if (form == null || form.IsDisposed) return;
            
            // 根据模式创建不同的弹窗
            var mode = showSavedMode ? PasswordPopupMode.ShowSaved : PasswordPopupMode.AskToSave;
            var popup = new SavePasswordPopup(host, username, password, mode);
            
            // 计算位置（在钥匙按钮下方）
            var screenLocation = parent.PointToScreen(location);
            var x = screenLocation.X - popup.Width + 30; // 右对齐到钥匙按钮
            var y = screenLocation.Y + 5; // 按钮下方
            
            // 确保位置在屏幕范围内
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
            
            // 处理"管理已保存的密码"链接点击
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
    
    private void SendSavedPasswords(BrowserTab tab, string host)
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
    
    private void SendHistoryData(BrowserTab tab, string? keyword = null)
    {
        if (tab.WebView?.CoreWebView2 == null || _historyService == null) return;
        
        try
        {
            var items = string.IsNullOrEmpty(keyword) 
                ? _historyService.GetHistory(100) 
                : _historyService.Search(keyword, 50);
            
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "historyData",
                items = items.Select(h => new
                {
                    url = h.Url,
                    title = h.Title,
                    visitTime = h.VisitTime.ToString("O"),
                    favicon = GetFaviconUrl(h.Url)
                })
            });
            
            tab.WebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }
    
    private static string GetFaviconUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=16";
        }
        catch
        {
            return "";
        }
    }
    
    private void BrowseDownloadPath(BrowserTab tab)
    {
        try
        {
            // 在 UI 线程上执行文件夹选择对话框
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => BrowseDownloadPath(tab));
                return;
            }
            
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择下载文件夹",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            
            // 设置初始路径
            if (_settingsService?.Settings?.DownloadPath is string currentPath && Directory.Exists(currentPath))
            {
                dialog.InitialDirectory = currentPath;
            }
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var selectedPath = dialog.SelectedPath;
                
                // 更新设置
                if (_settingsService?.Settings != null)
                {
                    _settingsService.Settings.DownloadPath = selectedPath;
                    _settingsService.Save();
                }
                
                // 发送消息给页面更新显示
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    action = "downloadPathSelected",
                    path = selectedPath
                });
                tab.WebView?.CoreWebView2?.PostWebMessageAsJson(json);
            }
        }
        catch { }
    }
    
    private void OpenSearchEngineManager(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenSearchEngineManager(tab));
                return;
            }
            
            if (_settingsService == null) return;
            
            using var dialog = new Forms.SearchEngineManagerDialog(_settingsService);
            dialog.ShowDialog();
            
            // 刷新设置页面
            tab.Navigate("about:settings");
        }
        catch { }
    }
    
    private void OpenAdBlockExceptions(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenAdBlockExceptions(tab));
                return;
            }
            
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
            // 获取规则文件夹路径
            var rulesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MiniWorld", "AdBlockRules");
            
            // 如果文件夹不存在则创建
            if (!Directory.Exists(rulesFolder))
            {
                Directory.CreateDirectory(rulesFolder);
            }
            
            // 打开文件夹
            System.Diagnostics.Process.Start("explorer.exe", rulesFolder);
        }
        catch { }
    }
    
    private void OpenContentSettings(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenContentSettings(tab));
                return;
            }
            
            if (_settingsService == null) return;
            
            using var dialog = new Forms.ContentSettingsDialog(_settingsService);
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void OpenClearBrowsingData(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => OpenClearBrowsingData(tab));
                return;
            }
            
            using var dialog = new Forms.ClearBrowsingDataDialog();
            dialog.ShowDialog();
        }
        catch { }
    }
    
    private void SendBookmarksData(BrowserTab tab, string? folderId)
    {
        if (tab.WebView?.CoreWebView2 == null || _bookmarkService == null) return;
        
        try
        {
            var items = string.IsNullOrEmpty(folderId) 
                ? _bookmarkService.GetBookmarkBarItems() 
                : _bookmarkService.GetChildren(folderId);
            
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "bookmarksData",
                items = items.Select(b => new
                {
                    id = b.Id,
                    title = b.Title,
                    url = b.Url ?? "",
                    isFolder = b.IsFolder,
                    favicon = b.FaviconUrl ?? GetFaviconUrl(b.Url ?? "")
                })
            });
            
            tab.WebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }
    
    private void SendBookmarksSearchData(BrowserTab tab, string keyword)
    {
        if (tab.WebView?.CoreWebView2 == null || _bookmarkService == null) return;
        
        try
        {
            var items = _bookmarkService.Search(keyword);
            
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "bookmarksData",
                items = items.Select(b => new
                {
                    id = b.Id,
                    title = b.Title,
                    url = b.Url ?? "",
                    isFolder = b.IsFolder,
                    favicon = b.FaviconUrl ?? GetFaviconUrl(b.Url ?? "")
                })
            });
            
            tab.WebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }
    
    private void ExportBookmarks(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.InvokeRequired == true)
            {
                tab.WebView.Invoke(() => ExportBookmarks(tab));
                return;
            }
            
            using var dialog = new SaveFileDialog
            {
                Title = "导出收藏",
                Filter = "HTML 文件 (*.html)|*.html",
                FileName = $"bookmarks_{DateTime.Now:yyyyMMdd}.html",
                DefaultExt = "html"
            };
            
            if (dialog.ShowDialog() != DialogResult.OK) return;
            
            var html = GenerateBookmarksExportHtml();
            File.WriteAllText(dialog.FileName, html, System.Text.Encoding.UTF8);
            
            MessageBox.Show($"收藏已导出到:\n{dialog.FileName}", "导出成功", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    private string GenerateBookmarksExportHtml()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        sb.AppendLine("<TITLE>Bookmarks</TITLE>");
        sb.AppendLine("<H1>Bookmarks</H1>");
        sb.AppendLine("<DL><p>");
        
        var items = _bookmarkService?.GetBookmarkBarItems() ?? new List<Models.Bookmark>();
        foreach (var item in items)
        {
            AppendBookmarkToHtml(sb, item, 1);
        }
        
        sb.AppendLine("</DL><p>");
        return sb.ToString();
    }
    
    private void AppendBookmarkToHtml(System.Text.StringBuilder sb, Models.Bookmark item, int indent)
    {
        var indentStr = new string(' ', indent * 4);
        
        if (item.IsFolder)
        {
            sb.AppendLine($"{indentStr}<DT><H3>{System.Net.WebUtility.HtmlEncode(item.Title)}</H3>");
            sb.AppendLine($"{indentStr}<DL><p>");
            
            var children = _bookmarkService?.GetChildren(item.Id) ?? new List<Models.Bookmark>();
            foreach (var child in children)
            {
                AppendBookmarkToHtml(sb, child, indent + 1);
            }
            
            sb.AppendLine($"{indentStr}</DL><p>");
        }
        else
        {
            sb.AppendLine($"{indentStr}<DT><A HREF=\"{System.Net.WebUtility.HtmlEncode(item.Url)}\">{System.Net.WebUtility.HtmlEncode(item.Title)}</A>");
        }
    }
    
    private void OnDownloadStarting(BrowserTab tab, Microsoft.Web.WebView2.Core.CoreWebView2DownloadStartingEventArgs e)
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
            
            // 获取文件名
            var resultFilePath = downloadOperation.ResultFilePath;
            if (!string.IsNullOrEmpty(resultFilePath))
            {
                downloadItem.FileName = Path.GetFileName(resultFilePath);
                downloadItem.FilePath = resultFilePath;
            }
            else
            {
                // 从 URL 获取文件名
                var uri = new Uri(downloadOperation.Uri);
                downloadItem.FileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrEmpty(downloadItem.FileName))
                    downloadItem.FileName = "download";
            }
            
            _downloads.Add(downloadItem);
            
            // 监听下载进度
            downloadOperation.BytesReceivedChanged += (s, args) =>
            {
                downloadItem.ReceivedBytes = (long)downloadOperation.BytesReceived;
                downloadItem.TotalBytes = (long)(downloadOperation.TotalBytesToReceive ?? (ulong)downloadItem.TotalBytes);
            };
            
            downloadOperation.StateChanged += (s, args) =>
            {
                switch (downloadOperation.State)
                {
                    case Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Completed:
                        downloadItem.Status = MiniWorldBrowser.Models.DownloadStatus.Completed;
                        downloadItem.FilePath = downloadOperation.ResultFilePath;
                        downloadItem.EndTime = DateTime.Now;
                        break;
                    case Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Interrupted:
                        downloadItem.Status = downloadOperation.InterruptReason == Microsoft.Web.WebView2.Core.CoreWebView2DownloadInterruptReason.UserCanceled
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
    
    private void HandleGesture(BrowserTab tab, string? gesture)
    {
        if (string.IsNullOrEmpty(gesture) || tab != _activeTab) return;
        
        switch (gesture)
        {
            case "L": // 左滑 - 后退
            case "UL": // 上左 - 后退
                tab.GoBack();
                break;
            case "R": // 右滑 - 前进
            case "UR": // 上右 - 前进
                tab.GoForward();
                break;
            case "U": // 上滑 - 滚动到顶部
                _ = tab.WebView?.CoreWebView2?.ExecuteScriptAsync("window.scrollTo(0, 0);");
                break;
            case "D": // 下滑 - 滚动到底部
                _ = tab.WebView?.CoreWebView2?.ExecuteScriptAsync("window.scrollTo(0, document.body.scrollHeight);");
                break;
            case "UD": // 上下 - 刷新
                tab.Refresh();
                break;
            case "DR": // 下右 - 关闭标签页
            case "RD": // 右下 - 关闭标签页
                CloseTab(tab);
                break;
            case "DU": // 下上 - 新建标签页
                _ = CreateTabAsync(_settingsService?.Settings?.HomePage ?? "about:newtab");
                break;
        }
    }
    
    private void HandleSettingUpdate(string? key, System.Text.Json.JsonElement value)
    {
        if (string.IsNullOrEmpty(key) || _settingsService?.Settings == null) return;
        
        try
        {
            switch (key)
            {
                case "hidebookmarkbar":
                    var hideBookmarkBar = value.GetBoolean();
                    _settingsService.Settings.AlwaysShowBookmarkBar = !hideBookmarkBar;
                    _settingsService.Save();
                    SettingChanged?.Invoke("hidebookmarkbar", hideBookmarkBar);
                    break;
                case "bookmarkbar":
                    var showBookmarkBar = value.GetBoolean();
                    _settingsService.Settings.AlwaysShowBookmarkBar = showBookmarkBar;
                    _settingsService.Save();
                    SettingChanged?.Invoke("bookmarkbar", showBookmarkBar);
                    break;
                case "homebutton":
                    _settingsService.Settings.ShowHomeButton = value.GetBoolean();
                    _settingsService.Save();
                    SettingChanged?.Invoke("homebutton", value.GetBoolean());
                    break;
                case "homepage":
                    _settingsService.Settings.HomePage = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "adblock":
                    _settingsService.Settings.EnableAdBlock = value.GetBoolean();
                    _settingsService.Save();
                    SettingChanged?.Invoke("adblock", value.GetBoolean());
                    break;
                case "adblockmode":
                    var adBlockMode = int.Parse(value.GetString() ?? "2");
                    _settingsService.Settings.AdBlockMode = adBlockMode;
                    _settingsService.Settings.EnableAdBlock = adBlockMode > 0;
                    _settingsService.Save();
                    // 更新 AdBlockService
                    if (_adBlockService != null)
                    {
                        _adBlockService.Mode = adBlockMode;
                        _adBlockService.Enabled = adBlockMode > 0;
                    }
                    SettingChanged?.Invoke("adblockmode", adBlockMode);
                    break;
                case "gesture":
                    _settingsService.Settings.EnableMouseGesture = value.GetBoolean();
                    _settingsService.Save();
                    SettingChanged?.Invoke("gesture", value.GetBoolean());
                    break;
                case "superdrag":
                    _settingsService.Settings.EnableSuperDrag = value.GetBoolean();
                    _settingsService.Save();
                    SettingChanged?.Invoke("superdrag", value.GetBoolean());
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
                    _settingsService.Save();
                    SettingChanged?.Invoke("search", searchIndex);
                    break;
                case "startup":
                    var startupIndex = int.Parse(value.GetString() ?? "0");
                    _settingsService.Settings.StartupBehavior = startupIndex;
                    _settingsService.Save();
                    break;
                case "downloadpath":
                    _settingsService.Settings.DownloadPath = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "askdownload":
                    _settingsService.Settings.AskDownloadLocation = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "crashupload":
                    _settingsService.Settings.EnableCrashUpload = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "rightclickclosetab":
                    var rightClickClose = value.GetBoolean();
                    _settingsService.Settings.RightClickCloseTab = rightClickClose;
                    _settingsService.Save();
                    // 更新所有标签的右击关闭设置
                    foreach (var t in _tabs)
                    {
                        if (t.TabButton != null)
                            t.TabButton.RightClickToClose = rightClickClose;
                    }
                    SettingChanged?.Invoke("rightclickclosetab", rightClickClose);
                    break;
                case "openlinksbackground":
                    _settingsService.Settings.OpenLinksInBackground = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "addressbarinput":
                    _settingsService.Settings.AddressBarInputMode = int.Parse(value.GetString() ?? "0");
                    _settingsService.Save();
                    break;
                case "newtabposition":
                    _settingsService.Settings.NewTabPosition = int.Parse(value.GetString() ?? "0");
                    _settingsService.Save();
                    break;
                case "smoothscrolling":
                    _settingsService.Settings.EnableSmoothScrolling = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "enableautofill":
                    _settingsService.Settings.EnableAutofill = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "savepasswords":
                    _settingsService.Settings.SavePasswords = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "fontsize":
                    var fontSize = int.Parse(value.GetString() ?? "2");
                    _settingsService.Settings.FontSize = fontSize;
                    _settingsService.Save();
                    // 应用字体大小到所有标签页
                    ApplyFontSizeToAllTabs(fontSize);
                    break;
                case "pagezoom":
                    var pageZoom = int.Parse(value.GetString() ?? "100");
                    _settingsService.Settings.PageZoom = pageZoom;
                    _settingsService.Save();
                    // 应用缩放到所有标签页
                    ApplyZoomToAllTabs(pageZoom);
                    break;
                case "aimode":
                    _settingsService.Settings.AiServiceMode = int.Parse(value.GetString() ?? "0");
                    _settingsService.Save();
                    SettingChanged?.Invoke("aimode", _settingsService.Settings.AiServiceMode);
                    break;
                case "aiapikey":
                    _settingsService.Settings.AiApiKey = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "aiapibaseurl":
                    _settingsService.Settings.AiApiBaseUrl = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "aimodelname":
                    _settingsService.Settings.AiModelName = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "aicustomweburl":
                    _settingsService.Settings.AiCustomWebUrl = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
            }
        }
        catch { }
    }
    
    #endregion
}
