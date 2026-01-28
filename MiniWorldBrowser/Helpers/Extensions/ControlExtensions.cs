namespace MiniWorldBrowser.Helpers.Extensions;

/// <summary>
/// 控件扩展方法
/// </summary>
public static class ControlExtensions
{
    /// <summary>
    /// 安全地在 UI 线程上执行操作
    /// </summary>
    public static void SafeInvoke(this Control control, Action action)
    {
        if (control.IsDisposed)
            return;
        
        if (control.InvokeRequired)
            control.BeginInvoke(action);
        else
            action();
    }
    
    /// <summary>
    /// 安全地在 UI 线程上执行操作并返回结果
    /// </summary>
    public static T SafeInvoke<T>(this Control control, Func<T> func)
    {
        if (control.IsDisposed)
            return default!;
        
        if (control.InvokeRequired)
            return (T)control.Invoke(func);
        else
            return func();
    }
    
    /// <summary>
    /// 刷新控件及其所有子控件
    /// </summary>
    public static void RefreshAll(this Control control)
    {
        control.Refresh();
        foreach (Control child in control.Controls)
        {
            child.RefreshAll();
        }
    }
    
    /// <summary>
    /// 开启双缓冲
    /// </summary>
    public static void SetDoubleBuffered(this Control control, bool enabled = true)
    {
        var prop = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        prop?.SetValue(control, enabled);
    }

    /// <summary>
    /// 创建圆角矩形路径
    /// </summary>
    public static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    
    /// <summary>
    /// 截断文本
    /// </summary>
    public static string Truncate(this string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }
}
