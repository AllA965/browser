using System.Drawing.Drawing2D;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// Edge 风格菜单渲染器
/// </summary>
public class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly bool _isDarkMode;
    private readonly Color _backgroundColor;
    private readonly Color _hoverColor;
    private readonly Color _selectedColor = Color.FromArgb(0, 103, 192); // Edge 蓝色
    private readonly Color _normalTextColor;
    private readonly Color _shortcutTextColor;
    private readonly Color _separatorColor;
    private readonly Color _borderColor;
    private readonly Color _iconColor;
    private readonly Color _checkColor = Color.FromArgb(0, 103, 192);

    public ModernMenuRenderer(bool isDarkMode = false) : base(new EdgeColorTable(isDarkMode))
    {
        _isDarkMode = isDarkMode;
        if (isDarkMode)
        {
            _backgroundColor = Color.FromArgb(45, 45, 48);
            _hoverColor = Color.FromArgb(62, 62, 64);
            _normalTextColor = Color.FromArgb(241, 241, 241);
            _shortcutTextColor = Color.FromArgb(180, 180, 180);
            _separatorColor = Color.FromArgb(70, 70, 70);
            _borderColor = Color.FromArgb(80, 80, 80);
            _iconColor = Color.FromArgb(220, 220, 220);
        }
        else
        {
            _backgroundColor = Color.FromArgb(249, 249, 249);
            _hoverColor = Color.FromArgb(232, 232, 232);
            _normalTextColor = Color.FromArgb(32, 32, 32);
            _shortcutTextColor = Color.FromArgb(96, 96, 96);
            _separatorColor = Color.FromArgb(224, 224, 224);
            _borderColor = Color.FromArgb(204, 204, 204);
            _iconColor = Color.FromArgb(32, 32, 32);
        }
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 绘制圆角背景
        var rect = new Rectangle(0, 0, e.ToolStrip.Width, e.ToolStrip.Height);
        using var brush = new SolidBrush(_backgroundColor);
        using var path = CreateRoundedRect(rect, 4);
        g.FillPath(brush, path);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using var pen = new Pen(_borderColor);
        using var path = CreateRoundedRect(rect, 4);
        g.DrawPath(pen, path);

        // 绘制阴影效果（隐身模式下阴影更深或不绘制）
        if (!_isDarkMode)
        {
            using var shadowPen = new Pen(Color.FromArgb(20, 0, 0, 0));
            g.DrawLine(shadowPen, 4, e.ToolStrip.Height, e.ToolStrip.Width - 1, e.ToolStrip.Height);
            g.DrawLine(shadowPen, e.ToolStrip.Width, 4, e.ToolStrip.Width, e.ToolStrip.Height - 1);
        }
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is ToolStripSeparator) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);

        if (e.Item.Selected && e.Item.Enabled)
        {
            using var brush = new SolidBrush(_hoverColor);
            using var path = CreateRoundedRect(rect, 4);
            g.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item == null) return;

        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (!e.Item.Enabled)
            e.TextColor = _isDarkMode ? Color.FromArgb(100, 100, 100) : Color.FromArgb(160, 160, 160);
        else
            e.TextColor = _normalTextColor;

        var textRect = e.TextRectangle;
        textRect.X = 44;
        e.TextRectangle = textRect;

        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        if (e.Item == null) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var arrowColor = e.Item.Enabled ? _iconColor : (_isDarkMode ? Color.FromArgb(100, 100, 100) : Color.FromArgb(160, 160, 160));
        var centerY = e.ArrowRectangle.Y + e.ArrowRectangle.Height / 2;
        var centerX = e.ArrowRectangle.X + e.ArrowRectangle.Width / 2;

        using var pen = new Pen(arrowColor, 1.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        g.DrawLine(pen, centerX - 2, centerY - 4, centerX + 2, centerY);
        g.DrawLine(pen, centerX + 2, centerY, centerX - 2, centerY + 4);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Item == null) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var centerX = 22;
        var centerY = e.Item.Height / 2;

        using var pen = new Pen(_checkColor, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        g.DrawLine(pen, centerX - 5, centerY, centerX - 2, centerY + 3);
        g.DrawLine(pen, centerX - 2, centerY + 3, centerX + 4, centerY - 3);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        var y = e.Item.Height / 2;
        using var pen = new Pen(_separatorColor);
        g.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
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
}

