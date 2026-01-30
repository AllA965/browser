using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 圆角按钮控件
/// </summary>
public class RoundedButton : BaseToolButton
{
    private Image? _iconImage;
    private bool _useGrayscale = false;

    public bool UseGrayscale
    {
        get => _useGrayscale;
        set { _useGrayscale = value; Invalidate(); }
    }

    public Image? IconImage
    {
        get => _iconImage;
        set { _iconImage = value; Invalidate(); }
    }
    
    public RoundedButton()
    {
        ForeColor = Color.FromArgb(80, 80, 80);
        Font = new Font("Segoe UI Symbol", DpiHelper.Scale(11F));
    }
    
    protected override void DrawContent(Graphics g)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        
        if (_iconImage != null)
        {
            int targetSize = Math.Min(Height, Width) - DpiHelper.Scale(10);
            if (targetSize <= 0) targetSize = Math.Min(Height, Width);
            int x = (Width - targetSize) / 2;
            int y = (Height - targetSize) / 2;
            var destRect = new Rectangle(x, y, targetSize, targetSize);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            if (_useGrayscale && !IsHovered)
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
}
