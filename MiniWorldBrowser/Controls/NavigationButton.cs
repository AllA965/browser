using System.Drawing.Drawing2D;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 导航按钮类型
/// </summary>
public enum NavigationButtonType
{
    Back,
    Forward,
    Refresh,
    Stop,
    Home
}

/// <summary>
/// 导航按钮控件 - 自定义绘制 Edge 风格图标
/// </summary>
public class NavigationButton : Control
{
    private bool _isHovered;
    private bool _isPressed;
    private int _cornerRadius = 6;
    private NavigationButtonType _buttonType = NavigationButtonType.Back;

    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; Invalidate(); }
    }

    public NavigationButtonType ButtonType
    {
        get => _buttonType;
        set { _buttonType = value; Invalidate(); }
    }

    public Color HoverBackColor { get; set; } = Color.FromArgb(220, 220, 220);
    public Color PressedBackColor { get; set; } = Color.FromArgb(200, 200, 200);
    public Color IconColor { get; set; } = Color.FromArgb(80, 80, 80);
    public Color DisabledIconColor { get; set; } = Color.FromArgb(180, 180, 180);

    public NavigationButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);

        BackColor = Color.Transparent;
        Size = new Size(32, 32);
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // 绘制背景
        Color bgColor;
        if (!Enabled)
            bgColor = Color.Transparent;
        else if (_isPressed)
            bgColor = PressedBackColor;
        else if (_isHovered)
            bgColor = HoverBackColor;
        else
            bgColor = BackColor;

        if (bgColor != Color.Transparent && bgColor.A > 0)
        {
            using var path = CreateRoundedRect(rect, _cornerRadius);
            using var brush = new SolidBrush(bgColor);
            g.FillPath(brush, path);
        }

        // 绘制图标
        switch (_buttonType)
        {
            case NavigationButtonType.Back:
                DrawBackIcon(g);
                break;
            case NavigationButtonType.Forward:
                DrawForwardIcon(g);
                break;
            case NavigationButtonType.Refresh:
                DrawRefreshIcon(g);
                break;
            case NavigationButtonType.Stop:
                DrawStopIcon(g);
                break;
            case NavigationButtonType.Home:
                DrawHomeIcon(g);
                break;
        }
    }
    
    private void DrawHomeIcon(Graphics g)
    {
        var color = Enabled ? IconColor : DisabledIconColor;
        float penWidth = 1.8f;

        float centerX = Width / 2f;
        float centerY = Height / 2f;
        float size = Math.Min(Width, Height) * 0.35f;

        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        // 绘制更简洁、现代的 Edge 风格房子图标
        // 1. 绘制屋顶（V形向上）
        float roofTop = centerY - size * 0.9f;
        float roofLeft = centerX - size;
        float roofRight = centerX + size;
        float roofBaseY = centerY - size * 0.1f;

        g.DrawLine(pen, roofLeft, roofBaseY, centerX, roofTop);
        g.DrawLine(pen, centerX, roofTop, roofRight, roofBaseY);

        // 2. 绘制房子主体轮廓（不带底部的矩形，更透气）
        float houseWidth = size * 1.5f;
        float houseLeft = centerX - houseWidth / 2f;
        float houseRight = centerX + houseWidth / 2f;
        float houseBottom = centerY + size * 0.9f;
        
        // 侧墙
        g.DrawLine(pen, houseLeft, roofBaseY + 1, houseLeft, houseBottom);
        g.DrawLine(pen, houseRight, roofBaseY + 1, houseRight, houseBottom);
        
        // 底边（带一点向内的间隙，或者直接连通）
        g.DrawLine(pen, houseLeft, houseBottom, houseRight, houseBottom);

        // 3. 绘制门（仅用一根短竖线或一个小倒 U 简化，这里选择倒 U 且不闭合底部）
        float doorWidth = size * 0.5f;
        float doorHeight = size * 0.6f;
        float doorLeft = centerX - doorWidth / 2;
        float doorRight = centerX + doorWidth / 2;
        float doorTop = houseBottom - doorHeight;

        g.DrawLine(pen, doorLeft, houseBottom, doorLeft, doorTop);
        g.DrawLine(pen, doorLeft, doorTop, doorRight, doorTop);
        g.DrawLine(pen, doorRight, doorTop, doorRight, houseBottom);
    }

    private void DrawBackIcon(Graphics g)
    {
        var color = Enabled ? IconColor : DisabledIconColor;
        float penWidth = 2f;

        float centerX = Width / 2f;
        float centerY = Height / 2f;
        float size = Math.Min(Width, Height) * 0.35f;

        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        // 绘制横线
        float lineLeft = centerX - size * 0.9f;
        float lineRight = centerX + size * 0.7f;
        g.DrawLine(pen, lineLeft, centerY, lineRight, centerY);

        // 绘制V形箭头（左侧）
        float arrowSize = size * 0.6f;
        g.DrawLine(pen, lineLeft + arrowSize, centerY - arrowSize, lineLeft, centerY);
        g.DrawLine(pen, lineLeft + arrowSize, centerY + arrowSize, lineLeft, centerY);
    }

    private void DrawForwardIcon(Graphics g)
    {
        var color = Enabled ? IconColor : DisabledIconColor;
        float penWidth = 2f;

        float centerX = Width / 2f;
        float centerY = Height / 2f;
        float size = Math.Min(Width, Height) * 0.35f;

        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        // 绘制横线
        float lineLeft = centerX - size * 0.7f;
        float lineRight = centerX + size * 0.9f;
        g.DrawLine(pen, lineLeft, centerY, lineRight, centerY);

        // 绘制V形箭头（右侧）
        float arrowSize = size * 0.6f;
        g.DrawLine(pen, lineRight - arrowSize, centerY - arrowSize, lineRight, centerY);
        g.DrawLine(pen, lineRight - arrowSize, centerY + arrowSize, lineRight, centerY);
    }

    private void DrawRefreshIcon(Graphics g)
    {
        var color = Enabled ? IconColor : DisabledIconColor;
        float penWidth = 2f;

        float centerX = Width / 2f;
        float centerY = Height / 2f;
        float radius = Math.Min(Width, Height) * 0.28f;

        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        // 绘制圆弧：从 0 度开始，顺时针旋转 285 度（留出一点间隙避免重叠）
        var arcRect = new RectangleF(centerX - radius, centerY - radius, radius * 2, radius * 2);
        g.DrawArc(pen, arcRect, 0, 285);

        // 箭头位置：圆弧末端稍后一点（300度位置）
        float arrowAngle = 300 * (float)(Math.PI / 180);
        float arrowX = centerX + radius * (float)Math.Cos(arrowAngle);
        float arrowY = centerY + radius * (float)Math.Sin(arrowAngle);

        // 箭头方向：切线方向（垂直于半径，顺时针）
        // 半径方向是 300 度，切线方向是 300 + 90 = 390 度 (即 30 度)
        float tangentAngle = (300 + 90) * (float)(Math.PI / 180);
        float arrowSize = radius * 0.5f;

        // 箭头翼角（相对于切线反方向偏转）
        float wingAngle = (float)(Math.PI * 0.2); // 约 36 度
        
        // 计算两个翼的末端点
        float x1 = arrowX - arrowSize * (float)Math.Cos(tangentAngle - wingAngle);
        float y1 = arrowY - arrowSize * (float)Math.Sin(tangentAngle - wingAngle);
        
        float x2 = arrowX - arrowSize * (float)Math.Cos(tangentAngle + wingAngle);
        float y2 = arrowY - arrowSize * (float)Math.Sin(tangentAngle + wingAngle);

        // 绘制箭头
        g.DrawLine(pen, arrowX, arrowY, x1, y1);
        g.DrawLine(pen, arrowX, arrowY, x2, y2);
    }

    private void DrawStopIcon(Graphics g)
    {
        var color = Enabled ? IconColor : DisabledIconColor;
        float penWidth = 2f;

        float centerX = Width / 2f;
        float centerY = Height / 2f;
        float size = Math.Min(Width, Height) * 0.25f;

        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        // 绘制X形
        g.DrawLine(pen, centerX - size, centerY - size, centerX + size, centerY + size);
        g.DrawLine(pen, centerX + size, centerY - size, centerX - size, centerY + size);
    }

    private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
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

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }
}
