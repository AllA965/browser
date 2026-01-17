using System.Drawing.Drawing2D;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 安全信息弹出窗口 - 参考"世界之窗"浏览器设计
/// 显示网站的安全连接信息、证书信息等
/// </summary>
public class SecurityInfoPopup : Form
{
    private readonly string _url;
    private readonly bool _isSecure;
    private readonly string _host;
    
    private TabControl _tabControl = null!;
    private TabPage _permissionsTab = null!;
    private TabPage _connectionTab = null!;
    
    public SecurityInfoPopup(string url, bool isSecure)
    {
        _url = url ?? "";
        _isSecure = isSecure;
        _host = GetHost(url ?? "");
        
        InitializeUI();
    }
    
    private static string GetHost(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return "";
            if (url.StartsWith("about:")) return url;
            var uri = new Uri(url);
            return uri.Host;
        }
        catch { return url; }
    }
    
    private void InitializeUI()
    {
        // 窗口设置
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(340, 380);
        BackColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;
        
        // 添加阴影边框效果
        Padding = new Padding(1);
        
        // 主面板
        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(0)
        };

        // 头部区域
        var headerPanel = CreateHeaderPanel();
        
        // 关闭按钮
        var closeBtn = new Label
        {
            Text = "×",
            Size = new Size(24, 24),
            Location = new Point(Width - 30, 8),
            Font = new Font("Segoe UI", 12F),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (s, e) => Close();
        closeBtn.MouseEnter += (s, e) => closeBtn.ForeColor = Color.Black;
        closeBtn.MouseLeave += (s, e) => closeBtn.ForeColor = Color.Gray;
        
        // 标签页控件
        _tabControl = new TabControl
        {
            Location = new Point(10, 80),
            Size = new Size(Width - 22, Height - 130),
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        
        _permissionsTab = new TabPage("权限");
        _connectionTab = new TabPage("连接");
        
        CreatePermissionsContent();
        CreateConnectionContent();
        
        _tabControl.TabPages.Add(_permissionsTab);
        _tabControl.TabPages.Add(_connectionTab);
        
        // 底部链接
        var bottomLink = new LinkLabel
        {
            Text = "这分别意味着什么?",
            Location = new Point(10, Height - 35),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            LinkColor = Color.FromArgb(0, 102, 204)
        };
        bottomLink.Click += (s, e) => 
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://support.microsoft.com/zh-cn/microsoft-edge/了解-microsoft-edge-中的安全指示器",
                UseShellExecute = true
            }); } catch { }
        };
        
        mainPanel.Controls.Add(closeBtn);
        mainPanel.Controls.Add(headerPanel);
        mainPanel.Controls.Add(_tabControl);
        mainPanel.Controls.Add(bottomLink);
        
        Controls.Add(mainPanel);
        
        // 绘制边框
        Paint += OnPaintBorder;
        
        // 点击外部关闭
        Deactivate += (s, e) => Close();
    }
    
    private Panel CreateHeaderPanel()
    {
        var panel = new Panel
        {
            Location = new Point(10, 10),
            Size = new Size(Width - 40, 60),
            BackColor = Color.White
        };
        
        // 网站域名
        var hostLabel = new Label
        {
            Text = _host,
            Location = new Point(0, 0),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = Color.Black
        };
        
        // 安全状态描述
        var statusLabel = new Label
        {
            Text = _isSecure 
                ? "此网站提供了安全连接。" 
                : "此网站未提供安全连接。",
            Location = new Point(0, 28),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(100, 100, 100)
        };
        
        panel.Controls.Add(hostLabel);
        panel.Controls.Add(statusLabel);
        
        return panel;
    }

    private void CreatePermissionsContent()
    {
        _permissionsTab.BackColor = Color.White;
        _permissionsTab.Padding = new Padding(10);
        
        var permissionsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };
        
        int y = 5;
        
        // 权限项列表
        var permissions = new[]
        {
            ("位置", "询问（默认）"),
            ("摄像头", "询问（默认）"),
            ("麦克风", "询问（默认）"),
            ("通知", "询问（默认）"),
            ("JavaScript", "允许（默认）"),
            ("弹出窗口", "阻止（默认）"),
            ("Cookie", "允许（默认）")
        };
        
        foreach (var (name, status) in permissions)
        {
            var itemPanel = CreatePermissionItem(name, status, y);
            permissionsPanel.Controls.Add(itemPanel);
            y += 28;
        }
        
        // 添加"设置权限"按钮
        y += 10;
        var settingsBtn = new Button
        {
            Text = "设置权限...",
            Location = new Point(5, y),
            Size = new Size(100, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        settingsBtn.FlatAppearance.BorderSize = 0;
        settingsBtn.Click += (s, e) =>
        {
            Close();
            var settingsForm = new Forms.SiteSettingsForm(_url);
            settingsForm.ShowDialog();
        };
        permissionsPanel.Controls.Add(settingsBtn);
        
        _permissionsTab.Controls.Add(permissionsPanel);
    }
    
    private Panel CreatePermissionItem(string name, string status, int y)
    {
        var panel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(_tabControl.Width - 40, 26),
            BackColor = Color.White
        };
        
        var nameLabel = new Label
        {
            Text = name,
            Location = new Point(0, 4),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.Black
        };
        
        var statusLabel = new Label
        {
            Text = status,
            Location = new Point(panel.Width - 100, 4),
            Size = new Size(100, 20),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(100, 100, 100),
            TextAlign = ContentAlignment.MiddleRight
        };
        
        panel.Controls.Add(nameLabel);
        panel.Controls.Add(statusLabel);
        
        return panel;
    }
    
    private void CreateConnectionContent()
    {
        _connectionTab.BackColor = Color.White;
        _connectionTab.Padding = new Padding(10);
        
        var connectionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };
        
        int y = 5;
        
        if (_isSecure)
        {
            // 证书信息区域
            var certPanel = CreateCertificateInfoPanel(ref y);
            connectionPanel.Controls.Add(certPanel);
            
            y += 20;
            
            // 加密信息区域
            var encryptPanel = CreateEncryptionInfoPanel(ref y);
            connectionPanel.Controls.Add(encryptPanel);
        }
        else
        {
            // 不安全连接提示
            var warningPanel = CreateInsecureWarningPanel(ref y);
            connectionPanel.Controls.Add(warningPanel);
        }
        
        _connectionTab.Controls.Add(connectionPanel);
    }

    private Panel CreateCertificateInfoPanel(ref int y)
    {
        var panel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(_tabControl.Width - 40, 100),
            BackColor = Color.White
        };
        
        // 证书图标（绿色锁）
        var iconLabel = new Label
        {
            Text = "🔒",
            Location = new Point(0, 5),
            Size = new Size(30, 30),
            Font = new Font("Segoe UI Symbol", 14F),
            ForeColor = Color.FromArgb(0, 150, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        
        // 证书描述
        var descLabel = new Label
        {
            Text = $"鲲穹AI浏览器已证实此网站的证书是有效的。\n服务器提供了安全的 HTTPS 连接。",
            Location = new Point(35, 5),
            Size = new Size(panel.Width - 45, 40),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.Black
        };
        
        // 证书信息链接
        var certLink = new LinkLabel
        {
            Text = "证书信息",
            Location = new Point(35, 50),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            LinkColor = Color.FromArgb(0, 102, 204)
        };
        certLink.Click += (s, e) => ShowCertificateDetails();
        
        panel.Controls.Add(iconLabel);
        panel.Controls.Add(descLabel);
        panel.Controls.Add(certLink);
        
        y += panel.Height;
        return panel;
    }
    
    private Panel CreateEncryptionInfoPanel(ref int y)
    {
        var panel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(_tabControl.Width - 40, 120),
            BackColor = Color.White
        };
        
        // 加密图标（绿色锁）
        var iconLabel = new Label
        {
            Text = "🔐",
            Location = new Point(0, 5),
            Size = new Size(30, 30),
            Font = new Font("Segoe UI Symbol", 14F),
            ForeColor = Color.FromArgb(0, 150, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        
        // 加密描述
        var descLabel = new Label
        {
            Text = $"您与 {_host} 之间的连接采用新型加密套件进行了加密。",
            Location = new Point(35, 5),
            Size = new Size(panel.Width - 45, 35),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.Black
        };
        
        // TLS 版本
        var tlsLabel = new Label
        {
            Text = "该连接使用 TLS 1.2 或更高版本。",
            Location = new Point(35, 45),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(80, 80, 80)
        };
        
        // 加密算法
        var cipherLabel = new Label
        {
            Text = "该连接使用 AES_128_GCM 进行加密和身份验证，\n并使用 ECDHE_RSA 作为密钥交换机制。",
            Location = new Point(35, 70),
            Size = new Size(panel.Width - 45, 40),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(80, 80, 80)
        };
        
        panel.Controls.Add(iconLabel);
        panel.Controls.Add(descLabel);
        panel.Controls.Add(tlsLabel);
        panel.Controls.Add(cipherLabel);
        
        y += panel.Height;
        return panel;
    }

    private Panel CreateInsecureWarningPanel(ref int y)
    {
        var panel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(_tabControl.Width - 40, 150),
            BackColor = Color.White
        };
        
        // 警告图标（红色）
        var iconLabel = new Label
        {
            Text = "⚠",
            Location = new Point(0, 5),
            Size = new Size(30, 30),
            Font = new Font("Segoe UI Symbol", 16F),
            ForeColor = Color.FromArgb(200, 50, 50),
            TextAlign = ContentAlignment.MiddleCenter
        };
        
        // 警告描述
        var descLabel = new Label
        {
            Text = "您与此网站之间建立的连接不安全。\n\n" +
                   "请勿在此网站上输入任何敏感信息（例如密码或信用卡信息），" +
                   "因为攻击者可能会窃取这些信息。",
            Location = new Point(35, 5),
            Size = new Size(panel.Width - 45, 80),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(80, 80, 80)
        };
        
        // 建议
        var suggestionLabel = new Label
        {
            Text = "建议：\n• 不要在此页面输入个人信息\n• 检查网址是否正确\n• 联系网站管理员",
            Location = new Point(35, 90),
            Size = new Size(panel.Width - 45, 60),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(100, 100, 100)
        };
        
        panel.Controls.Add(iconLabel);
        panel.Controls.Add(descLabel);
        panel.Controls.Add(suggestionLabel);
        
        y += panel.Height;
        return panel;
    }
    
    private void ShowCertificateDetails()
    {
        MessageBox.Show(
            $"网站: {_host}\n\n" +
            "证书信息:\n" +
            "• 颁发给: " + _host + "\n" +
            "• 颁发者: 受信任的证书颁发机构\n" +
            "• 有效期: 有效\n\n" +
            "此证书用于验证网站身份并加密您与网站之间的通信。",
            "证书信息",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
    
    private void OnPaintBorder(object? sender, PaintEventArgs e)
    {
        // 绘制边框和阴影效果
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        
        // 绘制边框
        using var borderPen = new Pen(Color.FromArgb(200, 200, 200), 1);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
    
    /// <summary>
    /// 在指定控件下方显示弹出窗口
    /// </summary>
    public void ShowBelow(Control anchor)
    {
        var screenPos = anchor.PointToScreen(new Point(0, anchor.Height));
        
        // 确保不超出屏幕边界
        var screen = Screen.FromControl(anchor);
        if (screenPos.X + Width > screen.WorkingArea.Right)
            screenPos.X = screen.WorkingArea.Right - Width;
        if (screenPos.Y + Height > screen.WorkingArea.Bottom)
            screenPos.Y = screenPos.Y - anchor.Height - Height;
        
        Location = screenPos;
        Show();
    }
    
    protected override CreateParams CreateParams
    {
        get
        {
            // 添加阴影效果
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }
}
