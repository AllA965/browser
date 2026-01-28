using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 下载按钮控件 - 自定义绘制下载图标
/// </summary>
public class DownloadButton : Control
{
    private bool _isHovered;
    private bool _isPressed;
    private int _cornerRadius = DpiHelper.Scale(6);
    
    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; Invalidate(); }
    }
    
    public Color HoverBackColor { get; set; } = Color.FromArgb(220, 220, 220);
    public Color PressedBackColor { get; set; } = Color.FromArgb(200, 200, 200);
    public Color IconColor { get; set; } = Color.FromArgb(80, 80, 80);
    
    public DownloadButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        
        BackColor = Color.Transparent;
        Size = DpiHelper.Scale(new Size(32, 32));
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
        if (_isPressed)
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
        
        // 绘制下载图标
        DrawDownloadIcon(g);
    }
    
    private void DrawDownloadIcon(Graphics g)
    {
        var color = Enabled ? IconColor : Color.Gray;
        float penWidth = DpiHelper.Scale(2f);
        
        // 计算图标区域（居中）
        float iconSize = Math.Min(Width, Height) * 0.5f;
        float centerX = Width / 2f;
        float centerY = Height / 2f;
        
        // 箭头参数
        float arrowTop = centerY - iconSize * 0.45f;
        float arrowBottom = centerY + iconSize * 0.25f;
        float arrowWidth = iconSize * 0.35f;
        
        // 底部横线位置
        float lineY = centerY + iconSize * 0.45f;
        float lineHalfWidth = iconSize * 0.45f;
        
        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        
        // 绘制垂直箭杆
        g.DrawLine(pen, centerX, arrowTop, centerX, arrowBottom);
        
        // 绘制箭头两翼（V形）
        float wingY = arrowBottom - iconSize * 0.25f;
        g.DrawLine(pen, centerX - arrowWidth, wingY, centerX, arrowBottom);
        g.DrawLine(pen, centerX + arrowWidth, wingY, centerX, arrowBottom);
        
        // 绘制底部横线
        g.DrawLine(pen, centerX - lineHalfWidth, lineY, centerX + lineHalfWidth, lineY);
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
