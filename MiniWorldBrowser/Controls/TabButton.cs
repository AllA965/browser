using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Helpers.Extensions;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 标签按钮控件 - Edge 风格，支持亮色和深色主题
/// </summary>
public class TabButton : Panel
{
    public string TabId { get; set; } = "";
    public bool IsActive { get; private set; }
    public bool IsPinned => _isPinned;

    public int PreferredWidth { get; set; } = DpiHelper.Scale(200);

    private static readonly int CompactThresholdWidth = DpiHelper.Scale(70);

    private bool IsCompact => _isPinned || Width <= CompactThresholdWidth;
    
    /// <summary>
    /// 是否启用右击关闭标签功能（按住Shift右击显示菜单）
    /// </summary>
    public bool RightClickToClose { get; set; } = false;

    private readonly PictureBox _favicon;
    private bool _isPinned;
    private readonly Label _titleLabel;
    private readonly PictureBox _loadingIndicator;
    private readonly TabCloseButton _closeButton;
    private bool _isHovering;
    private bool _isLoading;
    private bool _isDarkTheme;
    private float _animationProgress = 1f;
    private System.Windows.Forms.Timer? _animationTimer;

    public event Action<TabButton>? TabClicked;
    public event Action<TabButton>? CloseClicked;
    public event Action<TabButton>? NewTabRequested;
    public event Action<TabButton>? RefreshRequested;
    public event Action<TabButton>? DuplicateRequested;
    public event Action<TabButton>? PinRequested;
    public event Action<TabButton>? CloseOthersRequested;
    public event Action<TabButton>? CloseLeftRequested;
    public event Action<TabButton>? CloseRightRequested;
    public event Action<TabButton>? ReopenClosedRequested;
    public event Action<TabButton>? BookmarkAllRequested;

    // 亮色主题颜色
    private static readonly Color LightActiveBg = Color.White;
    private static readonly Color LightInactiveBg = Color.FromArgb(230, 230, 230);
    private static readonly Color LightHoverBg = Color.FromArgb(240, 240, 240);
    private static readonly Color LightForeColor = Color.FromArgb(60, 60, 60);

    // 深色主题颜色（隐身模式）
    private static readonly Color DarkActiveBg = Color.Black;
    private static readonly Color DarkInactiveBg = Color.FromArgb(35, 35, 35);
    private static readonly Color DarkHoverBg = Color.FromArgb(50, 50, 50);
    private static readonly Color DarkForeColor = Color.White;

    private Color ActiveBg => _isDarkTheme ? DarkActiveBg : LightActiveBg;
    private Color InactiveBg => _isDarkTheme ? DarkInactiveBg : LightInactiveBg;
    private Color HoverBg => _isDarkTheme ? DarkHoverBg : LightHoverBg;
    private Color TextColor => _isDarkTheme ? DarkForeColor : LightForeColor;

    private SolidBrush? _activeBrush;
    private SolidBrush? _inactiveBrush;
    private SolidBrush? _hoverBrush;

    private SolidBrush GetBrush(Color color)
    {
        if (color == ActiveBg) return _activeBrush ??= new SolidBrush(ActiveBg);
        if (color == InactiveBg) return _inactiveBrush ??= new SolidBrush(InactiveBg);
        if (color == HoverBg) return _hoverBrush ??= new SolidBrush(HoverBg);
        return new SolidBrush(color);
    }

    public TabButton(bool darkTheme = false)
    {
        _isDarkTheme = darkTheme;

        Height = DpiHelper.Scale(32);
        Width = PreferredWidth;
        Margin = new Padding(DpiHelper.Scale(1), 0, 0, 0);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        BorderStyle = BorderStyle.None;
        DoubleBuffered = true;

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.ResizeRedraw, true);

