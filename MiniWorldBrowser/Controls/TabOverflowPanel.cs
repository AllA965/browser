using MiniWorldBrowser.Browser;
using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Helpers;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 标签页溢出面板 - 类似 Edge 的标签页下拉面板
/// 支持 BrowserTab 和 IncognitoTab
/// </summary>
public class TabOverflowPanel : Control
{
    private readonly List<TabButton> _overflowTabs = new();
    private readonly Panel _contentPanel;
    private readonly Label _headerLabel;
    private bool _isDarkTheme;
    private const int MaxVisibleItems = 15;
    private const int ItemHeight = 36;
    private const int PanelWidth = 320;
    private const int PanelMaxHeight = 500;

    public event Action<object>? TabClicked;
    public event Action<object>? CloseClicked;
    public event Action? PanelClosed;

    public TabOverflowPanel(bool darkTheme = false)
    {
        _isDarkTheme = darkTheme;
        
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = _isDarkTheme ? Color.FromArgb(43, 44, 48) : Color.White;
        
        Size = new Size(PanelWidth, 0);
        Visible = false;
        
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _isDarkTheme ? Color.FromArgb(43, 44, 48) : Color.White
        };
        
        _headerLabel = new Label
        {
            Text = "打开的标签页",
            Dock = DockStyle.Top,
            Height = 32,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = _isDarkTheme ? Color.FromArgb(200, 200, 200) : Color.FromArgb(100, 100, 100),
            Padding = new Padding(12, 8, 0, 0),
            BackColor = _isDarkTheme ? Color.FromArgb(53, 54, 58) : Color.FromArgb(245, 245, 245)
        };
        
        Controls.Add(_contentPanel);
        Controls.Add(_headerLabel);
    }

    public void UpdateTheme(bool darkTheme)
    {
        if (_isDarkTheme == darkTheme) return;
        _isDarkTheme = darkTheme;
        
        BackColor = darkTheme ? Color.FromArgb(43, 44, 48) : Color.White;
        _contentPanel.BackColor = darkTheme ? Color.FromArgb(43, 44, 48) : Color.White;
        _headerLabel.BackColor = darkTheme ? Color.FromArgb(53, 54, 58) : Color.FromArgb(245, 245, 245);
        _headerLabel.ForeColor = darkTheme ? Color.FromArgb(200, 200, 200) : Color.FromArgb(100, 100, 100);
        
        foreach (var item in _contentPanel.Controls.OfType<TabOverflowItem>())
        {
            item.UpdateTheme(darkTheme);
        }
    }

    public void SetTabs<T>(List<T> tabs, object? activeTab = null) where T : class
    {
        _contentPanel.SuspendLayout();
        _contentPanel.Controls.Clear();
        _overflowTabs.Clear();
        
        var displayTabs = tabs.Take(MaxVisibleItems).ToList();
        int y = 0;
        
        foreach (var tab in displayTabs)
        {
            var tabButton = GetTabButton(tab);
            if (tabButton == null) continue;
            
            var item = new TabOverflowItem(tab, tabButton, _isDarkTheme)
            {
                Location = new Point(0, y),
                Width = PanelWidth - SystemInformation.VerticalScrollBarWidth
            };
            
            item.TabClicked += t => TabClicked?.Invoke(t);
            item.CloseClicked += t => CloseClicked?.Invoke(t);
            
            if (tab == activeTab)
            {
                item.SetActive(true);
            }
            
            _contentPanel.Controls.Add(item);
            _overflowTabs.Add(tabButton);
            y += ItemHeight;
        }
        
        var height = Math.Min(y + _headerLabel.Height, PanelMaxHeight);
        Size = new Size(PanelWidth, height);
        _contentPanel.AutoScrollMinSize = new Size(0, y);
        
        _contentPanel.ResumeLayout(true);
        Invalidate();
    }

    private static TabButton? GetTabButton<T>(T tab) where T : class
    {
        return tab switch
        {
            BrowserTab bt => bt.TabButton,
            IncognitoTab it => it.TabButton,
            _ => null
        };
    }

    public void Show(Point location)
    {
        Location = location;
        BringToFront();
        Visible = true;
        Invalidate();
    }

    public void HidePanel()
    {
        Visible = false;
        PanelClosed?.Invoke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = 8;
        
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
        path.CloseAllFigures();
        
        using var backBrush = new SolidBrush(_isDarkTheme ? Color.FromArgb(43, 44, 48) : Color.White);
        g.FillPath(backBrush, path);
        
        using var pen = new Pen(_isDarkTheme ? Color.FromArgb(80, 80, 80) : Color.FromArgb(220, 220, 220), 1);
        g.DrawPath(pen, path);
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        HidePanel();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (Parent != null)
        {
            Point clientMousePos = Parent.PointToClient(Control.MousePosition);
            if (!Bounds.Contains(clientMousePos))
            {
                HidePanel();
            }
        }
    }
}

/// <summary>
/// 溢出面板中的单个标签项
/// </summary>
internal class TabOverflowItem : Control
{
    private readonly object _tab;
    private bool _isDarkTheme;
    private readonly PictureBox _favicon;
    private readonly Label _titleLabel;
    private readonly Button _closeButton;
    private readonly Panel _activeIndicator;
    private bool _isActive;

