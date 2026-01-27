using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 设置窗口 - 参考世界之窗浏览器风格
/// </summary>
public class SettingsForm : Form
{
    private const int CornerRadius = 10;
    private readonly ISettingsService _settingsService;
    
    // 左侧导航
    private Panel _navPanel = null!;
    private ListBox _navList = null!;
    
    // 右侧内容区
    private Panel _contentPanel = null!;
    private Panel _historyPanel = null!;
    private Panel _basicPanel = null!;
    private Panel _privacyPanel = null!;
    private Panel _advancedPanel = null!;
    private Panel _aiPanel = null!;
    
    // AI 设置控件
    private RadioButton _aiModeWeb = null!;
    private RadioButton _aiModeApi = null!;
    private ComboBox _aiProviderCombo = null!;
    private ComboBox _aiModelPresetCombo = null!;
    private TextBox _aiApiKeyBox = null!;
    private TextBox _aiApiBaseUrlBox = null!;
    private TextBox _aiModelNameBox = null!;
    private TextBox _aiCustomWebUrlBox = null!;
    private Label _aiApiTipLabel = null!;
    
    // 基本设置控件
    private RadioButton _startupNewTab = null!;
    private RadioButton _startupLastSession = null!;
    private RadioButton _startupSpecificPages = null!;
    private LinkLabel _manageStartupBtn = null!;
    private TextBox _homePageBox = null!;
    private Button _setCurrentAsHomeBtn = null!;
    
    // 广告过滤控件
    private RadioButton _adBlockNone = null!;
    private RadioButton _adBlockPopup = null!;
    private RadioButton _adBlockAggressive = null!;
    private RadioButton _adBlockCustom = null!;
    private Button _manageFiltersBtn = null!;
    
    // 标签设置控件
    private CheckBox _showFullUrlCheck = null!;
    private CheckBox _selectAllOnClickCheck = null!;
    private ComboBox _inputModeCombo = null!;
    private ComboBox _newTabPositionCombo = null!;
    private CheckBox _doubleClickCloseCheck = null!;
    
    // 搜索引擎控件
    private ComboBox _searchEngineCombo = null!;
    private Button _manageSearchEnginesBtn = null!;

    // 用户数据控件
    private Button _importDataBtn = null!;
    
    // 外观控件
    private CheckBox _showHomeButtonCheck = null!;
    private CheckBox _showBookmarkBarCheck = null!;
    
    // 下载控件
    private ComboBox _downloadModeCombo = null!;
    private TextBox _downloadPathBox = null!;
    private Button _browseDownloadPathBtn = null!;
    private CheckBox _askDownloadLocationCheck = null!;
    
    // 隐私设置控件
    private CheckBox _clearHistoryOnExitCheck = null!;
    private CheckBox _clearDownloadsOnExitCheck = null!;
    private CheckBox _clearCacheOnExitCheck = null!;
    private CheckBox _clearCookiesOnExitCheck = null!;
    private CheckBox _sendDoNotTrackCheck = null!;
    private Button _clearBrowsingDataBtn = null!;
    
    // 高级设置控件
    private CheckBox _mouseGestureCheck = null!;
    private CheckBox _superDragCheck = null!;
    private NumericUpDown _memoryReleaseNum = null!;
    private Button _resetSettingsBtn = null!;
    
