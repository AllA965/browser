using MiniWorldBrowser.Browser;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Features;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Helpers.Extensions;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 隐身模式窗口 - 复用主窗口 UI，使用独立的用户数据目录
/// </summary>
public partial class IncognitoForm : Form
{
    #region 深色主题颜色
    
    private static readonly Color DarkBackground = Color.FromArgb(53, 54, 58);
    private static readonly Color DarkTabBar = Color.FromArgb(41, 42, 45);
    private static readonly Color DarkToolbar = Color.FromArgb(53, 54, 58);
    private static readonly Color DarkAddressBar = Color.FromArgb(32, 33, 36);
    private static readonly Color DarkBrowser = Color.FromArgb(32, 33, 36);
    private static readonly Color DarkStatusBar = Color.FromArgb(41, 42, 45);
    private static readonly Color DarkText = Color.FromArgb(200, 200, 200);
    private static readonly Color DarkSecondaryText = Color.FromArgb(150, 150, 150);
    private static readonly Color DarkBorder = Color.FromArgb(60, 60, 60);
    private static readonly Color DarkHover = Color.FromArgb(70, 70, 70);
    private static readonly Color IncognitoAccent = Color.FromArgb(100, 150, 255);
    
    #endregion
    
    #region 字段
    
    private readonly string _incognitoDataFolder;
    private readonly ISettingsService _settingsService;
    private readonly IBookmarkService _bookmarkService;
    private readonly ILoginService _loginService;
    private readonly IAdBlockService _adBlockService;
    private readonly IHistoryService _historyService;
    private readonly IHistoryService? _mainHistoryService; // 主窗口历史服务，用于获取经常访问的网站
    private IncognitoTabManager _tabManager = null!;
    private MouseGesture _mouseGesture = null!;
    private FullscreenManager _fullscreenManager = null!;
    
    #endregion
    
    #region UI 控件 - 与 MainForm 相同的控件类型
    
    private Panel _tabBar = null!;
    private FlowLayoutPanel _tabContainer = null!;
    private NewTabButton _newTabButton = null!;  // 使用 NewTabButton
    private Button _tabOverflowBtn = null!; // 标签溢出按钮
    private TabOverflowPanel _tabOverflowPanel = null!; // 标签溢出面板
    private Panel _incognitoIndicator = null!;
    private Button _minimizeBtn = null!, _maximizeBtn = null!, _closeBtn = null!;
    private Panel _toolbar = null!;
    private NavigationButton _backBtn = null!, _forwardBtn = null!, _refreshBtn = null!, _stopBtn = null!, _homeBtn = null!;  // 使用 NavigationButton
    private TextBox _addressBar = null!;
    private SecurityIcon _securityIcon = null!;
    private AnimatedBookmarkButton _bookmarkBtn = null!;  // 使用 AnimatedBookmarkButton
    private Button _zoomBtn = null!;  // 放大镜图标按钮
    private DownloadButton _downloadBtn = null!;  // 使用 DownloadButton
    private RoundedButton _settingsBtn = null!;  // 使用 RoundedButton
    private UserButton _userBtn = null!;  // 用户登录按钮
    private UserInfoPopup? _userInfoPopup;
    private DateTime _lastUserInfoPopupCloseTime = DateTime.MinValue;
    private bool _suppressUserInfoPopupClose = false;
    private RoundedButton? _passwordKeyBtn;  // 密码钥匙按钮
    private string? _pendingPasswordHost;
    private string? _pendingPasswordUsername;
    private string? _pendingPasswordPassword;
    private BookmarkBar _bookmarkBar = null!;
    private Panel _browserContainer = null!;
    private Panel _statusBar = null!;
    private Label _statusLabel = null!;
    private PictureBox _titleBarIcon = null!;
    private ModernProgressBar _progressBar = null!;
    private AddressBarDropdown _addressDropdown = null!;
    
    private readonly List<string> _urlHistory = new();
    private double _zoomLevel = 1.0;
    private ContextMenuStrip? _mainMenu;
    
    // 缩放弹窗相关
    private Panel? _zoomPopup;
    private Label? _zoomPopupLabel;
    private System.Windows.Forms.Timer? _zoomPopupTimer;
    
    #endregion
    
    #region 构造函数
    
    public IncognitoForm(ISettingsService settingsService, IBookmarkService bookmarkService, ILoginService loginService, IHistoryService? mainHistoryService = null)
    {
        _settingsService = settingsService;
        _bookmarkService = bookmarkService;
        _loginService = loginService;
        _mainHistoryService = mainHistoryService;
        _adBlockService = new AdBlockService 
        { 
            Enabled = _settingsService.Settings.EnableAdBlock,
            Mode = _settingsService.Settings.AdBlockMode
        };
        _adBlockService.SetExceptions(_settingsService.Settings.AdBlockExceptions);
        _historyService = new HistoryService();
        
        _incognitoDataFolder = Path.Combine(
            Path.GetTempPath(),
            "MiniWorld_Incognito_" + Guid.NewGuid().ToString("N")[..8]);
        
        InitializeUI();
        InitializeManagers();
        SetupBookmarkBarEvents();
        InitializeEvents();
        
        // 注册到多窗口管理器
        MultiWindowApplicationContext.Current?.RegisterForm(this);
        
        Shown += async (s, e) =>
        {
            RefreshAllControls();
            try
            {
                var homePage = _settingsService?.Settings?.HomePage ?? AppConstants.DefaultHomePage;
                await _tabManager.CreateTabAsync(homePage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"隐身模式初始化失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        
        FormClosed += OnFormClosed;
    }
    
    #endregion
    
    #region 初始化
    
    private void InitializeUI()
    {
        Text = "InPrivate - " + AppConstants.AppName;
        Size = new Size(1200, 800);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DarkBackground;
        FormBorderStyle = FormBorderStyle.None;
        
        // 设置窗口图标
        AppIconHelper.SetIcon(this);
        
        CreateTabBar();
        CreateTabOverflowPanel();
        CreateToolbar();
        CreateBookmarkBar();
        CreateBrowserContainer();
        CreateStatusBar();
        CreateAddressDropdown();
        
        Controls.Add(_browserContainer);
        Controls.Add(_statusBar);
        Controls.Add(_bookmarkBar);
        Controls.Add(_toolbar);
        Controls.Add(_tabBar);
    }
    
    private void CreateTabBar()
    {
        _tabBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = DarkTabBar
        };
        _tabBar.MouseDown += OnTitleBarMouseDown;

        // 标题栏图标
        _titleBarIcon = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = 8,
            BackColor = Color.Transparent,
            Visible = false
        };
        
        _titleBarIcon.Paint += (s, e) =>
        {
            if (AppIconHelper.AppIcon != null)
            {
                // 居中绘制图标，保留透明度
                int iconSize = 18;
                int x = (_titleBarIcon.Width - iconSize) / 2;
                int y = (_titleBarIcon.Height - iconSize) / 2;
                
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawIcon(AppIconHelper.AppIcon, new Rectangle(x, y, iconSize, iconSize));
            }
        };
        _titleBarIcon.MouseDown += OnTitleBarMouseDown;
        
        // 窗口控制按钮
        var windowControlPanel = new Panel { Dock = DockStyle.Right, Width = 138, BackColor = Color.Transparent };
        
        _minimizeBtn = CreateWindowControlButton("─");
        _minimizeBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
        
        _maximizeBtn = CreateWindowControlButton("☐");
        _maximizeBtn.Click += (s, e) => ToggleMaximize();
        
        _closeBtn = CreateWindowControlButton("✕");
        _closeBtn.Click += (s, e) => Close();
        _closeBtn.MouseEnter += (s, e) => { _closeBtn.BackColor = Color.FromArgb(232, 17, 35); _closeBtn.ForeColor = Color.White; };
        _closeBtn.MouseLeave += (s, e) => { _closeBtn.BackColor = Color.Transparent; _closeBtn.ForeColor = DarkText; };
        
        windowControlPanel.Controls.AddRange(new Control[] { _minimizeBtn, _maximizeBtn, _closeBtn });
        
        // 隐身模式标识
        _incognitoIndicator = CreateIncognitoIndicator();
        
        // 新标签按钮 - 使用 NewTabButton，深色模式
        _newTabButton = new NewTabButton(true)  // isDarkMode = true
        {
            Size = new Size(28, 28),
            Margin = new Padding(2, 4, 2, 0) // 调整边距使其对齐
        };
        new ToolTip().SetToolTip(_newTabButton, "新建标签页 (Ctrl+T)");

        // 标签容器
        _tabContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(4, 4, 0, 0),
            BackColor = Color.Transparent
        };
        _tabContainer.MouseDown += OnTitleBarMouseDown;
        
        // 将新标签按钮添加到容器中，这样它就会排在标签后面
        _tabContainer.Controls.Add(_newTabButton);
        
        // 标签溢出按钮
        _tabOverflowBtn = new Button
        {
            Dock = DockStyle.Right,
            Width = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Text = "﹀", // 或者使用 unicode 字符
            Font = new Font("Segoe UI Symbol", 9F),
            ForeColor = DarkText,
            Cursor = Cursors.Hand,
            Visible = false, // 默认隐藏
            Margin = new Padding(0)
        };
        _tabOverflowBtn.FlatAppearance.BorderSize = 0;
        _tabOverflowBtn.FlatAppearance.MouseOverBackColor = DarkHover;
        _tabOverflowBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, 60, 60);
        new ToolTip().SetToolTip(_tabOverflowBtn, "搜索标签页");

        var tabStripHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        tabStripHostPanel.Controls.Add(_tabContainer);
        tabStripHostPanel.Controls.Add(_tabOverflowBtn);

