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
            case Keys.Escape:
                if (_addressDropdown.Visible)
                {
                    _addressDropdown.Hide();
                    e.SuppressKeyPress = true;
                }
                else
                {
                    _tabManager.ActiveTab?.Stop();
                }
                break;
                
            case Keys.Down:
                if (_addressDropdown.Visible)
                {
                    _addressDropdown.MoveSelection(1);
                    var selected = _addressDropdown.GetSelectedText();
                    if (selected != null)
                    {
                        _addressBar.Text = selected;
                        // ChromeAddressBar now has SelectionStart property
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
        if (_addressBar == null || _addressDropdown == null || _addressDropdown.IsDisposed) return;

        var text = _addressBar.Text.Trim();
        _addressDropdown.SearchEngine = _settingsService.Settings.SearchEngine;
        _addressDropdown.Show(_addressBar, text, _urlHistory);
        
        // 只有当下拉框确实有建议并显示时，才改变地址栏样式
        _addressBar.IsDropdownOpen = _addressDropdown.Visible;
    }

    /// <summary>
    /// 在带有保护的情况下创建新标签页，防止地址栏下拉框意外弹出
    /// </summary>
    private async Task CreateNewTabWithProtection(string url, bool openInBackground = false)
    {
        if (_tabManager == null) return;

        _isInternalAddressUpdate = true;
        try
        {
            await _tabManager.CreateTabAsync(url, openInBackground);
        }
        finally
        {
            // 延迟重置，确保创建过程中的所有事件（如焦点转移、URL变化）都已处理
            await Task.Delay(250);
            _isInternalAddressUpdate = false;
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
                    _ = CreateNewTabWithProtection("about:newtab");
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
                    _ = CreateNewTabWithProtection("about:settings");
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
            
            // 杀毒软件对键盘钩子非常敏感
            // 仅在窗口处于前台且激活时才处理钩子逻辑，减少被判定为“键盘记录器”的风险
            if (nCode >= 0 && wParam == (IntPtr)Win32Constants.WM_KEYDOWN && IsFormActive())
            {
                int vkCode = Marshal.ReadInt32(lParam);
                // 仅处理特定功能键，不拦截普通输入字符
                if (vkCode == (int)Keys.F11 || vkCode == (int)Keys.Escape)
                {
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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NCCALCSIZE_PARAMS
    {
        public RECT rgrc0;
        public RECT rgrc1;
        public RECT rgrc2;
        public IntPtr lppos;
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;

        // 处理 WM_NCCALCSIZE：在保留系统边框/用户区域的前提下，吃掉顶部多余的 1px 间隙
        if (m.Msg == Win32Constants.WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
        {
            // 先让系统完成默认的非客户区计算，这样可以保留正常的调整大小边框
            base.WndProc(ref m);

            // 最大化时保持系统默认行为，避免内容错位
            if (WindowState == FormWindowState.Maximized)
                return;

            try
            {
                var ncc = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(m.LParam);
                // 向上扩展客户端区域 1 像素，用来盖住 Win11 标题栏用户区域留下的细小缝隙
                ncc.rgrc0.Top -= 1;
                Marshal.StructureToPtr(ncc, m.LParam, false);
            }
            catch
            {
                // 忽略结构体转换过程中的异常，保持系统默认行为
            }

            return;
        }

        if (m.Msg == WM_NCHITTEST)
        {
            // 全屏或者最大化时不允许通过边框调整大小，直接交给系统处理
            if (WindowState == FormWindowState.Maximized || _fullscreenManager.IsFullscreen)
            {
                base.WndProc(ref m);
                return;
            }

            if (WindowState == FormWindowState.Normal)
            {
                // 先让系统做一次命中测试（保留 Edge 类似的用户区域行为）
                base.WndProc(ref m);

                // 只有在系统认为当前是客户端区域时，才做自定义边框命中测试，
                // 这样可以在不破坏系统默认逻辑的情况下扩展可拖拽区域
                if ((int)m.Result == Win32Constants.HTCLIENT)
                {
                    var clientPoint = PointToClient(Cursor.Position);
                    var hitResult = GetResizeHitTest(clientPoint);
                    if (hitResult != 0)
                    {
                        m.Result = (IntPtr)hitResult;
                        return;
                    }
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