/// <summary>
/// Edge 风格颜色表
/// </summary>
public class EdgeColorTable : ProfessionalColorTable
{
    private readonly bool _isDarkMode;

    public EdgeColorTable(bool isDarkMode = false)
    {
        _isDarkMode = isDarkMode;
    }

    public override Color MenuItemSelected => _isDarkMode ? Color.FromArgb(62, 62, 64) : Color.FromArgb(232, 232, 232);
    public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
    public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuBorder => _isDarkMode ? Color.FromArgb(80, 80, 80) : Color.FromArgb(204, 204, 204);
    public override Color ToolStripDropDownBackground => _isDarkMode ? Color.FromArgb(45, 45, 48) : Color.FromArgb(249, 249, 249);
    public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
    public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
    public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
    public override Color SeparatorDark => _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(224, 224, 224);
    public override Color SeparatorLight => ToolStripDropDownBackground;
}

/// <summary>
/// 菜单图标绘制器 - 绘制 Edge 风格的线条图标
/// </summary>
public static class MenuIconDrawer
{
    private static readonly Color IconColor = Color.FromArgb(32, 32, 32);

    public static void DrawNewTab(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 绘制标签页轮廓
        g.DrawRectangle(pen, cx - 6, cy - 4, 12, 8);
        g.DrawLine(pen, cx - 6, cy - 1, cx + 6, cy - 1);
    }

    public static void DrawNewWindow(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 绘制窗口轮廓
        g.DrawRectangle(pen, cx - 6, cy - 5, 12, 10);
        g.DrawLine(pen, cx - 6, cy - 2, cx + 6, cy - 2);
    }

    public static void DrawIncognito(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 绘制眼镜形状
        g.DrawEllipse(pen, cx - 7, cy - 3, 6, 6);
        g.DrawEllipse(pen, cx + 1, cy - 3, 6, 6);
        g.DrawLine(pen, cx - 1, cy, cx + 1, cy);
        // 帽子
        g.DrawArc(pen, cx - 8, cy - 8, 16, 10, 180, 180);
    }

    public static void DrawZoom(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 放大镜
        g.DrawEllipse(pen, cx - 5, cy - 5, 8, 8);
        g.DrawLine(pen, cx + 2, cy + 2, cx + 6, cy + 6);
        // + 号
        g.DrawLine(pen, cx - 3, cy - 1, cx + 1, cy - 1);
        g.DrawLine(pen, cx - 1, cy - 3, cx - 1, cy + 1);
    }

    public static void DrawBookmark(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 星形
        var points = new PointF[5];
        for (int i = 0; i < 5; i++)
        {
            double angle = -Math.PI / 2 + i * 2 * Math.PI / 5;
            points[i] = new PointF(cx + 6 * (float)Math.Cos(angle), cy + 6 * (float)Math.Sin(angle));
        }
        // 连接星形的点
        g.DrawLine(pen, points[0], points[2]);
        g.DrawLine(pen, points[2], points[4]);
        g.DrawLine(pen, points[4], points[1]);
        g.DrawLine(pen, points[1], points[3]);
        g.DrawLine(pen, points[3], points[0]);
    }

    public static void DrawHistory(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 时钟
        g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
        g.DrawLine(pen, cx, cy - 4, cx, cy);
        g.DrawLine(pen, cx, cy, cx + 3, cy + 2);
    }

    public static void DrawDownload(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 向下箭头
        g.DrawLine(pen, cx, cy - 5, cx, cy + 2);
        g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
        g.DrawLine(pen, cx + 4, cy - 2, cx, cy + 2);
        // 底部横线
        g.DrawLine(pen, cx - 6, cy + 5, cx + 6, cy + 5);
    }

