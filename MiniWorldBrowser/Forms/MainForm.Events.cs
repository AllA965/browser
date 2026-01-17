using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Services;
using System.Runtime.InteropServices;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// MainForm - 事件处理部分
/// </summary>
public partial class MainForm
{
    #region 地址栏事件
    
    private void OnAddressBarKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                if (_addressDropdown.Visible)
                {
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null)
                    {
                        _addressBar.Text = selected;
                    }
                    _addressDropdown.Hide();
                }
                NavigateToAddress();
                e.SuppressKeyPress = true;
                break;
                
            case Keys.Escape:
                if (_addressDropdown.Visible) 
                    _addressDropdown.Hide();
                else 
                    _addressBar.Text = _tabManager.ActiveTab?.Url ?? "";
                e.SuppressKeyPress = true;
                break;
                
            case Keys.Down:
                if (_addressDropdown.Visible)
                {
                    _addressDropdown.MoveSelection(1);
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null)
                    {
                        _addressBar.Text = selected;
                        _addressBar.SelectionStart = _addressBar.Text.Length;
                    }
                    e.SuppressKeyPress = true;
                }
                else
                {
                    ShowAddressDropdown();
                    e.SuppressKeyPress = true;
                }
                break;
                
            case Keys.Up:
                if (_addressDropdown.Visible)
                {
                    _addressDropdown.MoveSelection(-1);
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null)
                    {
                        _addressBar.Text = selected;
                        _addressBar.SelectionStart = _addressBar.Text.Length;
                    }
                    e.SuppressKeyPress = true;
                }
                break;
                
            case Keys.Tab:
                if (_addressDropdown.Visible)
                {
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null)
                    {
                        _addressBar.Text = selected;
                        _addressBar.SelectionStart = _addressBar.Text.Length;
                    }
                    e.SuppressKeyPress = true;
                }
                break;
        }
    }
    
    private void NavigateToAddress()
    {
        _addressDropdown.Hide();
        var url = _addressBar.Text.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            _tabManager.ActiveTab?.Navigate(url);
            _browserContainer.Focus();
        }
    }
    
    #endregion
    
    #region 地址栏下拉框
    
    private void ShowAddressDropdown()
    {
        var text = _addressBar.Text.Trim();
        
        // 获取地址栏的父容器（带圆角边框的Panel）
        var addressPanel = _addressBar.Parent;
        if (addressPanel != null)
        {
            _addressDropdown.SearchEngine = _settingsService.Settings.SearchEngine;
            _addressDropdown.Show(addressPanel, text, _urlHistory);
        }
    }
    
    #endregion
    
    #region 键盘快捷键
    
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // 只有窗口获得焦点时才处理快捷键
        if (!IsFormActive())
            return;
            
        if (e.Control)
        {
            switch (e.KeyCode)
            {
                case Keys.T:
                    _ = _tabManager.CreateTabAsync("about:newtab");
                    e.Handled = true;
                    break;
                case Keys.W:
                    if (_tabManager.ActiveTab != null)
                        _tabManager.CloseTab(_tabManager.ActiveTab);
                    e.Handled = true;
                    break;
                case Keys.Tab:
                    if (e.Shift) _tabManager.SwitchToPreviousTab();
                    else _tabManager.SwitchToNextTab();
                    e.Handled = true;
                    break;
                case Keys.L:
                    if (e.Shift)
                    {
                        // Ctrl+Shift+L: 打开日志
                        ShowResourceLog();
                        e.Handled = true;
                    }
                    else
                    {
                        _addressBar.Focus();
                        _addressBar.SelectAll();
                        e.Handled = true;
                    }
                    break;
                case Keys.R:
                    _tabManager.ActiveTab?.Refresh();
                    e.Handled = true;
                    break;
                case Keys.D:
                    ToggleBookmark();
                    e.Handled = true;
                    break;
                case Keys.F:
                    OpenFindInPage();
                    e.Handled = true;
                    break;
                case Keys.B:
                    if (e.Shift)
                    {
                        _bookmarkBar.Visible = !_bookmarkBar.Visible;
                        e.Handled = true;
                    }
                    break;
                case Keys.S:
                    SavePageAs();
                    e.Handled = true;
                    break;
                case Keys.P:
                    PrintPage();
                    e.Handled = true;
                    break;
                case Keys.H:
                    // Ctrl+H: 历史记录
                    _ = _tabManager.CreateTabAsync("about:settings");
                    e.Handled = true;
                    break;
                case Keys.J:
                    // Ctrl+J: 打开下载对话框
                    OpenDownloadDialog();
                    e.Handled = true;
                    break;
                case Keys.N:
                    if (e.Shift)
                    {
                        OpenIncognitoWindow();
                        e.Handled = true;
                    }
                    else
                    {
                        System.Diagnostics.Process.Start(Application.ExecutablePath);
                        e.Handled = true;
                    }
                    break;
                case Keys.Delete:
                    if (e.Shift)
                    {
                        // Ctrl+Shift+Del: 清除浏览数据
                        ShowClearBrowsingDataDialog();
                        e.Handled = true;
                    }
                    break;
                case Keys.Oemplus:
                case Keys.Add:
                    // Ctrl++: 放大
                    ZoomIn();
                    e.Handled = true;
                    break;
                case Keys.OemMinus:
                case Keys.Subtract:
                    // Ctrl+-: 缩小
                    ZoomOut();
                    e.Handled = true;
                    break;
                case Keys.D0:
                case Keys.NumPad0:
                    // Ctrl+0: 重置缩放
                    ResetZoom();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Shift)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    // Shift+Esc: 任务管理器
                    ShowTaskManager();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Alt)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                    _tabManager.ActiveTab?.GoBack();
                    e.Handled = true;
                    break;
                case Keys.Right:
                    _tabManager.ActiveTab?.GoForward();
                    e.Handled = true;
                    break;
                case Keys.Home:
                    _tabManager.ActiveTab?.Navigate(_settingsService.Settings.HomePage);
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.KeyCode)
            {
                case Keys.F5:
                    _tabManager.ActiveTab?.Refresh();
                    e.Handled = true;
                    break;
                case Keys.F11:
                    _fullscreenManager.Toggle();
                    e.Handled = true;
                    break;
                case Keys.F12:
                    OpenDevTools();
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    if (_fullscreenManager.IsFullscreen)
                    {
                        _fullscreenManager.Toggle();
                        e.Handled = true;
                    }
                    else if (_tabManager.ActiveTab?.IsLoading == true)
                    {
                        _tabManager.ActiveTab.Stop();
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
    
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // 只有窗口获得焦点时才处理快捷键
        if (!IsFormActive())
            return base.ProcessCmdKey(ref msg, keyData);
            
        switch (keyData)
        {
            case Keys.F11:
                _fullscreenManager.Toggle();
                return true;
            case Keys.Escape:
                if (_fullscreenManager.IsFullscreen)
                {
                    _fullscreenManager.Toggle();
                    return true;
                }
                break;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    
    #endregion
    
    #region 键盘钩子
    
    private void SetupKeyboardHook()
    {
        try
        {
            _keyboardProc = HookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            var curModule = curProcess.MainModule;
            var moduleHandle = Win32Helper.GetModuleHandle(curModule?.ModuleName);
            _keyboardHookId = Win32Helper.SetWindowsHookEx(
                Win32Constants.WH_KEYBOARD_LL,
                _keyboardProc,
                moduleHandle, 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetupKeyboardHook failed: {ex.Message}");
        }
    }
    
    private void RemoveKeyboardHook()
    {
        if (_keyboardHookId != IntPtr.Zero)
        {
            Win32Helper.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }
    }
    
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            // 检查窗体是否已被释放
            if (IsDisposed || !IsHandleCreated)
                return Win32Helper.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
            
            // 只在窗口获得焦点时处理快捷键
            if (nCode >= 0 && wParam == (IntPtr)Win32Constants.WM_KEYDOWN && IsFormActive())
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == (int)Keys.F11)
                {
                    BeginInvoke(() => { if (!IsDisposed) _fullscreenManager.Toggle(); });
                    return (IntPtr)1;
                }
                if (vkCode == (int)Keys.Escape && _fullscreenManager.IsFullscreen)
                {
                    BeginInvoke(() => { if (!IsDisposed) _fullscreenManager.Toggle(); });
                    return (IntPtr)1;
                }
            }
        }
        catch { }
        return Win32Helper.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }
    
    /// <summary>
    /// 检查当前窗口是否处于活动状态
    /// </summary>
    private bool IsFormActive()
    {
        try
        {
            var foregroundWindow = Win32Helper.GetForegroundWindow();
            return foregroundWindow == Handle || IsChildWindow(foregroundWindow);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 检查指定窗口是否是当前窗口的子窗口
    /// </summary>
    private bool IsChildWindow(IntPtr hwnd)
    {
        try
        {
            var parent = Win32Helper.GetParent(hwnd);
            while (parent != IntPtr.Zero)
            {
                if (parent == Handle)
                    return true;
                parent = Win32Helper.GetParent(parent);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    #endregion
    
    #region 窗口消息处理
    
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        
        if (m.Msg == WM_NCHITTEST)
        {
            // 最大化或全屏时，禁止边框调整大小，直接返回 HTCLIENT
            if (WindowState == FormWindowState.Maximized || _fullscreenManager.IsFullscreen)
            {
                m.Result = (IntPtr)Win32Constants.HTCLIENT;
                return;
            }
            
            // 处理窗口边框命中测试，让 Windows 自动处理调整大小
            if (WindowState == FormWindowState.Normal)
            {
                // 先调用基类处理
                base.WndProc(ref m);
                
                // 获取鼠标位置（从消息参数中提取）
                int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                var screenPoint = new Point(x, y);
                var clientPoint = PointToClient(screenPoint);
                
                // 计算命中测试结果
                var hitResult = GetResizeHitTest(clientPoint);
                if (hitResult != 0)
                {
                    m.Result = (IntPtr)hitResult;
                }
                
                return;
            }
        }
        
        base.WndProc(ref m);
    }
    
    /// <summary>
    /// 获取调整大小的命中测试结果
    /// </summary>
    private int GetResizeHitTest(Point clientPoint)
    {
        int border = Win32Constants.BorderWidth;
        int corner = Win32Constants.CornerWidth;
        
        // 上下边框判断（保持原有逻辑）
        bool nearTop = clientPoint.Y < border;
        bool nearBottom = clientPoint.Y >= Height - border;
        
        // 左右边框判断（与上下边框保持一致的逻辑）
        bool nearLeft = clientPoint.X < border;
        bool nearRight = clientPoint.X >= Width - border;
        
        // 角落区域判断（保持原有逻辑）
        bool inTopCorner = clientPoint.Y < corner;
        bool inBottomCorner = clientPoint.Y >= Height - corner;
        bool inLeftCorner = clientPoint.X < corner;
        bool inRightCorner = clientPoint.X >= Width - corner;
        
        // 优先判断四个角落（使用更大的角落热区）
        if (inTopCorner && inLeftCorner)
            return Win32Constants.HTTOPLEFT;
        if (inTopCorner && inRightCorner)
            return Win32Constants.HTTOPRIGHT;
        if (inBottomCorner && inLeftCorner)
            return Win32Constants.HTBOTTOMLEFT;
        if (inBottomCorner && inRightCorner)
            return Win32Constants.HTBOTTOMRIGHT;
        
        // 然后判断四条边
        if (nearTop)
            return Win32Constants.HTTOP;
        if (nearBottom)
            return Win32Constants.HTBOTTOM;
        if (nearLeft)
            return Win32Constants.HTLEFT;
        if (nearRight)
            return Win32Constants.HTRIGHT;
        
        return 0; // 不在边框区域
    }
    
    private int _lastHitResult = 0;
    
    private void UpdateCursorStyle()
    {
        // 最大化或全屏时不显示调整大小光标
        if (WindowState == FormWindowState.Maximized || _fullscreenManager.IsFullscreen)
        {
            if (_lastHitResult != 0)
            {
                _lastHitResult = 0;
                Cursor = Cursors.Default;
            }
            return;
        }
        
        var clientPoint = PointToClient(Cursor.Position);
        int hitResult = GetResizeHitTest(clientPoint);
        
        // 只在命中结果改变时才更新光标，避免闪烁
        if (hitResult == _lastHitResult)
            return;
        
        _lastHitResult = hitResult;
        
        Cursor = hitResult switch
        {
            Win32Constants.HTTOPLEFT or Win32Constants.HTBOTTOMRIGHT => Cursors.SizeNWSE,
            Win32Constants.HTTOPRIGHT or Win32Constants.HTBOTTOMLEFT => Cursors.SizeNESW,
            Win32Constants.HTTOP or Win32Constants.HTBOTTOM => Cursors.SizeNS,
            Win32Constants.HTLEFT or Win32Constants.HTRIGHT => Cursors.SizeWE,
            _ => Cursors.Default
        };
    }
    
    private void OnFormMouseMove(object? sender, MouseEventArgs e)
    {
        UpdateCursorStyle();
    }
    
    #endregion
}
