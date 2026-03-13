using System.Drawing.Drawing2D;
using MiniWorldBrowser.Helpers;

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

        Opacity = 0;
        var animTimer = new System.Windows.Forms.Timer { Interval = 10 };
        animTimer.Tick += (s, e) => {
            if (Opacity < 1)
            {
                Opacity += 0.1;
            }
            else
            {
                Opacity = 1;
                animTimer.Stop();
                animTimer.Dispose();
            }
        };
        Load += (s, e) => animTimer.Start();
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
        Size = DpiHelper.Scale(new Size(340, 380));
        BackColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;
        
        // 添加阴影边框效果
        Padding = DpiHelper.Scale(new Padding(1));
        
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
            Size = DpiHelper.Scale(new Size(24, 24)),
            Location = new Point(Width - DpiHelper.Scale(30), DpiHelper.Scale(8)),
            Font = new Font("Segoe UI", DpiHelper.ScaleFont(12F)),
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
            Location = DpiHelper.Scale(new Point(10, 80)),
            Size = DpiHelper.Scale(new Size(340 - 22, 380 - 130)), // 修正 Size 计算，使用原始值计算后再缩放
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        
        _permissionsTab = new TabPage(Localization.Raw());
        _connectionTab = new TabPage(Localization.Raw());
        
        CreatePermissionsContent();
        CreateConnectionContent();
        
        _tabControl.TabPages.Add(_permissionsTab);
        _tabControl.TabPages.Add(_connectionTab);
        
        // 底部链接
        var bottomLink = new LinkLabel
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(10, 345)), // 使用 DpiHelper.Scale 缩放 Point
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
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

        // 设置双缓冲
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        
        // 点击外部关闭
        Deactivate += (s, e) => Close();
    }
    
    private Panel CreateHeaderPanel()
    {
        var panel = new Panel
        {
            Location = DpiHelper.Scale(new Point(10, 10)),
            Size = DpiHelper.Scale(new Size(340 - 40, 60)),
            BackColor = Color.White
        };
        
        // 网站域名
        var hostLabel = new Label
        {
            Text = _host,
            Location = DpiHelper.Scale(new Point(0, 0)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(11F), FontStyle.Bold),
            ForeColor = Color.Black
        };
        
        // 安全状态描述
        var statusLabel = new Label
        {
            Text = _isSecure 
                ? Localization.Raw()
                : Localization.Raw(),
            Location = DpiHelper.Scale(new Point(0, 28)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = Color.FromArgb(100, 100, 100)
        };
        
        panel.Controls.Add(hostLabel);
        panel.Controls.Add(statusLabel);
        
        return panel;
    }

    private void CreatePermissionsContent()
    {
        _permissionsTab.BackColor = Color.White;
        _permissionsTab.Padding = DpiHelper.Scale(new Padding(10));
        
        var permissionsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };
        
        int y = DpiHelper.Scale(5);
        
        // 权限项列表
        var permissions = new[]
        {
            (Localization.Raw(), Localization.Raw()),
            (Localization.Raw(), Localization.Raw()),
            (Localization.Raw(), Localization.Raw()),
            (Localization.Raw(), Localization.Raw()),
            (Localization.Raw(), Localization.Raw()),
            (Localization.Raw(), Localization.Raw()),
            (Localization.Raw(), Localization.Raw())
        };
        
        foreach (var (name, status) in permissions)
        {
            var itemPanel = CreatePermissionItem(name, status, y);
            permissionsPanel.Controls.Add(itemPanel);
            y += DpiHelper.Scale(28);
        }
        
        // 添加"设置权限"按钮
        y += DpiHelper.Scale(10);
        var settingsBtn = new Button
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(5, y)),
            Size = DpiHelper.Scale(new Size(100, 28)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
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
            Location = DpiHelper.Scale(new Point(0, y)),
            Size = new Size(_tabControl.Width - DpiHelper.Scale(20), DpiHelper.Scale(26)),
            BackColor = Color.White
        };

        var nameLabel = new Label
        {
            Text = name,
            Location = DpiHelper.Scale(new Point(0, 4)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = Color.Black
        };

        var statusLabel = new Label
        {
            Text = status,
            Location = new Point(panel.Width - DpiHelper.Scale(100), DpiHelper.Scale(4)),
            Size = DpiHelper.Scale(new Size(100, 20)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
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
        _connectionTab.Padding = DpiHelper.Scale(new Padding(10));

        var connectionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };

        int y = DpiHelper.Scale(5);

        if (_isSecure)
        {
            // 证书信息区域
            var certPanel = CreateCertificateInfoPanel(ref y);
            connectionPanel.Controls.Add(certPanel);

            y += DpiHelper.Scale(20);

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
            Location = DpiHelper.Scale(new Point(0, y)),
            Size = new Size(_tabControl.Width - DpiHelper.Scale(40), DpiHelper.Scale(100)),
            BackColor = Color.White
        };

        // 证书图标（绿色锁）
        var iconLabel = new Label
        {
            Text = "🔒",
            Location = DpiHelper.Scale(new Point(0, 5)),
            Size = DpiHelper.Scale(new Size(30, 30)),
            Font = new Font("Segoe UI Symbol", DpiHelper.ScaleFont(14F)),
            ForeColor = Color.FromArgb(0, 150, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // 证书描述
        var descLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(35, 5)),
            Size = new Size(panel.Width - DpiHelper.Scale(45), DpiHelper.Scale(40)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = Color.Black
        };

        // 证书信息链接
        var certLink = new LinkLabel
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(35, 50)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
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
            Location = DpiHelper.Scale(new Point(0, y)),
            Size = new Size(_tabControl.Width - DpiHelper.Scale(40), DpiHelper.Scale(120)),
            BackColor = Color.White
        };

        // 加密图标（绿色锁）
        var iconLabel = new Label
        {
            Text = "🔐",
            Location = DpiHelper.Scale(new Point(0, 5)),
            Size = DpiHelper.Scale(new Size(30, 30)),
            Font = new Font("Segoe UI Symbol", DpiHelper.ScaleFont(14F)),
            ForeColor = Color.FromArgb(0, 150, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // 加密描述
        var descLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(35, 5)),
            Size = new Size(panel.Width - DpiHelper.Scale(45), DpiHelper.Scale(35)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = Color.Black
        };

        // TLS 版本
        var tlsLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(35, 45)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        // 加密算法
        var cipherLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(35, 70)),
            Size = new Size(panel.Width - DpiHelper.Scale(45), DpiHelper.Scale(40)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
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
            Location = DpiHelper.Scale(new Point(0, y)),
            Size = new Size(_tabControl.Width - DpiHelper.Scale(40), DpiHelper.Scale(180)),
            BackColor = Color.White
        };

        // 警告图标（红色）
        var iconLabel = new Label
        {
            Text = "⚠",
            Location = DpiHelper.Scale(new Point(0, 5)),
            Size = DpiHelper.Scale(new Size(30, 30)),
            Font = new Font("Segoe UI Symbol", DpiHelper.ScaleFont(16F)),
            ForeColor = Color.FromArgb(200, 50, 50),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // 警告描述
        var descLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(35, 5)),
            Size = new Size(panel.Width - DpiHelper.Scale(45), DpiHelper.Scale(100)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        // 建议
        var suggestionLabel = new Label
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(35, 105)),
            Size = new Size(panel.Width - DpiHelper.Scale(45), DpiHelper.Scale(70)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
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
            Localization.Raw(),
            Localization.Raw(),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
    
    private void OnPaintBorder(object? sender, PaintEventArgs e)
    {
        // 绘制边框和阴影效果
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float penWidth = DpiHelper.Scale(1f);

        // 填充背景
        using (var brush = new SolidBrush(BackColor))
        {
            g.FillRectangle(brush, ClientRectangle);
        }

        // 绘制边框 (缩进以确保抗锯齿边缘不被裁剪)
        using var borderPen = new Pen(Color.FromArgb(200, 200, 200), penWidth);
        g.DrawRectangle(borderPen, penWidth / 2f, penWidth / 2f, Width - penWidth, Height - penWidth);
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
