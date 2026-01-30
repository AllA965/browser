using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 工具栏按钮基类 - 提供统一的圆角背景和悬停/按下效果
/// </summary>
public abstract class BaseToolButton : Control
{
    protected bool IsHovered;
    protected bool IsPressed;
    protected int CurrentCornerRadius = DpiHelper.Scale(6);

    public int CornerRadius
    {
        get => CurrentCornerRadius;
        set { CurrentCornerRadius = value; Invalidate(); }
    }

    public Color HoverBackColor { get; set; } = Color.FromArgb(220, 220, 220);
    public Color PressedBackColor { get; set; } = Color.FromArgb(200, 200, 200);

    protected BaseToolButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);

        BackColor = Color.Transparent;
        Size = DpiHelper.Scale(new Size(32, 32));
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 绘制背景
        DrawBackground(g);

        // 绘制内容（由子类实现）
        DrawContent(g);
    }

    protected virtual void DrawBackground(Graphics g)
    {
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        
        Color bgColor;
        if (IsPressed)
            bgColor = PressedBackColor;
        else if (IsHovered)
            bgColor = HoverBackColor;
        else
            bgColor = BackColor;

        if (bgColor != Color.Transparent && bgColor.A > 0)
        {
            using var path = CreateRoundedRect(rect, CurrentCornerRadius);
            using var brush = new SolidBrush(bgColor);
            g.FillPath(brush, path);
        }
    }

    protected abstract void DrawContent(Graphics g);

    protected static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
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
        IsHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        IsHovered = false;
        IsPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            IsPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        IsPressed = false;
        Invalidate();
    }
}