        _favicon = new PictureBox
        {
            Size = DpiHelper.Scale(new Size(16, 16)),
            Location = DpiHelper.Scale(new Point(10, 8)),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        _loadingIndicator = new PictureBox
        {
            Size = DpiHelper.Scale(new Size(16, 16)),
            Location = DpiHelper.Scale(new Point(10, 8)),
            BackColor = Color.Transparent,
            Visible = false
        };

        _titleLabel = new Label
        {
            AutoSize = false,
            Location = DpiHelper.Scale(new Point(30, 8)),
            Size = DpiHelper.Scale(new Size(120, 16)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            Text = "新标签页",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoEllipsis = true,
            Cursor = Cursors.Hand
        };

        _closeButton = new TabCloseButton(_isDarkTheme)
        {
            Visible = false
        };
        _closeButton.Click += (s, e) => CloseClicked?.Invoke(this);

        Controls.Add(_favicon);
        Controls.Add(_loadingIndicator);
        Controls.Add(_titleLabel);
        Controls.Add(_closeButton);

        // 事件绑定
        _titleLabel.Click += (s, e) => TabClicked?.Invoke(this);
        _titleLabel.DoubleClick += (s, e) => CloseClicked?.Invoke(this);
        _favicon.Click += (s, e) => TabClicked?.Invoke(this);
        Click += (s, e) => TabClicked?.Invoke(this);

        MouseEnter += OnMouseEnterTab;
        MouseLeave += OnMouseLeaveTab;
        _titleLabel.MouseEnter += OnMouseEnterTab;
        _titleLabel.MouseLeave += OnMouseLeaveTab;
        _favicon.MouseEnter += OnMouseEnterTab;
        _favicon.MouseLeave += OnMouseLeaveTab;

        // 右键菜单
        MouseUp += OnTabMouseUp;
        _titleLabel.MouseUp += OnTabMouseUp;
        _favicon.MouseUp += OnTabMouseUp;

        SetActive(false);

        UpdateLayoutForWidth();

        VisibleChanged += (s, e) =>
        {
            if (Visible)
            {
                _favicon.Refresh();
                _closeButton.Refresh();
            }
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 计算背景颜色
        Color bgColor;
        if (IsActive)
            bgColor = ActiveBg;
        else if (_isHovering)
            bgColor = HoverBg;
        else
            bgColor = InactiveBg;

        // 绘制圆角矩形背景（只有顶部圆角）
        var rect = new Rectangle(0, 0, Width - 1, Height);
        using var path = CreateTopRoundedRect(rect, DpiHelper.Scale(8));
        g.FillPath(GetBrush(bgColor), path);

        // 激活状态下绘制底部连接线（与内容区域连接）
        if (IsActive)
        {
            int lineHeight = DpiHelper.Scale(2);
            g.FillRectangle(GetBrush(ActiveBg), 0, Height - lineHeight, Width, lineHeight);
        }
    }

    private static GraphicsPath CreateTopRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;

        // 左上角圆弧
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        // 右上角圆弧
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        // 右下角（直角）
        path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom);
        // 底边
        path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
        // 左下角（直角）
        path.AddLine(rect.X, rect.Bottom, rect.X, rect.Y + radius);

        path.CloseFigure();
        return path;
    }

    public void SetLoading(bool isLoading)
    {
        _isLoading = isLoading;
        _loadingIndicator.Visible = isLoading;
        _favicon.Visible = !isLoading;
    }

