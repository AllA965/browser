using Microsoft.Web.WebView2.Core;
using MiniWorldBrowser.Browser;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Features;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Helpers.Extensions;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Services.Interfaces;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 主窗体 - 仅负责 UI 布局和事件绑定
/// </summary>
public partial class MainForm : Form
{
    #region 常量
    
    private static readonly Color IncognitoAccent = MiniWorldBrowser.Constants.UIConstants.IncognitoAccentColor;
    
    #endregion
    
    #region 服务和管理器
    
    private readonly ISettingsService _settingsService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IAdBlockService _adBlockService;
    private readonly IHistoryService _historyService;
    private readonly ILoginService _loginService;
    private readonly IAdService _adService;
    private readonly UpdateService _updateService; // 添加更新服务
    private readonly MediaDownloadService _mediaDownloadService; // 添加媒体下载服务
    private readonly bool _isIncognito;
    private readonly string? _incognitoDataFolder;
    private bool _isInternalAddressUpdate;
    private bool _hadBookmarks; // 记录上次检查时是否有收藏内容
    private BrowserTabManager _tabManager = null!;
    private MouseGesture _mouseGesture = null!;
    private BossKey? _bossKey;
    private FullscreenManager _fullscreenManager = null!;
    
    #endregion
    
    #region UI 控件
    
    private Panel _tabBar = null!;
    private Panel _incognitoIndicator = null!;
    private FlowLayoutPanel _tabContainer = null!;
    private NewTabButton _newTabButton = null!;
    private Button _tabOverflowBtn = null!; // 标签溢出按钮
    private TabOverflowPanel _tabOverflowPanel = null!; // 标签溢出面板
    private Button _minimizeBtn = null!, _maximizeBtn = null!, _closeBtn = null!;
    private Panel _toolbar = null!;
    private NavigationButton _backBtn = null!, _forwardBtn = null!, _refreshBtn = null!, _stopBtn = null!, _homeBtn = null!;
    private Controls.ChromeAddressBar _addressBar = null!; // 使用新的自定义控件
    private SecurityIcon _securityIcon = null!;

    private AnimatedBookmarkButton _bookmarkBtn = null!;
    private Button _passwordKeyBtn = null!; // 钥匙图标按钮
    private Button _zoomBtn = null!; // 放大镜图标按钮
    private Button _translateBtn = null!; // 翻译按钮
    private DownloadButton _downloadBtn = null!;
    private RoundedButton _settingsBtn = null!;
    private UserButton _userBtn = null!;
    private Controls.DownloadPanel? _downloadPanel;
    private UserInfoPopup? _userInfoPopup;
    private bool _suppressUserInfoPopupClose;
    private DateTime _lastUserInfoPopupCloseTime = DateTime.MinValue;
    
    // 待保存的密码信息
    private (string host, string username, string password)? _pendingPasswordInfo;
    // 密码是否已保存（用于决定显示哪种弹窗）
    private bool _isPasswordSaved;
    private BookmarkBar _bookmarkBar = null!;
    private Panel _browserContainer = null!;
    private Panel _statusBar = null!;
    private Label _statusLabel = null!;
    private PictureBox _titleBarIcon = null!;
    private ModernProgressBar _progressBar = null!;
    private AddressBarDropdown _addressDropdown = null!;
    private AdCarouselControl _adCarousel = null!;
    
    // AI 相关控件
    private Panel _aiSidePanel = null!;
    private Splitter _aiSplitter = null!;
    private Microsoft.Web.WebView2.WinForms.WebView2 _aiWebView = null!;
    private RoundedButton _aiBtn = null!;
    private Button _aiSummarizeBtn = null!;
    private AiApiBridge? _aiApiBridge;
    
    private readonly List<string> _urlHistory = new();
    private double _zoomLevel = 1.0;
    private ContextMenuStrip? _mainMenu;
    
    // 定时器
    private System.Windows.Forms.Timer? _cursorTimer;
    private System.Windows.Forms.Timer? _memoryTimer;
    private System.Windows.Forms.Timer? _adPopupTimer;
    
    #endregion
    
    #region 键盘钩子
    
    private IntPtr _keyboardHookId = IntPtr.Zero;
    private Win32Helper.LowLevelKeyboardProc? _keyboardProc;
    
    // 启用无边框窗口的调整大小功能（保持无边框外观）
    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_MINIMIZEBOX = 0x00020000;
            const int WS_MAXIMIZEBOX = 0x00010000;
            const int WS_THICKFRAME = 0x00040000;  // 允许调整窗口大小
            const int CS_DBLCLKS = 0x8;
            
