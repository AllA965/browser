using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 新建标签按钮 - Edge 风格 + 图标
/// </summary>
public class NewTabButton : Control
{
    private bool _isHovered;
    private bool _isPressed;
    private readonly bool _isDarkTheme;

    public Color HoverBackColor { get; set; }
    public Color PressedBackColor { get; set; }
    public Color IconColor { get; set; }

    public NewTabButton(bool darkTheme = false)
    {
        _isDarkTheme = darkTheme;

        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);

        BackColor = Color.Transparent;
        Size = DpiHelper.Scale(new Size(28, 28));
        Cursor = Cursors.Hand;

        // 根据主题设置颜色
        if (_isDarkTheme)
        {
            HoverBackColor = Color.FromArgb(70, 70, 70);
            PressedBackColor = Color.FromArgb(80, 80, 80);
            IconColor = Color.FromArgb(200, 200, 200);
        }
        else
        {
            HoverBackColor = Color.FromArgb(210, 210, 210);
            PressedBackColor = Color.FromArgb(195, 195, 195);
            IconColor = Color.FromArgb(80, 80, 80);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int padding = DpiHelper.Scale(2);
        var rect = new Rectangle(padding, padding, Width - DpiHelper.Scale(5), Height - DpiHelper.Scale(5));

        // 绘制背景
        Color bgColor;
        if (_isPressed)
            bgColor = PressedBackColor;
        else if (_isHovered)
            bgColor = HoverBackColor;
        else
            bgColor = Color.Transparent;

        if (bgColor != Color.Transparent)
        {
            using var path = CreateRoundedRect(rect, DpiHelper.Scale(6));
            using var brush = new SolidBrush(bgColor);
            g.FillPath(brush, path);
        }

        // 绘制 + 图标
        DrawPlusIcon(g);
    }

    private void DrawPlusIcon(Graphics g)
    {
        float centerX = Width / 2f;
        float centerY = Height / 2f;
        float size = DpiHelper.Scale(5f);
        float penWidth = DpiHelper.Scale(1.8f);

        using var pen = new Pen(IconColor, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        // 横线
        g.DrawLine(pen, centerX - size, centerY, centerX + size, centerY);
        // 竖线
        g.DrawLine(pen, centerX, centerY - size, centerX, centerY + size);
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
}
