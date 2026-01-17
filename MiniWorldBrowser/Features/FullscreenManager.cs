using System.Runtime.InteropServices;

namespace MiniWorldBrowser.Features;

/// <summary>
/// 全屏管理器
/// </summary>
public class FullscreenManager
{
    private readonly Form _form;
    private readonly Control[] _controlsToHide;
    private bool _isFullscreen;
    private bool _isToggling;
    private Panel? _fullscreenTip;
    
    // 保存进入全屏前的状态
    private FormWindowState _previousWindowState;
    private FormBorderStyle _previousBorderStyle;
    private Rectangle _previousBounds;
    
    // Windows API
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);
    
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left, Right, Top, Bottom;
    }
    
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOOWNERZORDER = 0x0200;
    
    public bool IsFullscreen => _isFullscreen;
    
    public event Action<bool>? FullscreenChanged;
    
    public FullscreenManager(Form form, params Control[] controlsToHide)
    {
        _form = form;
        _controlsToHide = controlsToHide;
    }
    
    private void SetWindowCornerPreference(bool rounded)
    {
        try
        {
            int preference = rounded ? DWMWCP_DEFAULT : DWMWCP_DONOTROUND;
            DwmSetWindowAttribute(_form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch
        {
            // 在不支持的系统上忽略错误
        }
    }
    
    public void Toggle()
    {
        if (_isToggling) return;
        _isToggling = true;
        
        try
        {
            if (_isFullscreen)
            {
                ExitFullscreen();
            }
            else
            {
                EnterFullscreen();
            }
            
            FullscreenChanged?.Invoke(_isFullscreen);
        }
        finally
        {
            _isToggling = false;
        }
    }
    
    private void EnterFullscreen()
    {
        // 保存当前状态
        _previousWindowState = _form.WindowState;
        _previousBorderStyle = _form.FormBorderStyle;
        _previousBounds = _form.Bounds;
        
        _isFullscreen = true;
        
        // 隐藏控件
        foreach (var control in _controlsToHide)
        {
            control.Visible = false;
        }
        
        // 禁用窗口圆角
        SetWindowCornerPreference(false);
        
        // 先恢复正常状态，再设置无边框
        _form.WindowState = FormWindowState.Normal;
        _form.FormBorderStyle = FormBorderStyle.None;
        
        // 获取当前屏幕的完整区域
        var screen = Screen.FromControl(_form);
        var bounds = screen.Bounds;
        
        // 扩展窗口边界几个像素来覆盖缝隙
        const int extend = 8;
        SetWindowPos(_form.Handle, HWND_TOP, 
            bounds.X - extend, bounds.Y - extend, 
            bounds.Width + extend * 2, bounds.Height + extend * 2, 
            SWP_SHOWWINDOW | SWP_FRAMECHANGED | SWP_NOOWNERZORDER);
        
        // 确保窗口在最前面
        _form.BringToFront();
        _form.Activate();
        
        ShowFullscreenTip();
    }
    
    private void ExitFullscreen()
    {
        _isFullscreen = false;
        StopMouseCheckTimer();
        HideFullscreenTip();
        
        // 恢复窗口圆角
        SetWindowCornerPreference(true);
        
        // 恢复边框
        var margins = new MARGINS { Left = 0, Right = 0, Top = 0, Bottom = 0 };
        DwmExtendFrameIntoClientArea(_form.Handle, ref margins);
        
        // 恢复边框样式
        _form.FormBorderStyle = _previousBorderStyle;
        
        // 恢复窗口状态和位置
        if (_previousWindowState == FormWindowState.Maximized)
        {
            _form.WindowState = FormWindowState.Maximized;
        }
        else
        {
            _form.Bounds = _previousBounds;
            _form.WindowState = _previousWindowState;
        }
        
        // 显示控件
        foreach (var control in _controlsToHide)
        {
            control.Visible = true;
        }
    }
    
    private System.Windows.Forms.Timer? _mouseCheckTimer;
    private System.Windows.Forms.Timer? _hideDelayTimer;
    private bool _tipVisible = false;
    private bool _mouseInTopArea = false;
    
    private void ShowFullscreenTip(bool autoHide = true)
    {
        // 取消延迟隐藏
        StopHideDelayTimer();
        
        if (_fullscreenTip != null) return;
        
        _fullscreenTip = CreateFullscreenTipPanel();
        
        _form.Controls.Add(_fullscreenTip);
        _fullscreenTip.BringToFront();
        // 初始位置在屏幕上方（隐藏状态）
        _fullscreenTip.Location = new Point((_form.Width - _fullscreenTip.Width) / 2, -50);
        _tipVisible = true;
        
        if (autoHide)
        {
            // 5秒后自动隐藏
            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!_mouseInTopArea)
                    HideFullscreenTip();
            };
            timer.Start();
        }
        
        // 启动滑动动画
        AnimateSlideIn(_fullscreenTip);
        
        // 启动鼠标位置检测定时器
        StartMouseCheckTimer();
    }
    
    private void StopHideDelayTimer()
    {
        if (_hideDelayTimer != null)
        {
            _hideDelayTimer.Stop();
            _hideDelayTimer.Dispose();
            _hideDelayTimer = null;
        }
    }
    
    private void StartHideDelayTimer()
    {
        StopHideDelayTimer();
        
        // 鼠标离开后延迟 1.5 秒隐藏
        _hideDelayTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _hideDelayTimer.Tick += (s, e) =>
        {
            StopHideDelayTimer();
            if (!_mouseInTopArea)
                HideFullscreenTip();
        };
        _hideDelayTimer.Start();
    }
    
    private Panel CreateFullscreenTipPanel()
    {
        var panel = new Panel
        {
            Size = new Size(320, 44),
            BackColor = Color.FromArgb(248, 249, 250)
        };
        
        // 自定义绘制边框和阴影
        panel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            // 绘制边框
            using var pen = new Pen(Color.FromArgb(218, 220, 224), 1);
            g.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        
        var lblText = new Label
        {
            Text = "您已进入全屏模式。",
            AutoSize = true,
            Location = new Point(24, 14),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(60, 64, 67),
            BackColor = Color.Transparent
        };
        
        var lblExit = new LinkLabel
        {
            Text = "退出全屏模式 (F11)",
            AutoSize = true,
            Location = new Point(156, 14),
            Font = new Font("Microsoft YaHei UI", 9F),
            LinkColor = Color.FromArgb(26, 115, 232),
            ActiveLinkColor = Color.FromArgb(23, 78, 166),
            BackColor = Color.Transparent
        };
        lblExit.LinkBehavior = LinkBehavior.HoverUnderline;
        lblExit.Click += (s, e) => Toggle();
        
        panel.Controls.Add(lblText);
        panel.Controls.Add(lblExit);
        
        return panel;
    }
    
    private void StartMouseCheckTimer()
    {
        StopMouseCheckTimer();
        
        _mouseCheckTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _mouseCheckTimer.Tick += OnMouseCheckTimerTick;
        _mouseCheckTimer.Start();
    }
    
    private void StopMouseCheckTimer()
    {
        if (_mouseCheckTimer != null)
        {
            _mouseCheckTimer.Stop();
            _mouseCheckTimer.Dispose();
            _mouseCheckTimer = null;
        }
    }
    
    private void OnMouseCheckTimerTick(object? sender, EventArgs e)
    {
        if (!_isFullscreen)
        {
            StopMouseCheckTimer();
            return;
        }
        
        var mousePos = _form.PointToClient(Control.MousePosition);
        
        // 鼠标在顶部 50 像素区域内
        bool inTopArea = mousePos.Y < 50 && mousePos.X >= 0 && mousePos.X < _form.Width;
        
        if (inTopArea)
        {
            _mouseInTopArea = true;
            StopHideDelayTimer();
            
            if (!_tipVisible && _fullscreenTip == null)
            {
                ShowFullscreenTip(autoHide: false);
            }
        }
        else
        {
            if (_mouseInTopArea)
            {
                _mouseInTopArea = false;
                // 鼠标离开顶部区域，延迟隐藏提示
                if (_tipVisible && _fullscreenTip != null)
                {
                    StartHideDelayTimer();
                }
            }
        }
    }
    
    private void HideFullscreenTip()
    {
        StopHideDelayTimer();
        if (_fullscreenTip != null)
        {
            AnimateSlideOut(_fullscreenTip);
        }
        _tipVisible = false;
    }
    
    private void AnimateSlideIn(Panel panel)
    {
        var animationTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
        int startY = -50;
        int endY = 20; // 弹窗最终位置
        int duration = 300; // 动画时长（毫秒）
        long startTime = DateTime.Now.Ticks;
        
        animationTimer.Tick += (s, e) =>
        {
            long elapsed = (DateTime.Now.Ticks - startTime) / 10000; // 转换为毫秒
            
            if (elapsed >= duration)
            {
                panel.Location = new Point((_form.Width - panel.Width) / 2, endY);
                animationTimer.Stop();
                animationTimer.Dispose();
            }
            else
            {
                // 使用缓动函数（ease-out）
                double progress = (double)elapsed / duration;
                double easeProgress = 1 - Math.Pow(1 - progress, 3); // ease-out-cubic
                
                int currentY = startY + (int)((endY - startY) * easeProgress);
                panel.Location = new Point((_form.Width - panel.Width) / 2, currentY);
            }
        };
        animationTimer.Start();
    }
    
    private void AnimateSlideOut(Panel panel)
    {
        var animationTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps
        int startY = panel.Location.Y;
        int endY = -50; // 滑出屏幕
        int duration = 200; // 动画时长（毫秒）
        long startTime = DateTime.Now.Ticks;
        
        animationTimer.Tick += (s, e) =>
        {
            long elapsed = (DateTime.Now.Ticks - startTime) / 10000; // 转换为毫秒
            
            if (elapsed >= duration)
            {
                _form.Controls.Remove(panel);
                panel.Dispose();
                _fullscreenTip = null;
                animationTimer.Stop();
                animationTimer.Dispose();
            }
            else
            {
                // 使用缓动函数（ease-in）
                double progress = (double)elapsed / duration;
                double easeProgress = progress * progress; // ease-in-quad
                
                int currentY = startY + (int)((endY - startY) * easeProgress);
                panel.Location = new Point((_form.Width - panel.Width) / 2, currentY);
            }
        };
        animationTimer.Start();
    }
}
