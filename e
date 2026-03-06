using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 下载按钮控件 - 自定义绘制下载图标
/// </summary>
public class DownloadButton : BaseToolButton
{
    public Color IconColor { get; set; } = Color.FromArgb(80, 80, 80);
    
    private float _offsetY = 0f;
    private System.Windows.Forms.Timer? _bounceTimer;
    private float _bouncePhase = 0f;

    public void StartBounceAnimation()
    {
        if (_bounceTimer == null)
        {
            _bounceTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _bounceTimer.Tick += (s, e) => {
                _bouncePhase += 0.2f;
                if (_bouncePhase > Math.PI * 3)
                {
                    _offsetY = 0;
                    _bouncePhase = 0;
                    _bounceTimer.Stop();
                }
                else
                {
                    _offsetY = -(float)Math.Abs(Math.Sin(_bouncePhase)) * DpiHelper.Scale(6);
                }
                Invalidate();
            };
        }
        
        if (!_bounceTimer.Enabled)
        {
            _bouncePhase = 0;
            _bounceTimer.Start();
        }
    }

    public DownloadButton()
    {
    }
    
    protected override void DrawContent(Graphics g)
    {
        DrawDownloadIcon(g);
    }

    private void DrawDownloadIcon(Graphics g)
    {
        g.TranslateTransform(0, _offsetY);

        var color = Enabled ? IconColor : Color.Gray;
        float penWidth = DpiHelper.Scale(2f);
        
        float iconSize = Math.Min(Width, Height) * 0.5f;
        float centerX = Width / 2f;
        float centerY = Height / 2f;
        
        float arrowTop = centerY - iconSize * 0.45f;
        float arrowBottom = centerY + iconSize * 0.25f;
        float arrowWidth = iconSize * 0.35f;
        
        float lineY = centerY + iconSize * 0.45f;
        float lineHalfWidth = iconSize * 0.45f;
        
        using var pen = new Pen(color, penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        
        g.DrawLine(pen, centerX, arrowTop, centerX, arrowBottom);
        
        float wingY = arrowBottom - iconSize * 0.25f;
        g.DrawLine(pen, centerX - arrowWidth, wingY, centerX, arrowBottom);
        g.DrawLine(pen, centerX + arrowWidth, wingY, centerX, arrowBottom);
        
        g.DrawLine(pen, centerX - lineHalfWidth, lineY, centerX + lineHalfWidth, lineY);

        g.ResetTransform();
    }
}
