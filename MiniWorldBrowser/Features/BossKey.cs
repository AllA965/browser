using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Features;

/// <summary>
/// 老板键功能 - 全局快捷键隐藏窗口
/// </summary>
public class BossKey : IDisposable
{
    private const int HotkeyId = 9000;
    
    private readonly Form _form;
    private readonly HotKeyWindow _window;
    private bool _isHidden;
    private FormWindowState _previousState;
    
    public event Action<bool>? VisibilityChanged;
    
    public BossKey(Form form)
    {
        _form = form;
        _window = new HotKeyWindow(this);
        _window.AssignHandle(form.Handle);
        
        // 只注册 Ctrl+Q 作为老板键，移除 ~ 键避免误触发
        Win32Helper.RegisterHotKey(_form.Handle, HotkeyId, Win32Constants.MOD_CONTROL, Win32Constants.VK_Q);
        // 不再注册 ~ 键: Win32Helper.RegisterHotKey(_form.Handle, HotkeyId + 1, Win32Constants.MOD_NONE, Win32Constants.VK_OEM_3);
    }
    
    public void Toggle()
    {
        if (_isHidden)
        {
            _form.Show();
            _form.WindowState = _previousState;
            _form.ShowInTaskbar = true;
            _isHidden = false;
        }
        else
        {
            _previousState = _form.WindowState;
            _form.WindowState = FormWindowState.Minimized;
            _form.Hide();
            _form.ShowInTaskbar = false;
            _isHidden = true;
        }
        VisibilityChanged?.Invoke(!_isHidden);
    }
    
    public void Dispose()
    {
        Win32Helper.UnregisterHotKey(_form.Handle, HotkeyId);
        // 不再需要注销 ~ 键: Win32Helper.UnregisterHotKey(_form.Handle, HotkeyId + 1);
        _window.ReleaseHandle();
    }
    
    private class HotKeyWindow : NativeWindow
    {
        private readonly BossKey _owner;
        
        public HotKeyWindow(BossKey owner) => _owner = owner;
        
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32Constants.WM_HOTKEY)
            {
                _owner.Toggle();
            }
            base.WndProc(ref m);
        }
    }
}
