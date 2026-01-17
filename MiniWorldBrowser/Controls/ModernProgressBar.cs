using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiniWorldBrowser.Controls
{
    /// <summary>
    /// 现代感设计的高性能进度条
    /// </summary>
    public class ModernProgressBar : Control
    {
        private float _marqueeOffset = 0;
        private readonly System.Windows.Forms.Timer _animationTimer;
        private bool _isMarquee = true;
        private int _value = 0;
        private int _maximum = 100;
        private Color _progressColor = Color.FromArgb(0, 120, 212); // 鲲穹蓝
        private Color _progressColor2 = Color.FromArgb(0, 190, 255); // 亮蓝

        public int Value
        {
            get => _value;
            set { _value = Math.Min(_maximum, Math.Max(0, value)); Invalidate(); }
        }

        public int Maximum
        {
            get => _maximum;
            set { _maximum = value; Invalidate(); }
        }

        public bool IsMarquee
        {
            get => _isMarquee;
            set { _isMarquee = value; Invalidate(); }
        }

        public Color ProgressColor
        {
            get => _progressColor;
            set { _progressColor = value; Invalidate(); }
        }

        public Color ProgressColor2
        {
            get => _progressColor2;
            set { _progressColor2 = value; Invalidate(); }
        }

        public ModernProgressBar()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Color.Transparent;
            Size = new Size(100, 6);

            _animationTimer = new System.Windows.Forms.Timer { Interval = 20 };
            _animationTimer.Tick += (s, e) =>
            {
                if (Visible && _isMarquee)
                {
                    _marqueeOffset += 4;
                    if (_marqueeOffset > Width) _marqueeOffset = -Width / 2;
                    Invalidate();
                }
            };
            _animationTimer.Start();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) _animationTimer.Start();
            else _animationTimer.Stop();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 考虑内边距
            int barWidth = Width - Padding.Left - Padding.Right;
            int barHeight = 4;
            int x = Padding.Left;
            int y = (Height - barHeight) / 2;

            if (barWidth <= 0) return;

            var rect = new Rectangle(x, y, barWidth, barHeight);
            var radius = barHeight / 2;

            // 绘制背景槽
            using (var bgPath = CreateRoundedRect(rect, radius))
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(30, Color.Gray)))
                {
                    g.FillPath(bgBrush, bgPath);
                }
            }

            if (_isMarquee)
            {
                // 绘制跑马灯动画
                float marqueeWidth = barWidth / 2f;
                var marqueeRect = new RectangleF(x + _marqueeOffset % (barWidth + marqueeWidth) - marqueeWidth, y, marqueeWidth, barHeight);
                
                // 裁剪区域，只在进度条槽内显示
                var oldClip = g.Clip;
                using (var bgPath = CreateRoundedRect(rect, radius))
                {
                    g.SetClip(bgPath);
                    
                    using (var brush = new LinearGradientBrush(marqueeRect, _progressColor, _progressColor2, 0f))
                    {
                        ColorBlend blend = new ColorBlend();
                        blend.Colors = new Color[] { Color.Transparent, _progressColor, _progressColor2, Color.Transparent };
                        blend.Positions = new float[] { 0f, 0.2f, 0.8f, 1f };
                        brush.InterpolationColors = blend;
                        
                        g.FillRectangle(brush, marqueeRect);
                    }
                    
                    g.Clip = oldClip;
                }
            }
            else
            {
                // 绘制普通进度
                if (_value > 0)
                {
                    float progressWidth = (float)_value / _maximum * barWidth;
                    var progressRect = new RectangleF(x, y, progressWidth, barHeight);
                    
                    using (var path = CreateRoundedRect(Rectangle.Round(progressRect), radius))
                    {
                        using (var brush = new LinearGradientBrush(progressRect, _progressColor, _progressColor2, 0f))
                        {
                            g.FillPath(brush, path);
                        }
                    }
                }
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) d = 1;

            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
