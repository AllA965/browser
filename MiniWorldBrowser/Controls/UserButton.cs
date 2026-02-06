using System.Drawing.Drawing2D;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 用户头像按钮 - 参考现代浏览器风格
/// </summary>
public class UserButton : BaseToolButton
{
    private UserInfo? _userInfo;
    private Image? _avatarImage;
    
    public UserInfo? UserInfo
    {        get => _userInfo;
        set 
        { 
            _userInfo = value; 
            _avatarImage = null;
            Invalidate(); 
            
            if (_userInfo != null && !string.IsNullOrEmpty(_userInfo.Avatar))
            {
                LoadAvatarAsync(_userInfo.Avatar);
            }
        }
    }

    private async void LoadAvatarAsync(string url)
    {
        try
        {
            var img = await ImageHelper.GetImageAsync(url);
            if (img != null)
            {
                _avatarImage = img;
                if (!IsDisposed)
                {
                    Invalidate();
                }
            }
        }
        catch
        {
            // 忽略加载错误
        }
    }

    public UserButton()
    {
        CornerRadius = DpiHelper.Scale(6); // 圆角矩形，与下载按钮一致
    }

    protected override void DrawContent(Graphics g)
    {
        // 绘制内容区域（头像或默认图标）
        // 保持图标居中且稍小一点，与下载图标视觉平衡
        var contentRect = new Rectangle(DpiHelper.Scale(6), DpiHelper.Scale(6), Width - DpiHelper.Scale(12), Height - DpiHelper.Scale(12));

        if (_userInfo != null && !string.IsNullOrEmpty(_userInfo.Nickname))
        {            if (_avatarImage != null)
            {
                // 绘制圆形头像
                using var path = new GraphicsPath();
                path.AddEllipse(contentRect);
                g.SetClip(path);
                g.DrawImage(_avatarImage, contentRect);
                g.ResetClip();
            }
            else
            {
                // 已登录但无头像或头像加载中：绘制带颜色的圆形和首字母
                using var brush = new SolidBrush(Color.FromArgb(0, 120, 215));
                g.FillEllipse(brush, contentRect);

                var initial = _userInfo.Nickname[0].ToString().ToUpper();
                using var font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10F), FontStyle.Bold);
                var size = g.MeasureString(initial, font);
                g.DrawString(initial, font, Brushes.White, 
                    contentRect.X + (contentRect.Width - size.Width) / 2, 
                    contentRect.Y + (contentRect.Height - size.Height) / 2 + DpiHelper.Scale(1));
            }
        }
        else
        {
            // 未登录：绘制默认用户图标
            DrawDefaultUserIcon(g, contentRect);
        }
    }

    private void DrawDefaultUserIcon(Graphics g, Rectangle rect)
    {
        Color iconColor = IsHovered ? Color.FromArgb(60, 60, 60) : Color.FromArgb(100, 100, 100);
        using var pen = new Pen(iconColor, DpiHelper.Scale(1.8f));
        using var brush = new SolidBrush(iconColor);
        
        // 绘制简单的用户轮廓图标
        // 头部
        int headSize = rect.Width * 45 / 100;
        var headRect = new Rectangle(rect.X + (rect.Width - headSize) / 2, rect.Y + DpiHelper.Scale(2), headSize, headSize);
        g.FillEllipse(brush, headRect);
        
        // 身体（圆弧）
        int bodyWidth = rect.Width * 80 / 100;
        int bodyHeight = rect.Height * 40 / 100;
        var bodyRect = new Rectangle(rect.X + (rect.Width - bodyWidth) / 2, rect.Bottom - bodyHeight, bodyWidth, bodyHeight);
        g.FillPie(brush, bodyRect, 180, 180);
    }
}
