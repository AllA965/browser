using System.Drawing.Drawing2D;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 现代化地址栏下拉框 - 参考 Chrome 风格
/// </summary>
public class AddressBarDropdown : Form
{
    private readonly IHistoryService _historyService;
    private readonly IBookmarkService _bookmarkService;
    private readonly Panel _suggestionPanel;
    private readonly FlowLayoutPanel _actionPanel;
    private readonly List<SuggestionItem> _suggestions = new();
    private int _selectedIndex = -1;
    private string _currentText = "";
    private string _searchEngine = "https://www.baidu.com/s?wd=";
    private bool _isInteracting = false;
    
    public event Action<string>? ItemSelected;
    public event Action<string>? SearchRequested;
    
    private Color _backgroundColor = Color.White;
    private Color _hoverColor = Color.FromArgb(245, 245, 245);
    private Color _selectedColor = Color.FromArgb(232, 240, 254);
    private Color _borderColor = Color.FromArgb(218, 220, 224);
    private Color _textColor = Color.FromArgb(32, 33, 36);
    private Color _secondaryTextColor = Color.FromArgb(95, 99, 104);
    private Color _iconColor = Color.FromArgb(95, 99, 104);
    private Color _actionBorderColor = Color.FromArgb(232, 234, 237);
    private readonly bool _isDarkMode;
    
    public AddressBarDropdown(IHistoryService historyService, IBookmarkService bookmarkService, bool isDarkMode = false)
    {
        _historyService = historyService;
        _bookmarkService = bookmarkService;
        _isDarkMode = isDarkMode;
        
        // 设置深色模式颜色
        if (isDarkMode)
        {
            _backgroundColor = Color.FromArgb(32, 33, 36);
            _hoverColor = Color.FromArgb(50, 50, 50);
            _selectedColor = Color.FromArgb(60, 90, 150);
            _borderColor = Color.FromArgb(60, 60, 60);
            _textColor = Color.FromArgb(200, 200, 200);
            _secondaryTextColor = Color.FromArgb(150, 150, 150);
            _iconColor = Color.FromArgb(150, 150, 150);
            _actionBorderColor = Color.FromArgb(60, 60, 60);
        }
        
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = _backgroundColor;
        TransparencyKey = Color.Magenta;
        BackColor = Color.Magenta;
        DoubleBuffered = true;

        Paint += OnFormPaint;
        
        // 设置 Padding 以留出边框空间 (左、右、下各 2px，上为 0 以实现无缝连接)
        this.Padding = DpiHelper.Scale(new Padding(2, 0, 2, 2));
        
        // 建议列表面板
        _suggestionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _backgroundColor, // 内部面板使用白色背景
            Padding = DpiHelper.Scale(new Padding(0, 4, 0, 4))
        };
        _suggestionPanel.Paint += OnSuggestionPanelPaint;
        _suggestionPanel.MouseMove += OnSuggestionPanelMouseMove;
        _suggestionPanel.MouseClick += OnSuggestionPanelMouseClick;
        _suggestionPanel.MouseLeave += (s, e) => { _selectedIndex = -1; _suggestionPanel.Invalidate(); };
        
        // 底部操作面板 - 使用FlowLayoutPanel水平排列按钮
        _actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = DpiHelper.Scale(36),
            BackColor = isDarkMode ? Color.FromArgb(40, 41, 45) : Color.FromArgb(248, 249, 250),
            Padding = DpiHelper.Scale(new Padding(8, 4, 8, 4)),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _actionPanel.Paint += OnActionPanelPaint;
        CreateActionButtons();
        
