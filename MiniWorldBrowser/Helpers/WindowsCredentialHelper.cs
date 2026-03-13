using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MiniWorldBrowser.Helpers;

/// <summary>
/// Windows 凭据验证帮助类
/// </summary>
public static class WindowsCredentialHelper
{
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(
        string lpszUsername,
        string lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out IntPtr phToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int LOGON32_LOGON_INTERACTIVE = 2;
    private const int LOGON32_PROVIDER_DEFAULT = 0;

    /// <summary>
    /// 获取当前 Windows 用户名
    /// </summary>
    public static string GetCurrentUsername()
    {
        return Environment.UserName;
    }

    /// <summary>
    /// 验证 Windows 密码
    /// </summary>
    /// <param name="password">用户输入的密码</param>
    /// <returns>验证是否成功</returns>
    public static bool ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        var username = Environment.UserName;
        var domain = Environment.UserDomainName;
        IntPtr token = IntPtr.Zero;

        try
        {
            // 尝试使用域名登录
            try
            {
                if (LogonUser(username, domain, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out token))
                {
                    if (token != IntPtr.Zero)
                        CloseHandle(token);
                    return true;
                }
            }
            catch { }
            finally
            {
                if (token != IntPtr.Zero) { try { CloseHandle(token); } catch { } token = IntPtr.Zero; }
            }
            
            // 如果域名登录失败，尝试使用本地计算机名
            try
            {
                if (LogonUser(username, Environment.MachineName, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out token))
                {
                    if (token != IntPtr.Zero)
                        CloseHandle(token);
                    return true;
                }
            }
            catch { }
            finally
            {
                if (token != IntPtr.Zero) { try { CloseHandle(token); } catch { } token = IntPtr.Zero; }
            }
            
            // 尝试使用 "." 作为本地域
            try
            {
                if (LogonUser(username, ".", password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out token))
                {
                    if (token != IntPtr.Zero)
                        CloseHandle(token);
                    return true;
                }
            }
            catch { }
            finally
            {
                if (token != IntPtr.Zero) { try { CloseHandle(token); } catch { } token = IntPtr.Zero; }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ValidatePassword error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 显示 Windows 密码验证对话框
    /// </summary>
    /// <param name="owner">父窗口</param>
    /// <returns>验证是否成功</returns>
    public static bool ShowPasswordDialog(IWin32Window? owner = null)
    {
        using var dialog = new WindowsPasswordDialog();
        return dialog.ShowDialog(owner) == DialogResult.OK && dialog.IsAuthenticated;
    }
}

/// <summary>
/// Windows 密码验证对话框
/// </summary>
public class WindowsPasswordDialog : Form
{
    private TextBox _passwordBox = null!;
    public bool IsAuthenticated { get; private set; }

    public WindowsPasswordDialog()
    {
        InitializeUI();
    }
    
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // 允许所有按键正常输入到密码框
        if (_passwordBox != null && _passwordBox.Focused)
        {
            // 只处理 Enter 和 Escape
            if (keyData == Keys.Enter || keyData == Keys.Escape)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }
            // 其他按键不拦截，让 TextBox 正常处理
            return false;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void InitializeUI()
    {
        Text = Localization.Raw();
        Size = DpiHelper.Scale(new Size(420, 280));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));
        KeyPreview = false; // 禁用键盘预览，让控件直接处理按键

        // 顶部蓝色横幅（带钥匙图标）
        var bannerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = DpiHelper.Scale(60),
            BackColor = Color.FromArgb(0, 102, 204)
        };
        bannerPanel.Paint += (s, e) =>
        {
            // 绘制钥匙图标
            e.Graphics.DrawString("🔑", new Font("Segoe UI Emoji", DpiHelper.ScaleFont(24F)), Brushes.Gold, DpiHelper.Scale(15), DpiHelper.Scale(10));
        };
        Controls.Add(bannerPanel);

        // 提示文字
        var tipLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(20, 80)),
            Size = DpiHelper.Scale(new Size(380, 40)),
            ForeColor = Color.FromArgb(0, 102, 204),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        Controls.Add(tipLabel);

        // 用户名标签
        var userLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(20, 135)),
            AutoSize = true
        };
        Controls.Add(userLabel);

        // 用户名显示
        var usernameLabel = new Label
        {
            Text = WindowsCredentialHelper.GetCurrentUsername(),
            Location = DpiHelper.Scale(new Point(120, 135)),
            AutoSize = true,
            ForeColor = Color.FromArgb(51, 51, 51)
        };
        Controls.Add(usernameLabel);

        // 密码标签
        var pwdLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(20, 170)),
            AutoSize = true
        };
        Controls.Add(pwdLabel);

        // 密码输入框
        _passwordBox = new TextBox
        {
            Location = DpiHelper.Scale(new Point(120, 167)),
            Size = DpiHelper.Scale(new Size(260, 23)),
            UseSystemPasswordChar = true
        };
        Controls.Add(_passwordBox);

        // 确定按钮
        var okBtn = new Button
        {
            Text = Localization.T("confirm.ok"),
            Location = DpiHelper.Scale(new Point(210, 210)),
            Size = DpiHelper.Scale(new Size(85, 28)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
        okBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        okBtn.Click += (s, e) =>
        {
            try
            {
                if (WindowsCredentialHelper.ValidatePassword(_passwordBox.Text))
                {
                    IsAuthenticated = true;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show(Localization.Raw(), Localization.Raw(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _passwordBox.SelectAll();
                    _passwordBox.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Password validation error: {ex.Message}");
                MessageBox.Show(Localization.Raw(), Localization.Raw(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _passwordBox.SelectAll();
                _passwordBox.Focus();
            }
        };
        Controls.Add(okBtn);

        // 取消按钮
        var cancelBtn = new Button
        {
            Text = Localization.T("confirm.cancel"),
            Location = DpiHelper.Scale(new Point(305, 210)),
            Size = DpiHelper.Scale(new Size(85, 28)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            DialogResult = DialogResult.Cancel
        };
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        Controls.Add(cancelBtn);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        // 窗体加载时聚焦密码框
        Load += (s, e) => _passwordBox.Focus();
    }
}
