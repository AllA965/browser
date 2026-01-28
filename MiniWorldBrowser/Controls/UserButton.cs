using System.Drawing.Drawing2D;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 用户头像按钮 - 参考现代浏览器风格
/// </summary>
public class UserButton : RoundedButton
{
    private UserInfo? _userInfo;
    private Image? _avatarImage;
    
    public UserInfo? UserInfo
    {
        get => _userInfo;
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
        Size = DpiHelper.Scale(new Size(32, 32));
        CornerRadius = DpiHelper.Scale(16); // 圆形
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 获取鼠标状态
        var isHovered = ClientRectangle.Contains(PointToClient(Control.MousePosition));

        // 1. 绘制背景（悬停效果）
        // 如果已登录，我们通常画蓝色圆圈；如果未登录，Hover时画淡灰色背景
        // 将 Y 从 4 改为 2，向上移动以对齐其他图标
        var rect = new Rectangle(DpiHelper.Scale(4), DpiHelper.Scale(2), Width - DpiHelper.Scale(9), Height - DpiHelper.Scale(9));

        if (_userInfo != null && !string.IsNullOrEmpty(_userInfo.Nickname))
        {
            if (_avatarImage != null)
            {
                // 绘制圆形头像
                using var path = new GraphicsPath();
                path.AddEllipse(rect);
                g.SetClip(path);
                g.DrawImage(_avatarImage, rect);
                g.ResetClip();
            }
            else
            {
                // 已登录但无头像或头像加载中：绘制带颜色的圆形和首字母
                using var brush = new SolidBrush(Color.FromArgb(0, 120, 215));
                g.FillEllipse(brush, rect);

                var initial = _userInfo.Nickname[0].ToString().ToUpper();
                using var font = new Font("Microsoft YaHei UI", DpiHelper.Scale(10F), FontStyle.Bold);
                var size = g.MeasureString(initial, font);
                g.DrawString(initial, font, Brushes.White, 
                    rect.X + (rect.Width - size.Width) / 2, 
                    rect.Y + (rect.Height - size.Height) / 2 + DpiHelper.Scale(1));
            }
        }
        else
        {
            // 未登录：平时透明，Hover显示背景
            if (isHovered)
            {
                using var bgBrush = new SolidBrush(Color.FromArgb(230, 230, 230));
                g.FillEllipse(bgBrush, rect);
            }

            // 绘制更现代的分体式剪影（自带“脖子”感）
            using var silhouetteBrush = new SolidBrush(isHovered ? Color.FromArgb(100, 100, 100) : Color.FromArgb(160, 160, 160));
            
            // 头部（稍小且居中）
            var headSize = DpiHelper.Scale(10);
            var headRect = new Rectangle(rect.X + (rect.Width - headSize) / 2, rect.Y + DpiHelper.Scale(6), headSize, headSize);
            g.FillEllipse(silhouetteBrush, headRect);
            
            // 身体（圆弧底部，与头部留有间隙显示“脖子”）
            var bodyWidth = DpiHelper.Scale(18);
            var bodyHeight = DpiHelper.Scale(10);
            var bodyRect = new Rectangle(rect.X + (rect.Width - bodyWidth) / 2, rect.Y + DpiHelper.Scale(18), bodyWidth, bodyHeight);
            g.FillPie(silhouetteBrush, bodyRect, 180, 180);
        }
    }
}