        _tabBar.Controls.Add(tabStripHostPanel);
        _tabBar.Controls.Add(_titleBarIcon);
        _tabBar.Controls.Add(_incognitoIndicator);
        _tabBar.Controls.Add(windowControlPanel);
    }

    private void CreateTabOverflowPanel()
    {
        _tabOverflowPanel = new TabOverflowPanel(true)
        {
            Visible = false
        };
        
        Controls.Add(_tabOverflowPanel);
        _tabOverflowPanel.BringToFront();
    }
    
    private Panel CreateIncognitoIndicator()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 90,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        
        var label = new Label
        {
            Text = "🕵️ InPrivate",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9F),
            ForeColor = IncognitoAccent,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        
        label.Click += (s, e) => ShowIncognitoInfo();
        panel.Click += (s, e) => ShowIncognitoInfo();
        label.MouseEnter += (s, e) => label.ForeColor = Color.FromArgb(150, 180, 255);
        label.MouseLeave += (s, e) => label.ForeColor = IncognitoAccent;
        
        panel.Controls.Add(label);
        return panel;
    }
    
    private void ShowIncognitoInfo()
    {
        MessageBox.Show(
            "您正在使用 InPrivate 浏览模式\n\n" +
            "✓ InPrivate 浏览的功能：\n" +
            "  • 不保存浏览历史记录\n" +
            "  • 不保存 Cookie 和网站数据\n" +
            "  • 不保存表单数据\n" +
            "  • 独立的会话环境\n\n" +
            "✗ InPrivate 浏览不会：\n" +
            "  • 对网络管理员隐藏浏览活动\n" +
            "  • 对 Internet 服务提供商隐藏活动\n" +
            "  • 阻止网站获取您的 IP 地址\n\n" +
            "注意：下载的文件和创建的书签会保留。",
            "InPrivate 浏览",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
    
    private void CreateToolbar()
    {
        _toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = DarkToolbar,
            Padding = new Padding(4, 0, 4, 0)
        };
        
        // 使用 NavigationButton - 与 MainForm 相同
        _backBtn = CreateNavigationButton(NavigationButtonType.Back, "后退 (Alt+Left)");
        _forwardBtn = CreateNavigationButton(NavigationButtonType.Forward, "前进 (Alt+Right)");
        _refreshBtn = CreateNavigationButton(NavigationButtonType.Refresh, "刷新 (F5)");
        _stopBtn = CreateNavigationButton(NavigationButtonType.Stop, "停止 (Esc)");
        _stopBtn.Visible = false;
        
        _homeBtn = CreateNavigationButton(NavigationButtonType.Home, "主页 (Alt+Home)");
        _homeBtn.Visible = _settingsService.Settings.ShowHomeButton;
        
        _securityIcon = new SecurityIcon
        {
            Size = new Size(22, 22),
            BackColor = DarkAddressBar
        };
        _securityIcon.SecurityInfoRequested += OnSecurityInfoRequested;
        
        _addressBar = new TextBox
        {
            Height = 22,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BorderStyle = BorderStyle.None,
            BackColor = DarkAddressBar,
            ForeColor = DarkText
        };
        
        // 使用 AnimatedBookmarkButton - 与 MainForm 相同
        _bookmarkBtn = new AnimatedBookmarkButton
        {
            Size = new Size(28, 28),
            BackColor = Color.Transparent
        };
        new ToolTip().SetToolTip(_bookmarkBtn, "添加到收藏夹 (Ctrl+D)");
        
        // 放大镜图标按钮（缩放不是100%时显示）
        _zoomBtn = new Button
        {
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Text = "🔍",
            Font = new Font("Segoe UI Emoji", 11F),
            Cursor = Cursors.Hand,
            Visible = false,
            Margin = new Padding(2),
            ForeColor = DarkText
        };
        _zoomBtn.FlatAppearance.BorderSize = 0;
        _zoomBtn.FlatAppearance.MouseOverBackColor = DarkHover;
        _zoomBtn.Click += (s, e) => ShowZoomPopup();
        new ToolTip().SetToolTip(_zoomBtn, "缩放");
        
        // 使用 DownloadButton - 与 MainForm 相同
        _downloadBtn = new DownloadButton
        {
            Size = new Size(32, 32),
            Margin = new Padding(2),
            IconColor = DarkText
        };
        new ToolTip().SetToolTip(_downloadBtn, "下载 (Ctrl+J)");
        
        // 使用 RoundedButton - 与 MainForm 相同
        _settingsBtn = CreateToolButton("☰", "菜单");
        
        // 用户按钮
        _userBtn = new UserButton
        {
            Size = new Size(32, 32),
            Margin = new Padding(2),
            BackColor = Color.Transparent
        };
        _userBtn.Click += OnUserButtonClick;
        RefreshLoginStatus();
        
        // 布局 - 与 MainForm 完全相同
        var toolPanel = new Panel { Dock = DockStyle.Fill, BackColor = DarkToolbar };
        
        var navPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(4, 4, 0, 4)
        };
        
        var refreshStopPanel = new Panel { Size = new Size(32, 32) };
        _refreshBtn.Dock = DockStyle.Fill;
        _stopBtn.Dock = DockStyle.Fill;
        refreshStopPanel.Controls.Add(_stopBtn);
        refreshStopPanel.Controls.Add(_refreshBtn);
        
        navPanel.Controls.AddRange(new Control[] { _backBtn, _forwardBtn, refreshStopPanel, _homeBtn });
        
        var menuPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 4, 4, 4)
        };
        menuPanel.Controls.Add(_zoomBtn);
        menuPanel.Controls.Add(_downloadBtn);
        menuPanel.Controls.Add(_userBtn);
        menuPanel.Controls.Add(_settingsBtn);
        
        var addressContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = DarkToolbar
        };
        
        var addressPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DarkAddressBar,
            Padding = new Padding(6, 5, 4, 3)
        };
        addressPanel.Paint += (s, e) =>
        {
            var rect = new Rectangle(0, 0, addressPanel.Width - 1, addressPanel.Height - 1);
            using var path = ControlExtensions.CreateRoundedRectangle(rect, 4);
            using var pen = new Pen(DarkBorder, 1);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        };
        
        _securityIcon.Dock = DockStyle.Left;
        _bookmarkBtn.Dock = DockStyle.Right;
        _addressBar.Dock = DockStyle.Fill;
        
        addressPanel.Controls.AddRange(new Control[] { _addressBar, _bookmarkBtn, _securityIcon });
        addressContainer.Controls.Add(addressPanel);
        
        toolPanel.Controls.AddRange(new Control[] { addressContainer, navPanel, menuPanel });
        _toolbar.Controls.Add(toolPanel);
    }
    
    private void CreateBookmarkBar()
    {
        _bookmarkBar = new BookmarkBar(_bookmarkService);
        _bookmarkBar.IsIncognito = true; // 设置为隐身模式样式
        _bookmarkBar.BackColor = DarkToolbar;
        _bookmarkBar.ForeColor = DarkText;
    }
    
    private void SetupBookmarkBarEvents()
    {
        _bookmarkBar.BookmarkClicked += url => _tabManager.ActiveTab?.Navigate(url);
        _bookmarkBar.BookmarkMiddleClicked += async (url, _) => await _tabManager.CreateTabAsync(url);
        _bookmarkBar.AddBookmarkRequested += ShowAddBookmarkDialog;
    }
    
    private void CreateBrowserContainer()
    {
        _browserContainer = new Panel { Dock = DockStyle.Fill, BackColor = DarkBrowser };
    }
    
    private void CreateStatusBar()
    {
        _statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            BackColor = DarkStatusBar
        };
        
        _statusLabel = new Label
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            Padding = new Padding(4, 3, 0, 0),
            Font = new Font("Microsoft YaHei UI", 8F),
            ForeColor = DarkSecondaryText,
            Text = "InPrivate - 您的浏览活动不会保存到此设备"
        };
        
        _progressBar = new ModernProgressBar
        {
            Dock = DockStyle.Right,
            Width = 110,
            Height = 22,
            Padding = new Padding(10, 0, 10, 0),
            Visible = false,
            IsMarquee = true,
            ProgressColor = Color.FromArgb(100, 150, 255),
            ProgressColor2 = Color.FromArgb(150, 200, 255)
        };
        
        _statusBar.Controls.AddRange(new Control[] { _statusLabel, _progressBar });
    }
    
    private void CreateAddressDropdown()
    {
        _addressDropdown = new AddressBarDropdown(_historyService, _bookmarkService, isDarkMode: true);
        _addressDropdown.SearchEngine = _settingsService.Settings.SearchEngine;
        _addressDropdown.ItemSelected += url =>
        {
            _addressBar.Text = url;
            _tabManager?.ActiveTab?.Navigate(url);
            _browserContainer.Focus();
        };
        _addressDropdown.SearchRequested += url =>
        {
            _addressBar.Text = url;
            _tabManager?.ActiveTab?.Navigate(url);
            _browserContainer.Focus();
        };
        _addressDropdown.GetOpenTabs += () =>
        {
            var tabs = new List<(string Title, string Url)>();
            if (_tabManager != null)
            {
                foreach (var tab in _tabManager.Tabs)
                    tabs.Add((tab.Title ?? "新标签页", tab.Url ?? ""));
            }
            return tabs;
        };
        _addressDropdown.RequestFocusRestore += () => BeginInvoke(() => _addressBar.Focus());
    }
    
    private void InitializeManagers()
    {
        _tabManager = new IncognitoTabManager(
            _browserContainer, _tabContainer, _newTabButton, _tabOverflowBtn,
            _settingsService, _adBlockService, _incognitoDataFolder, _mainHistoryService);
        
        _tabManager.SetOverflowPanel(_tabOverflowPanel);
        
        _tabManager.ActiveTabChanged += OnActiveTabChanged;
        _tabManager.TabTitleChanged += t => { if (t == _tabManager.ActiveTab) Text = $"🕵️ {t.Title} - InPrivate"; };
        _tabManager.TabUrlChanged += OnTabUrlChanged;
        _tabManager.TabLoadingStateChanged += OnTabLoadingStateChanged;
        _tabManager.TabSecurityStateChanged += t => { if (t == _tabManager.ActiveTab) UpdateSecurityIcon(t.IsSecure); };
        _tabManager.TabStatusTextChanged += (t, text) => { if (t == _tabManager.ActiveTab) _statusLabel.Text = string.IsNullOrEmpty(text) ? "InPrivate - 您的浏览活动不会保存到此设备" : text; };
        _tabManager.TabZoomChanged += OnTabZoomChanged;
        _tabManager.NewWindowRequested += url => _ = _tabManager.CreateTabAsync(url, _settingsService.Settings.OpenLinksInBackground);
        _tabManager.AllTabsClosed += () => Close();
        _tabManager.WebViewClicked += () => ClosePopups();
        _tabManager.PasswordKeyButtonRequested += OnPasswordKeyButtonRequested;
        _tabManager.BookmarkAllTabsRequested += OnBookmarkAllTabsRequested;
        _tabManager.SettingChanged += OnSettingChanged;
        
        _mouseGesture = new MouseGesture(this);
        _mouseGesture.Enabled = _settingsService.Settings.EnableMouseGesture;
        _mouseGesture.GestureBack += () => _tabManager.ActiveTab?.GoBack();
        _mouseGesture.GestureForward += () => _tabManager.ActiveTab?.GoForward();
        _mouseGesture.GestureRefresh += () => _tabManager.ActiveTab?.Refresh();
        _mouseGesture.GestureClose += () => { if (_tabManager.ActiveTab != null) _tabManager.CloseTab(_tabManager.ActiveTab); };
        
        _fullscreenManager = new FullscreenManager(this, _tabBar, _toolbar, _bookmarkBar, _statusBar);
    }
    
    private void InitializeEvents()
    {
        Deactivate += (s, e) =>
        {
            // 关闭菜单
            CloseMainMenu();
            
            // 如果下拉框正在交互，不要隐藏它
            if (!_addressDropdown.IsInteracting)
                _addressDropdown.Hide();

            if (!_suppressUserInfoPopupClose && _userInfoPopup != null && !_userInfoPopup.IsDisposed)
            {
                CloseUserInfoPopup();
            }
        };
        
        MouseDown += (s, e) => ClosePopups();
        _browserContainer.MouseDown += (s, e) => ClosePopups();
        _tabBar.MouseDown += (s, e) => ClosePopups();
        _toolbar.MouseDown += (s, e) => ClosePopups();
        _tabContainer.MouseDown += (s, e) => ClosePopups();
        _statusBar.MouseDown += (s, e) => ClosePopups();
        _bookmarkBar.MouseDown += (s, e) => ClosePopups();
        
        MouseMove += OnFormMouseMove;
        
        _backBtn.Click += (s, e) => _tabManager.ActiveTab?.GoBack();
        _forwardBtn.Click += (s, e) => _tabManager.ActiveTab?.GoForward();
        _refreshBtn.Click += (s, e) => _tabManager.ActiveTab?.Refresh();
        _stopBtn.Click += (s, e) => _tabManager.ActiveTab?.Stop();
        _homeBtn.Click += (s, e) => _tabManager.ActiveTab?.Navigate(_settingsService.Settings.HomePage);
        _downloadBtn.Click += (s, e) => OpenDownloadDialog();
        _settingsBtn.Click += (s, e) => ShowMainMenu();
        _bookmarkBtn.BookmarkClicked += (s, e) => ToggleBookmark();
        _newTabButton.Click += async (s, e) => await _tabManager.CreateTabAsync("about:newtab");
        
        // 登录相关事件
        _loginService.LoginStateChanged += () => RefreshLoginStatus();
        
        _addressBar.KeyDown += OnAddressBarKeyDown;
        _addressBar.TextChanged += (s, e) => { if (_addressBar.Focused && _tabManager?.ActiveTab != null) ShowAddressDropdown(); };
        _addressBar.GotFocus += (s, e) =>
        {
            _addressBar.SelectAll();
            if (_tabManager?.ActiveTab != null) ShowAddressDropdown();
        };
        _addressBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left && _tabManager?.ActiveTab != null) ShowAddressDropdown();
        };
        _addressBar.LostFocus += (s, e) =>
        {
            var timer = new System.Windows.Forms.Timer { Interval = 150 };
            timer.Tick += (ts, te) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!_addressDropdown.ContainsFocus && !_addressDropdown.IsInteracting && !_addressBar.Focused)
                    _addressDropdown.Hide();
            };
            timer.Start();
        };
        
        KeyPreview = true;
        KeyDown += OnKeyDown;
    }
    
    #endregion
    
    #region 辅助方法
    
    private RoundedButton CreateToolButton(string text, string tooltip)
    {
        var btn = new RoundedButton
        {
            Size = new Size(32, 32),
            Text = text,
            Font = new Font("Segoe UI", 11F),
            Margin = new Padding(2),
            ForeColor = DarkText,
            HoverBackColor = DarkHover
        };
        new ToolTip().SetToolTip(btn, tooltip);
        return btn;
    }
    
    private NavigationButton CreateNavigationButton(NavigationButtonType type, string tooltip)
    {
        var btn = new NavigationButton
        {
            Size = new Size(32, 32),
            ButtonType = type,
            Margin = new Padding(2),
            IconColor = DarkText
        };
        new ToolTip().SetToolTip(btn, tooltip);
        return btn;
    }
    
    private Button CreateWindowControlButton(string text)
    {
        var btn = new Button
        {
            Width = 46,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            Text = text,
            Font = new Font("Segoe UI", 10F),
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            ForeColor = DarkText,
            TabStop = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
        btn.MouseEnter += (s, e) => btn.BackColor = DarkHover;
        btn.MouseLeave += (s, e) => btn.BackColor = Color.Transparent;
        return btn;
    }
    
    private void RefreshAllControls()
    {
        _securityIcon?.Refresh();
        _bookmarkBtn?.Refresh();
        _bookmarkBar?.Refresh();
        foreach (Control ctrl in _tabContainer.Controls) ctrl.Refresh();
    }
    
    private void OnTitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (e.Clicks == 2) { ToggleMaximize(); return; }
            Win32Helper.EnableWindowDrag(Handle);
        }
    }
    
    private void ToggleMaximize()
    {
        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
            _maximizeBtn.Text = "☐";
        }
        else
        {
            MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
            WindowState = FormWindowState.Maximized;
            _maximizeBtn.Text = "❐";
        }
    }
    
    private void UpdateSecurityIcon(bool isSecure)
    {
        _securityIcon.IsSecure = isSecure;
        _securityIcon.CurrentUrl = _tabManager.ActiveTab?.Url ?? "";
    }
    
    private void OnSecurityInfoRequested(object? sender, EventArgs e)
    {
        var url = _tabManager.ActiveTab?.Url ?? "";
        var isSecure = _tabManager.ActiveTab?.IsSecure ?? false;
        var popup = new SecurityInfoPopup(url, isSecure);
        popup.ShowBelow(_securityIcon);
    }
    
    private void UpdateNavigationButtons()
    {
        _backBtn.Enabled = _tabManager.ActiveTab?.CanGoBack ?? false;
        _forwardBtn.Enabled = _tabManager.ActiveTab?.CanGoForward ?? false;
    }
    
    private void UpdateBookmarkButton(bool isBookmarked)
    {
        _bookmarkBtn.IsBookmarked = isBookmarked;
    }
    
    private void ClosePopups()
    {
        CloseMainMenu();
        _addressDropdown?.Hide();
        CloseDownloadDialog();
        _bookmarkBar?.CloseDropdowns();
        _tabOverflowPanel?.HidePanel();
        CloseUserInfoPopup();
    }
    
    private void CloseDownloadDialog()
    {
        try
        {
            var coreWebView = _tabManager?.ActiveTab?.WebView?.CoreWebView2;
            if (coreWebView?.IsDefaultDownloadDialogOpen == true)
                coreWebView.CloseDefaultDownloadDialog();
        }
        catch { }
    }
    
    private void OpenDownloadDialog()
    {
        try
        {
            var coreWebView = _tabManager.ActiveTab?.WebView?.CoreWebView2;
            if (coreWebView == null) return;
            if (coreWebView.IsDefaultDownloadDialogOpen)
                coreWebView.CloseDefaultDownloadDialog();
            else
                coreWebView.OpenDefaultDownloadDialog();
        }
        catch { }
    }
    
    #endregion

    #region 事件处理
    
    private void OnActiveTabChanged(IncognitoTab tab)
    {
        _addressBar.Text = tab.Url ?? "";
        Text = $"🕵️ {tab.Title ?? "新标签页"} - InPrivate";
        UpdateSecurityIcon(tab.IsSecure);
        UpdateNavigationButtons();
        _refreshBtn.Visible = !tab.IsLoading;
        _stopBtn.Visible = tab.IsLoading;
        _progressBar.Visible = tab.IsLoading;
        HidePasswordKeyButton();
    }
    
    private void OnTabUrlChanged(IncognitoTab tab)
    {
        if (tab != _tabManager.ActiveTab) return;
        _addressBar.Text = tab.Url ?? "";
        if (!string.IsNullOrEmpty(tab.Url) && !_urlHistory.Contains(tab.Url))
        {
            _urlHistory.Insert(0, tab.Url);
            if (_urlHistory.Count > AppConstants.MaxUrlHistoryItems)
                _urlHistory.RemoveAt(_urlHistory.Count - 1);
        }
        var isBookmarked = _bookmarkService.FindByUrl(tab.Url ?? "") != null;
        UpdateBookmarkButton(isBookmarked);
    }
    
    private void OnTabLoadingStateChanged(IncognitoTab tab)
    {
        if (tab != _tabManager.ActiveTab) return;
        _progressBar.Visible = tab.IsLoading;
        _statusLabel.Text = tab.IsLoading ? "加载中..." : "InPrivate - 您的浏览活动不会保存到此设备";
        _refreshBtn.Visible = !tab.IsLoading;
        _stopBtn.Visible = tab.IsLoading;
        UpdateNavigationButtons();
    }
    
    private void OnAddressBarKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                if (_addressDropdown.Visible)
                {
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null) _addressBar.Text = selected;
                    _addressDropdown.Hide();
                }
                NavigateToAddress();
                e.SuppressKeyPress = true;
                break;
            case Keys.Escape:
                if (_addressDropdown.Visible) _addressDropdown.Hide();
                else _addressBar.Text = _tabManager.ActiveTab?.Url ?? "";
                e.SuppressKeyPress = true;
                break;
            case Keys.Down:
                if (_addressDropdown.Visible)
                {
                    _addressDropdown.MoveSelection(1);
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null) { _addressBar.Text = selected; _addressBar.SelectionStart = _addressBar.Text.Length; }
                    e.SuppressKeyPress = true;
                }
                else { ShowAddressDropdown(); e.SuppressKeyPress = true; }
                break;
            case Keys.Up:
                if (_addressDropdown.Visible)
                {
                    _addressDropdown.MoveSelection(-1);
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null) { _addressBar.Text = selected; _addressBar.SelectionStart = _addressBar.Text.Length; }
                    e.SuppressKeyPress = true;
                }
                break;
            case Keys.Tab:
                if (_addressDropdown.Visible)
                {
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null) { _addressBar.Text = selected; _addressBar.SelectionStart = _addressBar.Text.Length; }
                    e.SuppressKeyPress = true;
                }
                break;
        }
    }
    
    private void ShowAddressDropdown()
    {
        var text = _addressBar.Text.Trim();
        var addressPanel = _addressBar.Parent;
        if (addressPanel != null)
        {
            _addressDropdown.SearchEngine = _settingsService.Settings.SearchEngine;
            _addressDropdown.Show(addressPanel, text, _urlHistory);
        }
    }
    
    private void NavigateToAddress()
    {
        _addressDropdown.Hide();
        var url = _addressBar.Text.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            _tabManager.ActiveTab?.Navigate(url);
            _browserContainer.Focus();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control)
        {
            switch (e.KeyCode)
            {
                case Keys.T: _ = _tabManager.CreateTabAsync("about:newtab"); e.Handled = true; break;
                case Keys.W: if (_tabManager.ActiveTab != null) _tabManager.CloseTab(_tabManager.ActiveTab); e.Handled = true; break;
                case Keys.Tab: if (e.Shift) _tabManager.SwitchToPreviousTab(); else _tabManager.SwitchToNextTab(); e.Handled = true; break;
                case Keys.L: _addressBar.Focus(); _addressBar.SelectAll(); e.Handled = true; break;
                case Keys.R: _tabManager.ActiveTab?.Refresh(); e.Handled = true; break;
                case Keys.D: ToggleBookmark(); e.Handled = true; break;
                case Keys.F: OpenFindInPage(); e.Handled = true; break;
                case Keys.B: if (e.Shift) { _bookmarkBar.Visible = !_bookmarkBar.Visible; e.Handled = true; } break;
                case Keys.S: SavePageAs(); e.Handled = true; break;
                case Keys.P: PrintPage(); e.Handled = true; break;
                case Keys.J: OpenDownloadDialog(); e.Handled = true; break;
                case Keys.N:
                    if (e.Shift) { var f = new IncognitoForm(_settingsService, _bookmarkService, _loginService, _mainHistoryService); f.Show(); e.Handled = true; }
                    else { System.Diagnostics.Process.Start(Application.ExecutablePath); e.Handled = true; }
                    break;
                case Keys.Oemplus:
                case Keys.Add:
                    ZoomIn(); e.Handled = true; break;
                case Keys.OemMinus:
                case Keys.Subtract:
                    ZoomOut(); e.Handled = true; break;
                case Keys.D0:
                case Keys.NumPad0:
                    ResetZoom(); e.Handled = true; break;
            }
        }
        else if (e.Alt)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: _tabManager.ActiveTab?.GoBack(); e.Handled = true; break;
                case Keys.Right: _tabManager.ActiveTab?.GoForward(); e.Handled = true; break;
                case Keys.Home: _tabManager.ActiveTab?.Navigate(_settingsService.Settings.HomePage); e.Handled = true; break;
            }
        }
        else
        {
            switch (e.KeyCode)
            {
                case Keys.F5: _tabManager.ActiveTab?.Refresh(); e.Handled = true; break;
                case Keys.F11: _fullscreenManager.Toggle(); e.Handled = true; break;
                case Keys.F12: OpenDevTools(); e.Handled = true; break;
                case Keys.Escape:
                    if (_fullscreenManager.IsFullscreen) { _fullscreenManager.Toggle(); e.Handled = true; }
                    else if (_tabManager.ActiveTab?.IsLoading == true) { _tabManager.ActiveTab.Stop(); e.Handled = true; }
                    break;
            }
        }
    }
    
    private void OnFormMouseMove(object? sender, MouseEventArgs e)
    {
        if (WindowState == FormWindowState.Maximized || (_fullscreenManager != null && _fullscreenManager.IsFullscreen))
        {
            if (Cursor != Cursors.Default) Cursor = Cursors.Default;
            return;
        }
        const int bw = 6;
        var p = e.Location;
        if (p.X < bw && p.Y < bw) Cursor = Cursors.SizeNWSE;
        else if (p.X > Width - bw && p.Y < bw) Cursor = Cursors.SizeNESW;
        else if (p.X < bw && p.Y > Height - bw) Cursor = Cursors.SizeNESW;
        else if (p.X > Width - bw && p.Y > Height - bw) Cursor = Cursors.SizeNWSE;
        else if (p.X < bw) Cursor = Cursors.SizeWE;
        else if (p.X > Width - bw) Cursor = Cursors.SizeWE;
        else if (p.Y < bw) Cursor = Cursors.SizeNS;
        else if (p.Y > Height - bw) Cursor = Cursors.SizeNS;
        else Cursor = Cursors.Default;
    }
    
    #endregion

    #region 主菜单
    
    private System.Windows.Forms.Timer? _menuCloseTimer;
    private Panel? _zoomPanel;
    private Label? _zoomLevelLabel;
    private bool _isMouseDownInMenu = false;
    private bool _lastMouseDownInMenuArea = false;
    private bool _reopenMenuAfterZoom = false;
    
    private void CloseMainMenu()
    {
        StopMenuCloseTimer();
        if (_mainMenu != null && _mainMenu.Visible)
        {
            _mainMenu.AutoClose = true;
            _mainMenu.Close();
        }
    }
    
    private void StartMenuCloseTimer()
    {
        StopMenuCloseTimer();
        _menuCloseTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _menuCloseTimer.Tick += OnMenuCloseTimerTick;
        _menuCloseTimer.Start();
    }
    
    private void StopMenuCloseTimer()
    {
        if (_menuCloseTimer != null)
        {
            _menuCloseTimer.Stop();
            _menuCloseTimer.Dispose();
            _menuCloseTimer = null;
        }
    }
    
    private void OnMenuCloseTimerTick(object? sender, EventArgs e)
    {
        if (_mainMenu == null || !_mainMenu.Visible)
        {
            StopMenuCloseTimer();
            return;
        }
        
        var mousePos = Control.MousePosition;
        var isMouseDown = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;
        
        // 如果需要重新打开菜单，不关闭
        if (_reopenMenuAfterZoom)
            return;
        
        // 检查鼠标是否在菜单区域内
        bool inMenuArea = IsMouseInMenuArea(mousePos);
        
        if (isMouseDown && !_isMouseDownInMenu)
        {
            _isMouseDownInMenu = true;
            _lastMouseDownInMenuArea = inMenuArea;
            
            // 如果在菜单区域外按下鼠标，立即关闭菜单（用于拖动窗口等操作）
            if (!inMenuArea)
            {
                CloseMainMenu();
                return;
            }
        }
        
        if (!isMouseDown && _isMouseDownInMenu)
        {
            _isMouseDownInMenu = false;
        }
    }
    
    private bool IsMouseInMenuArea(Point screenPos)
    {
        if (_mainMenu == null) return false;
        
        var menuBounds = _mainMenu.Bounds;
        menuBounds.Inflate(5, 5);
        if (menuBounds.Contains(screenPos))
            return true;
        
        // 检查缩放面板
        if (_zoomPanel != null && _zoomPanel.IsHandleCreated)
        {
            try
            {
                var panelScreen = _zoomPanel.PointToScreen(Point.Empty);
                var panelBounds = new Rectangle(panelScreen, _zoomPanel.Size);
                panelBounds.Inflate(5, 5);
                if (panelBounds.Contains(screenPos))
                    return true;
            }
            catch { }
        }
        
        if (CheckDropDownMenus(_mainMenu.Items, screenPos))
            return true;
        
        return false;
    }
    
    private bool CheckDropDownMenus(ToolStripItemCollection items, Point screenPos)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripMenuItem menuItem && menuItem.DropDown.Visible)
            {
                var bounds = menuItem.DropDown.Bounds;
                bounds.Inflate(5, 5);
                if (bounds.Contains(screenPos))
                    return true;
                if (CheckDropDownMenus(menuItem.DropDown.Items, screenPos))
                    return true;
            }
        }
        return false;
    }

    private void ShowMainMenu()
    {
        _mainMenu?.Close();
        _isMouseDownInMenu = false;
        _lastMouseDownInMenuArea = false;
        
        _mainMenu = new ContextMenuStrip
        {
            Font = new Font("Microsoft YaHei UI", 9F),
            AutoClose = false,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = DarkText,
            ShowImageMargin = true,
            ImageScalingSize = new Size(20, 20),
            Padding = new Padding(0, 4, 0, 4)
        };
        var menu = _mainMenu;
        menu.Renderer = new DarkMenuRenderer();
        
        // 菜单关闭时的处理
        menu.Closed += (s, e) =>
        {
            StopMenuCloseTimer();
            if (_reopenMenuAfterZoom)
            {
                _reopenMenuAfterZoom = false;
                BeginInvoke(() => ShowMainMenu());
            }
        };
        
        // 新建标签页
        menu.Items.Add(CreateDarkMenuItem("新建标签页(T)", "Ctrl+T", DarkMenuIconDrawer.DrawNewTab,
            async () => { CloseMainMenu(); await _tabManager.CreateTabAsync("about:newtab"); }));
        
        // 新建窗口
        menu.Items.Add(CreateDarkMenuItem("新建窗口(N)", "Ctrl+N", DarkMenuIconDrawer.DrawNewWindow,
            () => { CloseMainMenu(); System.Diagnostics.Process.Start(Application.ExecutablePath); }));
        
        // 新建隐私窗口
        menu.Items.Add(CreateDarkMenuItem("新建 InPrivate 窗口(I)", "Ctrl+Shift+N", DarkMenuIconDrawer.DrawIncognito,
            () => { CloseMainMenu(); var f = new IncognitoForm(_settingsService, _bookmarkService, _loginService, _mainHistoryService); f.Show(); }));
        
        menu.Items.Add(new ToolStripSeparator());
        
        // 缩放
        menu.Items.Add(CreateZoomMenuItem());
        
        menu.Items.Add(new ToolStripSeparator());
        
        // 收藏夹
        var bookmarks = CreateDarkMenuItem("收藏夹(B)", null, DarkMenuIconDrawer.DrawBookmark);
        bookmarks.DropDownDirection = ToolStripDropDownDirection.Left;
        bookmarks.DropDown.Renderer = new DarkMenuRenderer();
        bookmarks.DropDown.BackColor = Color.FromArgb(45, 45, 48);
        
        var showBar = new ToolStripMenuItem("显示收藏栏(S)") { ShortcutKeyDisplayString = "Ctrl+Shift+B", Checked = _bookmarkBar.Visible, ForeColor = DarkText };
        showBar.Click += (s, e) => 
        { 
            _bookmarkBar.Visible = !_bookmarkBar.Visible;
            _settingsService.Settings.AlwaysShowBookmarkBar = _bookmarkBar.Visible;
            _settingsService.Save();
            showBar.Checked = _bookmarkBar.Visible; 
        };
        bookmarks.DropDownItems.Add(showBar);
        
        var bookmarkManager = new ToolStripMenuItem("收藏夹管理器(B)") { ShortcutKeyDisplayString = "Ctrl+Shift+O", ForeColor = DarkText };
        bookmarkManager.Click += (s, e) => { CloseMainMenu(); ShowBookmarkManager(); };
        bookmarks.DropDownItems.Add(bookmarkManager);
        
        // 导入收藏和设置
        var importBookmarks = new ToolStripMenuItem("导入收藏和设置...") { ForeColor = DarkText };
        importBookmarks.Click += (s, e) => { CloseMainMenu(); ImportBookmarks(); };
        bookmarks.DropDownItems.Add(importBookmarks);
        
        bookmarks.DropDownItems.Add(new ToolStripSeparator());
        
        var addBm = new ToolStripMenuItem("为此网页添加收藏...") { ShortcutKeyDisplayString = "Ctrl+D", ForeColor = DarkText };
        addBm.Click += (s, e) => { CloseMainMenu(); ToggleBookmark(); };
        bookmarks.DropDownItems.Add(addBm);
        
        var addAllBm = new ToolStripMenuItem("为打开的网页添加收藏...") { ShortcutKeyDisplayString = "Ctrl+Shift+D", ForeColor = DarkText };
        addAllBm.Click += (s, e) => { CloseMainMenu(); OnBookmarkAllTabsRequested(); };
        bookmarks.DropDownItems.Add(addAllBm);
        
        bookmarks.DropDownItems.Add(new ToolStripSeparator());
        
        var barItems = _bookmarkService.GetBookmarkBarItems();
        if (barItems.Count > 0)
        {
            foreach (var item in barItems.Take(15))
            {
                var bmItem = new ToolStripMenuItem(item.IsFolder ? "📁 " + item.Title : item.Title) { ForeColor = DarkText };
                if (item.IsFolder)
                {
                    AddBookmarkFolderItems(bmItem, item.Id);
                }
                else
                {
                    bmItem.Image = Helpers.FaviconHelper.GetCachedFavicon(item.Url);
                    LoadMenuItemFaviconAsync(bmItem, item.Url, item.FaviconUrl);
                    var itemUrl = item.Url;
                    bmItem.Click += (s, e) => { CloseMainMenu(); _tabManager.ActiveTab?.Navigate(itemUrl); };
                }
                bookmarks.DropDownItems.Add(bmItem);
            }
            if (barItems.Count > 15)
            {
                bookmarks.DropDownItems.Add(new ToolStripSeparator());
                var moreBookmarks = new ToolStripMenuItem($"更多收藏 ({barItems.Count - 15})...") { ForeColor = DarkText };
                moreBookmarks.Click += (s, e) => { CloseMainMenu(); ShowBookmarkManager(); };
                bookmarks.DropDownItems.Add(moreBookmarks);
            }
        }
        else
        {
            var emptyItem = new ToolStripMenuItem("暂无收藏") { Enabled = false, ForeColor = DarkSecondaryText };
            bookmarks.DropDownItems.Add(emptyItem);
        }
        menu.Items.Add(bookmarks);
        
        // 历史记录
        var history = CreateDarkMenuItem("历史记录(H)", "Ctrl+H", DarkMenuIconDrawer.DrawHistory);
        history.DropDownDirection = ToolStripDropDownDirection.Left;
        history.DropDown.Renderer = new DarkMenuRenderer();
        history.DropDown.BackColor = Color.FromArgb(45, 45, 48);
        
        var historyNote = new ToolStripMenuItem("InPrivate 模式不记录历史") { Enabled = false, ForeColor = DarkSecondaryText };
        history.DropDownItems.Add(historyNote);
        
        if (_mainHistoryService != null)
        {
            history.DropDownItems.Add(new ToolStripSeparator());
            var recentHistory = _mainHistoryService.GetHistory(10);
            if (recentHistory.Count > 0)
            {
                var historyLabel = new ToolStripMenuItem("最近访问（来自主窗口）") { Enabled = false, ForeColor = DarkSecondaryText };
                history.DropDownItems.Add(historyLabel);
                foreach (var item in recentHistory)
                {
                    var title = string.IsNullOrEmpty(item.Title) ? item.Url : item.Title;
                    if (title.Length > 40) title = title[..40] + "...";
                    var historyItem = new ToolStripMenuItem(title) { ForeColor = DarkText };
                    historyItem.Image = Helpers.FaviconHelper.GetCachedFavicon(item.Url);
                    LoadMenuItemFaviconAsync(historyItem, item.Url);
                    var url = item.Url;
                    historyItem.Click += (s, e) => { CloseMainMenu(); _tabManager.ActiveTab?.Navigate(url); };
                    history.DropDownItems.Add(historyItem);
                }
            }
        }
        menu.Items.Add(history);
        
        // 下载
        menu.Items.Add(CreateDarkMenuItem("下载(D)", "Ctrl+J", DarkMenuIconDrawer.DrawDownload,
            () => { CloseMainMenu(); OpenDownloadDialog(); }));
        
        menu.Items.Add(new ToolStripSeparator());
        
        // 删除浏览数据
        menu.Items.Add(CreateDarkMenuItem("删除浏览数据", "Ctrl+Shift+Delete", DarkMenuIconDrawer.DrawClear,
            () => { CloseMainMenu(); ShowClearBrowsingDataDialog(); }));
        
        // 打印
        menu.Items.Add(CreateDarkMenuItem("打印(P)", "Ctrl+P", DarkMenuIconDrawer.DrawPrint,
            () => { CloseMainMenu(); PrintPage(); }));
        
        menu.Items.Add(new ToolStripSeparator());
        
        // 网页另存为
        menu.Items.Add(CreateDarkMenuItem("网页另存为(A)...", "Ctrl+S", DarkMenuIconDrawer.DrawSave,
            () => { CloseMainMenu(); SavePageAs(); }));
        
        // 在页面上查找
        menu.Items.Add(CreateDarkMenuItem("在页面上查找", "Ctrl+F", DarkMenuIconDrawer.DrawFind,
            () => { CloseMainMenu(); OpenFindInPage(); }));

        // 更多工具
        var tools = CreateDarkMenuItem("更多工具", null, DarkMenuIconDrawer.DrawTools);
        tools.DropDownDirection = ToolStripDropDownDirection.Left;
        tools.DropDown.Renderer = new DarkMenuRenderer();
        tools.DropDown.BackColor = Color.FromArgb(45, 45, 48);
        
        var taskManager = new ToolStripMenuItem("任务管理器(T)") { ShortcutKeyDisplayString = "Shift+Esc", ForeColor = DarkText };
        taskManager.Click += (s, e) => { CloseMainMenu(); ShowTaskManager(); };
        tools.DropDownItems.Add(taskManager);
        
        var encoding = new ToolStripMenuItem("编码(E)") { ForeColor = DarkText };
        encoding.DropDownDirection = ToolStripDropDownDirection.Left;
        encoding.DropDown.BackColor = Color.FromArgb(45, 45, 48);
        encoding.DropDown.Renderer = new DarkMenuRenderer();
        var encAuto = new ToolStripMenuItem("自动检测") { Checked = true, ForeColor = DarkText };
        encAuto.Click += (s, e) => { CloseMainMenu(); SetEncoding("auto"); };
        encoding.DropDownItems.Add(encAuto);
        encoding.DropDownItems.Add(new ToolStripSeparator());
        foreach (var (name, code) in new[] { ("Unicode (UTF-8)", "UTF-8"), ("简体中文 (GBK)", "GBK"), ("简体中文 (GB2312)", "GB2312"), ("繁体中文 (Big5)", "Big5"), ("日语 (Shift_JIS)", "Shift_JIS"), ("韩语 (EUC-KR)", "EUC-KR") })
        {
            var enc = new ToolStripMenuItem(name) { ForeColor = DarkText };
            var encCode = code;
            enc.Click += (s, e) => { CloseMainMenu(); SetEncoding(encCode); };
            encoding.DropDownItems.Add(enc);
        }
        tools.DropDownItems.Add(encoding);
        tools.DropDownItems.Add(new ToolStripSeparator());
        
        var devTools = new ToolStripMenuItem("开发者工具(D)") { ShortcutKeyDisplayString = "F12", ForeColor = DarkText };
        devTools.Click += (s, e) => { CloseMainMenu(); OpenDevTools(); };
        tools.DropDownItems.Add(devTools);
        menu.Items.Add(tools);
        
        menu.Items.Add(new ToolStripSeparator());
        
        // 广告过滤
        var adBlock = CreateDarkMenuItem("广告过滤(G)", null, _adBlockService.Enabled ? DarkMenuIconDrawer.DrawAdBlockEnabled : DarkMenuIconDrawer.DrawAdBlock);
        adBlock.Checked = _adBlockService.Enabled;
        adBlock.Click += (s, e) => 
        { 
            _adBlockService.Enabled = !_adBlockService.Enabled; 
            _settingsService.Settings.EnableAdBlock = _adBlockService.Enabled; 
            _settingsService.Save();
            adBlock.Checked = _adBlockService.Enabled;
            // 更新图标
            var iconBitmap = new Bitmap(20, 20);
            using (var g = Graphics.FromImage(iconBitmap))
            {
                g.Clear(Color.Transparent);
                if (_adBlockService.Enabled)
                    DarkMenuIconDrawer.DrawAdBlockEnabled(g, new Rectangle(0, 0, 20, 20));
                else
                    DarkMenuIconDrawer.DrawAdBlock(g, new Rectangle(0, 0, 20, 20));
            }
            adBlock.Image = iconBitmap;
        };
        menu.Items.Add(adBlock);
        
        menu.Items.Add(new ToolStripSeparator());
        
        // 设置
        menu.Items.Add(CreateDarkMenuItem("设置(S)", null, DarkMenuIconDrawer.DrawSettings,
            () => { CloseMainMenu(); ShowSettings(); }));
        
        // 关于 InPrivate
        menu.Items.Add(CreateDarkMenuItem("关于鲲穹AI浏览器", null, DarkMenuIconDrawer.DrawAbout,
            () => { CloseMainMenu(); MessageBox.Show(
                "鲲穹AI浏览器 (InPrivate 模式)\n版本 1.0\n\n基于 WebView2 内核\n\nInPrivate 浏览不会保存您的浏览历史记录。",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information); }));
        
        menu.Items.Add(new ToolStripSeparator());
        
        // 退出
        var exit = new ToolStripMenuItem("关闭鲲穹AI浏览器") { Padding = new Padding(8, 6, 8, 6), ForeColor = DarkText };
        exit.Click += (s, e) => { CloseMainMenu(); Close(); };
        menu.Items.Add(exit);
        
        menu.Show(_settingsBtn, new Point(_settingsBtn.Width - menu.Width, _settingsBtn.Height));
        StartMenuCloseTimer();
    }
    
    private void AddBookmarkFolderItems(ToolStripMenuItem parent, string folderId)
    {
        var children = _bookmarkService.GetChildren(folderId);
        foreach (var child in children)
        {
            var item = new ToolStripMenuItem(child.IsFolder ? "📁 " + child.Title : child.Title) { ForeColor = DarkText };
            if (child.IsFolder)
            {
                AddBookmarkFolderItems(item, child.Id);
            }
            else
            {
                var childUrl = child.Url;
                item.Click += (s, e) => { CloseMainMenu(); _tabManager.ActiveTab?.Navigate(childUrl); };
            }
            parent.DropDownItems.Add(item);
        }
    }
    
    private ToolStripMenuItem CreateDarkMenuItem(string text, string? shortcut, Action<Graphics, Rectangle>? iconDrawer, Action? onClick = null)
    {
        var item = new ToolStripMenuItem(text)
        {
            ShortcutKeyDisplayString = shortcut,
            Padding = new Padding(8, 6, 8, 6),
            ForeColor = DarkText
        };
        
        if (iconDrawer != null)
        {
            // 创建图标图像（深色主题使用浅色图标）
            var iconBitmap = new Bitmap(20, 20);
            using (var g = Graphics.FromImage(iconBitmap))
            {
                g.Clear(Color.Transparent);
                iconDrawer(g, new Rectangle(0, 0, 20, 20));
            }
            item.Image = iconBitmap;
            item.ImageScaling = ToolStripItemImageScaling.None;
        }
        
        if (onClick != null)
            item.Click += (s, e) => onClick();
        return item;
    }
    
    private ToolStripMenuItem CreateDarkMenuItemSimple(string text, string? shortcut, Action? onClick = null)
    {
        var item = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = shortcut, Padding = new Padding(8, 6, 8, 6), ForeColor = DarkText };
        if (onClick != null) item.Click += (s, e) => onClick();
        return item;
    }
    
    private ToolStripControlHost CreateZoomMenuItem()
    {
        return new ToolStripControlHost(CreateZoomPanel()) { AutoSize = false, Size = new Size(280, 36) };
    }

    private Panel CreateZoomPanel()
    {
        _zoomPanel = new Panel { Size = new Size(280, 34), BackColor = Color.Transparent };

        // 缩放图标
        var iconPanel = new Panel
        {
            Size = new Size(20, 20),
            Location = new Point(12, 7),
            BackColor = Color.Transparent
        };
        iconPanel.Paint += (s, e) => DarkMenuIconDrawer.DrawZoom(e.Graphics, new Rectangle(0, 0, 20, 20));

        var lblZoom = new Label
        {
            Text = "缩放",
            Location = new Point(40, 9),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = DarkText
        };

        var btnMinus = CreateZoomButton("—", new Point(120, 5), new Size(32, 24), () => { ZoomOutFromMenu(); });

        _zoomLevelLabel = new Label
        {
            Text = $"{(int)(_zoomLevel * 100)}%",
            Size = new Size(50, 24),
            Location = new Point(154, 7),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = DarkText
        };

        var btnPlus = CreateZoomButton("+", new Point(206, 5), new Size(32, 24), () => { ZoomInFromMenu(); });

        var btnFullscreen = CreateZoomButton("⛶", new Point(244, 5), new Size(28, 24), () =>
        {
            _reopenMenuAfterZoom = false;  // 确保不会重新打开菜单
            CloseMainMenu();
            _fullscreenManager.Toggle();
        }, keepMenuOpen: false);
        btnFullscreen.Font = new Font("Segoe UI Symbol", 11F);

        _zoomPanel.Controls.AddRange(new Control[] { iconPanel, lblZoom, btnMinus, _zoomLevelLabel, btnPlus, btnFullscreen });
        return _zoomPanel;
    }

    private Label CreateZoomButton(string text, Point location, Size size, Action? onClick = null, bool keepMenuOpen = true)
    {
        var btn = new Label
        {
            Text = text,
            Size = size,
            Location = location,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10F),
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            ForeColor = DarkText
        };

        btn.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
            using var pen = new Pen(DarkBorder);
            using var path = CreateRoundedRect(rect, 4);
            g.DrawPath(pen, path);
        };

        btn.MouseEnter += (s, e) => btn.BackColor = DarkHover;
        btn.MouseLeave += (s, e) => btn.BackColor = Color.Transparent;
        btn.MouseDown += (s, e) =>
        {
            btn.BackColor = Color.FromArgb(90, 90, 90);
            // 如果需要保持菜单打开，设置重新打开标志
            if (keepMenuOpen)
            {
                _reopenMenuAfterZoom = true;
                // 立即执行操作
                onClick?.Invoke();
            }
        };
        btn.MouseUp += (s, e) =>
        {
            btn.BackColor = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position))
                ? DarkHover : Color.Transparent;
            // 如果不需要保持菜单打开，在MouseUp时执行操作
            if (!keepMenuOpen && btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position)))
                onClick?.Invoke();
        };

        return btn;
    }
    
    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    
    private void UpdateZoomLabel() { if (_zoomLevelLabel != null) _zoomLevelLabel.Text = $"{(int)(_zoomLevel * 100)}%"; }
    private void ZoomIn() { if (_zoomLevel < 3.0) { _zoomLevel += 0.1; ApplyZoom(); ShowZoomPopup(); } }
    private void ZoomOut() { if (_zoomLevel > 0.25) { _zoomLevel -= 0.1; ApplyZoom(); ShowZoomPopup(); } }
    private void ZoomInFromMenu() { if (_zoomLevel < 3.0) { _zoomLevel += 0.1; ApplyZoom(); UpdateZoomLabel(); UpdateZoomButtonVisibility(); } }
    private void ZoomOutFromMenu() { if (_zoomLevel > 0.25) { _zoomLevel -= 0.1; ApplyZoom(); UpdateZoomLabel(); UpdateZoomButtonVisibility(); } }
    private void ResetZoom() { _zoomLevel = 1.0; ApplyZoom(); UpdateZoomLabel(); ShowZoomPopup(); }
    private void ApplyZoom() { if (_tabManager.ActiveTab?.WebView?.CoreWebView2 != null) _tabManager.ActiveTab.WebView.ZoomFactor = _zoomLevel; }
    
    private void UpdateZoomButtonVisibility()
    {
        // 缩放不是100%时显示放大镜按钮
        var isNotDefault = Math.Abs(_zoomLevel - 1.0) > 0.01;
        if (_zoomBtn != null)
            _zoomBtn.Visible = isNotDefault;
    }
    
    private void ShowZoomPopup()
    {
        // 更新菜单栏中的缩放比例
        UpdateZoomLabel();
        
        // 显示/隐藏放大镜按钮
        UpdateZoomButtonVisibility();
        
        // 创建或更新缩放弹窗
        if (_zoomPopup == null)
        {
            _zoomPopup = new Panel
            {
                Size = new Size(160, 70),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            _zoomPopupLabel = new Label
            {
                Text = $"缩放：{(int)(_zoomLevel * 100)}%",
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = Color.Black,
                Location = new Point(10, 10),
                AutoSize = true
            };
            _zoomPopup.Controls.Add(_zoomPopupLabel);
            
            var resetBtn = new Button
            {
                Text = "重置为默认设置",
                Font = new Font("Microsoft YaHei UI", 9F),
                Location = new Point(10, 35),
                Size = new Size(140, 28),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            resetBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            resetBtn.Click += (s, e) => { ResetZoom(); HideZoomPopup(); };
            _zoomPopup.Controls.Add(resetBtn);
            
            Controls.Add(_zoomPopup);
            _zoomPopup.BringToFront();
        }
        
        // 更新标签文本
        if (_zoomPopupLabel != null)
            _zoomPopupLabel.Text = $"缩放：{(int)(_zoomLevel * 100)}%";
        
        // 定位到放大镜按钮下方
        Control anchorBtn = _zoomBtn.Visible ? _zoomBtn : _downloadBtn;
        var btnScreenPos = anchorBtn.PointToScreen(Point.Empty);
        var formPos = PointToClient(btnScreenPos);
        var x = formPos.X + anchorBtn.Width - _zoomPopup.Width;
        var y = formPos.Y + anchorBtn.Height + 2;
        _zoomPopup.Location = new Point(x, y);
        _zoomPopup.Visible = true;
        
        // 设置自动隐藏定时器
        _zoomPopupTimer?.Stop();
        _zoomPopupTimer?.Dispose();
        _zoomPopupTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _zoomPopupTimer.Tick += (s, e) => HideZoomPopup();
        _zoomPopupTimer.Start();
    }
    
    private void HideZoomPopup()
    {
        _zoomPopupTimer?.Stop();
        _zoomPopupTimer?.Dispose();
        _zoomPopupTimer = null;
        
        if (_zoomPopup != null)
            _zoomPopup.Visible = false;
    }
    
    private void OnTabZoomChanged(IncognitoTab tab, double zoomFactor)
    {
        // 更新内部缩放级别
        _zoomLevel = zoomFactor;
        
        // 更新菜单栏中的缩放比例
        UpdateZoomLabel();
        
        // 更新放大镜按钮可见性
        UpdateZoomButtonVisibility();
        
        // 显示缩放弹窗
        ShowZoomPopup();
    }
    
    private void OnSettingChanged(string key, object value)
    {
        // 在 UI 线程上执行
        if (InvokeRequired)
        {
            BeginInvoke(() => OnSettingChanged(key, value));
            return;
        }
        
        switch (key)
        {
            case "hidebookmarkbar":
                // 隐藏收藏栏：value 为 true 时隐藏，false 时显示
                _bookmarkBar.Visible = !(bool)value;
                break;
            case "bookmarkbar":
                // 显示收藏栏：value 为 true 时显示，false 时隐藏
                _bookmarkBar.Visible = (bool)value;
                _settingsService.Settings.AlwaysShowBookmarkBar = (bool)value;
                _settingsService.Save();
                break;
            case "homebutton":
                // 显示主页按钮
                _homeBtn.Visible = (bool)value;
                break;
        }
    }
    
    #endregion

    #region 书签操作
    
    private void ToggleBookmark()
    {
        ShowAddBookmarkDialog();
    }
    
    private void ShowAddBookmarkDialog()
    {
        var url = _tabManager.ActiveTab?.Url;
        var title = _tabManager.ActiveTab?.Title ?? "新书签";

        if (string.IsNullOrEmpty(url) || url.StartsWith("about:")) 
        {
            MessageBox.Show("无法收藏此页面", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var existing = _bookmarkService.FindByUrl(url);
        
        using var dialog = new AddBookmarkDialog(
            _bookmarkService, 
            title, 
            url, 
            _tabManager.ActiveTab?.FaviconUrl,
            existing);
        
        var btnLocation = _bookmarkBtn.PointToScreen(new Point(_bookmarkBtn.Width, _bookmarkBtn.Height));
        dialog.SetAnchorPoint(btnLocation);
        
        var result = dialog.ShowDialog(this);
        
        UpdateBookmarkButton(result != DialogResult.Abort);
        
        if (result == DialogResult.Abort)
            _statusLabel.Text = "已取消收藏";
    }
    
    #endregion
    
    #region 登录相关
    
    private void RefreshLoginStatus()
    {
        if (InvokeRequired)
        {
            BeginInvoke(RefreshLoginStatus);
            return;
        }

        _userBtn.UserInfo = _loginService.CurrentUser;
        
        if (_loginService.IsLoggedIn)
        {
            new ToolTip().SetToolTip(_userBtn, $"已登录: {_loginService.CurrentUser?.Nickname}");
        }
        else
        {
            new ToolTip().SetToolTip(_userBtn, "登录/用户信息");
        }
    }

    private void OnUserButtonClick(object? sender, EventArgs e)
    {
        _userBtn.Focus();

        // 如果弹窗可见，则关闭它
        if (_userInfoPopup != null && !_userInfoPopup.IsDisposed && _userInfoPopup.Visible)
        {
            CloseUserInfoPopup();
            return;
        }

        // 冷却时间检查：防止点击头像关闭弹窗时，由于 Deactivate 先触发关闭，导致此处又立即打开
        if ((DateTime.Now - _lastUserInfoPopupCloseTime).TotalMilliseconds < 200)
        {
            return;
        }

        _suppressUserInfoPopupClose = true;
        _userInfoPopup = new UserInfoPopup(_loginService, StartLoginFlow, HandleLogout);
        
        // 计算弹出位置（在按钮下方对齐）
        var screenPos = _userBtn.PointToScreen(new Point(0, _userBtn.Height));
        _userInfoPopup.Location = new Point(screenPos.X - (_userInfoPopup.Width - _userBtn.Width) / 2, screenPos.Y + 5);
        
        _userInfoPopup.FormClosed += (s, ev) => _userInfoPopup = null;
        _userInfoPopup.Show(this);

        BeginInvoke(() => _suppressUserInfoPopupClose = false);
    }

    private void CloseUserInfoPopup()
    {
        if (_userInfoPopup != null && !_userInfoPopup.IsDisposed)
        {
            _userInfoPopup.Close();
            _userInfoPopup = null;
            _lastUserInfoPopupCloseTime = DateTime.Now;
        }
    }

    private CancellationTokenSource? _loginCts;

    private async void StartLoginFlow()
    {
        try
        {
            _loginCts = new CancellationTokenSource();
            
            // 1. 准备登录（生成 Nonce 并获取 URL）
            var (loginUrl, encodedNonce) = await _loginService.PrepareLoginAsync();

            // 2. 在应用内浏览器标签页打开
            await _tabManager.CreateTabAsync(loginUrl);

            // 3. 开始轮询
            var token = await _loginService.PollTokenAsync(encodedNonce, _loginCts.Token);

            if (token != null)
            {
                // 登录成功
                RefreshLoginStatus();
            }
        }
        catch (OperationCanceledException)
        {
            // 用户取消
        }
        catch (Exception ex)
        {
            MessageBox.Show($"登录失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _loginCts?.Dispose();
            _loginCts = null;
        }
    }

    private async void HandleLogout()
    {
        if (MessageBox.Show("确定要退出登录吗？", "确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
        {
            try
            {
                await _loginService.LogoutAsync();
                RefreshLoginStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"退出登录失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    #endregion
    
    #region 功能方法
    
    private async void SavePageAs()
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null) { MessageBox.Show("没有可保存的网页", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var webView = _tabManager.ActiveTab.WebView.CoreWebView2;
        var pageTitle = webView.DocumentTitle ?? "网页";
        var safeTitle = string.Join("_", pageTitle.Split(Path.GetInvalidFileNameChars()));
        if (safeTitle.Length > 50) safeTitle = safeTitle[..50];
        using var saveDialog = new SaveFileDialog { Title = "网页另存为", FileName = safeTitle, Filter = "网页，仅HTML (*.html)|*.html|MHTML文件 (*.mhtml)|*.mhtml|PDF文档 (*.pdf)|*.pdf", FilterIndex = 1, DefaultExt = "html", AddExtension = true };
        if (saveDialog.ShowDialog() != DialogResult.OK) return;
        var filePath = saveDialog.FileName;
        var filterIndex = saveDialog.FilterIndex;
        var pageSaveService = new PageSaveService();
        try
        {
            _statusLabel.Text = "正在保存网页..."; _progressBar.Visible = true;
            switch (filterIndex) { case 1: await pageSaveService.SaveAsHtmlOnlyAsync(webView, filePath); break; case 2: await pageSaveService.SaveAsMhtmlAsync(webView, filePath); break; case 3: await pageSaveService.SaveAsPdfAsync(webView, filePath); break; }
            _statusLabel.Text = "保存完成";
            MessageBox.Show($"网页已保存到:\n{filePath}", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { _statusLabel.Text = "保存失败"; MessageBox.Show($"保存网页时出错:\n{ex.Message}", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { _progressBar.Visible = false; }
    }
    
    private void OpenFindInPage() { if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null) return; try { _tabManager.ActiveTab.WebView.Focus(); SendKeys.Send("^f"); } catch { } }
    
    private async void PrintPage()
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null) { MessageBox.Show("没有可打印的网页", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try { _statusLabel.Text = "正在准备打印..."; await _tabManager.ActiveTab.WebView.CoreWebView2.ExecuteScriptAsync("window.print()"); _statusLabel.Text = "InPrivate - 您的浏览活动不会保存到此设备"; }
        catch (Exception ex) { _statusLabel.Text = "打印失败"; MessageBox.Show($"打印时出错:\n{ex.Message}", "打印失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    
    private async void SetEncoding(string encoding)
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null) return;
        try { if (encoding == "auto") _tabManager.ActiveTab.Refresh(); else { await _tabManager.ActiveTab.WebView.CoreWebView2.ExecuteScriptAsync($"document.charset = '{encoding}';"); _tabManager.ActiveTab.Refresh(); } _statusLabel.Text = $"编码已设置为: {encoding}"; } catch { }
    }
    
    private void OpenDevTools() { _tabManager.ActiveTab?.WebView?.CoreWebView2?.OpenDevToolsWindow(); }
    
    private void ShowBookmarkManager()
    {
        using var manager = new BookmarkManagerForm(_bookmarkService, url => _tabManager.ActiveTab?.Navigate(url));
        manager.ShowDialog(this);
    }
    
    private void ImportBookmarks()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入收藏",
            Filter = "HTML 文件 (*.html;*.htm)|*.html;*.htm|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var content = File.ReadAllText(dialog.FileName);
            var importedCount = ImportBookmarksFromHtml(content);
            
            _statusLabel.Text = $"已导入 {importedCount} 个收藏";
            MessageBox.Show($"成功导入 {importedCount} 个收藏", "导入完成", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    private int ImportBookmarksFromHtml(string html)
    {
        var count = 0;
        var regex = new System.Text.RegularExpressions.Regex(
            @"<A\s+HREF=""([^""]+)""[^>]*>([^<]+)</A>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var matches = regex.Matches(html);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var url = match.Groups[1].Value;
            var title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value);
            if (_bookmarkService.FindByUrl(url) != null) continue;
            if (url.StartsWith("javascript:") || url.StartsWith("place:")) continue;
            _bookmarkService.AddBookmark(title, url);
            count++;
        }
        return count;
    }
    
    private void ShowClearBrowsingDataDialog()
    {
        using var dialog = new ClearBrowsingDataDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _tabManager.ActiveTab?.Refresh();
            _statusLabel.Text = "浏览数据已清除";
        }
    }
    
    private async void LoadMenuItemFaviconAsync(ToolStripMenuItem menuItem, string url, string? faviconUrl = null)
    {
        try
        {
            var icon = await Helpers.FaviconHelper.GetFaviconAsync(url, faviconUrl);
            if (icon != null && !menuItem.IsDisposed)
            {
                BeginInvoke(() => menuItem.Image = icon);
            }
        }
        catch { }
    }
    
    private void ShowTaskManager()
    {
        try
        {
            _tabManager.ActiveTab?.WebView?.CoreWebView2?.OpenTaskManagerWindow();
        }
        catch { }
    }
    
    private void ShowSettings()
    {
        _ = _tabManager.CreateTabAsync("about:settings");
    }
    
    #endregion

    #region 密码功能
    
    private void OnPasswordKeyButtonRequested(string host, string username, string password)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnPasswordKeyButtonRequested(host, username, password));
            return;
        }
        
        _pendingPasswordHost = host;
        _pendingPasswordUsername = username;
        _pendingPasswordPassword = password;
        
        ShowPasswordKeyButton();
    }
    
    private void ShowPasswordKeyButton()
    {
        if (_passwordKeyBtn != null)
        {
            _passwordKeyBtn.Visible = true;
            return;
        }
        
        _passwordKeyBtn = new RoundedButton
        {
            Size = new Size(32, 32),
            Text = "🔑",
            Font = new Font("Segoe UI Emoji", 11F),
            Margin = new Padding(2),
            ForeColor = DarkText,
            HoverBackColor = DarkHover
        };
        new ToolTip().SetToolTip(_passwordKeyBtn, "保存密码");
        
        _passwordKeyBtn.Click += OnPasswordKeyButtonClick;
        
        // 添加到工具栏右侧面板
        var menuPanel = _toolbar.Controls.OfType<Panel>().FirstOrDefault()?.Controls.OfType<FlowLayoutPanel>()
            .FirstOrDefault(p => p.Dock == DockStyle.Right);
        if (menuPanel != null)
        {
            menuPanel.Controls.Add(_passwordKeyBtn);
            menuPanel.Controls.SetChildIndex(_passwordKeyBtn, 0);
        }
    }
    
    private void HidePasswordKeyButton()
    {
        if (_passwordKeyBtn != null)
        {
            _passwordKeyBtn.Visible = false;
        }
        _pendingPasswordHost = null;
        _pendingPasswordUsername = null;
        _pendingPasswordPassword = null;
    }
    
    private void OnPasswordKeyButtonClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_pendingPasswordHost) || _passwordKeyBtn == null) return;
        
        _tabManager.ShowPasswordPopup(
            _pendingPasswordHost,
            _pendingPasswordUsername ?? "",
            _pendingPasswordPassword ?? "",
            _passwordKeyBtn,
            new Point(_passwordKeyBtn.Width / 2, _passwordKeyBtn.Height),
            false,
            (saved, neverSave) =>
            {
                if (saved || neverSave)
                {
                    HidePasswordKeyButton();
                }
            });
    }
    
    private void OnBookmarkAllTabsRequested()
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnBookmarkAllTabsRequested);
            return;
        }
        
        var tabs = _tabManager.Tabs.Where(t => !string.IsNullOrEmpty(t.Url) && !t.Url.StartsWith("about:")).ToList();
        if (tabs.Count == 0)
        {
            MessageBox.Show("没有可收藏的标签页", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        
        var folderName = $"已收藏的标签页 ({DateTime.Now:yyyy-MM-dd HH:mm})";
        var folder = _bookmarkService.AddFolder(folderName);
        
        foreach (var tab in tabs)
        {
            _bookmarkService.AddBookmark(tab.Title ?? tab.Url!, tab.Url!, folder.Id, tab.FaviconUrl);
        }
        
        _bookmarkBar.Refresh();
        MessageBox.Show($"已将 {tabs.Count} 个标签页添加到收藏夹", "收藏成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    #endregion
    
    #region 窗口关闭和清理
    
    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _tabManager?.Dispose();
        Task.Run(async () => { await Task.Delay(1000); try { if (Directory.Exists(_incognitoDataFolder)) Directory.Delete(_incognitoDataFolder, true); } catch { } });
    }
    
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32Constants.WM_NCHITTEST)
        {
            // 最大化或全屏时，禁止边框调整大小，直接返回 HTCLIENT
            if (WindowState == FormWindowState.Maximized || (_fullscreenManager != null && _fullscreenManager.IsFullscreen))
            {
                m.Result = (IntPtr)Win32Constants.HTCLIENT;
                return;
            }

            if (WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                var result = Win32Helper.HandleResizeHitTest(this, Cursor.Position);
                if (result != IntPtr.Zero) { m.Result = result; return; }
                return;
            }
        }
        base.WndProc(ref m);
    }
    
    #endregion
}

/// <summary>
/// 深色菜单图标绘制器 - 绘制浅色图标用于深色主题
/// </summary>
public static class DarkMenuIconDrawer
{
    private static readonly Color IconColor = Color.FromArgb(200, 200, 200);

    public static void DrawNewTab(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawRectangle(pen, cx - 6, cy - 4, 12, 8);
        g.DrawLine(pen, cx - 6, cy - 1, cx + 6, cy - 1);
    }

    public static void DrawNewWindow(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawRectangle(pen, cx - 6, cy - 5, 12, 10);
        g.DrawLine(pen, cx - 6, cy - 2, cx + 6, cy - 2);
    }

    public static void DrawIncognito(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawEllipse(pen, cx - 7, cy - 3, 6, 6);
        g.DrawEllipse(pen, cx + 1, cy - 3, 6, 6);
        g.DrawLine(pen, cx - 1, cy, cx + 1, cy);
        g.DrawArc(pen, cx - 8, cy - 8, 16, 10, 180, 180);
    }

    public static void DrawZoom(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawEllipse(pen, cx - 5, cy - 5, 8, 8);
        g.DrawLine(pen, cx + 2, cy + 2, cx + 6, cy + 6);
        g.DrawLine(pen, cx - 3, cy - 1, cx + 1, cy - 1);
        g.DrawLine(pen, cx - 1, cy - 3, cx - 1, cy + 1);
    }

    public static void DrawBookmark(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        var points = new PointF[5];
        for (int i = 0; i < 5; i++)
        {
            double angle = -Math.PI / 2 + i * 2 * Math.PI / 5;
            points[i] = new PointF(cx + 6 * (float)Math.Cos(angle), cy + 6 * (float)Math.Sin(angle));
        }
        g.DrawLine(pen, points[0], points[2]);
        g.DrawLine(pen, points[2], points[4]);
        g.DrawLine(pen, points[4], points[1]);
        g.DrawLine(pen, points[1], points[3]);
        g.DrawLine(pen, points[3], points[0]);
    }

    public static void DrawHistory(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
        g.DrawLine(pen, cx, cy - 4, cx, cy);
        g.DrawLine(pen, cx, cy, cx + 3, cy + 2);
    }

    public static void DrawDownload(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawLine(pen, cx, cy - 5, cx, cy + 2);
        g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
        g.DrawLine(pen, cx + 4, cy - 2, cx, cy + 2);
        g.DrawLine(pen, cx - 6, cy + 5, cx + 6, cy + 5);
    }

    public static void DrawClear(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawRectangle(pen, cx - 5, cy - 3, 10, 9);
        g.DrawLine(pen, cx - 3, cy - 6, cx + 3, cy - 6);
        g.DrawLine(pen, cx - 3, cy - 6, cx - 3, cy - 3);
        g.DrawLine(pen, cx + 3, cy - 6, cx + 3, cy - 3);
        g.DrawLine(pen, cx - 2, cy, cx - 2, cy + 4);
        g.DrawLine(pen, cx + 2, cy, cx + 2, cy + 4);
    }

    public static void DrawPrint(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawRectangle(pen, cx - 6, cy - 2, 12, 6);
        g.DrawRectangle(pen, cx - 4, cy - 6, 8, 4);
        g.DrawRectangle(pen, cx - 4, cy + 2, 8, 4);
    }

    public static void DrawSave(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawRectangle(pen, cx - 6, cy - 6, 12, 12);
        g.DrawRectangle(pen, cx - 3, cy - 6, 6, 4);
        g.DrawRectangle(pen, cx - 4, cy + 1, 8, 5);
    }

    public static void DrawFind(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawEllipse(pen, cx - 5, cy - 5, 8, 8);
        g.DrawLine(pen, cx + 2, cy + 2, cx + 6, cy + 6);
    }

    public static void DrawTools(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
        g.DrawLine(pen, cx - 5, cy - 5, cx - 2, cy - 5);
        g.DrawLine(pen, cx - 5, cy - 5, cx - 5, cy - 2);
        g.DrawEllipse(pen, cx + 1, cy + 1, 6, 6);
    }

    public static void DrawAdBlock(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
        g.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
    }

    public static void DrawAdBlockEnabled(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(100, 200, 100), 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
        g.DrawLine(pen, cx - 3, cy, cx - 1, cy + 3);
        g.DrawLine(pen, cx - 1, cy + 3, cx + 4, cy - 3);
    }

    public static void DrawSettings(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawEllipse(pen, cx - 3, cy - 3, 6, 6);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            var x1 = cx + 5 * (float)Math.Cos(angle);
            var y1 = cy + 5 * (float)Math.Sin(angle);
            var x2 = cx + 7 * (float)Math.Cos(angle);
            var y2 = cy + 7 * (float)Math.Sin(angle);
            g.DrawLine(pen, x1, y1, x2, y2);
        }
    }

    public static void DrawAbout(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
        g.DrawLine(pen, cx, cy - 2, cx, cy + 2);
        g.DrawEllipse(pen, cx - 1, cy + 4, 2, 2);
    }
}

/// <summary>
/// 深色菜单渲染器
/// </summary>
public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color DarkBg = Color.FromArgb(45, 45, 48);
    private static readonly Color DarkHv = Color.FromArgb(62, 62, 66);
    private static readonly Color DarkBd = Color.FromArgb(60, 60, 60);
    private static readonly Color DarkTx = Color.FromArgb(200, 200, 200);
    
    public DarkMenuRenderer() : base(new DarkColorTable()) { }
    
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(Point.Empty, e.Item.Size);
        using var brush = new SolidBrush(e.Item.Selected ? DarkHv : DarkBg);
        e.Graphics.FillRectangle(brush, rc);
    }
    
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(DarkBd);
        e.Graphics.DrawLine(pen, 0, e.Item.Size.Height / 2, e.Item.Size.Width, e.Item.Size.Height / 2);
    }
    
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(DarkBd);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
    
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = DarkTx; base.OnRenderItemText(e); }
}

public class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => Color.FromArgb(62, 62, 66);
    public override Color MenuItemBorder => Color.FromArgb(62, 62, 66);
    public override Color MenuBorder => Color.FromArgb(60, 60, 60);
    public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 48);
    public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 48);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 48);
    public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 48);
    public override Color SeparatorDark => Color.FromArgb(60, 60, 60);
    public override Color SeparatorLight => Color.FromArgb(60, 60, 60);
}
