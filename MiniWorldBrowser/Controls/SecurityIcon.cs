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
        set { _isSecure = value; Invalidate(); }
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
        
        Size = DpiHelper.Scale(new Size(22, 22));
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
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        
        // 悬停背景
        if (_isHovered)
        {
            using var hoverBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0));
            g.FillEllipse(hoverBrush, DpiHelper.Scale(1), DpiHelper.Scale(1), Width - DpiHelper.Scale(2), Height - DpiHelper.Scale(2));
        }
        
        if (_isSecure)
            DrawSecureLock(g);
        else
            DrawInsecureLock(g);
    }

    /// <summary>
    /// 绘制安全锁图标（绿色锁，参考世界之窗浏览器）
    /// </summary>
    private void DrawSecureLock(Graphics g)
    {
        int cx = Width / 2;
        int cy = Height / 2;
        
        // 锁的尺寸
        int lockWidth = DpiHelper.Scale(10);
        int lockHeight = DpiHelper.Scale(8);
        int shackleWidth = DpiHelper.Scale(6);
        int shackleHeight = DpiHelper.Scale(5);
        
        // 锁体位置
        int bodyX = cx - lockWidth / 2;
        int bodyY = cy - DpiHelper.Scale(1);
        
        // 绿色渐变
        var greenDark = Color.FromArgb(34, 139, 34);   // 深绿
        var greenLight = Color.FromArgb(50, 205, 50);  // 浅绿
        var greenMid = Color.FromArgb(60, 179, 60);    // 中绿
        
        // 绘制锁扣（U形部分）
        using (var shacklePen = new Pen(greenDark, DpiHelper.Scale(1.5f)))
        {
            shacklePen.StartCap = LineCap.Round;
            shacklePen.EndCap = LineCap.Round;
            
            int shackleX = cx - shackleWidth / 2;
            int shackleY = bodyY - shackleHeight + DpiHelper.Scale(1);
            
            // 绘制U形锁扣
            using var path = new GraphicsPath();
            path.AddArc(shackleX, shackleY, shackleWidth, shackleHeight * 2, 180, 180);
            g.DrawPath(shacklePen, path);
            
            // 绘制两侧竖线
            g.DrawLine(shacklePen, shackleX, shackleY + shackleHeight, shackleX, bodyY + DpiHelper.Scale(1));
            g.DrawLine(shacklePen, shackleX + shackleWidth, shackleY + shackleHeight, shackleX + shackleWidth, bodyY + DpiHelper.Scale(1));
        }
        
        // 绘制锁体（带渐变的圆角矩形）
        var bodyRect = new Rectangle(bodyX, bodyY, lockWidth, lockHeight);
        using (var bodyBrush = new LinearGradientBrush(bodyRect, greenLight, greenDark, LinearGradientMode.Vertical))
        {
            using var bodyPath = CreateRoundedRect(bodyRect, DpiHelper.Scale(2));
            g.FillPath(bodyBrush, bodyPath);
        }
        
        // 绘制锁体边框
        using (var borderPen = new Pen(greenDark, DpiHelper.Scale(0.5f)))
        {
            using var bodyPath = CreateRoundedRect(bodyRect, DpiHelper.Scale(2));
            g.DrawPath(borderPen, bodyPath);
        }
        
        // 绘制锁孔（白色小圆点）
        int holeSize = DpiHelper.Scale(3);
        int holeX = cx - holeSize / 2;
        int holeY = bodyY + lockHeight / 2 - holeSize / 2;
        using (var holeBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(holeBrush, holeX, holeY, holeSize, holeSize);
        }
    }
    
    /// <summary>
    /// 绘制不安全锁图标（灰色开锁）
    /// </summary>
    private void DrawInsecureLock(Graphics g)
    {
        int cx = Width / 2;
        int cy = Height / 2;
        
        int lockWidth = DpiHelper.Scale(10);
        int lockHeight = DpiHelper.Scale(8);
        int shackleWidth = DpiHelper.Scale(6);
        int shackleHeight = DpiHelper.Scale(5);
        
        int bodyX = cx - lockWidth / 2;
        int bodyY = cy - DpiHelper.Scale(1);
        
        var grayDark = Color.FromArgb(120, 120, 120);
        var grayLight = Color.FromArgb(180, 180, 180);
        
        // 绘制开着的锁扣（倾斜的U形）
        using (var shacklePen = new Pen(grayDark, DpiHelper.Scale(2.0f)))
        {
            shacklePen.StartCap = LineCap.Round;
            shacklePen.EndCap = LineCap.Round;
            
            int shackleX = cx - shackleWidth / 2 - DpiHelper.Scale(1);
            int shackleY = bodyY - shackleHeight - DpiHelper.Scale(2);
            
            // 开锁状态 - 锁扣向上打开
            using var path = new GraphicsPath();
            path.AddArc(shackleX, shackleY, shackleWidth, shackleHeight * 2, 180, 180);
            g.DrawPath(shacklePen, path);
            
            // 只绘制右侧竖线（左侧打开）
            g.DrawLine(shacklePen, shackleX + shackleWidth, shackleY + shackleHeight, shackleX + shackleWidth, bodyY + DpiHelper.Scale(1));
        }
        
        // 绘制锁体
        var bodyRect = new Rectangle(bodyX, bodyY, lockWidth, lockHeight);
        using (var bodyBrush = new LinearGradientBrush(bodyRect, grayLight, grayDark, LinearGradientMode.Vertical))
        {
            using var bodyPath = CreateRoundedRect(bodyRect, DpiHelper.Scale(2));
            g.FillPath(bodyBrush, bodyPath);
        }
        
        using (var borderPen = new Pen(grayDark, DpiHelper.Scale(0.5f)))
        {
            using var bodyPath = CreateRoundedRect(bodyRect, DpiHelper.Scale(2));
            g.DrawPath(borderPen, bodyPath);
        }
        
        // 锁孔
        int holeSize = DpiHelper.Scale(3);
        int holeX = cx - holeSize / 2;
        int holeY = bodyY + lockHeight / 2 - holeSize / 2;
        using (var holeBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(holeBrush, holeX, holeY, holeSize, holeSize);
        }
    }
    
    private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        
        return path;
    }
}
