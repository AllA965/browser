using System.Runtime.InteropServices;

namespace MiniWorldBrowser.Helpers;

/// <summary>
/// Win32 API 封装
/// </summary>
public static class Win32Helper
{
    #region DLL Imports
    
    [DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    
    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();
    
    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);
    
    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hInstance);
    
    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);
    
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
    
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetParent(IntPtr hWnd);
    
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    #endregion
    
    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        int GetCount([Out] out uint propertyCount);
        int GetAt([In] uint propertyIndex, [Out, MarshalAs(UnmanagedType.Struct)] out PropertyKey key);
        int GetValue([In, MarshalAs(UnmanagedType.Struct)] ref PropertyKey key, [Out, MarshalAs(UnmanagedType.Struct)] out PropVariant value);
        int SetValue([In, MarshalAs(UnmanagedType.Struct)] ref PropertyKey key, [In, MarshalAs(UnmanagedType.Struct)] ref PropVariant value);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr ptr;

        public void SetString(string value)
        {
            vt = 31; // VT_LPWSTR
            ptr = Marshal.StringToCoTaskMemUni(value);
        }
    }

    private static readonly Guid IID_IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly PropertyKey AppUserModel_ID = new PropertyKey { fmtid = new Guid("9F4C0559-8192-4919-9E7E-6D201F546743"), pid = 5 };

    /// <summary>
    /// 设置窗口的 AppUserModelID，用于任务栏分组
    /// </summary>
    public static void SetWindowAppUserModelId(IntPtr hwnd, string appId)
    {
        try
        {
            var iid = IID_IPropertyStore;
            if (SHGetPropertyStoreForWindow(hwnd, ref iid, out var propertyStore) == 0)
            {
                var pv = new PropVariant();
                pv.SetString(appId);
                var key = AppUserModel_ID;
                propertyStore.SetValue(ref key, ref pv);
                propertyStore.Commit();
                Marshal.ReleaseComObject(propertyStore);
            }
        }
        catch { }
    }
    
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public enum DWM_WINDOW_CORNER_PREFERENCE
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }
    
    /// <summary>
    /// 为窗口应用圆角（仅 Win11 及更新版本的 Win10 部分版本支持）
    /// </summary>
    public static void ApplyRoundedCorners(IntPtr handle)
    {
        try
        {
            var attribute = DWMWA_WINDOW_CORNER_PREFERENCE;
            var preference = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            DwmSetWindowAttribute(handle, attribute, ref preference, sizeof(int));
        }
        catch { }
    }
    
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    
    /// <summary>
    /// 启用窗口拖动
    /// </summary>
    public static void EnableWindowDrag(IntPtr handle)
    {
        ReleaseCapture();
        SendMessage(handle, Constants.Win32Constants.WM_NCLBUTTONDOWN, Constants.Win32Constants.HT_CAPTION, 0);
    }
    
    /// <summary>
    /// 处理窗口边框调整大小的命中测试
    /// </summary>
    public static IntPtr HandleResizeHitTest(Form form, Point cursorPosition)
    {
        var cursor = form.PointToClient(cursorPosition);
        int border = Constants.Win32Constants.BorderWidth;
        
        if (cursor.Y < border)
        {
            if (cursor.X < border) return (IntPtr)Constants.Win32Constants.HTTOPLEFT;
            if (cursor.X > form.Width - border) return (IntPtr)Constants.Win32Constants.HTTOPRIGHT;
            return (IntPtr)Constants.Win32Constants.HTTOP;
        }
        
        if (cursor.Y > form.Height - border)
        {
            if (cursor.X < border) return (IntPtr)Constants.Win32Constants.HTBOTTOMLEFT;
            if (cursor.X > form.Width - border) return (IntPtr)Constants.Win32Constants.HTBOTTOMRIGHT;
            return (IntPtr)Constants.Win32Constants.HTBOTTOM;
        }
        
        if (cursor.X < border) return (IntPtr)Constants.Win32Constants.HTLEFT;
        if (cursor.X > form.Width - border) return (IntPtr)Constants.Win32Constants.HTRIGHT;
        
        return IntPtr.Zero;
    }
}