    public async void SetFavicon(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            if (!IsDisposed && _favicon != null && !_favicon.IsDisposed)
                _favicon.Image = null;
            return;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var data = await client.GetByteArrayAsync(url);

            // 检查控件是否已被释放
            if (IsDisposed || _favicon == null || _favicon.IsDisposed)
                return;

            using var ms = new MemoryStream(data);
            using var tempImage = Image.FromStream(ms);
            var bitmap = new Bitmap(tempImage.Width, tempImage.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(tempImage, 0, 0, tempImage.Width, tempImage.Height);
            }

            // 再次检查控件是否已被释放
            if (IsDisposed || _favicon == null || _favicon.IsDisposed)
            {
                bitmap.Dispose();
                return;
            }

            var oldImage = _favicon.Image;
            _favicon.Image = bitmap;
            oldImage?.Dispose();
        }
        catch
        {
            if (!IsDisposed && _favicon != null && !_favicon.IsDisposed)
                _favicon.Image = null;
        }
    }

    public void SetTitle(string title)
    {
        _titleLabel.Text = title;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        if (!IsCompact && (active || _isHovering))
            _closeButton.ShowButton();
        else
            _closeButton.HideButton();
            
        _titleLabel.Visible = !IsCompact;
        _titleLabel.ForeColor = TextColor;
        Invalidate();
    }

    public void SetPinned(bool pinned)
    {
        _isPinned = pinned;
        if (pinned)
        {
            Width = DpiHelper.Scale(40);
        }
        else
        {
            Width = PreferredWidth;
        }
        UpdateLayoutForWidth();
        Invalidate();
    }

    /// <summary>
    /// 动画过渡到指定宽度
    /// </summary>
    public void AnimateToWidth(int targetWidth, int? startWidth = null)
    {
        if (Width == targetWidth && !startWidth.HasValue && (_animationTimer == null || !_animationTimer.Enabled)) return;
        
        // 如果是关闭动画正在进行中，不要被其他宽度动画打断，但允许继续执行关闭动画
        if (_isClosing && targetWidth != 0) return;

        _targetWidth = targetWidth;
        _startWidth = startWidth ?? Width;
        
        if (startWidth.HasValue)
        {
            Width = startWidth.Value;
        }

        _animationProgress = 0f;

        if (_animationTimer == null)
        {
            _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animationTimer.Tick += OnAnimationTick;
        }
        
        if (!_animationTimer.Enabled)
        {
            _animationTimer.Start();
        }
    }

    private int _targetWidth;
    private int _startWidth;
    private bool _isClosing;

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        _animationProgress += 0.15f; // 稍微加快步进，原来是 0.1f
        if (_animationProgress >= 1f)
        {
            _animationProgress = 1f;
            Width = _targetWidth;
            _animationTimer?.Stop();
            
            if (_isClosing)
            {
                _isClosing = false;
                _onCloseComplete?.Invoke();
            }
            return;
        }

        // 优化缓动函数：使用更轻快的 Quad Out
        float eased = 1f - (1f - _animationProgress) * (1f - _animationProgress);
        Width = (int)(_startWidth + (_targetWidth - _startWidth) * eased);
        
        // 确保布局更新 - 只有在没有父容器自动布局时才需要，或者手动触发一次以确保平滑
        // 对于 FlowLayoutPanel，改变 Width 会自动触发布局，但显式调用可能更平滑
        // if (Parent != null) Parent.PerformLayout();
    }

    private Action? _onCloseComplete;

    /// <summary>
    /// 播放关闭动画
    /// </summary>
    public void PlayCloseAnimation(Action onComplete)
    {
        _isClosing = true;
        _onCloseComplete = onComplete;
        AnimateToWidth(0);
    }

    /// <summary>
    /// 播放出现动画
    /// </summary>
    public void PlayShowAnimation()
    {
        _animationProgress = 0f;
        int minWidth = DpiHelper.Scale(40);
        Width = minWidth; // 从小宽度开始
        _startWidth = minWidth;

        _targetWidth = _isPinned ? minWidth : PreferredWidth;
        _isClosing = false;

        if (_animationTimer == null)
        {
            _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animationTimer.Tick += OnAnimationTick;
        }
        _animationTimer.Start();
    }

    private void OnMouseEnterTab(object? sender, EventArgs e)
    {
        _isHovering = true;
        if (!IsCompact)
            _closeButton.ShowButton();
        Invalidate();
    }

    private void OnMouseLeaveTab(object? sender, EventArgs e)
    {
        var pos = PointToClient(Cursor.Position);
        if (!ClientRectangle.Contains(pos))
        {
            _isHovering = false;
            if (!IsCompact && IsActive)
                _closeButton.ShowButton();
            else
                _closeButton.HideButton();
            Invalidate();
        }
    }

    private void OnTabMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            // 如果启用了右击关闭功能
            if (RightClickToClose)
            {
                // 按住Shift时显示菜单，否则直接关闭
                if (Control.ModifierKeys.HasFlag(Keys.Shift))
                {
                    ShowContextMenu(PointToScreen(e.Location));
                }
                else
                {
                    CloseClicked?.Invoke(this);
                }
            }
            else
            {
                // 默认行为：显示右键菜单
                ShowContextMenu(PointToScreen(e.Location));
            }
        }
    }

    private void ShowContextMenu(Point screenLocation)
    {
        var menu = new ContextMenuStrip
        {
            Font = new Font("Microsoft YaHei UI", 9),
            ShowImageMargin = false
        };

        // 打开新的标签页
        var newTab = new ToolStripMenuItem("打开新的标签页") { ShortcutKeyDisplayString = "Ctrl+T" };
        newTab.Click += (s, e) => NewTabRequested?.Invoke(this);
        menu.Items.Add(newTab);

        menu.Items.Add(new ToolStripSeparator());

        // 重新加载
        var reload = new ToolStripMenuItem("重新加载") { ShortcutKeyDisplayString = "Ctrl+R" };
        reload.Click += (s, e) => RefreshRequested?.Invoke(this);
        menu.Items.Add(reload);

        // 复制
        var duplicate = new ToolStripMenuItem("复制");
        duplicate.Click += (s, e) => DuplicateRequested?.Invoke(this);
        menu.Items.Add(duplicate);

        // 固定标签页
        var pin = new ToolStripMenuItem(_isPinned ? "取消固定标签页" : "固定标签页");
        pin.Click += (s, e) => PinRequested?.Invoke(this);
        menu.Items.Add(pin);

        menu.Items.Add(new ToolStripSeparator());

        // 关闭标签页
        var close = new ToolStripMenuItem("关闭标签页") { ShortcutKeyDisplayString = "Ctrl+W" };
        close.Click += (s, e) => CloseClicked?.Invoke(this);
        menu.Items.Add(close);

        // 关闭其他标签页
        var closeOthers = new ToolStripMenuItem("关闭其他标签页");
        closeOthers.Click += (s, e) => CloseOthersRequested?.Invoke(this);
        menu.Items.Add(closeOthers);

        // 关闭左侧标签页
        var closeLeft = new ToolStripMenuItem("关闭左侧标签页");
        closeLeft.Click += (s, e) => CloseLeftRequested?.Invoke(this);
        menu.Items.Add(closeLeft);

        // 关闭右侧标签页
        var closeRight = new ToolStripMenuItem("关闭右侧标签页");
        closeRight.Click += (s, e) => CloseRightRequested?.Invoke(this);
        menu.Items.Add(closeRight);

        menu.Items.Add(new ToolStripSeparator());

        // 重新打开关闭的标签页
        var reopen = new ToolStripMenuItem("重新打开关闭的标签页(E)") { ShortcutKeyDisplayString = "Ctrl+E" };
        reopen.Click += (s, e) => ReopenClosedRequested?.Invoke(this);
        menu.Items.Add(reopen);

        // 为所有标签页添加收藏
        var bookmarkAll = new ToolStripMenuItem("为所有标签页添加收藏...") { ShortcutKeyDisplayString = "Ctrl+Shift+D" };
        bookmarkAll.Click += (s, e) => BookmarkAllRequested?.Invoke(this);
        menu.Items.Add(bookmarkAll);

        menu.Show(screenLocation);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateLayoutForWidth();
    }

    private void UpdateLayoutForWidth()
    {
        if (IsDisposed) return;

        if (_favicon == null || _loadingIndicator == null || _titleLabel == null || _closeButton == null) return;

        if (IsCompact)
        {
            var x = Math.Max(0, (Width - DpiHelper.Scale(16)) / 2);
            _favicon.Location = new Point(x, DpiHelper.Scale(8));
            _loadingIndicator.Location = new Point(x, DpiHelper.Scale(8));
            _titleLabel.Visible = false;
            _closeButton.HideButton();
        }
        else
        {
            _favicon.Location = DpiHelper.Scale(new Point(10, 8));
            _loadingIndicator.Location = DpiHelper.Scale(new Point(10, 8));
            _titleLabel.Visible = true;
            _titleLabel.Location = DpiHelper.Scale(new Point(30, 8));
            
            // 优化：使用 PreferredWidth 而不是当前 Width，防止动画过程中文字逐个出现的卡顿感
            // 让 Panel 的剪裁机制处理视觉上的缩减，而不是让 Label 频繁重新布局文字
            int targetLabelWidth = Math.Max(0, PreferredWidth - DpiHelper.Scale(30 + 28));
            if (_titleLabel.Width != targetLabelWidth)
            {
                _titleLabel.Width = targetLabelWidth;
            }

            _closeButton.Location = new Point(Width - DpiHelper.Scale(28), DpiHelper.Scale(4));
            
            if (IsActive || _isHovering)
                _closeButton.ShowButton();
            else
                _closeButton.HideButton();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer?.Stop();
            _animationTimer?.Dispose();
            _favicon.Image?.Dispose();
            _loadingIndicator.Image?.Dispose();
            _activeBrush?.Dispose();
            _inactiveBrush?.Dispose();
            _hoverBrush?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// 标签关闭按钮 - 自定义绘制 X 图标
/// </summary>
public class TabCloseButton : Control
{
    private bool _isHovered;
    private bool _isPressed;
    private readonly bool _isDarkTheme;
    private float _opacity = 1f;
    private System.Windows.Forms.Timer _fadeTimer;
    private bool _fadingIn;

    public TabCloseButton(bool darkTheme = false)
    {
        _isDarkTheme = darkTheme;

        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);

        BackColor = Color.Transparent;
        Size = DpiHelper.Scale(new Size(24, 24));
        Cursor = Cursors.Hand;
        
        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += (s, e) =>
        {
            float step = 0.15f;
            if (_fadingIn)
            {
                if (_opacity < 1f)
                {
                    _opacity += step;
                    if (_opacity > 1f) _opacity = 1f;
                    Invalidate();
                }
                else
                {
                    _fadeTimer.Stop();
                }
            }
            else
            {
                if (_opacity > 0f)
                {
                    _opacity -= step;
                    if (_opacity < 0f) _opacity = 0f;
                    Invalidate();
                }
                else
                {
                    _fadeTimer.Stop();
                    Visible = false;
                }
            }
        };
    }

    public void ShowButton()
    {
        if (_opacity >= 1f && Visible) return;
        _fadingIn = true;
        if (!Visible)
        {
            _opacity = 0f;
            Visible = true;
        }
        _fadeTimer.Start();
    }

    public void HideButton()
    {
        if (_opacity <= 0f && !Visible) return;
        _fadingIn = false;
        _fadeTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_opacity <= 0) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // 绘制背景
        if (_isPressed)
        {
            using var brush = new SolidBrush(Color.FromArgb((int)(255 * _opacity), 196, 43, 28));
            using var path = CreateRoundedRect(rect, DpiHelper.Scale(4));
            g.FillPath(brush, path);
        }
        else if (_isHovered)
        {
            using var brush = new SolidBrush(Color.FromArgb((int)(255 * _opacity), 232, 17, 35));
            using var path = CreateRoundedRect(rect, DpiHelper.Scale(4));
            g.FillPath(brush, path);
        }

        // 绘制 X 图标
        float centerX = Width / 2f;
        float centerY = Height / 2f;
        float size = DpiHelper.Scale(4f);

        var baseIconColor = (_isHovered || _isPressed) ? Color.White :
                        (_isDarkTheme ? Color.FromArgb(180, 180, 180) : Color.FromArgb(100, 100, 100));
        var iconColor = Color.FromArgb((int)(255 * _opacity), baseIconColor);

        using var pen = new Pen(iconColor, DpiHelper.Scale(1.5f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        g.DrawLine(pen, centerX - size, centerY - size, centerX + size, centerY + size);
        g.DrawLine(pen, centerX + size, centerY - size, centerX - size, centerY + size);
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

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _isPressed = false;
        Invalidate();
    }
}
