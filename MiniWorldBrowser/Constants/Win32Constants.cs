namespace MiniWorldBrowser.Constants;

/// <summary>
/// Win32 API 常量定义
/// </summary>
public static class Win32Constants
{
    // 窗口消息
    public const int WM_NCLBUTTONDOWN = 0xA1;
    public const int WM_NCHITTEST = 0x84;
    public const int WM_NCCALCSIZE = 0x0083;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_HOTKEY = 0x0312;
    
    // 命中测试结果
    public const int HTCLIENT = 1;
    public const int HT_CAPTION = 0x2;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;
    
    // 键盘钩子
    public const int WH_KEYBOARD_LL = 13;
    
    // 热键修饰符
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    
    // 虚拟键码
    public const uint VK_Q = 0x51;
    public const uint VK_OEM_3 = 0xC0; // ~ 键
    
    // 窗口边框宽度（用于调整大小的热区）
    public const int BorderWidth = 8;
    // 角落区域宽度（更大的热区便于拖拽）
    public const int CornerWidth = 16;
}
