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
        private static float _dpiScale = 0f;

        /// <summary>
        /// 获取当前系统的 DPI 缩放比例
        /// </summary>
        public static float DpiScale
        {
            get
            {
                if (_dpiScale == 0f)
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        _dpiScale = g.DpiX / 96f;
                    }
                }
                return _dpiScale;
            }
        }

        /// <summary>
        /// 将逻辑像素转换为当前 DPI 下的物理像素
        /// </summary>
        public static int Scale(int pixels)
        {
            return (int)Math.Round(pixels * DpiScale);
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
            // 字体大小缩放通常由系统自动处理，但在某些手动创建字体的地方需要
            return fontSize; // 暂时返回原值，因为 WinForms 字体通常已经考虑了 DPI
        }
    }
}
