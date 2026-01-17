using System.Drawing;
using System.IO;

namespace MiniWorldBrowser.Helpers;

/// <summary>
/// 应用程序图标辅助类
/// </summary>
public static class AppIconHelper
{
    private static Icon? _appIcon;
    private static readonly string IconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "鲲穹AI浏览器.ico");

    /// <summary>
    /// 获取应用程序统一图标
    /// </summary>
    public static Icon? AppIcon
    {
        get
        {
            if (_appIcon == null)
            {
                if (File.Exists(IconPath))
                {
                    try
                    {
                        _appIcon = new Icon(IconPath);
                    }
                    catch
                    {
                        // 忽略加载错误
                    }
                }
            }
            return _appIcon;
        }
    }

    /// <summary>
    /// 为窗体设置统一图标
    /// </summary>
    public static void SetIcon(Form form)
    {
        if (AppIcon != null)
        {
            form.Icon = AppIcon;
        }
    }
}