    public static void DrawSave(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 软盘图标
        g.DrawRectangle(pen, cx - 6, cy - 6, 12, 12);
        g.DrawRectangle(pen, cx - 3, cy - 6, 6, 4);
        g.DrawRectangle(pen, cx - 4, cy + 1, 8, 5);
    }

    public static void DrawFind(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 放大镜
        g.DrawEllipse(pen, cx - 5, cy - 5, 8, 8);
        g.DrawLine(pen, cx + 2, cy + 2, cx + 6, cy + 6);
    }

    public static void DrawPrint(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 打印机
        g.DrawRectangle(pen, cx - 6, cy - 2, 12, 6);
        g.DrawRectangle(pen, cx - 4, cy - 6, 8, 4);
        g.DrawRectangle(pen, cx - 4, cy + 2, 8, 4);
    }

    public static void DrawTools(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 扳手
        g.DrawLine(pen, cx - 5, cy + 5, cx + 2, cy - 2);
        g.DrawEllipse(pen, cx, cy - 6, 6, 6);
    }

    public static void DrawSettings(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 齿轮
        g.DrawEllipse(pen, cx - 3, cy - 3, 6, 6);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            float x1 = cx + 4 * (float)Math.Cos(angle);
            float y1 = cy + 4 * (float)Math.Sin(angle);
            float x2 = cx + 6 * (float)Math.Cos(angle);
            float y2 = cy + 6 * (float)Math.Sin(angle);
            g.DrawLine(pen, x1, y1, x2, y2);
        }
    }

    public static void DrawAbout(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 问号圆圈
        g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
        // 问号
        g.DrawArc(pen, cx - 3, cy - 4, 6, 5, 180, 180);
        g.DrawLine(pen, cx, cy + 1, cx, cy + 2);
        g.FillEllipse(new SolidBrush(IconColor), cx - 1, cy + 3, 2, 2);
    }

    public static void DrawAdBlock(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 盾牌
        var points = new PointF[]
        {
            new PointF(cx, cy - 6),
            new PointF(cx + 6, cy - 3),
            new PointF(cx + 6, cy + 2),
            new PointF(cx, cy + 6),
            new PointF(cx - 6, cy + 2),
            new PointF(cx - 6, cy - 3)
        };
        g.DrawPolygon(pen, points);
    }

    public static void DrawAdBlockEnabled(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var enabledColor = Color.FromArgb(0, 120, 212); // 蓝色表示启用
        using var pen = new Pen(enabledColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var brush = new SolidBrush(Color.FromArgb(40, 0, 120, 212)); // 半透明填充

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 盾牌
        var points = new PointF[]
        {
            new PointF(cx, cy - 6),
            new PointF(cx + 6, cy - 3),
            new PointF(cx + 6, cy + 2),
            new PointF(cx, cy + 6),
            new PointF(cx - 6, cy + 2),
            new PointF(cx - 6, cy - 3)
        };
        g.FillPolygon(brush, points);
        g.DrawPolygon(pen, points);

        // 勾选标记
        using var checkPen = new Pen(enabledColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(checkPen, cx - 3, cy, cx - 1, cy + 2);
        g.DrawLine(checkPen, cx - 1, cy + 2, cx + 3, cy - 2);
    }

    public static void DrawClear(Graphics g, Rectangle rect)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IconColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        // 垃圾桶
        g.DrawLine(pen, cx - 5, cy - 4, cx + 5, cy - 4);
        g.DrawLine(pen, cx - 4, cy - 4, cx - 4, cy + 5);
        g.DrawLine(pen, cx + 4, cy - 4, cx + 4, cy + 5);
        g.DrawLine(pen, cx - 4, cy + 5, cx + 4, cy + 5);
        g.DrawLine(pen, cx - 2, cy - 6, cx + 2, cy - 6);
        g.DrawLine(pen, cx - 2, cy - 6, cx - 2, cy - 4);
        g.DrawLine(pen, cx + 2, cy - 6, cx + 2, cy - 4);
    }
}
