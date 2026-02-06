using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiniWorldBrowser.Helpers
{
    /// <summary>
    /// DPI 缩放助手，用于在不同 DPI 设置下保持 UI 比例一致
    /// </summary>
    public static class DpiHelper
    {
        /// <summary>
        /// 获取当前系统的 DPI 缩放比例（基于主显示器）
        /// </summary>
        public static float DpiScale
        {
            get
            {
                // 不再缓存，以支持动态 DPI 切换
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return g.DpiX / 96f;
                }
            }
        }

        /// <summary>
        /// 获取指定控件所在显示器的 DPI 缩放比例
        /// </summary>
        public static float GetControlDpiScale(Control? control)
        {
            if (control != null)
            {
                return control.DeviceDpi / 96f;
            }
            return DpiScale;
        }

        /// <summary>
        /// 将逻辑像素转换为当前 DPI 下的物理像素
        /// </summary>
        public static int Scale(int pixels)
        {
            return (int)Math.Round(pixels * DpiScale);
        }

        /// <summary>
        /// 基于特定控件的 DPI 进行缩放
        /// </summary>
        public static int Scale(int pixels, Control? control)
        {
            return (int)Math.Round(pixels * GetControlDpiScale(control));
        }

        /// <summary>
        /// 将逻辑像素转换为当前 DPI 下的物理像素 (float)
        /// </summary>
        public static float Scale(float pixels)
        {
            return pixels * DpiScale;
        }

        /// <summary>
        /// 缩放 Size
        /// </summary>
        public static Size Scale(Size size)
        {
            return new Size(Scale(size.Width), Scale(size.Height));
        }

        /// <summary>
        /// 缩放 Point
        /// </summary>
        public static Point Scale(Point point)
        {
            return new Point(Scale(point.X), Scale(point.Y));
        }

        /// <summary>
        /// 缩放 Padding
        /// </summary>
        public static Padding Scale(Padding padding)
        {
            return new Padding(
                Scale(padding.Left),
                Scale(padding.Top),
                Scale(padding.Right),
                Scale(padding.Bottom)
            );
        }
        
        /// <summary>
        /// 缩放字体大小
        /// </summary>
        public static float ScaleFont(float fontSize)
        {
            // 在 PerMonitorV2 模式下，手动创建的字体需要显式缩放
            return fontSize * DpiScale;
        }

        /// <summary>
        /// 基于特定控件缩放字体大小
        /// </summary>
        public static float ScaleFont(float fontSize, Control? control)
        {
            return fontSize * GetControlDpiScale(control);
        }
    }
}