            var cp = base.CreateParams;
            cp.Style |= WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_THICKFRAME;
            cp.ClassStyle |= CS_DBLCLKS;
            return cp;
        }
    }
    
    #endregion
    
    public MainForm(bool isIncognito = false)
    {
        _isIncognito = isIncognito;
        if (_isIncognito)
        {
            _incognitoDataFolder = Path.Combine(
                Path.GetTempPath(),
                "MiniWorld_Incognito_" + Guid.NewGuid().ToString("N")[..8]);
        }

        // 开启双缓冲，解决窗口拉伸时的闪烁和残影问题
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        // 初始化服务
        _settingsService = new SettingsService();
        _bookmarkService = new BookmarkService();
        _adBlockService = new AdBlockService 
        { 
            Enabled = _settingsService.Settings.EnableAdBlock,
            Mode = _settingsService.Settings.AdBlockMode
        };
        _adBlockService.SetExceptions(_settingsService.Settings.AdBlockExceptions);
        
        // 隐身模式使用独立的内存历史服务
        _historyService = _isIncognito ? new HistoryService(false) : new HistoryService(); 
        _loginService = new LoginService(_settingsService);
        _adService = new AdService();
        _updateService = new UpdateService(); // 初始化更新服务
        _mediaDownloadService = new MediaDownloadService(); // 初始化媒体下载服务
        
        InitializeUI();
        InitializeManagers();
        InitializeAIWebView();
        SetupBookmarkBarEvents();
        InitializeEvents();
        
        _loginService.LoginStateChanged += () => Invoke(RefreshLoginStatus);
        try { Localization.Initialize(string.Equals(_settingsService.Settings.LanguageCode, "auto", StringComparison.OrdinalIgnoreCase) ? null : _settingsService.Settings.LanguageCode); } catch { }
        RefreshLocalizedTexts();
        RefreshLoginStatus();
        
        Shown += async (s, e) =>
        {
            RefreshAllControls();
            _adCarousel?.BringToFront(); // 再次确保广告在最上层
            
            // 检查登录状态
            if (_loginService != null)
            {
                await _loginService.CheckLoginAsync();
            }

            // 启动时自动检查更新 (仅在非隐身模式下)
            if (!_isIncognito)
            {
                _ = _updateService.CheckAndPromptUpdateAsync(this);
            }
            
            try
            {
                if (_tabManager == null)
                {
                    throw new Exception("TabManager 未初始化");
                }

                await StartupHelper.HandleStartupAsync(
                    _settingsService,
                    _tabManager,
                    _isIncognito,
                    async (url) => await CreateNewTabWithProtection(url)
                );
                
                // 强制刷新标签容器
                _tabContainer?.Invalidate();
                _tabContainer?.Update();
            }
            catch (Exception ex)
            {
                var fullMessage = GetFullExceptionMessage(ex);
                MessageBox.Show(fullMessage, "启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
    }
    
    #region 初始化
    
    private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int diameter = radius * 2;
        
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        
        return path;
    }
    
    private void InitializeUI()
    {
        // 开启窗体级的双缓冲和硬件加速优化
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                      ControlStyles.AllPaintingInWmPaint | 
                      ControlStyles.UserPaint, true);
        this.UpdateStyles();

        Text = _isIncognito ? "InPrivate - " + AppConstants.AppName : AppConstants.AppName;
        Size = DpiHelper.Scale(UIConstants.DefaultWindowSize);
        MinimumSize = DpiHelper.Scale(UIConstants.MinWindowSize);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _isIncognito ? UIConstants.IncognitoBackColor : UIConstants.DefaultBackColor;
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
        if (!_isIncognito)
        {
            // CreateAdCarousel();
        }
        CreateAISidePanel();
        
        // 应用圆角
        Win32Helper.ApplyRoundedCorners(this.Handle);
        
        // 注意：WinForms 中后 Add 的控件默认在最上层
        // 我们先添加基础布局控件
        Controls.Add(_browserContainer);
        Controls.Add(_aiSplitter);
        Controls.Add(_aiSidePanel);
        Controls.Add(_statusBar);
        Controls.Add(_bookmarkBar);
        Controls.Add(_toolbar);
        Controls.Add(_tabBar);

        // 递归开启所有容器的硬件加速和双缓冲
        EnableDoubleBuffering(this);
    }

    private void EnableDoubleBuffering(Control control)
    {
        if (control is Panel || control is FlowLayoutPanel || control is TableLayoutPanel || control is PictureBox)
        {
            var prop = typeof(Control).GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prop?.SetValue(control, true, null);
        }

        foreach (Control child in control.Controls)
        {
            EnableDoubleBuffering(child);
        }
    }
    
    private void CreateTabBar()
    {
        _tabBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = DpiHelper.Scale(36),
            BackColor = _isIncognito ? Color.FromArgb(20, 20, 20) : Color.FromArgb(232, 234, 237)
        };
        _tabBar.MouseDown += OnTitleBarMouseDown;

        // 标题栏图标
        _titleBarIcon = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = DpiHelper.Scale(8), // 保留极小间距或设为0
            BackColor = Color.Transparent,
            Visible = false
        };

        _titleBarIcon.Paint += (s, e) =>
        {
            if (AppIconHelper.AppIcon != null)
            {
                // 居中绘制图标，直接使用 DrawIcon 以保留完美透明度
                int iconSize = DpiHelper.Scale(18); 
                int x = (_titleBarIcon.Width - iconSize) / 2;
                int y = (_titleBarIcon.Height - iconSize) / 2;
                
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawIcon(AppIconHelper.AppIcon, new Rectangle(x, y, iconSize, iconSize));
            }
        };
        _titleBarIcon.MouseDown += OnTitleBarMouseDown;
        
        // 窗口控制按钮
        var windowControlPanel = new Panel 
        { 
            Dock = DockStyle.Right, 
            Width = DpiHelper.Scale(138), 
            Height = DpiHelper.Scale(36),
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // ... (CreateWindowControlButton uses hardcoded sizes too, but I'll check it later)
        
        _minimizeBtn = CreateWindowControlButton("─");
        _minimizeBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
        
        _maximizeBtn = CreateWindowControlButton("☐");
        _maximizeBtn.Click += (s, e) => ToggleMaximize();
        
        _closeBtn = CreateWindowControlButton("✕");
        _closeBtn.Click += (s, e) => Close();
        _closeBtn.MouseEnter += (s, e) => { _closeBtn.BackColor = Color.FromArgb(232, 17, 35); _closeBtn.ForeColor = Color.White; };
        _closeBtn.MouseLeave += (s, e) => { _closeBtn.BackColor = Color.Transparent; _closeBtn.ForeColor = _isIncognito ? Color.White : Color.Black; };
        
        windowControlPanel.Controls.Add(_minimizeBtn);
        windowControlPanel.Controls.Add(_maximizeBtn);
        windowControlPanel.Controls.Add(_closeBtn);
        
        // 隐身模式标识
        if (_isIncognito)
        {
            _incognitoIndicator = CreateIncognitoIndicator();
        }

        // 新标签按钮
        _newTabButton = new NewTabButton(_isIncognito)
        {
            Size = DpiHelper.Scale(new Size(28, 28)),
            Margin = DpiHelper.Scale(new Padding(0, 4, 0, 0)) // 调整边距使其对齐
        };
        new ToolTip().SetToolTip(_newTabButton, Localization.T("tooltips.new_tab"));

        // 标签容器
        _tabContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Padding = DpiHelper.Scale(new Padding(4, 4, 0, 0))
        };
        _tabContainer.MouseDown += OnTitleBarMouseDown;
        
        // 将新标签按钮添加到容器中，这样它就会排在标签后面
        _tabContainer.Controls.Add(_newTabButton);
        
        // 标签溢出按钮
        _tabOverflowBtn = new Button
        {
            Dock = DockStyle.Right,
            Width = DpiHelper.Scale(32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Text = "﹀", // 或者使用 unicode 字符
            Font = new Font("Segoe UI Symbol", DpiHelper.ScaleFont(9F)),
            ForeColor = _isIncognito ? Color.White : Color.Black,
            Cursor = Cursors.Hand,
            Visible = false, // 默认隐藏
            Margin = Padding.Empty
        };
        _tabOverflowBtn.FlatAppearance.BorderSize = 0;
        _tabOverflowBtn.FlatAppearance.MouseOverBackColor = _isIncognito ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);
        new ToolTip().SetToolTip(_tabOverflowBtn, Localization.T("tooltips.search_tabs"));

        var tabStripHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        tabStripHostPanel.Controls.Add(_tabContainer);
        tabStripHostPanel.Controls.Add(_tabOverflowBtn);

        _tabBar.Controls.Add(tabStripHostPanel);
        _tabBar.Controls.Add(_titleBarIcon);
        if (_isIncognito)
        {
            _tabBar.Controls.Add(_incognitoIndicator);
        }
        _tabBar.Controls.Add(windowControlPanel);
    }

    private Panel CreateIncognitoIndicator()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Right,
            Width = DpiHelper.Scale(90),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        
        var label = new Label
        {
            Text = "🕵️ InPrivate",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", DpiHelper.ScaleFont(9F)),
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
            Localization.T("incognito.info.message"),
            Localization.T("incognito.info.title"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void CreateTabOverflowPanel()
    {
        _tabOverflowPanel = new TabOverflowPanel(false)
        {
            Visible = false
        };
        
        Controls.Add(_tabOverflowPanel);
        _tabOverflowPanel.BringToFront();
    }
    
    private void CreateToolbar()
    {
        _toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = DpiHelper.Scale(44),
            BackColor = _isIncognito ? Color.FromArgb(35, 35, 35) : Color.White,
            Padding = DpiHelper.Scale(new Padding(4, 4, 4, 4))
        };
        
        _backBtn = CreateNavigationButton(NavigationButtonType.Back, Localization.T("nav.back"));
        _forwardBtn = CreateNavigationButton(NavigationButtonType.Forward, Localization.T("nav.forward"));
        _refreshBtn = CreateNavigationButton(NavigationButtonType.Refresh, Localization.T("nav.refresh"));
        _stopBtn = CreateNavigationButton(NavigationButtonType.Stop, Localization.T("nav.stop"));
        _homeBtn = CreateNavigationButton(NavigationButtonType.Home, Localization.T("nav.home"));
        
        _stopBtn.Visible = false;
        
        _downloadBtn = new DownloadButton
        {
            Size = DpiHelper.Scale(new Size(32, 32)),
            Margin = DpiHelper.Scale(new Padding(2)),
            IconColor = _isIncognito ? Color.FromArgb(200, 200, 200) : Color.FromArgb(80, 80, 80)
        };
        new ToolTip().SetToolTip(_downloadBtn, Localization.T("tooltips.download"));

        _userBtn = new UserButton { Margin = DpiHelper.Scale(new Padding(2)), Visible = !_isIncognito };
        if (!_isIncognito)
        {
            new ToolTip().SetToolTip(_userBtn, Localization.T("tooltips.user_login"));
            _userBtn.Click += OnUserButtonClick;
            
            _userBtn.MouseEnter += (s, e) => _userBtn.Invalidate();
            _userBtn.MouseLeave += (s, e) => _userBtn.Invalidate();
        }

        _settingsBtn = CreateToolButton("☰", "菜单");
        
        _aiBtn = CreateToolButton(string.Empty, Localization.T("ai.assistant"));
        _aiBtn.UseGrayscale = true;
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(iconPath))
            {
                using var icon = new Icon(iconPath, 16, 16);
                _aiBtn.IconImage = icon.ToBitmap();
            }
        }
        catch { }
        _aiBtn.Click += OnAiButtonClick;

        // 布局
        var toolPanel = new Panel { Dock = DockStyle.Fill, BackColor = _isIncognito ? Color.FromArgb(35, 35, 35) : Color.White };
        
        var navPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Padding = DpiHelper.Scale(new Padding(4, 4, 0, 4))
        };
        
        var refreshStopPanel = new Panel { Size = DpiHelper.Scale(new Size(32, 32)) };
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
            Padding = DpiHelper.Scale(new Padding(0, 4, 4, 4))
        };
        // 注意：这里不再重复添加 _passwordKeyBtn 和 _zoomBtn 到 menuPanel，因为它们被移动到了地址栏内部
        menuPanel.Controls.Add(_aiBtn); // AI 助手按钮
        menuPanel.Controls.Add(_userBtn);
        menuPanel.Controls.Add(_downloadBtn);
        menuPanel.Controls.Add(_settingsBtn);
        
        // 3. Address Bar Container (The "Omnibox")
        _addressBar = new Controls.ChromeAddressBar
        {
            Dock = DockStyle.Fill,
            TabIndex = 0,
            IsDarkMode = _isIncognito
        };

        // Inner controls inside the address bar (Icons)
        _securityIcon = new SecurityIcon { Size = DpiHelper.Scale(new Size(28, 20)), BackColor = Color.Transparent, Padding = DpiHelper.Scale(new Padding(4,0,0,0)), Cursor = Cursors.Hand };
        _securityIcon.SecurityInfoRequested += OnSecurityInfoRequested;
        UpdateSecurityIcon(false);

        _translateBtn = new Button
        {
            Size = DpiHelper.Scale(new Size(32, 28)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Text = "🌐",
            Font = new Font("Segoe UI Emoji", DpiHelper.ScaleFont(12F)),
            Cursor = Cursors.Hand,
            Visible = false,
            Margin = DpiHelper.Scale(new Padding(2, 0, 2, 0)),
            ForeColor = _isIncognito ? Color.White : Color.Black
        };
        _translateBtn.FlatAppearance.BorderSize = 0;
        _translateBtn.FlatAppearance.MouseOverBackColor = _isIncognito ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);
        _translateBtn.Click += OnTranslateButtonClick;
        new ToolTip().SetToolTip(_translateBtn, Localization.T("tooltips.translate_page"));
        
        _bookmarkBtn = new AnimatedBookmarkButton { Size = DpiHelper.Scale(new Size(28, 24)), BackColor = Color.Transparent, Margin = DpiHelper.Scale(new Padding(2,0,2,0)) };
        
        _zoomBtn = new Button
        {
            Size = DpiHelper.Scale(new Size(32, 28)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Text = "🔍",
            Font = new Font("Segoe UI Emoji", DpiHelper.ScaleFont(10F)),
            Cursor = Cursors.Hand,
            Visible = false,
            Margin = DpiHelper.Scale(new Padding(2, 0, 2, 0)),
            ForeColor = _isIncognito ? Color.White : Color.Black,
            TabStop = false // 禁用 Tab 停靠，防止获得焦点显示黑色边框
        };
        _zoomBtn.FlatAppearance.BorderSize = 0;
        _zoomBtn.FlatAppearance.BorderColor = Color.FromArgb(0, 0, 0, 0); // 彻底透明边框
        _zoomBtn.FlatAppearance.MouseDownBackColor = _isIncognito ? Color.FromArgb(90, 90, 90) : Color.FromArgb(200, 200, 200);
        _zoomBtn.FlatAppearance.MouseOverBackColor = _isIncognito ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);
        _zoomBtn.Click += (s, e) => ShowZoomPopup();
        new ToolTip().SetToolTip(_zoomBtn, Localization.T("tooltips.zoom"));

        _passwordKeyBtn = new Button
        {
            Size = DpiHelper.Scale(new Size(32, 28)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Text = "🔑",
            Font = new Font("Segoe UI Emoji", DpiHelper.ScaleFont(10F)),
            Cursor = Cursors.Hand,
            Visible = false,
            Margin = DpiHelper.Scale(new Padding(2, 0, 2, 0)),
            ForeColor = _isIncognito ? Color.White : Color.Black
        };
        _passwordKeyBtn.FlatAppearance.BorderSize = 0;
        _passwordKeyBtn.FlatAppearance.MouseOverBackColor = _isIncognito ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);
        _passwordKeyBtn.Click += OnPasswordKeyButtonClick;
        new ToolTip().SetToolTip(_passwordKeyBtn, Localization.T("tooltips.manage_passwords"));

        // Add icons to the custom address bar's internal container mechanism
        var rightIconPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = DpiHelper.Scale(new Padding(0, 4, 0, 0)) // Center vertically
        };
        rightIconPanel.Controls.Add(_passwordKeyBtn);
        rightIconPanel.Controls.Add(_zoomBtn);
        rightIconPanel.Controls.Add(_translateBtn);
        rightIconPanel.Controls.Add(_bookmarkBtn);

        _addressBar.Controls.Add(rightIconPanel);
        _addressBar.Controls.Add(_securityIcon);
        _securityIcon.Dock = DockStyle.Left; // Security icon on the left
        
        // Event wiring
        _addressBar.EnterKeyPressed += (s, e) => NavigateToAddress();
        _addressBar.Click += (s, e) => _addressBar.SelectAll();
        
        // Layout the main toolbar
        _toolbar.Controls.Add(_addressBar); // Fill
        _toolbar.Controls.Add(navPanel);    // Left
        _toolbar.Controls.Add(menuPanel);   // Right
        
        // Fix Z-order for Docking
        menuPanel.SendToBack();
        navPanel.SendToBack();
        _addressBar.BringToFront();
    }
    
    // Adaptor for the event handler
    private void OnAddressBarKeyDown(object? sender, EventArgs e)
    {
        NavigateToAddress();
    }
    
    private void CreateBookmarkBar()
    {
        _bookmarkBar = new BookmarkBar(_bookmarkService);
        _bookmarkBar.IsIncognito = _isIncognito;
        _bookmarkBar.BackColor = _isIncognito ? Color.FromArgb(53, 54, 58) : Color.White;
        // 初始可见性设为 false，由 UpdateBookmarkBarVisibility 根据内容决定
        _bookmarkBar.Visible = false;
    }    // 事件绑定移到 InitializeManagers 之后，避免空引用
    
    private void SetupBookmarkBarEvents()
    {
        _bookmarkBar.BookmarkClicked += url => _tabManager.ActiveTab?.Navigate(url);
        _bookmarkBar.BookmarkMiddleClicked += async (url, _) => await CreateNewTabWithProtection(url);
        _bookmarkBar.AddBookmarkRequested += AddCurrentPageToBookmarks;

        // 监听书签变更
        _bookmarkService.BookmarksChanged += () => {
            UpdateBookmarkBarVisibility();
            UpdateCurrentTabBookmarkState();
        };

        // 初始记录书签状态，避免启动时触发“从无到有”的自动勾选逻辑
        _hadBookmarks = (_bookmarkService.GetBookmarkBarItems().Count > 0) || 
                          (_bookmarkService.GetOtherBookmarks().Count > 0);

        // 初始设置可见性
        UpdateBookmarkBarVisibility();
    }

    private bool _isUpdatingBookmarkBar = false;
    private System.Windows.Forms.Timer? _bookmarkBarTimer;
    private int _bookmarkBarTargetHeight;
    private const int BookmarkBarDefaultHeight = 40;

    /// <summary>
    /// 以动画方式显示/隐藏收藏栏
    /// </summary>
    private void AnimateBookmarkBar(bool show)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AnimateBookmarkBar(show)));
            return;
        }

        int targetHeight = show ? DpiHelper.Scale(BookmarkBarDefaultHeight) : 0;
        
        // 如果已经在目标状态且没有在动画中，直接返回
        if (_bookmarkBar.Height == targetHeight && _bookmarkBar.Visible == show && (_bookmarkBarTimer == null || !_bookmarkBarTimer.Enabled)) 
            return;

        _bookmarkBarTargetHeight = targetHeight;
        
        if (_bookmarkBarTimer == null)
        {
            _bookmarkBarTimer = new System.Windows.Forms.Timer { Interval = 10 };
            _bookmarkBarTimer.Tick += (s, e) =>
            {
                int currentHeight = _bookmarkBar.Height;
                int diff = _bookmarkBarTargetHeight - currentHeight;
                if (Math.Abs(diff) <= 1)
                {
                    _bookmarkBar.Height = _bookmarkBarTargetHeight;
                    _bookmarkBarTimer.Stop();
                    if (_bookmarkBarTargetHeight == 0)
                    {
                        _bookmarkBar.Visible = false;
                    }
                    return;
                }

                int step = (int)(diff * 0.25); // 稍微加快一点速度
                if (step == 0) step = diff > 0 ? 1 : -1;
                
                _bookmarkBar.Height = currentHeight + step;
            };
        }

        if (show && !_bookmarkBar.Visible)
        {
            _bookmarkBar.Height = 0;
            _bookmarkBar.Visible = true;
        }
        
        _bookmarkBarTimer.Start();
    }

    /// <summary>
    /// 更新收藏栏可见性：当没有收藏内容时强制隐藏以优化空间
    /// </summary>
    private void UpdateBookmarkBarVisibility()
    {
        if (_isUpdatingBookmarkBar) return;
        
        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdateBookmarkBarVisibility));
            return;
        }

        _isUpdatingBookmarkBar = true;
        try
        {
            var settings = _settingsService.Settings;
            
            // 只有当书签栏根目录或“其他收藏”中有内容时，才认为“有内容”
            var hasBookmarks = (_bookmarkService.GetBookmarkBarItems().Count > 0) || 
                              (_bookmarkService.GetOtherBookmarks().Count > 0);
            
            // 记录旧状态
            bool wasHadBookmarks = _hadBookmarks;
            // 立即更新记录状态，防止 Save() 触发的事件递归进入时逻辑错误
            _hadBookmarks = hasBookmarks;
            
            // 智能逻辑：如果从“无内容”变为“有内容”，自动开启“总是显示收藏栏”
            if (!wasHadBookmarks && hasBookmarks)
            {
                if (!settings.AlwaysShowBookmarkBar)
                {
                    settings.AlwaysShowBookmarkBar = true;
                    _settingsService.Save();
                }
            }
            
            // 逻辑：如果设置了总是显示，且确实有内容，则显示；
            // 如果没有内容，则强制收起并同步更新设置，保持状态一致
            bool shouldShow = settings.AlwaysShowBookmarkBar && hasBookmarks;
            
            // 如果设置是“总是显示”但实际没内容导致收起了，则同步取消设置中的勾选
            if (settings.AlwaysShowBookmarkBar && !hasBookmarks)
            {
                settings.AlwaysShowBookmarkBar = false;
                _settingsService.Save();
            }
            
            if (_bookmarkBar.Visible != shouldShow || (shouldShow && _bookmarkBar.Height < DpiHelper.Scale(BookmarkBarDefaultHeight)))
            {
                AnimateBookmarkBar(shouldShow);
            }
        }
        finally
        {
            _isUpdatingBookmarkBar = false;
        }
    }
    
    private void CreateBrowserContainer()
    {
        _browserContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
    }
    
    private void CreateStatusBar()
    {
        _statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = DpiHelper.Scale(22),
            BackColor = _isIncognito ? Color.FromArgb(41, 42, 45) : Color.White
        };
        
        _statusLabel = new Label
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            Padding = DpiHelper.Scale(new Padding(4, 3, 0, 0)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(8F)),
            ForeColor = _isIncognito ? Color.FromArgb(150, 150, 150) : Color.Black,
            Text = _isIncognito ? Localization.T("status.incognito") : Localization.T("status.ready")
        };

        _progressBar = new ModernProgressBar
        {
            Dock = DockStyle.Right,
            Width = DpiHelper.Scale(110),
            Height = DpiHelper.Scale(22),
            Padding = DpiHelper.Scale(new Padding(10, 0, 10, 0)),
            Visible = false,
            IsMarquee = true
        };
        
        _statusBar.Controls.AddRange(new Control[] { _statusLabel, _progressBar });
    }
    
    private void CreateAddressDropdown()
    {
        _addressDropdown = new AddressBarDropdown(_historyService, _bookmarkService, _isIncognito)
        {
            Owner = this // 确保所有权，防止 Z-order 问题
        };
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
                {
                    tabs.Add((tab.Title ?? "新标签页", tab.Url ?? ""));
                }
            }
            return tabs;
        };
        
        // 当下拉框隐藏时，重置地址栏状态
        _addressDropdown.DropdownHidden += () =>
        {
            _addressBar.IsDropdownOpen = false;
        };

        // 当下拉框按钮被点击后，恢复地址栏焦点
        _addressDropdown.RequestFocusRestore += () =>
        {
            BeginInvoke(() => _addressBar.Focus());
        };
    }

    private void CreateAdCarousel()
    {
        _adCarousel = new AdCarouselControl
        {
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            AutoExpandOnFirstLoad = false // 取消自动弹出，改为定时弹出
        };
        
        // 确保它在最顶层
        _adCarousel.BringToFront();

        // 启动 15 秒定时弹出
        _adPopupTimer = new System.Windows.Forms.Timer { Interval = 15000 };
        _adPopupTimer.Tick += (s, e) =>
        {
            if (_adPopupTimer != null)
            {
                _adPopupTimer.Stop();
                _adPopupTimer.Dispose();
                _adPopupTimer = null;
            }
            
            if (_adCarousel != null && !_adCarousel.IsDisposed)
            {
                _adCarousel.ExpandWithAnimation();
            }
        };
        _adPopupTimer.Start();
    }

    private void CreateAISidePanel()
    {
        _aiSidePanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = DpiHelper.Scale(380),
            BackColor = Color.FromArgb(250, 250, 250),
            Visible = false,
            BorderStyle = BorderStyle.None
        };

        _aiSplitter = new Splitter
        {
            Dock = DockStyle.Right,
            Width = DpiHelper.Scale(3),
            BackColor = Color.FromArgb(220, 220, 220),
            Visible = false
        };

        _aiWebView = new Microsoft.Web.WebView2.WinForms.WebView2
        {
            Dock = DockStyle.Fill
        };

        // 顶部栏
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = DpiHelper.Scale(45),
            BackColor = Color.FromArgb(250, 251, 252),
            Padding = DpiHelper.Scale(new Padding(12, 0, 8, 0))
        };

        // 添加底部边框线
        topPanel.Paint += (s, e) => {
            using (var pen = new Pen(Color.FromArgb(230, 233, 237), DpiHelper.Scale(1)))
            {
                e.Graphics.DrawLine(pen, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            }
        };

        var titleLabel = new Label
        {
            Text = Localization.T("ai.assistant"),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10F), FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 55, 72),
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Width = DpiHelper.Scale(150) // 给定一个足够的宽度
        };

        _aiSummarizeBtn = new Button
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            Size = DpiHelper.Scale(new Size(90, 32)),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand,
            Margin = DpiHelper.Scale(new Padding(0, 6, 4, 6)),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(74, 85, 104),
            TabStop = false,
            UseVisualStyleBackColor = false
        };
        _aiSummarizeBtn.FlatAppearance.BorderSize = 0;

        int summarizeBgAlpha = 0;
        int summarizeTargetAlpha = 0;
        var summarizeAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };

        _aiSummarizeBtn.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = _aiSummarizeBtn.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            int radius = DpiHelper.Scale(6);

            // 绘制圆角矩形背景
            if (summarizeBgAlpha > 0)
            {
                using (var path = CreateRoundedRectanglePath(rect, radius))
                using (var brush = new SolidBrush(Color.FromArgb(summarizeBgAlpha, 59, 130, 246))) // 使用更精致的淡蓝色 (Blue 500)
                {
                    g.FillPath(brush, path);
                }
            }

            // 绘制文字
            TextRenderer.DrawText(g, _aiSummarizeBtn.Text, _aiSummarizeBtn.Font, rect, _aiSummarizeBtn.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        summarizeAnimTimer.Tick += (s, e) =>
        {
            bool changed = false;
            if (summarizeBgAlpha != summarizeTargetAlpha)
            {
                int step = 25;
                if (summarizeBgAlpha < summarizeTargetAlpha) summarizeBgAlpha = Math.Min(summarizeTargetAlpha, summarizeBgAlpha + step);
                else summarizeBgAlpha = Math.Max(summarizeTargetAlpha, summarizeBgAlpha - step);
                changed = true;
            }

            if (changed) _aiSummarizeBtn.Invalidate();
            else if (summarizeTargetAlpha == 0 && summarizeBgAlpha == 0) summarizeAnimTimer.Stop();
        };

        _aiSummarizeBtn.MouseEnter += (s, e) =>
        {
            _aiSummarizeBtn.ForeColor = Color.FromArgb(37, 99, 235);
            summarizeTargetAlpha = 25; // 悬停时淡淡的蓝色背景
            summarizeAnimTimer.Start();
        };

        _aiSummarizeBtn.MouseDown += (s, e) =>
        {
            summarizeTargetAlpha = 50; // 按下时加深
            summarizeAnimTimer.Start();
        };

        _aiSummarizeBtn.MouseUp += (s, e) =>
        {
            summarizeTargetAlpha = 25;
            summarizeAnimTimer.Start();
        };

        _aiSummarizeBtn.MouseLeave += (s, e) =>
        {
            _aiSummarizeBtn.ForeColor = Color.FromArgb(74, 85, 104);
            summarizeTargetAlpha = 0;
            summarizeAnimTimer.Start();
        };

        var closeBtn = new Button
        {
            Text = "", // 不直接使用文本，改用 Paint 绘制
            Size = DpiHelper.Scale(new Size(32, 32)),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand,
            Margin = DpiHelper.Scale(new Padding(4, 6, 4, 6)),
            BackColor = Color.Transparent
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.FlatAppearance.MouseOverBackColor = Color.Transparent; // 禁用自带的悬停背景
        closeBtn.FlatAppearance.MouseDownBackColor = Color.Transparent;

        float rotationAngle = 0;
        float targetRotation = 0;
        int backgroundAlpha = 0;
        int targetAlpha = 0;
        var animTimer = new System.Windows.Forms.Timer { Interval = 15 };

        closeBtn.Paint += (s, e) => {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            
            // 确保绘制的是正圆：取宽高的最小值作为直径
            int size = Math.Min(closeBtn.Width, closeBtn.Height) - DpiHelper.Scale(12);
            int x = (closeBtn.Width - size) / 2;
            int y = (closeBtn.Height - size) / 2;
            Rectangle circleRect = new Rectangle(x, y, size, size);

            // 绘制圆形背景
            if (backgroundAlpha > 0)
            {
                // 使用更高级的渐变红或纯色
                using (var brush = new SolidBrush(Color.FromArgb(backgroundAlpha, 239, 68, 68))) // Tailwind Red 500
                {
                    e.Graphics.FillEllipse(brush, circleRect);
                }
            }

            // 绘制 X 图标
            e.Graphics.TranslateTransform(closeBtn.Width / 2f, closeBtn.Height / 2f);
            e.Graphics.RotateTransform(rotationAngle);
            
            // 图标缩放动画效果：悬停时稍微放大
            float scale = 1.0f + (backgroundAlpha / 255f) * 0.2f;
            e.Graphics.ScaleTransform(scale, scale);
            
            Color iconColor = backgroundAlpha > 150 ? Color.White : Color.FromArgb(100, 116, 139); // Slate 500
            using (var pen = new Pen(iconColor, DpiHelper.Scale(2f)))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                float iconSize = DpiHelper.Scale(4.5f);
                e.Graphics.DrawLine(pen, -iconSize, -iconSize, iconSize, iconSize);
                e.Graphics.DrawLine(pen, iconSize, -iconSize, -iconSize, iconSize);
            }
            e.Graphics.ResetTransform();
        };

        animTimer.Tick += (s, e) => {
            bool changed = false;
            
            // 角度动画
            if (Math.Abs(rotationAngle - targetRotation) > 0.1f)
            {
                rotationAngle += (targetRotation - rotationAngle) * 0.3f;
                changed = true;
            }

            // 背景透明度动画
            if (backgroundAlpha != targetAlpha)
            {
                int step = 25;
                if (backgroundAlpha < targetAlpha) backgroundAlpha = Math.Min(targetAlpha, backgroundAlpha + step);
                else backgroundAlpha = Math.Max(targetAlpha, backgroundAlpha - step);
                changed = true;
            }

            if (changed) closeBtn.Invalidate();
            else if (targetAlpha == 0 && Math.Abs(rotationAngle) < 0.1f) animTimer.Stop();
        };

        closeBtn.MouseEnter += (s, e) => {
            targetRotation = 90f;
            targetAlpha = 40; // 悬停时的透明度
            animTimer.Start();
        };

        closeBtn.MouseDown += (s, e) => {
            targetAlpha = 200; // 按下时加深
            closeBtn.Invalidate();
        };

        closeBtn.MouseUp += (s, e) => {
            targetAlpha = 40;
            closeBtn.Invalidate();
        };

        closeBtn.MouseLeave += (s, e) => {
            targetRotation = 0f;
            targetAlpha = 0;
            animTimer.Start();
        };
        
        closeBtn.Click += (s, e) => ToggleAISidePanel();

        topPanel.Controls.Add(titleLabel);
        topPanel.Controls.Add(_aiSummarizeBtn);
        topPanel.Controls.Add(closeBtn);

        _aiSidePanel.Controls.Add(_aiWebView);
        _aiSidePanel.Controls.Add(topPanel);

        UpdateAiSummarizeButtonUi();
    }

    private void UpdateAiSummarizeButtonUi()
    {
        if (_aiSummarizeBtn == null) return;

        var settings = _settingsService.Settings;
        bool isApiMode = settings.AiServiceMode == 1;

        _aiSummarizeBtn.Click -= OnAiSummarizeClickSummarize;
        _aiSummarizeBtn.Click -= OnAiSummarizeClickOpenSettings;

        if (isApiMode)
        {
            _aiSummarizeBtn.Text = Localization.T("ai.summarize");
            _aiSummarizeBtn.Enabled = true;
            _aiSummarizeBtn.Click += OnAiSummarizeClickSummarize;
        }
        else
        {
            _aiSummarizeBtn.Text = Localization.T("ai.mode");
            _aiSummarizeBtn.Enabled = true;
            _aiSummarizeBtn.Click += OnAiSummarizeClickOpenSettings;
        }
    }

    private async void InitializeAIWebView()
    {
        try
        {
            // 1. 检查 WebView2Runtime
            string? runtimePath = MiniWorldBrowser.Browser.BrowserTab.FindWebView2Runtime();
            if (runtimePath == null)
            {
                // 如果没有找到打包的运行时，尝试检查系统是否安装了 WebView2
                try
                {
                    string version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
                    if (string.IsNullOrEmpty(version)) throw new Exception();
                }
                catch
                {
                    MessageBox.Show(Localization.Raw(), Localization.Raw(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // 使用共享环境初始化，防止与标签页初始化冲突 (0x8007139F)
            string userDataFolder = MiniWorldBrowser.Browser.BrowserTab.GetUserDataFolder(_incognitoDataFolder, _settingsService);
            var env = await MiniWorldBrowser.Browser.BrowserTab.GetSharedEnvironmentAsync(userDataFolder, _settingsService);
            
            await _aiWebView.EnsureCoreWebView2Async(env);
            
            if (_aiWebView.CoreWebView2 == null)
            {
                throw new Exception("CoreWebView2 对象为 null");
            }

            // 注册桥接对象
            _aiApiBridge = new AiApiBridge(_settingsService, new MiniWorldBrowser.Helpers.BrowserController(_tabManager, this, _settingsService, CreateNewTabWithProtection));
            
            // 订阅流式输出事件
            _aiApiBridge.OnStreamChunk += (content, type) => {
                if (_aiWebView != null && _aiWebView.CoreWebView2 != null)
                {
                    try {
                        var json = System.Text.Json.JsonSerializer.Serialize(new { type = type, content = content });
                        _aiWebView.CoreWebView2.PostWebMessageAsJson(json);
                    } catch { }
                }
            };

            _aiWebView.CoreWebView2.AddHostObjectToScript("bridge", _aiApiBridge);
            
            // 监听设置变更
            _settingsService.SettingsChanged += () => {
                if (_aiWebView != null && _aiWebView.CoreWebView2 != null)
                {
                    this.Invoke(new Action(async () => {
                        try {
                            await _aiWebView.CoreWebView2.ExecuteScriptAsync("if(typeof updateModelName === 'function') updateModelName();");
                        } catch { }
                    }));
                }
                
                // 确保 UI 线程更新收藏栏和 AI 总结按钮
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        UpdateBookmarkBarVisibility();
                        UpdateAiSummarizeButtonUi();
                        try { Localization.Initialize(string.Equals(_settingsService.Settings.LanguageCode, "auto", StringComparison.OrdinalIgnoreCase) ? null : _settingsService.Settings.LanguageCode); } catch { }
                        RefreshLocalizedTexts();
                    }));
                }
                else
                {
                    UpdateBookmarkBarVisibility();
                    UpdateAiSummarizeButtonUi();
                    try { Localization.Initialize(string.Equals(_settingsService.Settings.LanguageCode, "auto", StringComparison.OrdinalIgnoreCase) ? null : _settingsService.Settings.LanguageCode); } catch { }
                    RefreshLocalizedTexts();
                }
            };

            // 拦截 AI 面板内的导航
            _aiWebView.CoreWebView2.NewWindowRequested += async (s, e) =>
            {
                var uri = e.Uri?.ToLower() ?? string.Empty;

                if (!e.IsUserInitiated)
                {
                    if (string.IsNullOrEmpty(uri) || uri == "about:blank" || uri.StartsWith("data:image") ||
                        uri.EndsWith(".jpg") || uri.EndsWith(".jpeg") || uri.EndsWith(".png") || uri.EndsWith(".gif") ||
                        uri.EndsWith(".webp") || uri.EndsWith(".bmp"))
                    {
                        e.Handled = true;
                        return;
                    }
                }

                bool isPopup = e.WindowFeatures.HasPosition || e.WindowFeatures.HasSize || !e.IsUserInitiated;
                if (e.Uri.Contains("weixin.qq.com") || e.Uri.Contains("graph.qq.com") || e.Uri.Contains("passport.baidu.com"))
                {
                    isPopup = true;
                }

                if (isPopup)
                {
                    e.Handled = true;
                    var env = _aiWebView.CoreWebView2.Environment;
                    var popup = new PopupWindow(env, e);
                    popup.Owner = this;
                    popup.Show();
                }
                else
                {
                    e.Handled = true;
                    if (!string.IsNullOrEmpty(e.Uri))
                    {
                        await CreateNewTabWithProtection(e.Uri);
                    }
                }
            };
            
            _aiWebView.CoreWebView2.NavigationStarting += async (s, e) =>
            {
                // 允许加载 AI 聊天界面本身或空白页
                if (e.Uri.StartsWith("file://") || e.Uri == "about:blank") return;
                
                // 允许加载设置中指定的自定义 AI 网页
                var settings = _settingsService.Settings;
                if (settings.AiServiceMode == 0 && !string.IsNullOrEmpty(settings.AiCustomWebUrl) && e.Uri.StartsWith(settings.AiCustomWebUrl)) return;
                if (settings.AiServiceMode == 0 && string.IsNullOrEmpty(settings.AiCustomWebUrl) && e.Uri.Contains("deepseek.com")) return;

                // 阻止其他所有导航，并在主浏览器标签中打开
                e.Cancel = true;
                await CreateNewTabWithProtection(e.Uri);
            };

            var settings = _settingsService.Settings;
            if (settings.AiServiceMode == 0) // 网页模式
            {
                _aiWebView.Source = new Uri(string.IsNullOrEmpty(settings.AiCustomWebUrl) ? "https://chat.deepseek.com/" : settings.AiCustomWebUrl);
            }
            else // API 模式
            {
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ai_chat.html");
                if (File.Exists(htmlPath))
                {
                    // 使用绝对路径 URI
                    _aiWebView.Source = new Uri(htmlPath);
                }
                else
                {
                    // 尝试从 exe 所在目录查找
                    string altPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? "", "Resources", "ai_chat.html");
                    if (File.Exists(altPath))
                    {
                        _aiWebView.Source = new Uri(altPath);
                    }
                    else
                    {
                        // 如果文件不存在，直接加载 HTML 字符串或显示错误
                        _aiWebView.NavigateToString(Localization.T("ai.missing_html", new Dictionary<string, string> { { "path1", htmlPath }, { "path2", altPath } }));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            string errorMsg = $"AI WebView 初始化失败: {ex.Message}";
            Debug.WriteLine(errorMsg);
            
            // 如果是常见的 HRESULT 错误，提供更详细的建议
            if (ex.Message.Contains("0x8007139F"))
            {
                errorMsg += Localization.Raw();
            }
            else if (ex.Message.Contains("WebView2Loader.dll"))
            {
                errorMsg += Localization.Raw();
            }

            // 只在 AI 面板显示时提示用户，或者记录到状态栏
            _statusLabel.Text = Localization.T("ai.init_failed");
            _statusLabel.ForeColor = Color.Red;
        }
    }

    private System.Windows.Forms.Timer? _aiSidePanelTimer;
    private int _aiSidePanelTargetWidth;
    private const int AISidePanelDefaultWidth = 380;

    private void ToggleAISidePanel()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ToggleAISidePanel));
            return;
        }

        bool show = !_aiSidePanel.Visible || _aiSidePanel.Width == 0;
        int targetWidth = show ? DpiHelper.Scale(AISidePanelDefaultWidth) : 0;
        _aiSidePanelTargetWidth = targetWidth;

        if (_aiSidePanelTimer == null)
        {
            _aiSidePanelTimer = new System.Windows.Forms.Timer { Interval = 10 };
            _aiSidePanelTimer.Tick += (s, e) =>
            {
                int currentWidth = _aiSidePanel.Width;
                int diff = _aiSidePanelTargetWidth - currentWidth;
                
                if (Math.Abs(diff) <= 1)
                {
                    _aiSidePanel.Width = _aiSidePanelTargetWidth;
                    _aiSidePanelTimer.Stop();
                    if (_aiSidePanelTargetWidth == 0)
                    {
                        _aiSidePanel.Visible = false;
                        _aiSplitter.Visible = false;
                    }
                    return;
                }

                int step = (int)(diff * 0.25);
                if (step == 0) step = diff > 0 ? 1 : -1;
                
                _aiSidePanel.Width = currentWidth + step;
            };
        }

        if (show)
        {
            if (!_aiSidePanel.Visible)
            {
                _aiSidePanel.Width = 0;
                _aiSidePanel.Visible = true;
                _aiSplitter.Visible = true;
            }

            if (_aiWebView.CoreWebView2 == null)
            {
                InitializeAIWebView();
            }
            else
            {
                var settings = _settingsService.Settings;
                bool isApiMode = settings.AiServiceMode == 1;
                bool currentlyApiPage = _aiWebView.Source.ToString().Contains("ai_chat.html");
                
                if (isApiMode != currentlyApiPage)
                {
                    InitializeAIWebView();
                }
            }
        }
        
        _aiSidePanelTimer.Start();
    }

    private void CloseAISidePanel()
    {
        if (_aiSidePanelTimer != null && _aiSidePanelTimer.Enabled)
        {
            _aiSidePanelTimer.Stop();
        }

        _aiSidePanelTargetWidth = 0;
        _aiSidePanel.Width = 0;
        _aiSidePanel.Visible = false;
        _aiSplitter.Visible = false;
    }

    private async void SummarizeCurrentPage()
    {
        if (_tabManager.ActiveTab == null) return;
        
        try
        {
            // 获取网页主要文本
            string script = @"
                (function() {
                    // 尝试获取主要内容，优先正文
                    const article = document.querySelector('article');
                    if (article) return article.innerText;
                    
                    const main = document.querySelector('main');
                    if (main) return main.innerText;
                    
                    return document.body.innerText;
                })()";
            
            string text = await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(script);
            
            // 处理返回的 JSON 字符串
            if (text.StartsWith("\"") && text.EndsWith("\""))
            {
                text = System.Text.RegularExpressions.Regex.Unescape(text.Substring(1, text.Length - 2));
            }

            // 如果文本太长，截断一下，防止注入失败
            if (text.Length > 3000) text = text.Substring(0, 3000) + "...";

            // 构造 AI 提示词
            string prompt = $"请帮我总结一下这个网页的主要内容：\\n\\n{text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")}";
            
            // 确保 AI 侧边栏显示
             if (!_aiSidePanel.Visible) ToggleAISidePanel();
             
             _tabManager.ActiveTab.IsTranslated = true;
             _translateBtn.Visible = true;
             
             var settings = _settingsService.Settings;
            if (settings.AiServiceMode == 0) // 网页模式 (针对 DeepSeek)
            {
                string aiScript = $@"
                    (function() {{
                        const textarea = document.querySelector('textarea');
                        if (textarea) {{
                            textarea.value = ""{prompt}"";
                            textarea.dispatchEvent(new Event('input', {{ bubbles: true }}));
                            
                            // 尝试自动点击发送按钮
                            setTimeout(() => {{
                                const sendBtn = document.querySelector('div[role=""button""][aria-label=""Send""]') || 
                                              document.querySelector('button[type=""submit""]') ||
                                              document.querySelector('.send-button'); // 备选选择器
                                if (sendBtn) sendBtn.click();
                            }}, 500);
                        }}
                    }})()";
                
                await _aiWebView.ExecuteScriptAsync(aiScript);
            }
            else // API 模式
                {
                    // 使用重试机制确保页面加载完成后能设置 Prompt
                    string aiScript = $@"
                        (function() {{
                            function trySetPrompt(count) {{
                                if (window.setAiPrompt) {{
                                    window.setAiPrompt(""{prompt}"", true);
                                }} else if (count > 0) {{
                                    setTimeout(() => trySetPrompt(count - 1), 500);
                                }}
                            }}
                            trySetPrompt(10);
                        }})()";
                    await _aiWebView.ExecuteScriptAsync(aiScript);
                }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"总结失败: {ex.Message}");
        }
    }

    private void OnAiSummarizeClickSummarize(object? sender, EventArgs e)
    {
        SummarizeCurrentPage();
    }

    private void OnAiSummarizeClickOpenSettings(object? sender, EventArgs e)
    {
        _ = CreateNewTabWithProtection("about:settings");
    }
    
    
    private void InitializeManagers()
    {
        _tabManager = new BrowserTabManager(
            _browserContainer, _tabContainer, _newTabButton, _tabOverflowBtn,
            _settingsService, _adBlockService, _historyService, _bookmarkService,
            _incognitoDataFolder);
        
        _tabManager.SetOverflowPanel(_tabOverflowPanel);
        
        _tabManager.ActiveTabChanged += OnActiveTabChanged;
        _tabManager.TabTitleChanged += t => { 
            if (t == _tabManager.ActiveTab) 
                Text = _isIncognito ? $"InPrivate - {t.Title} - {AppConstants.AppName}" : $"{t.Title} - {AppConstants.AppName}"; 
        };
        _tabManager.TabUrlChanged += OnTabUrlChanged;
        _tabManager.TabLoadingStateChanged += OnTabLoadingStateChanged;
        _tabManager.TabSecurityStateChanged += t => { if (t == _tabManager.ActiveTab) UpdateSecurityIcon(t.IsSecure); };
        _tabManager.TabStatusTextChanged += (t, text) => { 
            if (t == _tabManager.ActiveTab) 
            {
                if (string.IsNullOrEmpty(text))
                {
                    _statusLabel.Text = _isIncognito ? Localization.T("status.incognito") : Localization.T("status.ready");
                }
                else
                {
                    _statusLabel.Text = text;
                }
            }
        };
        _tabManager.TabZoomChanged += OnTabZoomChanged;
        _tabManager.TabTranslationRequested += t => { if (t == _tabManager.ActiveTab) TranslateCurrentPageWithAI(); };
        _tabManager.TabMediaExtractionRequested += (t, type) => { if (t == _tabManager.ActiveTab) OnMediaExtractionRequested(type); };
        _tabManager.NewWindowRequested += url => _ = CreateNewTabWithProtection(url, _settingsService.Settings.OpenLinksInBackground);
        _tabManager.NewWindowRequestedWithArgs += OnNewWindowRequestedWithArgs;
        _tabManager.SettingChanged += OnSettingChanged;
        _tabManager.WebViewClicked += ClosePopups;
        _tabManager.PasswordKeyButtonRequested += OnPasswordKeyButtonRequested;
        _tabManager.DownloadStarted += OnDownloadStarted;
        
        _mouseGesture = new MouseGesture(this);
        _mouseGesture.Enabled = _settingsService.Settings.EnableMouseGesture;
        _mouseGesture.GestureBack += () => _tabManager.ActiveTab?.GoBack();
        _mouseGesture.GestureForward += () => _tabManager.ActiveTab?.GoForward();
        _mouseGesture.GestureRefresh += () => _tabManager.ActiveTab?.Refresh();
        _mouseGesture.GestureClose += () => { if (_tabManager.ActiveTab != null) _tabManager.CloseTab(_tabManager.ActiveTab); };
        
        _fullscreenManager = new FullscreenManager(this, _tabBar, _toolbar, _bookmarkBar, _statusBar);
        _fullscreenManager.FullscreenChanged += isFullscreen => {
            if (!isFullscreen)
            {
                // 退出全屏时，重新检查收藏栏可见性
                UpdateBookmarkBarVisibility();
            }
        };
    }
    
    private void OnDownloadStarted(MiniWorldBrowser.Models.DownloadItem item)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnDownloadStarted(item)));
            return;
        }
        
        _downloadBtn.StartBounceAnimation();
        
        // 更新状态栏提示
        if (_statusLabel != null)
        {
            _statusLabel.Text = Localization.T("status.downloading", new Dictionary<string, string> { { "file", item.FileName } });
        }
    }

    private void InitializeEvents()
    {
        Load += (s, e) =>
        {
            // 设置任务栏分组 ID (AppUserModelID)
            // 隐身模式和普通模式使用不同的 ID，防止在任务栏合并
            string appId = _isIncognito ? "MiniWorldBrowser.Incognito" : "MiniWorldBrowser.Normal";
            Win32Helper.SetWindowAppUserModelId(this.Handle, appId);

            // 启动时默认最大化并露出任务栏
            var workArea = Screen.FromHandle(Handle).WorkingArea;
            int borderX = SystemInformation.FrameBorderSize.Width + SystemInformation.Border3DSize.Width;
            int borderY = SystemInformation.FrameBorderSize.Height + SystemInformation.Border3DSize.Height;
            
            MaximizedBounds = new Rectangle(
                workArea.X - borderX,
                workArea.Y - borderY,
                workArea.Width + borderX * 2,
                workArea.Height + borderY * 2
            );
            WindowState = FormWindowState.Maximized;
            if (_maximizeBtn != null) _maximizeBtn.Text = "❐";

            try
            {
                _bossKey = new BossKey(this);
                SetupKeyboardHook();
                
                // 确保启动时收藏栏状态正确
                UpdateBookmarkBarVisibility();
                
                // 启动光标更新定时器
                _cursorTimer = new System.Windows.Forms.Timer { Interval = 50 };
                _cursorTimer.Tick += (s, e) => 
                {
                    if (!IsDisposed && IsHandleCreated)
                        UpdateCursorStyle();
                };
                _cursorTimer.Start();
            }
            catch { }
        };
        
        // 窗体失去焦点时关闭菜单和下拉框
        Deactivate += (s, e) => 
        { 
            try
            {
                // 检查鼠标是否在缩放按钮区域内
                bool inZoomButton = false;
                if (_zoomPanel != null && _zoomPanel.IsHandleCreated && !_zoomPanel.IsDisposed)
                {
                    var mousePos = _zoomPanel.PointToClient(Control.MousePosition);
                    var minusRect = new Rectangle(120, 5, 32, 24);
                    var plusRect = new Rectangle(206, 5, 32, 24);
                    inZoomButton = minusRect.Contains(mousePos) || plusRect.Contains(mousePos);
                }
                
                // 如果在缩放按钮区域内，设置重新打开标志
                if (inZoomButton)
                {
                    _reopenMenuAfterZoom = true;
                }
                
                // 如果不需要重新打开菜单，关闭菜单
                if (!_reopenMenuAfterZoom)
                    CloseMainMenu(); 
                    
                // 如果下拉框正在交互，不要隐藏它
                if (_addressDropdown != null && !_addressDropdown.IsDisposed && !_addressDropdown.IsInteracting)
                    _addressDropdown.Hide();

                // 窗体失焦时关闭用户信息弹窗
                if (!_suppressUserInfoPopupClose && _userInfoPopup != null && !_userInfoPopup.IsDisposed)
                {
                    // 如果鼠标在弹窗范围内，说明正在与弹窗交互，不要关闭
                    if (_userInfoPopup.Bounds.Contains(Control.MousePosition))
                        return;

                    if (Form.ActiveForm != _userInfoPopup)
                        CloseUserInfoPopup();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Deactivate: {ex.Message}");
            }
        };
        
        // 点击窗体其他区域时关闭菜单和下拉框
        MouseDown += (s, e) => ClosePopups();
        _browserContainer.MouseDown += (s, e) => ClosePopups();
        _tabBar.MouseDown += (s, e) => ClosePopups();
        _toolbar.MouseDown += (s, e) => ClosePopups();
        _tabContainer.MouseDown += (s, e) => ClosePopups();
        _statusBar.MouseDown += (s, e) => ClosePopups();
        _bookmarkBar.MouseDown += (s, e) => ClosePopups();
        
        // 窗口关闭前保存会话
        FormClosing += (s, e) =>
        {
            try
            {
                // 隐身模式不保存会话
                if (_isIncognito) return;

                // 保存当前所有标签页的URL（用于"继续浏览上次"功能）
                if (_tabManager != null && _settingsService?.Settings != null)
                {
                    var realUrls = _tabManager.Tabs
                        .Select(t => t.Url)
                        .Where(url => !string.IsNullOrEmpty(url) && !url.StartsWith("about:") && !url.StartsWith("data:"))
                        .ToList();
                    
                    _settingsService.Settings.LastSessionUrls = realUrls;
                    _settingsService.Save();
                }
            }
            catch { }
        };
        
        FormClosed += (s, e) =>
        {
            if (_isIncognito && !string.IsNullOrEmpty(_incognitoDataFolder))
            {
                // 尝试清理隐身模式数据目录
                // 注意：WebView2 进程可能还未完全退出，所以可能无法立即删除
                // 这里我们只是尽力而为，或者可以注册一个延迟清理任务
                Task.Run(async () => {
                    await Task.Delay(1000); // 等待 WebView2 释放文件
                    try
                    {
                        if (Directory.Exists(_incognitoDataFolder))
                            Directory.Delete(_incognitoDataFolder, true);
                    }
                    catch { }
                });
            }

            try
            {
                // 停止所有定时器
                _cursorTimer?.Stop();
                _cursorTimer?.Dispose();
                _cursorTimer = null;
                
                _memoryTimer?.Stop();
                _memoryTimer?.Dispose();
                _memoryTimer = null;
                
                _adPopupTimer?.Stop();
                _adPopupTimer?.Dispose();
                _adPopupTimer = null;
                
                _bossKey?.Dispose();
                RemoveKeyboardHook();
                
                // 清理所有标签页
                if (_tabManager != null)
                {
                    foreach (var tab in _tabManager.Tabs.ToList())
                    {
                        try { tab.Dispose(); } catch { }
                    }
                }
                
                // 保存历史记录
                if (_historyService is IDisposable disposable)
                    disposable.Dispose();
            }
            catch { }
        };
        
        // 窗口边框鼠标样式变换
        MouseMove += OnFormMouseMove;
        
        // 导航按钮
        _backBtn.Click += (s, e) => _tabManager?.ActiveTab?.GoBack();
        _forwardBtn.Click += (s, e) => _tabManager?.ActiveTab?.GoForward();
        _refreshBtn.Click += (s, e) => _tabManager?.ActiveTab?.Refresh();
        _stopBtn.Click += (s, e) => _tabManager?.ActiveTab?.Stop();
        _homeBtn.Click += (s, e) => _tabManager?.ActiveTab?.Navigate(_settingsService.Settings.HomePage);
        _downloadBtn.Click += OnDownloadButtonClick;
        _settingsBtn.Click += (s, e) => ShowMainMenu();
        _bookmarkBtn.BookmarkClicked += (s, e) => ToggleBookmark();
        _newTabButton.Click += async (s, e) => {
            await CreateNewTabWithProtection("about:newtab");
        };
        
        // 地址栏
        _addressBar.EnterKeyPressed += (s, e) => NavigateToAddress();
        _addressBar.TextChanged += (s, e) => { 
            if (_addressBar.Focused && !_isInternalAddressUpdate) 
                ShowAddressDropdown(); 
        };
        _addressBar.GotFocus += (s, e) => { 
            if (!_isInternalAddressUpdate)
            {
                _addressBar.SelectAll(); 
                
                // 只有当用户通过鼠标或快捷键主动进入地址栏时才显示下拉框
                // 避免切换标签页或新建标签页时自动弹出
                if (Control.MouseButtons != MouseButtons.None || ModifierKeys != Keys.None)
                {
                    ShowAddressDropdown(); 
                }
            }
        };
        _addressBar.LostFocus += (s, e) => 
        {
            // 延迟检查，给按钮点击事件足够时间处理
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
        
        // 键盘快捷键
        KeyPreview = true;
        KeyDown += OnKeyDown;
        
        // 内存释放定时器（仅在窗口激活时执行，降低频率）
        _memoryTimer = new System.Windows.Forms.Timer { Interval = 300000 }; // 5分钟
        _memoryTimer.Tick += (s, e) => 
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (WindowState == FormWindowState.Minimized) return;
                GC.Collect(0, GCCollectionMode.Optimized);
            }
            catch { }
        };
        _memoryTimer.Start();
        
        // 窗口状态变化时的处理
        Resize += (s, e) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                // 最小化时减少资源占用
                GC.Collect(0, GCCollectionMode.Optimized);
            }
            else if (WindowState == FormWindowState.Normal)
            {
                // 从其他状态恢复到 Normal 时，强制刷新布局以消除间隙
                _tabManager?.UpdateTabLayout();
                this.PerformLayout();
            }
        };
    }
    
    #endregion
    
    private void RefreshLocalizedTexts()
    {
        try
        {
            _backBtn.Text = Localization.T("nav.back");
            _forwardBtn.Text = Localization.T("nav.forward");
            _refreshBtn.Text = Localization.T("nav.refresh");
            _stopBtn.Text = Localization.T("nav.stop");
            _homeBtn.Text = Localization.T("nav.home");
            
            new ToolTip().SetToolTip(_newTabButton, Localization.T("tooltips.new_tab"));
            new ToolTip().SetToolTip(_tabOverflowBtn, Localization.T("tooltips.search_tabs"));
            new ToolTip().SetToolTip(_downloadBtn, Localization.T("tooltips.download"));
            new ToolTip().SetToolTip(_userBtn, Localization.T("tooltips.user_login"));
            new ToolTip().SetToolTip(_translateBtn, Localization.T("tooltips.translate_page"));
            new ToolTip().SetToolTip(_zoomBtn, Localization.T("tooltips.zoom"));
            new ToolTip().SetToolTip(_passwordKeyBtn, Localization.T("tooltips.manage_passwords"));
            
            _statusLabel.Text = _isIncognito ? Localization.T("status.incognito") : Localization.T("status.ready");
            _aiSummarizeBtn.Text = _settingsService.Settings.AiServiceMode == 1 ? Localization.T("ai.summarize") : Localization.T("ai.mode");
            
            _tabManager?.RefreshTabTitles();
        }
        catch { }
    }
    
    #region 登录功能

    private void RefreshLoginStatus()
    {
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

    private void CloseUserInfoPopup()
    {
        if (_userInfoPopup != null && !_userInfoPopup.IsDisposed)
        {
            _userInfoPopup.Close();
            _userInfoPopup = null;
            _lastUserInfoPopupCloseTime = DateTime.Now;
        }
    }

    private void OnUserButtonClick(object? sender, EventArgs e)
    {
        _userBtn.Focus();

        CloseAISidePanel();
        CloseDownloadDialog();

        if (_userInfoPopup != null && !_userInfoPopup.IsDisposed && _userInfoPopup.Visible)
        {
            CloseUserInfoPopup();
            return;
        }

        if ((DateTime.Now - _lastUserInfoPopupCloseTime).TotalMilliseconds < 200)
        {
            return;
        }

        _suppressUserInfoPopupClose = true;
        _userInfoPopup = new UserInfoPopup(_loginService, StartLoginFlow, HandleLogout);

        var location = CalculatePopupLocationBelow(_userBtn, _userInfoPopup.Size, DpiHelper.Scale(5));
        _userInfoPopup.Location = location;

        _userInfoPopup.FormClosed += (s, ev) => _userInfoPopup = null;
        _userInfoPopup.Show(this);

        BeginInvoke(() => _suppressUserInfoPopupClose = false);
    }

    private void OnAiButtonClick(object? sender, EventArgs e)
    {
        CloseUserInfoPopup();
        CloseDownloadDialog();

        if (_aiSidePanel.Visible && _aiSidePanel.Width > 0)
        {
            CloseAISidePanel();
            return;
        }

        ToggleAISidePanel();
    }

    private void OnDownloadButtonClick(object? sender, EventArgs e)
    {
        CloseUserInfoPopup();
        CloseAISidePanel();
        OpenDownloadDialog();
    }

    private Point CalculatePopupLocationBelow(Control anchor, Size popupSize, int offsetY)
    {
        var screenPos = anchor.PointToScreen(new Point(0, anchor.Height));
        int x = screenPos.X - (popupSize.Width - anchor.Width) / 2;
        int y = screenPos.Y + DpiHelper.Scale(offsetY);

        var screen = Screen.FromControl(this);

        if (x < screen.WorkingArea.Left)
            x = screen.WorkingArea.Left + DpiHelper.Scale(4);
        if (x + popupSize.Width > screen.WorkingArea.Right)
            x = screen.WorkingArea.Right - popupSize.Width - DpiHelper.Scale(4);
        if (y + popupSize.Height > screen.WorkingArea.Bottom)
            y = screen.WorkingArea.Bottom - popupSize.Height - DpiHelper.Scale(4);

        return new Point(x, y);
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
            await CreateNewTabWithProtection(loginUrl);

            // 3. 开始轮询（在后台静默进行，不再显示进度弹窗）
            var token = await _loginService.PollTokenAsync(encodedNonce, _loginCts.Token);

            if (token != null)
            {
                ShowModernMessage("提示", "登录成功！", ModernDialogIcon.Success);
            }
        }
        catch (OperationCanceledException)
        {
            // 用户取消
        }
        catch (Exception ex)
        {
            ShowModernMessage("错误", $"登录失败: {ex.Message}", ModernDialogIcon.Error);
        }
        finally
        {
            _loginCts?.Dispose();
            _loginCts = null;
        }
    }

    private async void HandleLogout()
    {
        if (ShowModernConfirm("确认", "确定要退出登录吗？", "退出", "取消") == DialogResult.OK)
        {
            try
            {
                bool success = await _loginService.LogoutAsync();
                if (success)
                {
                    // 状态刷新已由 LoginStateChanged 事件处理
                }
                else
                {
                    // 即使服务器返回失败（如 Token 已失效），本地也已强制退出
                }
            }
            catch (Exception ex)
            {
                ShowModernMessage("错误", $"退出登录时发生错误：{ex.Message}", ModernDialogIcon.Error);
            }
        }
    }

    private enum ModernDialogIcon
    {
        Info,
        Success,
        Warning,
        Error,
        Question
    }

    private DialogResult ShowModernConfirm(string title, string message, string okText, string cancelText)
    {
        using var dlg = new ModernDialog(title, message, ModernDialogIcon.Question, okText, cancelText);
        return dlg.ShowDialog(this);
    }

    private void ShowModernMessage(string title, string message, ModernDialogIcon icon)
    {
        using var dlg = new ModernDialog(title, message, icon, "确定", null);
        dlg.ShowDialog(this);
    }

    private sealed class ModernButton : Button
    {
        public int CornerRadius { get; set; } = 8;
        private bool _isHovered;
        private bool _isPressed;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;

            MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            MouseLeave += (s, e) => { _isHovered = false; _isPressed = false; Invalidate(); };
            MouseDown += (s, e) => { _isPressed = true; Invalidate(); };
            MouseUp += (s, e) => { _isPressed = false; Invalidate(); };
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width <= 0 || Height <= 0) return;
            using var path = CreateRoundedRectPath(new RectangleF(0, 0, Width, Height), CornerRadius);
            Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            var bgColor = _isPressed ? Color.FromArgb(0, 80, 150) : 
                         (_isHovered ? Color.FromArgb(0, 100, 180) : BackColor);

            if (bgColor == Color.Transparent && BackColor == Color.Transparent)
            {
                // 如果是透明背景（如取消按钮），使用特定颜色
                bgColor = _isHovered ? Color.FromArgb(240, 240, 240) : Color.FromArgb(248, 249, 250);
            }

            using (var path = CreateRoundedRectPath(rect, CornerRadius))
            {
                // 填充背景
                using (var brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                }

                // 如果有边框要求（如取消按钮）
                if (FlatAppearance.BorderSize > 0)
                {
                    using var pen = new Pen(FlatAppearance.BorderColor, FlatAppearance.BorderSize);
                    g.DrawPath(pen, path);
                }
            }

            // 绘制文字
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor, 
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class ModernDialog : Form
    {
        private const int CornerRadius = 12;

        public ModernDialog(string title, string message, ModernDialogIcon icon, string okText, string? cancelText)
        {
            Text = string.Empty;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));
            ClientSize = DpiHelper.Scale(new Size(360, cancelText == null ? 210 : 220));
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            var titleLabel = new Label
            {
                Text = title,
                Location = DpiHelper.Scale(new Point(20, 18)),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(11F), FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            var closeLabel = new Label
            {
                Text = "×",
                Location = new Point(ClientSize.Width - DpiHelper.Scale(36), DpiHelper.Scale(12)),
                Size = DpiHelper.Scale(new Size(24, 24)),
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(12F)),
                ForeColor = Color.FromArgb(140, 140, 140),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            closeLabel.MouseEnter += (s, e) => closeLabel.ForeColor = Color.FromArgb(80, 80, 80);
            closeLabel.MouseLeave += (s, e) => closeLabel.ForeColor = Color.FromArgb(140, 140, 140);
            closeLabel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            int iconSize = DpiHelper.Scale(40);
            var iconBox = new Panel
            {
                Location = DpiHelper.Scale(new Point(20, 56)),
                Size = new Size(iconSize, iconSize),
                BackColor = Color.Transparent
            };
            iconBox.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // 改进图标抗锯齿
                
                using var brush = new SolidBrush(GetIconBackColor(icon));
                e.Graphics.FillEllipse(brush, 0, 0, iconSize, iconSize);

                using var textBrush = new SolidBrush(Color.White);
                using var iconFont = new Font("Segoe UI Symbol", DpiHelper.ScaleFont(16F), FontStyle.Bold);
                var ch = GetIconChar(icon);
                
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(ch, iconFont, textBrush, new RectangleF(0, 0, iconSize, iconSize), sf);
            };

            var messageLabel = new Label
            {
                Text = message,
                Location = DpiHelper.Scale(new Point(72, 56)),
                Size = new Size(ClientSize.Width - DpiHelper.Scale(92), DpiHelper.Scale(96)),
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9.5F)),
                ForeColor = Color.FromArgb(70, 70, 70)
            };

            var okBtn = new ModernButton
            {
                Text = okText,
                Size = DpiHelper.Scale(new Size(cancelText == null ? 120 : 110, 34)),
                Location = cancelText == null ? 
                    new Point(ClientSize.Width - DpiHelper.Scale(140), ClientSize.Height - DpiHelper.Scale(54)) : 
                    new Point(ClientSize.Width - DpiHelper.Scale(240), ClientSize.Height - DpiHelper.Scale(54)),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                CornerRadius = DpiHelper.Scale(8)
            };
            okBtn.Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F), FontStyle.Bold);
            okBtn.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(titleLabel);
            Controls.Add(closeLabel);
            Controls.Add(iconBox);
            Controls.Add(messageLabel);
            Controls.Add(okBtn);

            if (cancelText != null)
            {
                var cancelBtn = new ModernButton
                {
                    Text = cancelText,
                    Size = DpiHelper.Scale(new Size(110, 34)),
                    Location = new Point(ClientSize.Width - DpiHelper.Scale(120), ClientSize.Height - DpiHelper.Scale(54)),
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(60, 60, 60),
                    CornerRadius = DpiHelper.Scale(8)
                };
                cancelBtn.FlatAppearance.BorderSize = 1;
                cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(230, 230, 230);
                cancelBtn.Click += (s, e) =>
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                };
                Controls.Add(cancelBtn);
                CancelButton = cancelBtn;
            }
            else
            {
                CancelButton = okBtn;
            }

            AcceptButton = okBtn;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width <= 0 || Height <= 0) return;
            // 扩大 Region 1像素，确保抗锯齿边缘不被硬裁剪
            using var path = CreateRoundedRectPath(new Rectangle(-1, -1, Width + 2, Height + 2), DpiHelper.Scale(CornerRadius) + 1);
            Region = new Region(path);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. 填充背景
            using (var brush = new SolidBrush(BackColor))
            {
                using var fillPath = CreateRoundedRectPath(new Rectangle(0, 0, Width, Height), DpiHelper.Scale(CornerRadius));
                g.FillPath(brush, fillPath);
            }

            // 2. 绘制边框
            float penWidth = DpiHelper.Scale(1f);
            using var path = CreateRoundedRectPath(new RectangleF(penWidth / 2f, penWidth / 2f, Width - penWidth, Height - penWidth), DpiHelper.Scale(CornerRadius));
            using var pen = new Pen(Color.FromArgb(220, 220, 220), penWidth);
            g.DrawPath(pen, path);
        }

        private static string GetIconChar(ModernDialogIcon icon)
        {
            return icon switch
            {
                ModernDialogIcon.Success => "✓",
                ModernDialogIcon.Warning => "!",
                ModernDialogIcon.Error => "✕",
                ModernDialogIcon.Question => "?",
                _ => "i"
            };
        }

        private static Color GetIconBackColor(ModernDialogIcon icon)
        {
            return icon switch
            {
                ModernDialogIcon.Success => Color.FromArgb(34, 197, 94),
                ModernDialogIcon.Warning => Color.FromArgb(245, 158, 11),
                ModernDialogIcon.Error => Color.FromArgb(239, 68, 68),
                ModernDialogIcon.Question => Color.FromArgb(59, 130, 246),
                _ => Color.FromArgb(0, 120, 215)
            };
        }
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var path = CreateRoundedRectPath(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region = new Region(path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float d = radius * 2;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        return CreateRoundedRectPath(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
    }

    #endregion

    #region 辅助方法
    
    private RoundedButton CreateToolButton(string text, string tooltip)
    {
        var btn = new RoundedButton
        {
            Size = DpiHelper.Scale(new Size(32, 32)),
            Text = text,
            Font = new Font("Segoe UI", DpiHelper.ScaleFont(11F)),
            Margin = DpiHelper.Scale(new Padding(2))
        };
        new ToolTip().SetToolTip(btn, tooltip);
        return btn;
    }
    
    private NavigationButton CreateNavigationButton(NavigationButtonType type, string tooltip)
    {
        var btn = new NavigationButton
        {
            Size = DpiHelper.Scale(new Size(32, 32)),
            ButtonType = type,
            Margin = DpiHelper.Scale(new Padding(2)),
            IconColor = Color.FromArgb(80, 80, 80)
        };
        new ToolTip().SetToolTip(btn, tooltip);
        return btn;
    }
    
    private Button CreateWindowControlButton(string text)
    {
        var btn = new Button
        {
            Width = DpiHelper.Scale(46),
            Height = DpiHelper.Scale(36), // 显式设置高度匹配 _tabBar
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            Text = text,
            Font = new Font("Segoe UI", DpiHelper.ScaleFont(10F)),
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            ForeColor = Color.Black,
            TabStop = false,
            Margin = Padding.Empty, // 移除边距
            Padding = Padding.Empty // 移除内边距
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 180, 180);
        btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(200, 200, 200);
        btn.MouseLeave += (s, e) => btn.BackColor = Color.Transparent;
        return btn;
    }
    
    private void RefreshAllControls()
    {
        _securityIcon?.Refresh();
        _bookmarkBtn?.Refresh();
        _bookmarkBar?.Refresh();
        foreach (Control ctrl in _tabContainer.Controls)
            ctrl.Refresh();
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
            
            // 延迟刷新布局以解决取消最大化时的间隙问题
            BeginInvoke(new Action(() => {
                this.PerformLayout();
                _tabManager?.UpdateTabLayout();
                RefreshAllControls();
            }));
        }
        else
        {
            // 获取工作区域
            var workArea = Screen.FromHandle(Handle).WorkingArea;
            
            // 获取系统边框大小（WS_THICKFRAME 边框）
            int borderX = SystemInformation.FrameBorderSize.Width + SystemInformation.Border3DSize.Width;
            int borderY = SystemInformation.FrameBorderSize.Height + SystemInformation.Border3DSize.Height;
            
            // 扩展边界以覆盖隐藏的边框
            MaximizedBounds = new Rectangle(
                workArea.X - borderX,
                workArea.Y - borderY,
                workArea.Width + borderX * 2,
                workArea.Height + borderY * 2
            );
            WindowState = FormWindowState.Maximized;
            _maximizeBtn.Text = "❐";
        }
    }
    
    private void UpdateSecurityIcon(bool isSecure)
    {
        _securityIcon.IsSecure = isSecure;
        _securityIcon.CurrentUrl = _tabManager?.ActiveTab?.Url ?? "";
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
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateBookmarkButton(isBookmarked)));
            return;
        }
        _bookmarkBtn.IsBookmarked = isBookmarked;
    }

    /// <summary>
    /// 更新当前标签页的收藏状态按钮
    /// </summary>
    private void UpdateCurrentTabBookmarkState()
    {
        var url = _tabManager.ActiveTab?.Url;
        if (string.IsNullOrEmpty(url))
        {
            UpdateBookmarkButton(false);
            return;
        }
        var isBookmarked = _bookmarkService.FindByUrl(url) != null;
        UpdateBookmarkButton(isBookmarked);
    }
    
    private void OpenDownloadDialog()
    {
        try
        {
            // 工具栏下载按钮：呼出 WebView2 的默认下载浮窗（与点击链接弹出一致）
            if (_downloadPanel != null) _downloadPanel.Visible = false; // 确保不与自定义面板重叠

            var core = _tabManager.ActiveTab?.WebView?.CoreWebView2;
            if (core != null)
            {
                try
                {
                    var mi = core.GetType().GetMethod("OpenDefaultDownloadDialog");
                    if (mi != null)
                    {
                        mi.Invoke(core, null);
                        return;
                    }
                }
                catch { }
            }

            // 兼容旧版 SDK：无法打开浮窗时退化为打开下载目录
            var path = _settingsService.Settings.DownloadPath;
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            try { Process.Start("explorer.exe", $"\"{path}\""); }
            catch
            {
                try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
                catch { }
            }
        }
        catch { }
    }

    private void CloseDownloadDialog()
    {
        if (_downloadPanel != null) _downloadPanel.Visible = false;
    }
    
    #endregion
    
    #region 事件处理
    
    private void OnActiveTabChanged(BrowserTab tab)
    {
        _isInternalAddressUpdate = true;
        try
        {
            _addressBar.Text = tab.Url ?? "";
        }
        finally
        {
            _isInternalAddressUpdate = false;
        }
        
        Text = $"{tab.Title ?? "新标签页"} - {AppConstants.AppName}";
        UpdateSecurityIcon(tab.IsSecure);
        UpdateNavigationButtons();
        _refreshBtn.Visible = !tab.IsLoading;
        _stopBtn.Visible = tab.IsLoading;
        _progressBar.Visible = tab.IsLoading;
        _translateBtn.Visible = tab.IsTranslated;
        UpdateCurrentTabBookmarkState();

        // 切换标签时确保缩放倍数正确，防止因 DPI 感知导致的不同标签页缩放不一致
        if (tab.WebView?.CoreWebView2 != null)
        {
            tab.WebView.ZoomFactor = _zoomLevel;
        }
    }
    
    private void OnTabUrlChanged(BrowserTab tab)
    {
        if (tab != _tabManager.ActiveTab) return;
        
        // URL 变化时，如果不是翻译后的 URL，重置翻译状态
        // 百度/必应翻译的 URL 通常包含其域名
        if (!string.IsNullOrEmpty(tab.Url) && 
            !tab.Url.Contains("fanyi.baidu.com") && 
            !tab.Url.Contains("bing.com/translator"))
        {
            tab.IsTranslated = false;
        }

        _translateBtn.Visible = tab.IsTranslated;
        
        _isInternalAddressUpdate = true;
        try
        {
            _addressBar.Text = tab.Url ?? "";
        }
        finally
        {
            _isInternalAddressUpdate = false;
        }

        if (!string.IsNullOrEmpty(tab.Url) && !_urlHistory.Contains(tab.Url))
        {
            _urlHistory.Insert(0, tab.Url);
            if (_urlHistory.Count > AppConstants.MaxUrlHistoryItems)
                _urlHistory.RemoveAt(_urlHistory.Count - 1);
        }
        
        var isBookmarked = _bookmarkService.FindByUrl(tab.Url ?? "") != null;
        UpdateBookmarkButton(isBookmarked);
        
        // URL 变化时隐藏钥匙图标
        HidePasswordKeyButton();
        
        // URL 变化时根据状态显示/隐藏翻译按钮
        _translateBtn.Visible = tab.IsTranslated;
    }
    
    private void OnTabLoadingStateChanged(BrowserTab tab)
    {
        if (tab != _tabManager.ActiveTab) return;
        
        _progressBar.Visible = tab.IsLoading;
        _statusLabel.Text = tab.IsLoading ? "加载中..." : "就绪";
        _refreshBtn.Visible = !tab.IsLoading;
        _stopBtn.Visible = tab.IsLoading;
        UpdateNavigationButtons();
    }
    
    #endregion
    
    #region 辅助方法
    
    private static string GetFullExceptionMessage(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"初始化失败: {ex.Message}");
        
        var inner = ex.InnerException;
        int level = 1;
        while (inner != null && level <= 5)
        {
            sb.AppendLine($"\n内部错误 {level}: {inner.Message}");
            inner = inner.InnerException;
            level++;
        }
        
        sb.AppendLine($"\n堆栈跟踪:\n{ex.StackTrace}");
        sb.AppendLine("\n请确保已安装 Microsoft Edge WebView2 Runtime。");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 当检测到密码时，显示钥匙图标
    /// </summary>
    private void OnPasswordKeyButtonRequested(string host, string username, string password)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnPasswordKeyButtonRequested(host, username, password));
            return;
        }
        
        // 存储待保存的密码信息
        _pendingPasswordInfo = (host, username, password);
        _isPasswordSaved = false;
        
        // 显示钥匙图标
        _passwordKeyBtn.Visible = true;
        
        // 自动显示询问弹窗（立即响应）
        ShowPasswordPopup(false);
    }
    
    /// <summary>
    /// 钥匙图标点击事件
    /// </summary>
    private void OnPasswordKeyButtonClick(object? sender, EventArgs e)
    {
        // 如果密码已保存，显示"已保存"弹窗；否则显示"询问"弹窗
        ShowPasswordPopup(_isPasswordSaved);
    }
    
    /// <summary>
    /// 显示密码保存弹窗
    /// </summary>
    /// <param name="showSavedMode">true=显示已保存模式，false=显示询问模式</param>
    private void ShowPasswordPopup(bool showSavedMode)
    {
        if (!_pendingPasswordInfo.HasValue) return;
        
        var (host, username, password) = _pendingPasswordInfo.Value;
        
        // 计算弹窗位置（在钥匙按钮下方）
        var location = new Point(_passwordKeyBtn.Width, _passwordKeyBtn.Height);
        
        _tabManager.ShowPasswordPopup(host, username, password, _passwordKeyBtn, location, showSavedMode, (saved, neverSave) =>
        {
            // 回调：密码保存状态变化
            if (saved)
            {
                _isPasswordSaved = true;
                // 保存后钥匙图标保持显示，点击可查看已保存信息
            }
            else if (neverSave)
            {
                // 选择"一律不"后隐藏钥匙图标
                BeginInvoke(() => HidePasswordKeyButton());
            }
        });
    }
    
    /// <summary>
    /// 隐藏钥匙图标
    /// </summary>
    private void HidePasswordKeyButton()
    {
        _passwordKeyBtn.Visible = false;
        _pendingPasswordInfo = null;
        _isPasswordSaved = false;
    }

    /// <summary>
    /// 打开资源加载日志
    /// </summary>
    private void ShowResourceLog()
    {
        try
        {
            var logPath = Path.Combine(AppConstants.UserDataFolder, "webview2_resource_log.txt");
            if (File.Exists(logPath))
            {
                Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(Localization.Raw(), Localization.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(Localization.T("clear.failed", new Dictionary<string, string> { { "msg", ex.Message } }), Localization.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
                _settingsService.Settings.AlwaysShowBookmarkBar = !(bool)value;
                UpdateBookmarkBarVisibility();
                break;
            case "bookmarkbar":
                // 显示收藏栏：value 为 true 时显示，false 时隐藏
                _settingsService.Settings.AlwaysShowBookmarkBar = (bool)value;
                UpdateBookmarkBarVisibility();
                break;
            case "adblock":
                _adBlockService.Enabled = (bool)value;
                break;
            case "adblockmode":
                var mode = (int)value;
                _adBlockService.Mode = mode;
                _adBlockService.Enabled = mode > 0;
                break;
            case "gesture":
                // 鼠标手势
                _mouseGesture.Enabled = (bool)value;
                _settingsService.Settings.EnableMouseGesture = (bool)value;
                _settingsService.Save();
                break;
            case "superdrag":
                // 超级拖拽
                _settingsService.Settings.EnableSuperDrag = (bool)value;
                _settingsService.Save();
                break;
            case "homebutton":
                // 显示主页按钮
                _homeBtn.Visible = (bool)value;
                break;
            case "language":
                try
                {
                    var languageCode = value?.ToString();
                    Localization.Initialize(string.Equals(languageCode, "auto", StringComparison.OrdinalIgnoreCase) ? null : languageCode);
                }
                catch { }
                RefreshLocalizedTexts();
                break;
        }
    }
    
    private void OnTranslateButtonClick(object? sender, EventArgs e)
    {
        if (_tabManager.ActiveTab == null) return;
        
        var currentUrl = _tabManager.ActiveTab.Url;
        if (string.IsNullOrEmpty(currentUrl) || currentUrl.StartsWith("about:") || currentUrl.StartsWith("data:"))
        {
            ShowModernMessage(Localization.T("common.info"), Localization.Raw(), ModernDialogIcon.Info);
            return;
        }

        // 创建翻译选项菜单
        var menu = new ContextMenuStrip();
        
        var aiItem = new ToolStripMenuItem(Localization.Raw(), null, (s, ev) => TranslateCurrentPageWithAI());
        aiItem.Font = new Font(aiItem.Font, FontStyle.Bold);
        
        var baiduItem = new ToolStripMenuItem(Localization.Raw(), null, (s, ev) => {
             string translateUrl = $"https://fanyi.baidu.com/transpage?query={Uri.EscapeDataString(currentUrl)}&from=auto&to=zh&source=url&render=1";
             _tabManager.ActiveTab.IsTranslated = true;
             _translateBtn.Visible = true;
             _tabManager.ActiveTab.Navigate(translateUrl);
         });
 
         var bingItem = new ToolStripMenuItem(Localization.Raw(), null, (s, ev) => {
             string translateUrl = $"https://www.bing.com/translator/?to=zh-Hans&url={Uri.EscapeDataString(currentUrl)}";
             _tabManager.ActiveTab.IsTranslated = true;
             _translateBtn.Visible = true;
             _tabManager.ActiveTab.Navigate(translateUrl);
         });

        menu.Items.Add(aiItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(baiduItem);
        menu.Items.Add(bingItem);

        menu.Show(_translateBtn, new Point(0, _translateBtn.Height));
    }
    
    private async void TranslateCurrentPageWithAI()
    {
        if (_tabManager.ActiveTab == null) return;
        
        _tabManager.ActiveTab.IsTranslated = true;
        _translateBtn.Visible = true;
        
        try
        {
            // 获取网页主要文本
            string script = @"
                (function() {
                    // 尝试获取主要内容，优先正文
                    const article = document.querySelector('article');
                    if (article) return article.innerText;
                    
                    const main = document.querySelector('main');
                    if (main) return main.innerText;
                    
                    return document.body.innerText;
                })()";
            
            string text = await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(script);
            
            // 处理返回的 JSON 字符串
            if (text.StartsWith("\"") && text.EndsWith("\""))
            {
                text = System.Text.RegularExpressions.Regex.Unescape(text.Substring(1, text.Length - 2));
            }

            // 如果文本太长，截断一下，防止注入失败
            if (text.Length > 3000) text = text.Substring(0, 3000) + "...";

            // 构造 AI 提示词
            string prompt = Localization.T("ai.translate_prompt", new Dictionary<string, string> { { "text", text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") } });
            
            // 确保 AI 侧边栏显示
             if (!_aiSidePanel.Visible) ToggleAISidePanel();
             
             _tabManager.ActiveTab.IsTranslated = true;
             _translateBtn.Visible = true;
             
             var settings = _settingsService.Settings;
            if (settings.AiServiceMode == 0) // 网页模式
            {
                string aiScript = $@"
                    (function() {{
                        const textarea = document.querySelector('textarea');
                        if (textarea) {{
                            textarea.value = ""{prompt}"";
                            textarea.dispatchEvent(new Event('input', {{ bubbles: true }}));
                            
                            setTimeout(() => {{
                                const sendBtn = document.querySelector('div[role=""button""][aria-label=""Send""]') || 
                                              document.querySelector('button[type=""submit""]') ||
                                              document.querySelector('.send-button');
                                if (sendBtn) sendBtn.click();
                            }}, 500);
                        }}
                    }})()";
                
                await _aiWebView.ExecuteScriptAsync(aiScript);
            }
            else // API 模式
            {
                string aiScript = $@"
                    (function() {{
                        function trySetPrompt(count) {{
                            if (window.setAiPrompt) {{
                                window.setAiPrompt(""{prompt}"", true);
                            }} else if (count > 0) {{
                                setTimeout(() => trySetPrompt(count - 1), 500);
                            }}
                        }}
                        trySetPrompt(10);
                    }})()";
                await _aiWebView.ExecuteScriptAsync(aiScript);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(Localization.T("ai.translate_failed", new Dictionary<string, string> { { "msg", ex.Message } }));
        }
    }
    
    #endregion
}
