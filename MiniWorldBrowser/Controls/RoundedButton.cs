using System.Drawing.Drawing2D;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 圆角按钮控件
/// </summary>
public class RoundedButton : Control
{
    private bool _isHovered;
    private bool _isPressed;
    private int _cornerRadius = 6;
    private Image? _iconImage;
    private bool _useGrayscale = false;

    public bool UseGrayscale
    {
        get => _useGrayscale;
        set { _useGrayscale = value; Invalidate(); }
    }
    
    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; Invalidate(); }
    }

    public Image? IconImage
    {
        get => _iconImage;
        set { _iconImage = value; Invalidate(); }
    }
    
    public Color HoverBackColor { get; set; } = Color.FromArgb(220, 220, 220);
    public Color PressedBackColor { get; set; } = Color.FromArgb(200, 200, 200);
    
    public RoundedButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        
        BackColor = Color.Transparent;
        ForeColor = Color.FromArgb(80, 80, 80);
        Font = new Font("Segoe UI Symbol", 11F);
        Size = new Size(32, 32);
        Cursor = Cursors.Hand;
        TabStop = true;
    }
    
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        
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
        
        if (_iconImage != null)
        {
            int targetSize = Math.Min(Height, Width) - 10;
            if (targetSize <= 0) targetSize = Math.Min(Height, Width);
            int x = (Width - targetSize) / 2;
            int y = (Height - targetSize) / 2;
            var destRect = new Rectangle(x, y, targetSize, targetSize);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            if (_useGrayscale && !_isHovered)
             {
                 using var attributes = new System.Drawing.Imaging.ImageAttributes();
                 // 使用高亮度灰阶矩阵，让图标看起来更“灰白”
                 var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                 {
                     new float[] {.3f, .3f, .3f, 0, 0},
                     new float[] {.59f, .59f, .59f, 0, 0},
                     new float[] {.11f, .11f, .11f, 0, 0},
                     new float[] {0, 0, 0, 1, 0},
                     new float[] {0.2f, 0.2f, 0.2f, 0, 1} // 增加亮度偏移
                 });
                 attributes.SetColorMatrix(matrix);
                 g.DrawImage(_iconImage, destRect, 0, 0, _iconImage.Width, _iconImage.Height, GraphicsUnit.Pixel, attributes);
             }
            else
            {
                g.DrawImage(_iconImage, destRect);
            }
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            using var brush = new SolidBrush(Enabled ? ForeColor : Color.Gray);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(Text, Font, brush, rect, format);
        }
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
