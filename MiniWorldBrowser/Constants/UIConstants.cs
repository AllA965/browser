using System.Drawing;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Constants;

/// <summary>
/// UI 相关常量定义
/// </summary>
public static class UIConstants
{
    // 颜色
    public static readonly Color IncognitoAccentColor = Color.FromArgb(138, 180, 248);
    public static readonly Color IncognitoBackColor = Color.FromArgb(53, 54, 58);
    public static readonly Color DefaultBackColor = Color.FromArgb(232, 234, 237);

    // 窗口尺寸
    public static readonly Size DefaultWindowSize = new(1200, 800);
    public static readonly Size MinWindowSize = new(800, 600);

    // 标签页布局
    public const int NormalTabMaxWidthRaw = 200;
    public const int NormalTabMinWidthRaw = 100;
    public const int PinnedTabWidthRaw = 40;
    public const int OverflowButtonWidthRaw = 32;
    public const int NewTabButtonWidthRaw = 32;
    public const int TabBarPaddingRaw = 4;
    
    // 动态计算的 DPI 缩放值 (使用属性以便在运行时获取)
    public static int NormalTabMaxWidth => DpiHelper.Scale(NormalTabMaxWidthRaw);
    public static int NormalTabMinWidth => DpiHelper.Scale(NormalTabMinWidthRaw);
    public static int PinnedTabWidth => DpiHelper.Scale(PinnedTabWidthRaw);
    public static int OverflowButtonWidth => DpiHelper.Scale(OverflowButtonWidthRaw);
    public static int NewTabButtonWidth => DpiHelper.Scale(NewTabButtonWidthRaw);
    public static int TabBarPadding => DpiHelper.Scale(TabBarPaddingRaw);

    // 动画起始宽度
    public const int NewTabStartWidthRaw = 40;
    public const int PinnedTabStartWidthRaw = 20;
    
    public static int NewTabStartWidth => DpiHelper.Scale(NewTabStartWidthRaw);
    public static int PinnedTabStartWidth => DpiHelper.Scale(PinnedTabStartWidthRaw);

    // 动画持续时间
    public const int NewTabAnimationDuration = 40;
    public const int TabAnimationDuration = 20;
}