    public SettingsForm(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeUI();
        LoadSettings();
    }
    
    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }
    
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0) return;
        using var path = CreateRoundedRectPath(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
        Invalidate();
    }
    
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = CreateRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var pen = new Pen(Color.FromArgb(220, 220, 220), 1);
        e.Graphics.DrawPath(pen, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    
    private void InitializeUI()
    {
        Text = "设置";
        Size = new Size(800, 600);
        MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);
        FormBorderStyle = FormBorderStyle.None;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        
        // 设置窗口图标
        AppIconHelper.SetIcon(this);
        
        CreateNavPanel();
        CreateContentPanel();
        CreateBasicPanel();
        
        // 默认显示历史记录
        ShowHistoryPanel();
        _navList.SelectedIndex = 0;
    }

    #region 创建导航面板
    
    private void CreateNavPanel()
    {
        _navPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 120,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding = new Padding(0, 10, 0, 10)
        };
        
        // 标题
        var titleLabel = new Label
        {
            Text = "鲲穹AI浏览器",
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 51, 51),
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _navPanel.MouseDown += OnTitleBarMouseDown;
        titleLabel.MouseDown += OnTitleBarMouseDown;
        
        _navList = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(245, 245, 245),
            Font = new Font("Microsoft YaHei UI", 10F),
            ItemHeight = 36,
            DrawMode = DrawMode.OwnerDrawFixed
        };
        
        _navList.Items.AddRange(new object[] { "历史记录", "设置", "AI 设置" });
        _navList.DrawItem += OnNavListDrawItem;
        _navList.SelectedIndexChanged += OnNavListSelectedIndexChanged;
        
        _navPanel.Controls.Add(_navList);
        _navPanel.Controls.Add(titleLabel);
        Controls.Add(_navPanel);
    }
    
    private void OnNavListDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        
        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bgColor = isSelected ? Color.FromArgb(230, 230, 230) : Color.FromArgb(245, 245, 245);
        var textColor = isSelected ? Color.FromArgb(0, 120, 215) : Color.FromArgb(51, 51, 51);
        
        using var bgBrush = new SolidBrush(bgColor);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);
        
        // 选中时左侧显示蓝色条
        if (isSelected)
        {
            using var accentBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
            e.Graphics.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
        }
        
        var text = _navList.Items[e.Index]?.ToString() ?? "";
        using var textBrush = new SolidBrush(textColor);
        var format = new StringFormat { LineAlignment = StringAlignment.Center };
        var textRect = new Rectangle(e.Bounds.X + 15, e.Bounds.Y, e.Bounds.Width - 15, e.Bounds.Height);
        e.Graphics.DrawString(text, e.Font ?? _navList.Font, textBrush, textRect, format);
    }
    
    private void OnNavListSelectedIndexChanged(object? sender, EventArgs e)
    {
        switch (_navList.SelectedIndex)
        {
            case 0: // 历史记录
                ShowHistoryPanel();
                break;
            case 1: // 设置
                ShowPanel(_basicPanel);
                break;
            case 2: // AI 设置
                if (_aiPanel == null) CreateAiPanel();
                ShowPanel(_aiPanel!);
                break;
        }
    }
    
    private void ManageStartupPages()
    {
        var settings = _settingsService.Settings;
        var urls = string.Join(Environment.NewLine, settings.StartupPages);
        
        using var form = new Form
        {
            Text = "设置启动页",
            Size = new Size(400, 300),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        
        var label = new Label { Text = "请输入网页地址（每行一个）：", Location = new Point(10, 10), AutoSize = true };
        var textBox = new TextBox 
        { 
            Multiline = true, 
            Location = new Point(10, 30), 
            Size = new Size(365, 180),
            ScrollBars = ScrollBars.Vertical,
            Text = urls
        };
        
        var okBtn = new Button { Text = "确定", Location = new Point(210, 220), DialogResult = DialogResult.OK };
        var cancelBtn = new Button { Text = "取消", Location = new Point(300, 220), DialogResult = DialogResult.Cancel };
        
        form.Controls.AddRange(new Control[] { label, textBox, okBtn, cancelBtn });
        form.AcceptButton = okBtn;
        form.CancelButton = cancelBtn;
        
        if (form.ShowDialog() == DialogResult.OK)
        {
            var newUrls = textBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrEmpty(u))
                .ToList();
            
            settings.StartupPages = newUrls;
            _settingsService.Save();
        }
    }
    
    private void ShowHistoryPanel()
    {
        if (_historyPanel == null)
        {
            _historyPanel = new Panel { Dock = DockStyle.Fill };
            
            var headerLabel = new Label
            {
                Text = "历史记录",
                Font = new Font("Microsoft YaHei UI", 16F),
                Dock = DockStyle.Top,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _historyPanel.Controls.Add(headerLabel);
            
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            
            listView.Columns.Add("网址", 300);
            listView.Columns.Add("标题", 200);
            listView.Columns.Add("访问时间", 150);
            
            // 添加示例历史记录（实际应从数据库读取）
            listView.Items.Add(new ListViewItem(new[] { "https://www.example.com", "Example", DateTime.Now.ToString() }));
            
            _historyPanel.Controls.Add(listView);
            _contentPanel.Controls.Add(_historyPanel);
        }
        
        ShowPanel(_historyPanel);
    }
    
    #endregion

    #region 创建内容面板
    
    private void CreateContentPanel()
    {
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(20)
        };
        Controls.Add(_contentPanel);
    }
    
    private void ShowPanel(Panel panel)
    {
        // 隐藏所有面板
        if (_historyPanel != null) _historyPanel.Visible = false;
        if (_basicPanel != null) _basicPanel.Visible = false;
        if (_privacyPanel != null) _privacyPanel.Visible = false;
        if (_advancedPanel != null) _advancedPanel.Visible = false;
        if (_aiPanel != null) _aiPanel.Visible = false;
        
        // 显示目标面板
        panel.Visible = true;
        
        // 确保目标面板在内容面板中
        if (!_contentPanel.Controls.Contains(panel))
        {
            _contentPanel.Controls.Add(panel);
        }
    }
    
    private void ShowAboutInfo()
    {
        MessageBox.Show(
            "鲲穹AI浏览器\n版本 1.0.0\n\n基于 WebView2 内核",
            "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    #endregion
    
    #region 创建基本设置面板
    
    private void CreateBasicPanel()
    {
        _basicPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _contentPanel.Controls.Add(_basicPanel);
        
        var y = 0;
        
        // 标题和搜索框
        var headerPanel = CreateHeaderPanel("设置", "在设置中搜索", _basicPanel);
        headerPanel.Location = new Point(0, y);
        _basicPanel.Controls.Add(headerPanel);
        y += headerPanel.Height + 20;
        
        // 主页设置 分组
        var homeGroup = CreateGroupBox("主页", 90);
        homeGroup.Location = new Point(0, y);
        
        var homeLabel = new Label { Text = "主页地址:", Location = new Point(15, 28), AutoSize = true };
        _homePageBox = new TextBox { Location = new Point(80, 25), Width = 300 };
        _setCurrentAsHomeBtn = new Button
        {
            Text = "使用当前页",
            Location = new Point(390, 23),
            Size = new Size(80, 25),
            FlatStyle = FlatStyle.Flat
        };
        _setCurrentAsHomeBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        
        var useNewTabBtn = new Button
        {
            Text = "使用新标签页",
            Location = new Point(80, 55),
            Size = new Size(100, 25),
            FlatStyle = FlatStyle.Flat
        };
        useNewTabBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        useNewTabBtn.Click += (s, e) => _homePageBox.Text = AppConstants.DefaultHomePage;
        
        homeGroup.Controls.AddRange(new Control[] { homeLabel, _homePageBox, _setCurrentAsHomeBtn, useNewTabBtn });
        _basicPanel.Controls.Add(homeGroup);
        y += homeGroup.Height + 15;
        
        // 启动时 分组
        var startupGroup = CreateGroupBox("启动时", 130);
        startupGroup.Location = new Point(0, y);
        
        _startupNewTab = new RadioButton { Text = "打开新标签页", Location = new Point(15, 25), AutoSize = true };
        _startupLastSession = new RadioButton { Text = "继续浏览上次关闭时正在查看的网页", Location = new Point(15, 50), AutoSize = true };
        _startupSpecificPages = new RadioButton { Text = "打开特定网页或一组网页", Location = new Point(15, 75), AutoSize = true };
        
        _manageStartupBtn = CreateLinkLabel("管理网页", new Point(200, 75));
        _manageStartupBtn.Click += (s, e) => ManageStartupPages();
        
        startupGroup.Controls.AddRange(new Control[] { _startupNewTab, _startupLastSession, _startupSpecificPages, _manageStartupBtn });
        _basicPanel.Controls.Add(startupGroup);
        y += startupGroup.Height + 15;
        
        // 广告过滤 分组
        var adBlockGroup = CreateGroupBox("广告过滤", 160);
        adBlockGroup.Location = new Point(0, y);
        
        _adBlockNone = new RadioButton { Text = "不过滤任何广告", Location = new Point(15, 25), AutoSize = true };
        _adBlockPopup = new RadioButton { Text = "仅过滤弹出窗口", Location = new Point(15, 50), AutoSize = true };
        _adBlockAggressive = new RadioButton { Text = "激进过滤网页广告", Location = new Point(15, 75), AutoSize = true };
        _adBlockCustom = new RadioButton { Text = "自定义广告过滤规则", Location = new Point(15, 100), AutoSize = true };
        
        var customRulesLink = CreateLinkLabel("自定义规则", new Point(170, 100));
        _manageFiltersBtn = new Button { Text = "管理我的规则...", Location = new Point(15, 125), Size = new Size(110, 25), FlatStyle = FlatStyle.Flat };
        _manageFiltersBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        
        adBlockGroup.Controls.AddRange(new Control[] { _adBlockNone, _adBlockPopup, _adBlockAggressive, _adBlockCustom, customRulesLink, _manageFiltersBtn });
        _basicPanel.Controls.Add(adBlockGroup);
        y += adBlockGroup.Height + 15;
        
        // 标签 分组
        y = CreateTabSettingsGroup(y);
        
        // 地址栏搜索引擎 分组
        y = CreateSearchEngineGroup(y);
        
        // 用户数据 分组
        y = CreateUserDataGroup(y);
        
        // 外观 分组
        y = CreateAppearanceGroup(y);
        
        // 下载内容 分组
        y = CreateDownloadGroup(y);
        
        // 默认浏览器 分组
        y = CreateDefaultBrowserGroup(y);
        
        // 高级设置链接
        var advancedLink = CreateLinkLabel("显示高级设置...", new Point(0, y));
        advancedLink.Click += (s, e) => 
        {
            if (_advancedPanel == null) CreateAdvancedPanel();
            ShowPanel(_advancedPanel!);
        };
        _basicPanel.Controls.Add(advancedLink);
    }
    
    #endregion

    #region 创建设置分组
    
    private int CreateTabSettingsGroup(int y)
    {
        var group = CreateGroupBox("标签", 140);
        group.Location = new Point(0, y);
        
        _showFullUrlCheck = new CheckBox { Text = "在地址栏显示完整URL（按住Shift时可显示完整地址）", Location = new Point(15, 25), AutoSize = true };
        _selectAllOnClickCheck = new CheckBox { Text = "单击地址栏时全选URL", Location = new Point(15, 50), AutoSize = true };
        
        var inputModeLabel = new Label { Text = "地址栏输入方式:", Location = new Point(15, 80), AutoSize = true };
        _inputModeCombo = new ComboBox { Location = new Point(120, 77), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _inputModeCombo.Items.AddRange(new object[] { "输入即搜索（推荐）", "回车搜索" });
        
        var newTabLabel = new Label { Text = "新打开页面时:", Location = new Point(15, 110), AutoSize = true };
        _newTabPositionCombo = new ComboBox { Location = new Point(120, 107), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _newTabPositionCombo.Items.AddRange(new object[] { "当前标签右侧打开", "最后位置打开" });
        
        _doubleClickCloseCheck = new CheckBox { Text = "双击标签页关闭", Location = new Point(300, 25), AutoSize = true };
        
        group.Controls.AddRange(new Control[] { _showFullUrlCheck, _selectAllOnClickCheck, inputModeLabel, _inputModeCombo, newTabLabel, _newTabPositionCombo, _doubleClickCloseCheck });
        _basicPanel.Controls.Add(group);
        
        return y + group.Height + 15;
    }
    
    private int CreateSearchEngineGroup(int y)
    {
        var group = CreateGroupBox("地址栏搜索引擎", 70);
        group.Location = new Point(0, y);
        
        _searchEngineCombo = new ComboBox { Location = new Point(15, 28), Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        RefreshSearchEngineCombo();
        
        _manageSearchEnginesBtn = new Button { Text = "管理搜索引擎...", Location = new Point(105, 26), Size = new Size(110, 25), FlatStyle = FlatStyle.Flat };
        _manageSearchEnginesBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _manageSearchEnginesBtn.Click += OnManageSearchEngines;
        
        group.Controls.AddRange(new Control[] { _searchEngineCombo, _manageSearchEnginesBtn });
        _basicPanel.Controls.Add(group);
        
        return y + group.Height + 15;
    }
    
    private void RefreshSearchEngineCombo()
    {
        _searchEngineCombo.Items.Clear();
        _searchEngineCombo.Items.AddRange(new object[] { "360", "百度", "必应", "Google" });
        
        // 添加自定义搜索引擎
        foreach (var engine in _settingsService.Settings.CustomSearchEngines)
        {
            _searchEngineCombo.Items.Add(engine.Name);
        }
    }
    
    private void OnManageSearchEngines(object? sender, EventArgs e)
    {
        using var dialog = new SearchEngineManagerDialog(_settingsService);
        dialog.ShowDialog(this);
        
        // 刷新下拉框
        var currentIndex = _settingsService.Settings.AddressBarSearchEngine;
        RefreshSearchEngineCombo();
        if (currentIndex < _searchEngineCombo.Items.Count)
            _searchEngineCombo.SelectedIndex = currentIndex;
    }
    
    private int CreateUserDataGroup(int y)
    {
        var group = CreateGroupBox("用户数据", 60);
        group.Location = new Point(0, y);
        
        _importDataBtn = new Button { Text = "导入收藏和设置...", Location = new Point(15, 25), Size = new Size(120, 25), FlatStyle = FlatStyle.Flat };
        _importDataBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        
        group.Controls.Add(_importDataBtn);
        _basicPanel.Controls.Add(group);
        
        return y + group.Height + 15;
    }
    
    private int CreateAppearanceGroup(int y)
    {
        var group = CreateGroupBox("外观", 110);
        group.Location = new Point(0, y);
        
        _showHomeButtonCheck = new CheckBox { Text = "显示\"主页\"按钮", Location = new Point(15, 25), AutoSize = true };
        _showBookmarkBarCheck = new CheckBox { Text = "总是显示书签栏", Location = new Point(15, 50), AutoSize = true };
        _showBookmarkBarCheck.CheckedChanged += (s, e) => 
        {
            if (_showBookmarkBarCheck.Focused) // 只有用户点击时才触发
            {
                _settingsService.Settings.AlwaysShowBookmarkBar = _showBookmarkBarCheck.Checked;
                _settingsService.Save();
            }
        };
        
        // 隐藏收藏栏功能
        var hideBookmarkBarCheck = new CheckBox 
        { 
            Text = "隐藏收藏栏", 
            Location = new Point(15, 75), 
            AutoSize = true 
        };
        hideBookmarkBarCheck.CheckedChanged += (s, e) =>
        {
            // 这里可以添加隐藏收藏栏的逻辑
        };
        
        group.Controls.AddRange(new Control[] { _showHomeButtonCheck, _showBookmarkBarCheck, hideBookmarkBarCheck });
        _basicPanel.Controls.Add(group);
        
        return y + group.Height + 15;
    }
    
    private int CreateDownloadGroup(int y)
    {
        var group = CreateGroupBox("下载内容", 110);
        group.Location = new Point(0, y);
        
        var downloadModeLabel = new Label { Text = "选择默认下载工具:", Location = new Point(15, 25), AutoSize = true };
        _downloadModeCombo = new ComboBox { Location = new Point(130, 22), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        _downloadModeCombo.Items.AddRange(new object[] { "使用内置下载器", "使用外部工具" });
        
        var downloadPathLabel = new Label { Text = "下载内容保存位置:", Location = new Point(15, 55), AutoSize = true };
        _downloadPathBox = new TextBox { Location = new Point(130, 52), Width = 200 };
        _browseDownloadPathBtn = new Button { Text = "浏览...", Location = new Point(335, 50), Size = new Size(60, 25), FlatStyle = FlatStyle.Flat };
        _browseDownloadPathBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _browseDownloadPathBtn.Click += OnBrowseDownloadPath;
        
        _askDownloadLocationCheck = new CheckBox { Text = "下载前询问每个文件的保存位置", Location = new Point(15, 82), AutoSize = true };
        
        group.Controls.AddRange(new Control[] { downloadModeLabel, _downloadModeCombo, downloadPathLabel, _downloadPathBox, _browseDownloadPathBtn, _askDownloadLocationCheck });
        _basicPanel.Controls.Add(group);
        
        return y + group.Height + 15;
    }
    
    private int CreateDefaultBrowserGroup(int y)
    {
        var group = CreateGroupBox("默认浏览器", 80);
        group.Location = new Point(0, y);
        
        var setDefaultBtn = new Button { Text = "设为默认浏览器", Location = new Point(15, 25), Size = new Size(140, 28), FlatStyle = FlatStyle.Flat };
        setDefaultBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        
        var statusLabel = new Label { Text = "鲲穹AI浏览器目前不是默认浏览器。", Location = new Point(15, 55), AutoSize = true, ForeColor = Color.Gray };
        
        group.Controls.AddRange(new Control[] { setDefaultBtn, statusLabel });
        _basicPanel.Controls.Add(group);
        
        return y + group.Height + 15;
    }
    
    #endregion

    #region 创建隐私设置面板
    
    private void CreateAiPanel()
    {
        _aiPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false };
        _contentPanel.Controls.Add(_aiPanel);
        
        var y = 0;
        var headerPanel = CreateHeaderPanel("AI 设置", "配置您的 AI 助手", _aiPanel);
        headerPanel.Location = new Point(0, y);
        _aiPanel.Controls.Add(headerPanel);
        y += headerPanel.Height + 20;

        // 服务模式
        var modeGroup = CreateGroupBox("服务模式", 80);
        modeGroup.Location = new Point(0, y);
        _aiModeWeb = new RadioButton { Text = "内置网页模式 (DeepSeek)", Location = new Point(15, 25), AutoSize = true };
        _aiModeApi = new RadioButton { Text = "自定义 API 模式 (支持 OpenAI 兼容接口)", Location = new Point(15, 50), AutoSize = true };
        modeGroup.Controls.AddRange(new Control[] { _aiModeWeb, _aiModeApi });
        _aiPanel.Controls.Add(modeGroup);
        y += modeGroup.Height + 15;

        // API 配置
        var apiGroup = CreateGroupBox("API 配置", 230);
        apiGroup.Location = new Point(0, y);
        
        var providerLabel = new Label { Text = "服务商预设:", Location = new Point(15, 30), AutoSize = true };
        _aiProviderCombo = new ComboBox { Location = new Point(120, 27), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _aiProviderCombo.Items.AddRange(new object[] { "自定义", "DeepSeek", "OpenAI", "Anthropic (Claude)", "Groq", "MiniMax", "阿里百炼 (DashScope)", "Ollama (本地)" });
        _aiProviderCombo.SelectedIndexChanged += OnAiProviderChanged;

        var apiKeyLabel = new Label { Text = "API Key:", Location = new Point(15, 65), AutoSize = true };
        _aiApiKeyBox = new TextBox { Location = new Point(120, 62), Width = 300, PasswordChar = '*' };
        
        var apiBaseLabel = new Label { Text = "API Proxy URL:", Location = new Point(15, 100), AutoSize = true };
        _aiApiBaseUrlBox = new TextBox { Location = new Point(120, 97), Width = 300 };
        
        var modelPresetLabel = new Label { Text = "模型预设:", Location = new Point(15, 135), AutoSize = true };
        _aiModelPresetCombo = new ComboBox { Location = new Point(120, 132), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _aiModelPresetCombo.SelectedIndexChanged += OnAiModelPresetChanged;

        var modelLabel = new Label { Text = "Model Name:", Location = new Point(15, 170), AutoSize = true };
        _aiModelNameBox = new TextBox { Location = new Point(120, 167), Width = 300 };

        _aiApiTipLabel = new Label
        {
            Text = "提示：适用于 DeepSeek, OpenAI, Ollama 等兼容接口",
            Location = new Point(120, 200),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8F)
        };

        apiGroup.Controls.AddRange(new Control[] { 
            providerLabel, _aiProviderCombo, 
            apiKeyLabel, _aiApiKeyBox, 
            apiBaseLabel, _aiApiBaseUrlBox, 
            modelPresetLabel, _aiModelPresetCombo,
            modelLabel, _aiModelNameBox, 
            _aiApiTipLabel
        });
        _aiPanel.Controls.Add(apiGroup);
        y += apiGroup.Height + 15;

        // 网页配置
        var webGroup = CreateGroupBox("网页模式配置", 70);
        webGroup.Location = new Point(0, y);
        var webUrlLabel = new Label { Text = "AI 网页地址:", Location = new Point(15, 30), AutoSize = true };
        _aiCustomWebUrlBox = new TextBox { Location = new Point(120, 27), Width = 300 };
        webGroup.Controls.AddRange(new Control[] { webUrlLabel, _aiCustomWebUrlBox });
        _aiPanel.Controls.Add(webGroup);
        
        // 绑定事件，根据模式启用/禁用控件
        _aiModeWeb.CheckedChanged += (s, e) => UpdateAiControlStates();
        _aiModeApi.CheckedChanged += (s, e) => UpdateAiControlStates();
        
        // 实时更新设置对象，以便在不关闭窗口的情况下生效
        _aiModelNameBox.TextChanged += (s, e) => {
             if (_settingsService != null) _settingsService.Settings.AiModelName = _aiModelNameBox.Text;
        };
        
        UpdateAiControlStates();
        LoadAiSettings();
    }

    private void UpdateAiControlStates()
    {
        bool isApiMode = _aiModeApi.Checked;
        _aiProviderCombo.Enabled = isApiMode;
        _aiModelPresetCombo.Enabled = isApiMode;
        _aiApiKeyBox.Enabled = isApiMode;
        _aiApiBaseUrlBox.Enabled = isApiMode;
        _aiModelNameBox.Enabled = isApiMode;
        _aiCustomWebUrlBox.Enabled = !isApiMode;
    }

    private void OnAiProviderChanged(object? sender, EventArgs e)
    {
        var provider = _aiProviderCombo.SelectedItem?.ToString();
        _aiModelPresetCombo.Items.Clear();

        if (_aiApiTipLabel != null)
        {
            _aiApiTipLabel.Text = provider == "阿里百炼 (DashScope)"
                ? "提示：百炼 OpenAI 兼容接口 BaseUrl 默认 https://dashscope.aliyuncs.com/compatible-mode/v1，可按需替换为 -intl/-us 地域"
                : "提示：适用于 DeepSeek, OpenAI, Ollama 等兼容接口";
        }

        switch (provider)
        {
            case "DeepSeek":
                _aiApiBaseUrlBox.Text = "https://api.deepseek.com/v1";
                _aiModelPresetCombo.Items.AddRange(new object[] { "deepseek-chat", "deepseek-reasoner" });
                _aiModelPresetCombo.SelectedIndex = 0;
                break;
            case "OpenAI":
                _aiApiBaseUrlBox.Text = "https://api.openai.com/v1";
                _aiModelPresetCombo.Items.AddRange(new object[] { "gpt-4o", "gpt-4-turbo", "gpt-3.5-turbo" });
                _aiModelPresetCombo.SelectedIndex = 0;
                break;
            case "Anthropic (Claude)":
                _aiApiBaseUrlBox.Text = "https://api.anthropic.com/v1";
                _aiModelPresetCombo.Items.AddRange(new object[] { "claude-3-5-sonnet-20240620", "claude-3-opus-20240229" });
                _aiModelPresetCombo.SelectedIndex = 0;
                break;
            case "Groq":
                _aiApiBaseUrlBox.Text = "https://api.groq.com/openai/v1";
                _aiModelPresetCombo.Items.AddRange(new object[] { "llama3-70b-8192", "mixtral-8x7b-32768" });
                _aiModelPresetCombo.SelectedIndex = 0;
                break;
            case "MiniMax":
                _aiApiBaseUrlBox.Text = "https://api.minimaxi.com/v1";
                _aiModelPresetCombo.Items.AddRange(new object[] { "MiniMax-M2.1", "MiniMax-M2.1-lightning", "MiniMax-M2" });
                _aiModelPresetCombo.SelectedIndex = 0;
                break;
            case "阿里百炼 (DashScope)":
                _aiApiBaseUrlBox.Text = "https://dashscope.aliyuncs.com/compatible-mode/v1";
                _aiModelPresetCombo.Items.AddRange(new object[] { 
                    "qwen3-max (通义千问 3-Max)", 
                    "qwen3-max-latest (通义千问 3-Max 最新版)", 
                    "qwen-max (通义千问 Max)", 
                    "qwen-max-latest (通义千问 Max 最新版)",
                    "qwen-plus (通义千问 Plus)", 
                    "qwen-plus-latest (通义千问 Plus 最新版)", 
                    "qwen-turbo (通义千问 Turbo)", 
                    "qwen-turbo-latest (通义千问 Turbo 最新版)", 
                    "qwen-long (通义千问 Long)", 
                    "qwen-long-latest (通义千问 Long 最新版)", 
                    "qwen-flash (通义千问 Flash)", 
                    "qwen-coder-plus (通义千问 Coder Plus)", 
                    "qwen-coder-turbo (通义千问 Coder Turbo)", 
                    "qwq-plus (QwQ Plus)", 
                    "qwq-plus-latest (QwQ Plus 最新版)" 
                });
                _aiModelPresetCombo.SelectedIndex = 0;
                break;
            case "Ollama (本地)":
                _aiApiBaseUrlBox.Text = "http://localhost:11434/v1";
                _aiModelPresetCombo.Items.AddRange(new object[] { "llama3", "qwen2", "gemma" });
                _aiModelPresetCombo.SelectedIndex = 0;
                break;
            default:
                _aiModelPresetCombo.Items.Add("自定义");
                _aiModelPresetCombo.SelectedIndex = 0;
                break;
        }
    }

    private void OnAiModelPresetChanged(object? sender, EventArgs e)
    {
        var selectedItem = _aiModelPresetCombo.SelectedItem?.ToString();
        if (selectedItem != null && selectedItem != "自定义")
        {
            // 如果包含括号，提取括号前的 ID
            string modelId = selectedItem;
            if (selectedItem.Contains(" ("))
            {
                modelId = selectedItem.Split(" (")[0].Trim();
            }
            _aiModelNameBox.Text = modelId;
        }
    }

    private void LoadAiSettings()
    {
        var settings = _settingsService.Settings;
        if (settings.AiServiceMode == 0) _aiModeWeb.Checked = true;
        else _aiModeApi.Checked = true;

        // 尝试匹配预设服务商
        if (string.IsNullOrEmpty(settings.AiApiBaseUrl)) _aiProviderCombo.SelectedIndex = 0;
        else if (settings.AiApiBaseUrl.Contains("deepseek")) _aiProviderCombo.SelectedIndex = 1;
        else if (settings.AiApiBaseUrl.Contains("openai")) _aiProviderCombo.SelectedIndex = 2;
        else if (settings.AiApiBaseUrl.Contains("anthropic")) _aiProviderCombo.SelectedIndex = 3;
        else if (settings.AiApiBaseUrl.Contains("groq")) _aiProviderCombo.SelectedIndex = 4;
        else if (settings.AiApiBaseUrl.Contains("minimax")) _aiProviderCombo.SelectedIndex = 5;
        else if (settings.AiApiBaseUrl.Contains("minimaxi")) _aiProviderCombo.SelectedIndex = 5;
        else if (settings.AiApiBaseUrl.Contains("dashscope")) _aiProviderCombo.SelectedIndex = 6;
        else if (settings.AiApiBaseUrl.Contains("aliyuncs")) _aiProviderCombo.SelectedIndex = 6;
        else if (settings.AiApiBaseUrl.Contains("localhost")) _aiProviderCombo.SelectedIndex = 7;
        else _aiProviderCombo.SelectedIndex = 0;

        _aiApiKeyBox.Text = settings.AiApiKey;
        _aiApiBaseUrlBox.Text = settings.AiApiBaseUrl;
        _aiModelNameBox.Text = settings.AiModelName;
        _aiCustomWebUrlBox.Text = settings.AiCustomWebUrl;
        
        // 匹配预设模型
        bool matched = false;
        foreach (var item in _aiModelPresetCombo.Items)
        {
            var itemStr = item.ToString();
            if (itemStr != null && (itemStr == settings.AiModelName || itemStr.StartsWith(settings.AiModelName + " (")))
            {
                _aiModelPresetCombo.SelectedItem = item;
                matched = true;
                break;
            }
        }
        
        if (!matched && !string.IsNullOrEmpty(settings.AiModelName))
        {
            // 如果没匹配到预设，且不是空的，可能需要手动处理或保持当前状态
        }

        UpdateAiControlStates();
    }

    private void CreatePrivacyPanel()
    {
        _privacyPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false };
        _contentPanel.Controls.Add(_privacyPanel);
        
        var y = 0;
        
        var headerPanel = CreateHeaderPanel("隐私设置", "");
        headerPanel.Location = new Point(0, y);
        _privacyPanel.Controls.Add(headerPanel);
        y += headerPanel.Height + 20;
        
        // 清除浏览数据
        var clearGroup = CreateGroupBox("清除浏览数据", 100);
        clearGroup.Location = new Point(0, y);
        
        _clearBrowsingDataBtn = new Button { Text = "清除浏览数据...", Location = new Point(15, 25), Size = new Size(120, 28), FlatStyle = FlatStyle.Flat };
        _clearBrowsingDataBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _clearBrowsingDataBtn.Click += OnClearBrowsingData;
        
        var clearLabel = new Label { Text = "清除浏览历史记录、Cookie、缓存等数据", Location = new Point(15, 60), AutoSize = true, ForeColor = Color.Gray };
        
        clearGroup.Controls.AddRange(new Control[] { _clearBrowsingDataBtn, clearLabel });
        _privacyPanel.Controls.Add(clearGroup);
        y += clearGroup.Height + 15;
        
        // 退出时清除
        var exitClearGroup = CreateGroupBox("退出时自动清除", 150);
        exitClearGroup.Location = new Point(0, y);
        
        _clearHistoryOnExitCheck = new CheckBox { Text = "浏览历史记录", Location = new Point(15, 25), AutoSize = true };
        _clearDownloadsOnExitCheck = new CheckBox { Text = "下载记录", Location = new Point(15, 50), AutoSize = true };
        _clearCacheOnExitCheck = new CheckBox { Text = "缓存的图片和文件", Location = new Point(15, 75), AutoSize = true };
        _clearCookiesOnExitCheck = new CheckBox { Text = "Cookie及其他网站数据", Location = new Point(15, 100), AutoSize = true };
        
        exitClearGroup.Controls.AddRange(new Control[] { _clearHistoryOnExitCheck, _clearDownloadsOnExitCheck, _clearCacheOnExitCheck, _clearCookiesOnExitCheck });
        _privacyPanel.Controls.Add(exitClearGroup);
        y += exitClearGroup.Height + 15;
        
        // 隐私选项
        var privacyGroup = CreateGroupBox("隐私选项", 60);
        privacyGroup.Location = new Point(0, y);
        
        _sendDoNotTrackCheck = new CheckBox { Text = "随浏览流量一起发送\"请勿跟踪\"请求", Location = new Point(15, 25), AutoSize = true };
        
        privacyGroup.Controls.Add(_sendDoNotTrackCheck);
        _privacyPanel.Controls.Add(privacyGroup);
    }
    
    #endregion
    
    #region 创建高级设置面板
    
    private void CreateAdvancedPanel()
    {
        _advancedPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false };
        _contentPanel.Controls.Add(_advancedPanel);
        
        var y = 0;
        
        var headerPanel = CreateHeaderPanel("高级设置", "");
        headerPanel.Location = new Point(0, y);
        _advancedPanel.Controls.Add(headerPanel);
        y += headerPanel.Height + 20;
        
        // 鼠标手势
        var gestureGroup = CreateGroupBox("鼠标手势", 80);
        gestureGroup.Location = new Point(0, y);
        
        _mouseGestureCheck = new CheckBox { Text = "启用鼠标手势", Location = new Point(15, 25), AutoSize = true };
        _superDragCheck = new CheckBox { Text = "启用超级拖拽", Location = new Point(15, 50), AutoSize = true };
        
        gestureGroup.Controls.AddRange(new Control[] { _mouseGestureCheck, _superDragCheck });
        _advancedPanel.Controls.Add(gestureGroup);
        y += gestureGroup.Height + 15;
        
        // 性能
        var perfGroup = CreateGroupBox("性能", 70);
        perfGroup.Location = new Point(0, y);
        
        var memLabel = new Label { Text = "后台标签内存释放时间:", Location = new Point(15, 28), AutoSize = true };
        _memoryReleaseNum = new NumericUpDown { Location = new Point(160, 25), Width = 60, Minimum = 1, Maximum = 60 };
        var minLabel = new Label { Text = "分钟", Location = new Point(225, 28), AutoSize = true };
        
        perfGroup.Controls.AddRange(new Control[] { memLabel, _memoryReleaseNum, minLabel });
        _advancedPanel.Controls.Add(perfGroup);
        y += perfGroup.Height + 15;
        
        // 重置设置
        var resetGroup = CreateGroupBox("重置设置", 80);
        resetGroup.Location = new Point(0, y);
        
        _resetSettingsBtn = new Button { Text = "将设置还原为原始默认设置", Location = new Point(15, 25), Size = new Size(180, 28), FlatStyle = FlatStyle.Flat };
        _resetSettingsBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _resetSettingsBtn.Click += OnResetSettings;
        
        var resetLabel = new Label { Text = "这不会影响您的书签、历史记录和保存的密码。", Location = new Point(15, 55), AutoSize = true, ForeColor = Color.Gray };
        
        resetGroup.Controls.AddRange(new Control[] { _resetSettingsBtn, resetLabel });
        _advancedPanel.Controls.Add(resetGroup);
        y += resetGroup.Height + 15;
        
        // 返回基本设置
        var backLink = CreateLinkLabel("← 返回基本设置", new Point(0, y));
        backLink.Click += (s, e) => ShowPanel(_basicPanel);
        _advancedPanel.Controls.Add(backLink);
    }
    
    #endregion

    #region 辅助方法
    
    private Panel CreateHeaderPanel(string title, string searchPlaceholder, Panel? targetPanel = null)
    {
        var panel = new Panel { Size = new Size(600, 50) };
        
        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Microsoft YaHei UI", 16F),
            Location = new Point(0, 10),
            AutoSize = true
        };
        
        if (!string.IsNullOrEmpty(searchPlaceholder))
        {
            var searchBox = new TextBox
            {
                Location = new Point(400, 12),
                Width = 150,
                Text = searchPlaceholder,
                ForeColor = Color.Gray
            };
            
            // 焦点处理
            searchBox.GotFocus += (s, e) => { if (searchBox.Text == searchPlaceholder) { searchBox.Text = ""; searchBox.ForeColor = Color.Black; } };
            searchBox.LostFocus += (s, e) => { if (string.IsNullOrEmpty(searchBox.Text)) { searchBox.Text = searchPlaceholder; searchBox.ForeColor = Color.Gray; } };
            
            // 搜索逻辑
            if (targetPanel != null)
            {
                searchBox.TextChanged += (s, e) => 
                {
                    var keyword = searchBox.Text;
                    // 如果内容等于占位符，视为无搜索关键字
                    if (keyword == searchPlaceholder) keyword = "";
                    keyword = keyword.Trim();
                    
                    targetPanel.SuspendLayout();
                    try
                    {
                        foreach (Control ctrl in targetPanel.Controls)
                        {
                            if (ctrl is GroupBox group)
                            {
                                bool visible = string.IsNullOrEmpty(keyword);
                                if (!visible)
                                {
                                    // 搜索 GroupBox 标题
                                    if (group.Text.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)) 
                                    {
                                        visible = true;
                                    }
                                    else 
                                    {
                                        // 搜索 GroupBox 内部控件文本
                                        foreach (Control inner in group.Controls)
                                        {
                                            if (!string.IsNullOrEmpty(inner.Text) && 
                                                inner.Text.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
                                            {
                                                visible = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                group.Visible = visible;
                            }
                        }
                    }
                    finally
                    {
                        targetPanel.ResumeLayout();
                    }
                };
            }
            
            panel.Controls.Add(searchBox);
        }
        
        panel.Controls.Add(titleLabel);
        return panel;
    }
    
    private GroupBox CreateGroupBox(string title, int height)
    {
        return new GroupBox
        {
            Text = title,
            Size = new Size(550, height),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(51, 51, 51)
        };
    }
    
    private LinkLabel CreateLinkLabel(string text, Point location)
    {
        var link = new LinkLabel
        {
            Text = text,
            Location = location,
            AutoSize = true,
            LinkColor = Color.FromArgb(0, 102, 204),
            ActiveLinkColor = Color.FromArgb(0, 80, 160)
        };
        return link;
    }
    
    #endregion
    
    #region 事件处理
    
    private void OnBrowseDownloadPath(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择下载保存位置",
            SelectedPath = _downloadPathBox.Text
        };
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _downloadPathBox.Text = dialog.SelectedPath;
        }
    }
    
    private void OnClearBrowsingData(object? sender, EventArgs e)
    {
        using var dialog = new ClearBrowsingDataDialog();
        dialog.ShowDialog();
    }
    
    private void OnResetSettings(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "确定要将所有设置还原为默认值吗？\n\n这不会影响您的书签、历史记录和保存的密码。",
            "重置设置",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        
        if (result == DialogResult.Yes)
        {
            _settingsService.Reset();
            LoadSettings();
            MessageBox.Show("设置已重置为默认值。", "重置完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
    
    private void OnTitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Win32Helper.EnableWindowDrag(Handle);
        }
    }
    
    #endregion

    #region 加载和保存设置
    
    private void LoadSettings()
    {
        var settings = _settingsService.Settings;
        
        // 主页
        _homePageBox.Text = settings.HomePage;
        
        // 启动时
        switch (settings.StartupBehavior)
        {
            case 0: _startupNewTab.Checked = true; break;
            case 1: _startupLastSession.Checked = true; break;
            case 2: _startupSpecificPages.Checked = true; break;
        }
        
        // 广告过滤
        switch (settings.AdBlockMode)
        {
            case 0: _adBlockNone.Checked = true; break;
            case 1: _adBlockPopup.Checked = true; break;
            case 2: _adBlockAggressive.Checked = true; break;
            case 3: _adBlockCustom.Checked = true; break;
        }
        
        // 标签设置
        _showFullUrlCheck.Checked = settings.ShowFullUrlInAddressBar;
        _selectAllOnClickCheck.Checked = settings.SelectAllOnAddressBarClick;
        _inputModeCombo.SelectedIndex = settings.AddressBarInputMode;
        _newTabPositionCombo.SelectedIndex = settings.NewTabPosition;
        _doubleClickCloseCheck.Checked = settings.DoubleClickCloseTab;
        
        // 搜索引擎
        _searchEngineCombo.SelectedIndex = settings.AddressBarSearchEngine;
        
        // 外观
        _showHomeButtonCheck.Checked = settings.ShowHomeButton;
        _showBookmarkBarCheck.Checked = settings.AlwaysShowBookmarkBar;
        
        // 下载
        _downloadModeCombo.SelectedIndex = settings.UseBuiltInDownloader ? 0 : 1;
        _downloadPathBox.Text = settings.DownloadPath;
        _askDownloadLocationCheck.Checked = settings.AskDownloadLocation;
        
        // 隐私
        _clearHistoryOnExitCheck.Checked = settings.ClearHistoryOnExit;
        _clearDownloadsOnExitCheck.Checked = settings.ClearDownloadsOnExit;
        _clearCacheOnExitCheck.Checked = settings.ClearCacheOnExit;
        _clearCookiesOnExitCheck.Checked = settings.ClearCookiesOnExit;
        _sendDoNotTrackCheck.Checked = settings.SendDoNotTrack;
        
        // 高级
        _mouseGestureCheck.Checked = settings.EnableMouseGesture;
        _superDragCheck.Checked = settings.EnableSuperDrag;
        _memoryReleaseNum.Value = settings.MemoryReleaseMinutes;
    }
    
    public void SaveSettings()
    {
        var settings = _settingsService.Settings;
        
        // 主页
        settings.HomePage = _homePageBox.Text;
        
        // 启动时
        if (_startupNewTab.Checked) settings.StartupBehavior = 0;
        else if (_startupLastSession.Checked) settings.StartupBehavior = 1;
        else if (_startupSpecificPages.Checked) settings.StartupBehavior = 2;
        
        // 广告过滤
        if (_adBlockNone.Checked) { settings.AdBlockMode = 0; settings.EnableAdBlock = false; }
        else if (_adBlockPopup.Checked) { settings.AdBlockMode = 1; settings.EnableAdBlock = true; }
        else if (_adBlockAggressive.Checked) { settings.AdBlockMode = 2; settings.EnableAdBlock = true; }
        else if (_adBlockCustom.Checked) { settings.AdBlockMode = 3; settings.EnableAdBlock = true; }
        
        // 标签设置
        settings.ShowFullUrlInAddressBar = _showFullUrlCheck.Checked;
        settings.SelectAllOnAddressBarClick = _selectAllOnClickCheck.Checked;
        settings.AddressBarInputMode = _inputModeCombo.SelectedIndex;
        settings.NewTabPosition = _newTabPositionCombo.SelectedIndex;
        settings.DoubleClickCloseTab = _doubleClickCloseCheck.Checked;
        
        // 搜索引擎
        settings.AddressBarSearchEngine = _searchEngineCombo.SelectedIndex;
        settings.SearchEngine = _searchEngineCombo.SelectedIndex switch
        {
            0 => "https://www.so.com/s?q=",
            1 => "https://www.baidu.com/s?wd=",
            2 => "https://www.bing.com/search?q=",
            _ => "https://www.google.com/search?q="
        };
        
        // 外观
        settings.ShowHomeButton = _showHomeButtonCheck.Checked;
        settings.AlwaysShowBookmarkBar = _showBookmarkBarCheck.Checked;
        
        // 下载
        settings.UseBuiltInDownloader = _downloadModeCombo.SelectedIndex == 0;
        settings.DownloadPath = _downloadPathBox.Text;
        settings.AskDownloadLocation = _askDownloadLocationCheck.Checked;
        
        // 隐私
        settings.ClearHistoryOnExit = _clearHistoryOnExitCheck.Checked;
        settings.ClearDownloadsOnExit = _clearDownloadsOnExitCheck.Checked;
        settings.ClearCacheOnExit = _clearCacheOnExitCheck.Checked;
        settings.ClearCookiesOnExit = _clearCookiesOnExitCheck.Checked;
        settings.SendDoNotTrack = _sendDoNotTrackCheck.Checked;
        
        // 高级
        settings.EnableMouseGesture = _mouseGestureCheck.Checked;
        settings.EnableSuperDrag = _superDragCheck.Checked;
        settings.MemoryReleaseMinutes = (int)_memoryReleaseNum.Value;
        
        // AI 设置
        if (_aiPanel != null)
        {
            settings.AiServiceMode = _aiModeWeb.Checked ? 0 : 1;
            settings.AiApiKey = _aiApiKeyBox.Text;
            settings.AiApiBaseUrl = _aiApiBaseUrlBox.Text;
            settings.AiModelName = _aiModelNameBox.Text;
            settings.AiCustomWebUrl = _aiCustomWebUrlBox.Text;
        }
        
        _settingsService.Save();
    }
    
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        base.OnFormClosing(e);
    }
    
    #endregion
}