        Controls.Add(_suggestionPanel);
        Controls.Add(_actionPanel);
    }

    public string SearchEngine
    {
        get => _searchEngine;
        set => _searchEngine = value;
    }
    
    public bool IsInteracting => _isInteracting;
    
    private System.Windows.Forms.Timer? _fadeTimer;

    public void Show(Control anchor, string text, List<string> urlHistory)
    {
        // 如果窗体已被释放，不执行任何操作
        if (IsDisposed) return;
        
        _isInteracting = false;  // 重置交互标志
        _currentText = text;
        UpdateSuggestions(text, urlHistory);
        
        if (_suggestions.Count == 0)
        {
            if (Visible) Hide();
            return;
        }
        
        // 计算位置和大小 - 仿 Chrome MD3 悬浮风格
        // 获取 anchor 在屏幕上的位置
        var anchorScreenPos = anchor.PointToScreen(Point.Empty);
        
        // 宽度完全对齐地址栏
        int targetWidth = anchor.Width;
        
        // 计算 X 坐标
        int x = anchorScreenPos.X;
        
        // 边界检查：防止超出屏幕右侧
        var screen = Screen.FromControl(anchor);
        if (x + targetWidth > screen.WorkingArea.Right)
        {
            targetWidth = screen.WorkingArea.Right - x - 4;
        }
        if (x < screen.WorkingArea.Left)
        {
            x = screen.WorkingArea.Left + 4;
            targetWidth -= 4;
        }

        // Y 坐标：紧贴地址栏底部
        // AddressBar OnPaint draws border up to this.Height - 1
        // We overlap 1px to cover the seam
        int y = anchorScreenPos.Y + anchor.Height - 1; 

        Location = new Point(x, y);
        Width = targetWidth;
        
        int itemHeight = DpiHelper.Scale(40);
        int suggestionHeight = Math.Min(_suggestions.Count * itemHeight, DpiHelper.Scale(480)); // 增加最大高度限制
        Height = suggestionHeight + _actionPanel.Height + DpiHelper.Scale(8);
        
        _selectedIndex = -1;
        
        // 确保窗口区域圆角正确
        UpdateRegion();
        
        // 显示下拉框
        if (!Visible)
        {
            Opacity = 0;
            base.Show();
            
            if (_fadeTimer == null)
            {
                _fadeTimer = new System.Windows.Forms.Timer { Interval = 10 };
                _fadeTimer.Tick += (s, e) => {
                    if (Opacity < 1)
                    {
                        Opacity += 0.2;
                    }
                    else
                    {
                        Opacity = 1;
                        _fadeTimer.Stop();
                    }
                };
            }
            _fadeTimer.Start();
        }
        _suggestionPanel.Invalidate();
    }

    private void UpdateRegion()
    {
        if (Width > 0 && Height > 0)
        {
            // 使用 Region 裁剪窗体形状，避免 TransparencyKey 导致的粉色杂边
            int cornerRadius = DpiHelper.Scale(16);
            int d = cornerRadius * 2;
            
            using var path = new GraphicsPath();
            // 顶部直角
            path.AddLine(0, 0, Width, 0); 
            // 右侧线
            path.AddLine(Width, 0, Width, Height - cornerRadius);
            // 右下圆角
            path.AddArc(Width - d, Height - d, d, d, 0, 90);
            // 底部线
            path.AddLine(Width - cornerRadius, Height, cornerRadius, Height);
            // 左下圆角
            path.AddArc(0, Height - d, d, d, 90, 90);
            // 左侧线
            path.AddLine(0, Height - cornerRadius, 0, 0);
            path.CloseFigure();
            
            this.Region = new Region(path);
            
            // 强制触发重绘
            this.Invalidate();
        }
    }

    private void OnFormPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        int cornerRadius = DpiHelper.Scale(16); 
        int d = cornerRadius * 2;
        float borderWidth = DpiHelper.Scale(1f);

        // 1. 填充背景
        // 直接清除为背景色，避免透明键杂色
        e.Graphics.Clear(_backgroundColor);

        // 2. 绘制边框
        using var pen = new Pen(_borderColor, borderWidth);
        using var borderPath = new GraphicsPath();

        // 绘制 左右+底部 边框（顶部不画，与地址栏衔接）
        // 稍微缩进 1 像素绘制，确保在 Region 内部，避免被裁剪或留下杂色
        float offset = borderWidth / 2;
        float w = Width - borderWidth;
        float h = Height - borderWidth;

        borderPath.AddLine(w, 0, w, h - cornerRadius);
        borderPath.AddArc(w - d, h - d, d, d, 0, 90);
        borderPath.AddLine(w - cornerRadius, h, cornerRadius, h);
        borderPath.AddArc(0, h - d, d, d, 90, 90);
        borderPath.AddLine(0, h - cornerRadius, 0, 0);

        e.Graphics.DrawPath(pen, borderPath);
    }
    
    private void UpdateSuggestions(string text, List<string> urlHistory)
    {
        _currentUrlHistory = urlHistory;
        _currentFilter = FilterMode.All;  // 重置筛选模式
        UpdateFilterButtonStyles();
        UpdateSuggestionsWithFilter(text, urlHistory);
    }
    
    private void UpdateSuggestionsWithFilter(string text, List<string> urlHistory)
    {
        _suggestions.Clear();
        
        switch (_currentFilter)
        {
            case FilterMode.History:
                AddHistorySuggestions(text, urlHistory);
                break;
            case FilterMode.Bookmark:
                AddBookmarkSuggestions(text);
                break;
            case FilterMode.Tabs:
                AddTabsSuggestions(text);
                break;
            default:
                AddAllSuggestions(text, urlHistory);
                break;
        }
    }
    
    private void AddAllSuggestions(string text, List<string> urlHistory)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            // 显示最近历史
            foreach (var url in urlHistory.Take(8))
            {
                _suggestions.Add(new SuggestionItem
                {
                    Text = url,
                    Type = SuggestionType.History,
                    Icon = "🕐"
                });
            }
        }
        else
        {
            // 搜索建议
            if (!text.Contains('.') && !text.StartsWith("http"))
            {
                _suggestions.Add(new SuggestionItem
                {
                    Text = text,
                    DisplayText = Localization.T("address_dropdown.search", new Dictionary<string, string> { { "text", text } }),
                    Type = SuggestionType.Search,
                    Icon = "🔍"
                });
            }
            
            // 匹配历史记录
            var matches = urlHistory
                .Where(u => u.Contains(text, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(u => u.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                .Take(7);
            
            foreach (var url in matches)
            {
                _suggestions.Add(new SuggestionItem
                {
                    Text = url,
                    Type = SuggestionType.History,
                    Icon = "🕐"
                });
            }
            
            // 如果输入看起来像URL，添加直接访问选项
            if (text.Contains('.') || text.StartsWith("http"))
            {
                var url = text.StartsWith("http") ? text : $"https://{text}";
                if (!_suggestions.Any(s => s.Text == url))
                {
                    _suggestions.Insert(0, new SuggestionItem
                    {
                        Text = url,
                        DisplayText = Localization.T("address_dropdown.visit", new Dictionary<string, string> { { "text", text } }),
                        Type = SuggestionType.Url,
                        Icon = "🌐"
                    });
                }
            }
        }
    }
    
    private void AddHistorySuggestions(string text, List<string> urlHistory)
    {
        List<Models.HistoryItem> matches;
        if (string.IsNullOrWhiteSpace(text))
        {
            matches = _historyService.GetHistory(10);
        }
        else
        {
            matches = _historyService.Search(text, 10);
        }
        
        foreach (var item in matches)
        {
            _suggestions.Add(new SuggestionItem
            {
                Text = item.Url,
                DisplayText = string.IsNullOrEmpty(item.Title) ? item.Url : item.Title,
                Type = SuggestionType.History,
                Icon = "🕐"
            });
        }
    }
    
    private void AddBookmarkSuggestions(string text)
    {
        List<Models.Bookmark> matches;
        if (string.IsNullOrWhiteSpace(text))
        {
            matches = _bookmarkService.GetBookmarkBarItems();
        }
        else
        {
            matches = _bookmarkService.Search(text);
        }
        
        foreach (var item in matches.Where(b => !b.IsFolder).Take(10))
        {
            _suggestions.Add(new SuggestionItem
            {
                Text = item.Url ?? "",
                DisplayText = string.IsNullOrEmpty(item.Title) ? item.Url ?? "" : item.Title,
                Type = SuggestionType.Bookmark,
                Icon = "★"
            });
        }
    }
    
    private void AddTabsSuggestions(string text)
    {
        var tabs = GetOpenTabs?.Invoke() ?? new List<(string Title, string Url)>();
        
        IEnumerable<(string Title, string Url)> matches;
        if (string.IsNullOrWhiteSpace(text))
        {
            matches = tabs.Take(10);
        }
        else
        {
            matches = tabs
                .Where(t => t.Url.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                           t.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
                .Take(10);
        }
        
        foreach (var (title, url) in matches)
        {
            _suggestions.Add(new SuggestionItem
            {
                Text = url,
                DisplayText = string.IsNullOrEmpty(title) ? url : title,
                Type = SuggestionType.Tab,
                Icon = "▢"
            });
        }
    }
    
    // 筛选模式
    private FilterMode _currentFilter = FilterMode.All;
    private Button? _historyBtn;
    private Button? _bookmarkBtn;
    private Button? _tabsBtn;
    private List<string> _currentUrlHistory = new();
    
    // 标签页列表事件
    public event Func<List<(string Title, string Url)>>? GetOpenTabs;
    
    private void CreateActionButtons()
    {
        // 筛选搜索标签
        var filterLabel = new Label
        {
            Text = Localization.Raw(),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(8.5F)),
            ForeColor = _secondaryTextColor,
            Padding = DpiHelper.Scale(new Padding(0, 6, 8, 0))
        };
        
        _historyBtn = CreateFilterButton(Localization.Raw(), "🕐", FilterMode.History);
        _bookmarkBtn = CreateFilterButton(Localization.Raw(), "☆", FilterMode.Bookmark);
        _tabsBtn = CreateFilterButton(Localization.Raw(), "▢", FilterMode.Tabs);
        
        _actionPanel.Controls.Add(filterLabel);
        _actionPanel.Controls.Add(_historyBtn);
        _actionPanel.Controls.Add(_bookmarkBtn);
        _actionPanel.Controls.Add(_tabsBtn);
    }
    
    private Button CreateFilterButton(string text, string icon, FilterMode mode)
    {
        var btn = CreateActionButton(text, icon);
        btn.Click += (s, e) => 
        {
            _isInteracting = true;
            SetFilter(mode);
            // 触发事件通知外部需要恢复焦点
            RequestFocusRestore?.Invoke();
        };
        return btn;
    }
    
    /// <summary>
    /// 请求恢复地址栏焦点的事件
    /// </summary>
    public event Action? RequestFocusRestore;
    
    private void SetFilter(FilterMode mode)
    {
        _currentFilter = mode;
        UpdateFilterButtonStyles();
        RefreshSuggestions();
    }
    
    private void UpdateFilterButtonStyles()
    {
        var activeColor = Color.FromArgb(0, 120, 212);
        var normalColor = _secondaryTextColor;
        var activeBg = _selectedColor;
        var normalBg = Color.Transparent;
        
        if (_historyBtn != null)
        {
            _historyBtn.ForeColor = _currentFilter == FilterMode.History ? activeColor : normalColor;
            _historyBtn.BackColor = _currentFilter == FilterMode.History ? activeBg : normalBg;
        }
        if (_bookmarkBtn != null)
        {
            _bookmarkBtn.ForeColor = _currentFilter == FilterMode.Bookmark ? activeColor : normalColor;
            _bookmarkBtn.BackColor = _currentFilter == FilterMode.Bookmark ? activeBg : normalBg;
        }
        if (_tabsBtn != null)
        {
            _tabsBtn.ForeColor = _currentFilter == FilterMode.Tabs ? activeColor : normalColor;
            _tabsBtn.BackColor = _currentFilter == FilterMode.Tabs ? activeBg : normalBg;
        }
    }
    
    private void RefreshSuggestions()
    {
        UpdateSuggestionsWithFilter(_currentText, _currentUrlHistory);
        
        if (_suggestions.Count == 0)
        {
            // 显示空状态提示
            _suggestions.Add(new SuggestionItem
            {
                Text = "",
                DisplayText = GetEmptyMessage(),
                Type = SuggestionType.Search,
                Icon = "ℹ"
            });
        }
        
        // 重新计算高度
        int suggestionHeight = Math.Min(_suggestions.Count * DpiHelper.Scale(40), DpiHelper.Scale(320));
        Height = suggestionHeight + _actionPanel.Height + DpiHelper.Scale(8);
        
        _selectedIndex = -1;
        _suggestionPanel.Invalidate();
    }
    
    private string GetEmptyMessage()
    {
        return _currentFilter switch
        {
            FilterMode.History => Localization.T("address_dropdown.empty_history"),
            FilterMode.Bookmark => Localization.T("address_dropdown.empty_bookmark"),
            FilterMode.Tabs => Localization.T("address_dropdown.empty_tabs"),
            _ => Localization.T("address_dropdown.empty_all")
        };
    }
    
    private enum FilterMode
    {
        All,
        History,
        Bookmark,
        Tabs
    }
    
    private Button CreateActionButton(string text, string icon)
    {
        var btn = new NoFocusButton
        {
            Text = $"{icon} {text}",
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            Height = DpiHelper.Scale(28),
            Padding = DpiHelper.Scale(new Padding(8, 0, 8, 0)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(8.5F)),
            ForeColor = _secondaryTextColor,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Margin = DpiHelper.Scale(new Padding(0, 0, 8, 0)),
            TabStop = false
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = _actionBorderColor;
        btn.FlatAppearance.MouseOverBackColor = _hoverColor;
        
        // 鼠标按下时标记正在交互，防止下拉框被隐藏
        btn.MouseDown += (s, e) => _isInteracting = true;
        // 延迟重置交互标志，确保LostFocus检查时标志仍为true
        btn.MouseUp += (s, e) => 
        {
            var timer = new System.Windows.Forms.Timer { Interval = 200 };
            timer.Tick += (ts, te) => { timer.Stop(); timer.Dispose(); _isInteracting = false; };
            timer.Start();
        };
        
        return btn;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // 设置圆角窗口区域
        UpdateRegion();
    }
    
    public new void Hide()
    {
        _isInteracting = false;  // 隐藏时重置交互标志
        _selectedIndex = -1;     // 重置选中索引
        
        // 通知地址栏关闭状态
        if (Parent is Form parentForm)
        {
            // 通过反射或寻找 MainForm 中的地址栏来设置状态
            // 这种方式比较 hack，更好的方式是事件
        }
        
        DropdownHidden?.Invoke();
        base.Hide();
    }
    
    public event Action? DropdownHidden;
    
    private void OnSuggestionPanelPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        
        int y = DpiHelper.Scale(8); // Top padding inside the panel
        int itemHeight = DpiHelper.Scale(36); // Chrome standard height for suggestion items
        int iconSize = DpiHelper.Scale(20);
        int sidePadding = DpiHelper.Scale(10);
        int iconLeftMargin = DpiHelper.Scale(14);
        int textLeftMargin = DpiHelper.Scale(12);
        
        for (int i = 0; i < _suggestions.Count; i++)
        {
            var item = _suggestions[i];
            // Chrome style: rounded rectangle selection, with side padding
            var itemRect = new Rectangle(sidePadding, y, _suggestionPanel.Width - sidePadding * 2, itemHeight);
            
            // 背景
            if (i == _selectedIndex)
            {
                using var brush = new SolidBrush(_selectedColor);
                using var path = CreateRoundedRect(itemRect, DpiHelper.Scale(18)); // 18px radius for full rounding effect
                e.Graphics.FillPath(brush, path);
            }
            
            // 图标
            var iconRect = new Rectangle(itemRect.X + iconLeftMargin, itemRect.Y + (itemHeight - iconSize) / 2, iconSize, iconSize);
            using (var iconBrush = new SolidBrush(_iconColor))
            {
                // Use Segoe UI Emoji or Symbol for better icon rendering
                using var iconFont = new Font("Segoe UI Emoji", DpiHelper.ScaleFont(11F));
                // Center icon
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(item.Icon, iconFont, iconBrush, iconRect, format);
            }
            
            // 文本
            var textRect = new Rectangle(iconRect.Right + textLeftMargin, itemRect.Y, itemRect.Width - DpiHelper.Scale(80), itemHeight);
            var displayText = item.DisplayText ?? item.Text;
            
            using (var textBrush = new SolidBrush(_textColor))
            {
                using var textFont = new Font("Segoe UI", DpiHelper.ScaleFont(10F)); // Chrome font
                using var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisPath,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                e.Graphics.DrawString(displayText, textFont, textBrush, textRect, format);
            }
            
            // 删除按钮（仅历史记录显示）
            if (item.Type == SuggestionType.History && i == _selectedIndex)
            {
                int deleteSize = DpiHelper.Scale(20);
                var deleteRect = new Rectangle(itemRect.Right - DpiHelper.Scale(32), itemRect.Y + (itemHeight - deleteSize) / 2, deleteSize, deleteSize);
                // Draw 'X'
                using var deleteBrush = new SolidBrush(_secondaryTextColor);
                using var deleteFont = new Font("Segoe UI", DpiHelper.ScaleFont(9F));
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("✕", deleteFont, deleteBrush, deleteRect, format);
            }
            
            y += itemHeight;
        }
    }
    
    private void OnSuggestionPanelMouseMove(object? sender, MouseEventArgs e)
    {
        int yOffset = DpiHelper.Scale(8);
        int itemHeight = DpiHelper.Scale(36);
        int index = (e.Y - yOffset) / itemHeight;
        if (index >= 0 && index < _suggestions.Count && index != _selectedIndex)
        {
            _selectedIndex = index;
            _suggestionPanel.Invalidate();
        }
    }
    
    private void OnSuggestionPanelMouseClick(object? sender, MouseEventArgs e)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _suggestions.Count)
        {
            var item = _suggestions[_selectedIndex];
            
            // 检查是否点击了删除按钮
            int yOffset = DpiHelper.Scale(8);
            int itemHeight = DpiHelper.Scale(36);
            int sidePadding = DpiHelper.Scale(10);
            var itemRect = new Rectangle(sidePadding, yOffset + _selectedIndex * itemHeight, _suggestionPanel.Width - sidePadding * 2, itemHeight);
            int deleteSize = DpiHelper.Scale(20);
            var deleteRect = new Rectangle(itemRect.Right - DpiHelper.Scale(32), itemRect.Y + (itemHeight - deleteSize) / 2, deleteSize, deleteSize);
            
            if (item.Type == SuggestionType.History && deleteRect.Contains(e.Location))
            {
                // TODO: 从历史记录中删除
                return;
            }
            
            // 选择项目
            if (item.Type == SuggestionType.Search)
            {
                SearchRequested?.Invoke(_searchEngine + Uri.EscapeDataString(item.Text));
            }
            else
            {
                ItemSelected?.Invoke(item.Text);
            }
            Hide();
        }
    }
    
    private void OnActionPanelPaint(object? sender, PaintEventArgs e)
    {
        // 顶部分隔线
        using var pen = new Pen(_actionBorderColor);
        e.Graphics.DrawLine(pen, 0, 0, _actionPanel.Width, 0);
    }
    
    public void MoveSelection(int delta)
    {
        int newIndex = _selectedIndex + delta;
        if (newIndex >= -1 && newIndex < _suggestions.Count)
        {
            _selectedIndex = newIndex;
            _suggestionPanel.Invalidate();
        }
    }
    
    public string? GetSelectedText()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _suggestions.Count)
        {
            return _suggestions[_selectedIndex].Text;
        }
        return null;
    }
    
    public void SelectCurrent()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _suggestions.Count)
        {
            var item = _suggestions[_selectedIndex];
            if (item.Type == SuggestionType.Search)
            {
                SearchRequested?.Invoke(_searchEngine + Uri.EscapeDataString(item.Text));
            }
            else
            {
                ItemSelected?.Invoke(item.Text);
            }
            Hide();
        }
    }
    
    private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080;
            cp.ExStyle |= 0x08000000;
            return cp;
        }
    }
    
    protected override bool ShowWithoutActivation => true;
    
    // 处理鼠标消息，允许点击但不激活窗口
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }
    
    /// <summary>
    /// 不获取焦点的按钮
    /// </summary>
    private class NoFocusButton : Button
    {
        public NoFocusButton()
        {
            SetStyle(ControlStyles.Selectable, false);
        }
    }
    
    private class SuggestionItem
    {
        public string Text { get; set; } = "";
        public string? DisplayText { get; set; }
        public SuggestionType Type { get; set; }
        public string Icon { get; set; } = "";
    }
    
    private enum SuggestionType
    {
        History,
        Search,
        Url,
        Bookmark,
        Tab
    }
}
