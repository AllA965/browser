using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 安全图标控件 - 参考"世界之窗"浏览器的绿色小锁设计
/// 支持安全(HTTPS)和不安全(HTTP)两种状态
/// </summary>
public class SecurityIcon : Control
{
    private bool _isSecure = true;
    private bool _isHovered;
    private string _currentUrl = "";
    
    public bool IsSecure
    {
        get => _isSecure;
        set 
        { 
            if (_isSecure != value)
            {
                _isSecure = value; 
                UpdateSize();
                Invalidate(); 
            }
        }
    }
    
    private void UpdateSize()
    {
        Height = DpiHelper.Scale(22);
        // 安全状态下只显示图标，不安全状态下显示图标+文字+背景
        if (_isSecure)
        {
            Width = DpiHelper.Scale(32); // Increased from 28
        }
        else
        {
            Width = DpiHelper.Scale(80); // Increased from 72
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateSize();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
    
    public string CurrentUrl
    {
        get => _currentUrl;
        set { _currentUrl = value; }
    }
    
    public event EventHandler? SecurityInfoRequested;
    
    public SecurityIcon()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        
        UpdateSize();
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        
        var tooltip = new ToolTip();
        MouseEnter += (s, e) => 
        {
            _isHovered = true;
            tooltip.SetToolTip(this, _isSecure ? "连接是安全的" : "连接不安全");
            Invalidate();
        };
        MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
        Click += (s, e) => SecurityInfoRequested?.Invoke(this, EventArgs.Empty);
    }
    
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        
        if (_isSecure)
        {
            // 悬停背景 (Chrome 风格：正圆形)
            if (_isHovered)
            {
                float diameter = Math.Min(Width, Height) - DpiHelper.Scale(2);
                float cx = Width / 2f;
                float cy = Height / 2f;
                using var hoverBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0));
                g.FillEllipse(hoverBrush, cx - diameter / 2f, cy - diameter / 2f, diameter, diameter);
            }
            
            // 安全状态显示灰色 Tune 图标
            DrawTuneIcon(g, Color.FromArgb(95, 99, 104));
        }
        else
        {
            // 不安全状态显示胶囊背景 + 警告图标 + "不安全"文字
            DrawInsecureBadge(g);
        }
    }

    private void DrawInsecureBadge(Graphics g)
    {
        // 1. 绘制胶囊背景
        RectangleF rect = new RectangleF(DpiHelper.Scale(2), DpiHelper.Scale(2), Width - DpiHelper.Scale(4), Height - DpiHelper.Scale(4));
        float radius = rect.Height / 2;
        
        using (var path = CreateRoundedRectPath(rect, radius))
        {
            // 悬停时背景深一点
            using var bgBrush = new SolidBrush(_isHovered ? Color.FromArgb(230, 230, 230) : Color.FromArgb(241, 243, 244));
            g.FillPath(bgBrush, path);
        }

        // 2. 绘制警告三角形图标
        float iconSize = DpiHelper.Scale(12f);
        float iconX = rect.X + DpiHelper.Scale(6);
        float iconY = (Height - iconSize) / 2f;
        
        Color textColor = Color.FromArgb(60, 64, 67);
        using (var pen = new Pen(textColor, DpiHelper.Scale(1.2f)))
        {
            // 三角形顶点
            PointF p1 = new PointF(iconX + iconSize / 2f, iconY);
            PointF p2 = new PointF(iconX, iconY + iconSize);
            PointF p3 = new PointF(iconX + iconSize, iconY + iconSize);
            g.DrawPolygon(pen, new[] { p1, p2, p3 });
            
            // 感叹号
            float centerX = iconX + iconSize / 2f;
            g.DrawLine(pen, centerX, iconY + DpiHelper.Scale(3), centerX, iconY + DpiHelper.Scale(7));
            g.FillEllipse(new SolidBrush(textColor), centerX - DpiHelper.Scale(0.6f), iconY + DpiHelper.Scale(8.5f), DpiHelper.Scale(1.2f), DpiHelper.Scale(1.2f));
        }

        // 3. 绘制 "不安全" 文字
        using (var font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)))
        using (var brush = new SolidBrush(textColor))
        {
            string text = "不安全";
            g.DrawString(text, font, brush, iconX + iconSize + DpiHelper.Scale(4), (Height - g.MeasureString(text, font).Height) / 2f + DpiHelper.Scale(0.5f));
        }
    }

    private static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        
        return path;
    }

    /// <summary>
    /// 绘制 Chrome 风格的“调优/设置(Tune)”图标
    /// </summary>
    private void DrawTuneIcon(Graphics g, Color color)
    {
        float w = Width;
        float h = Height;
        
        // 调整图标比例：使其更接近正方形，减少扁平感
        float iconWidth = DpiHelper.Scale(13f);
        float iconHeight = DpiHelper.Scale(10f); // 稍微缩减高度比例
        float x = (w - iconWidth) / 2f;
        float y = (h - iconHeight) / 2f;

        // 使用稍细的笔触，增加精致感
        using var pen = new Pen(color, DpiHelper.Scale(1.3f));
        using var brush = new SolidBrush(color);

        // 两根横线之间的间距
        float lineSpacing = DpiHelper.Scale(6f);
        // 圆点的直径
        float dotSize = DpiHelper.Scale(4f);
        
        // 1. 第一根横线（圆点在左侧 30% 处）
        float line1Y = y + (iconHeight - lineSpacing) / 2f;
        g.DrawLine(pen, x, line1Y, x + iconWidth, line1Y);
        g.FillEllipse(brush, x + iconWidth * 0.3f - dotSize / 2f, line1Y - dotSize / 2f, dotSize, dotSize);

        // 2. 第二根横线（圆点在右侧 70% 处）
        float line2Y = line1Y + lineSpacing;
        g.DrawLine(pen, x, line2Y, x + iconWidth, line2Y);
        g.FillEllipse(brush, x + iconWidth * 0.7f - dotSize / 2f, line2Y - dotSize / 2f, dotSize, dotSize);
    }
}
