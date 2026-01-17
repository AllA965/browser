namespace MiniWorldBrowser.Controls;

/// <summary>
/// 手势轨迹绘制层
/// </summary>
public class GestureOverlay : Form
{
    private readonly List<Point> _points = new();
    private readonly Pen _pen = new(Color.FromArgb(200, 255, 80, 80), 3);
    
    public GestureOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
    }
    
    public void AddPoint(Point p)
    {
        _points.Add(p);
        Invalidate();
    }
    
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_points.Count < 2) return;
        
        var screenPoints = _points
            .Select(p => PointToClient(Owner?.PointToScreen(p) ?? p))
            .ToArray();
        
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(_pen, screenPoints);
    }
    
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | 0x20; // WS_EX_LAYERED | WS_EX_TRANSPARENT
            return cp;
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pen.Dispose();
        }
        base.Dispose(disposing);
    }
}
