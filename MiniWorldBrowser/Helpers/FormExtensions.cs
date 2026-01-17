using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiniWorldBrowser.Helpers;

public static class FormExtensions
{
    public static void ApplySmoothRoundedCorners(this Form form, int radius)
    {
        form.Region = CreateRoundedRegion(form.ClientRectangle, radius);
    }

    public static Region CreateRoundedRegion(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        
        return new Region(path);
    }

    public static GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        
        // 稍微缩进 0.5 像素以获得更清晰的边缘
        float x = rect.X + 0.5f;
        float y = rect.Y + 0.5f;
        float w = rect.Width - 1f;
        float h = rect.Height - 1f;

        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        
        return path;
    }
}
