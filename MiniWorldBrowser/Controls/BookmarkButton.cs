using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 收藏按钮控件 - 带动画效果，参考 Edge 风格
/// </summary>
public class AnimatedBookmarkButton : Control
{
    private bool _isBookmarked;
    private bool _isHovered;
    private float _animationProgress = 0f;
    private float _scaleProgress = 1f;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private bool _isAnimating;
    
    public bool IsBookmarked
    {
        get => _isBookmarked;
        set
        {
            if (_isBookmarked != value)
            {
                _isBookmarked = value;
                if (value)
                {
                    // 添加收藏时播放动画
                    StartAnimation();
                }
                else
                {
                    _animationProgress = 0f;
                    _scaleProgress = 1f;
                }
                Invalidate();
            }
        }
    }
    
    public event EventHandler? BookmarkClicked;
    
    private static readonly Color StarOutlineColor = Color.FromArgb(120, 120, 120);
    private static readonly Color StarFilledColor = Color.FromArgb(255, 200, 0); // 金黄色
    private static readonly Color HoverBgColor = Color.FromArgb(230, 230, 230);
    
    public AnimatedBookmarkButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        
        Size = DpiHelper.Scale(new Size(32, 32));
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;
        
        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
        _animationTimer.Tick += OnAnimationTick;
    }
    
    private void StartAnimation()
    {
        _animationProgress = 0f;
        _scaleProgress = 0.5f; // 从小开始
        _isAnimating = true;
        _animationTimer.Start();
    }
    
    private void OnAnimationTick(object? sender, EventArgs e)
    {
        // 填充动画
        _animationProgress += 0.15f;
        
        // 缩放动画 - 弹性效果
        if (_scaleProgress < 1.2f)
        {
            _scaleProgress += 0.08f;
        }
        else if (_scaleProgress > 1f)
        {
            _scaleProgress -= 0.03f;
        }
        
        if (_animationProgress >= 1f && Math.Abs(_scaleProgress - 1f) < 0.05f)
        {
            _animationProgress = 1f;
            _scaleProgress = 1f;
            _isAnimating = false;
            _animationTimer.Stop();
        }
        
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        
        // 悬停背景 - 圆形
        if (_isHovered)
        {
            using var brush = new SolidBrush(HoverBgColor);
            int offset = DpiHelper.Scale(2);
            g.FillEllipse(brush, offset, offset, Width - offset * 2, Height - offset * 2);
        }
        
        // 计算星星大小和位置
        float scale = _isAnimating ? _scaleProgress : 1f;
        float starSize = DpiHelper.Scale(14) * scale;
        float centerX = Width / 2f;
        float centerY = Height / 2f;
        
        // 绘制五角星
        var starPath = CreateStarPath(centerX, centerY, starSize / 2f, starSize / 4f);
        
        if (_isBookmarked)
        {
            // 已收藏 - 填充金色
            float fillProgress = _isAnimating ? _animationProgress : 1f;
            
            // 先绘制轮廓
            using (var outlinePen = new Pen(StarFilledColor, DpiHelper.Scale(1.5f)))
            {
                g.DrawPath(outlinePen, starPath);
            }
            
            // 填充（带动画）
            if (fillProgress > 0)
            {
                using var clip = new Region(starPath);
                var oldClip = g.Clip;
                g.Clip = clip;
                
                using var fillBrush = new SolidBrush(StarFilledColor);
                // 从下往上填充
                float fillHeight = Height * fillProgress;
                g.FillRectangle(fillBrush, 0, Height - fillHeight, Width, fillHeight);
                
                g.Clip = oldClip;
            }
        }
        else
        {
            // 未收藏 - 只绘制轮廓
            using var pen = new Pen(StarOutlineColor, DpiHelper.Scale(1.5f));
            g.DrawPath(pen, starPath);
        }
        
        starPath.Dispose();
    }
    
    private static GraphicsPath CreateStarPath(float cx, float cy, float outerRadius, float innerRadius)
    {
        var path = new GraphicsPath();
        var points = new PointF[10];
        
        for (int i = 0; i < 10; i++)
        {
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            double angle = Math.PI / 2 + i * Math.PI / 5; // 从顶部开始
            points[i] = new PointF(
                cx + (float)(radius * Math.Cos(angle)),
                cy - (float)(radius * Math.Sin(angle))
            );
        }
        
        path.AddPolygon(points);
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
        Invalidate();
    }
    
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        BookmarkClicked?.Invoke(this, e);
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