    public event Action<object>? TabClicked;
    public event Action<object>? CloseClicked;

    public TabButton TabButton { get; }

    public TabOverflowItem(object tab, TabButton tabButton, bool darkTheme)
    {
        _tab = tab;
        _isDarkTheme = darkTheme;
        TabButton = tabButton;
        
        Height = 36;
        Cursor = Cursors.Hand;
        
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = GetBaseBackColor();
        
        _activeIndicator = new Panel
        {
            Dock = DockStyle.Left,
            Width = 3,
            BackColor = _isDarkTheme ? Color.FromArgb(0, 120, 215) : Color.FromArgb(0, 120, 215),
            Visible = false
        };
        
        _favicon = new PictureBox
        {
            Location = new Point(8, 8),
            Size = new Size(20, 20),
            SizeMode = PictureBoxSizeMode.StretchImage
        };
        
        _titleLabel = new Label
        {
            Location = new Point(36, 8),
            AutoSize = false,
            Size = new Size(200, 20),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = _isDarkTheme ? Color.FromArgb(220, 220, 220) : Color.FromArgb(60, 60, 60)
        };
        
        _closeButton = new Button
        {
            Location = new Point(280, 6),
            Size = new Size(24, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Text = "×",
            Font = new Font("Microsoft YaHei UI", 10F),
            ForeColor = _isDarkTheme ? Color.FromArgb(180, 180, 180) : Color.FromArgb(140, 140, 140),
            Cursor = Cursors.Hand
        };
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.MouseEnter += (s, e) => 
        {
            _closeButton.BackColor = _isDarkTheme ? Color.FromArgb(80, 80, 80) : Color.FromArgb(230, 230, 230);
            _closeButton.ForeColor = _isDarkTheme ? Color.White : Color.FromArgb(60, 60, 60);
        };
        _closeButton.MouseLeave += (s, e) =>
        {
            _closeButton.BackColor = Color.Transparent;
            _closeButton.ForeColor = _isDarkTheme ? Color.FromArgb(180, 180, 180) : Color.FromArgb(140, 140, 140);
        };
        _closeButton.Click += (s, e) => CloseClicked?.Invoke(_tab);
        
        Controls.Add(_activeIndicator);
        Controls.Add(_favicon);
        Controls.Add(_titleLabel);
        Controls.Add(_closeButton);
        
        UpdateContent();
        
        MouseEnter += (s, e) => 
        {
            BackColor = _isDarkTheme ? Color.FromArgb(60, 60, 60) : Color.FromArgb(240, 240, 240);
        };
        MouseLeave += (s, e) =>
        {
            BackColor = GetBaseBackColor();
        };
        Click += (s, e) => TabClicked?.Invoke(_tab);
    }

    private Color GetBaseBackColor()
    { 
        return _isDarkTheme ? Color.FromArgb(43, 44, 48) : Color.White;
    }

    public void UpdateTheme(bool darkTheme)
    {
        _isDarkTheme = darkTheme;
        BackColor = GetBaseBackColor();
        _activeIndicator.BackColor = Color.FromArgb(0, 120, 215);
        _titleLabel.ForeColor = darkTheme ? Color.FromArgb(220, 220, 220) : Color.FromArgb(60, 60, 60);
        _closeButton.ForeColor = darkTheme ? Color.FromArgb(180, 180, 180) : Color.FromArgb(140, 140, 140);
        Invalidate();
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        _activeIndicator.Visible = active;
        Invalidate();
    }

    private void UpdateContent()
    {
        string title = "新标签页";
        string url = "";
        
        if (_tab is BrowserTab bt)
        {
            title = !string.IsNullOrEmpty(bt.Title) ? bt.Title : "新标签页";
            url = bt.Url ?? "";
        }
        else if (_tab is IncognitoTab it)
        {
            title = !string.IsNullOrEmpty(it.Title) ? it.Title : "新标签页";
            url = it.Url ?? "";
        }
        
        _titleLabel.Text = TruncateText(title, 180);
        
        if (!string.IsNullOrEmpty(url))
        {
            LoadFavicon(url);
        }
    }

    private string TruncateText(string text, int maxWidth)
    {
        using var g = CreateGraphics();
        var textSize = g.MeasureString(text, _titleLabel.Font);
        if (textSize.Width <= maxWidth) return text;
        
        for (int i = text.Length; i > 0; i--)
        {
            var truncated = text.Substring(0, i) + "...";
            if (g.MeasureString(truncated, _titleLabel.Font).Width <= maxWidth)
            {
                return truncated;
            }
        }
        
        return text.Substring(0, 10) + "...";
    }

    private void LoadFavicon(string url)
    {
        try
        {
            var favicon = FaviconHelper.GetCachedFavicon(url);
            if (favicon != null)
            {
                _favicon.Image = favicon;
            }
        }
        catch { }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        if (_isActive)
        {
            using var brush = new SolidBrush(_isDarkTheme ? Color.FromArgb(50, 50, 55) : Color.FromArgb(235, 235, 235));
            e.Graphics.FillRectangle(brush, 0, 0, Width, Height);
        }
    }
}
