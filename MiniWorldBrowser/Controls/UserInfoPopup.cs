using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;
using MiniWorldBrowser.Helpers;
using System.Drawing.Drawing2D;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 用户信息弹出窗口
/// </summary>
public class UserInfoPopup : Form
{
    private readonly ILoginService _loginService;
    private readonly Action _onLoginClick;
    private readonly Action _onLogoutClick;
    private Image? _avatarImage;
    private Panel? _avatarBox;
    private const int CornerRadius = 10;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    public UserInfoPopup(ILoginService loginService, Action onLoginClick, Action onLogoutClick)
    {
        _loginService = loginService;
        _onLoginClick = onLoginClick;
        _onLogoutClick = onLogoutClick;

        InitializeUI();
        
        if (_loginService.IsLoggedIn && _loginService.CurrentUser != null && !string.IsNullOrEmpty(_loginService.CurrentUser.Avatar))
        {
            LoadAvatarAsync(_loginService.CurrentUser.Avatar);
        }

        Size = new Size(260, (_loginService.IsLoggedIn ? 200 : 150) + 6);
        BackColor = Color.White;
        ShowInTaskbar = false;
        Padding = new Padding(1); // 为边框预留
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        
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

    private async void LoadAvatarAsync(string url)
    {
        try
        {
            var img = await ImageHelper.GetImageAsync(url);
            if (img != null)
            {
                _avatarImage = img;
                if (!IsDisposed && _avatarBox != null)
                {
                    _avatarBox.Invalidate();
                }
            }
        }
        catch
        {
            // 忽略加载错误
        }
    }

    private void AddDivider(Control container, int y)
    { 
        var divider = new Panel
        {
            Location = new Point(18, y),
            Size = new Size(224, 1),
            BackColor = Color.FromArgb(240, 240, 240)
        };
        container.Controls.Add(divider);
    }

    private void InitializeUI()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        
        // 顶部预留 6 像素给小三角
        var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 24, 18, 18) };

        if (_loginService.IsLoggedIn)
        {
            var userInfo = _loginService.CurrentUser;
            
            // 头像容器
            _avatarBox = new Panel { Size = new Size(52, 52), Location = new Point(18, 18), BackColor = Color.Transparent };
            _avatarBox.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, 51, 51);

                if (userInfo != null)
                {
                    if (_avatarImage != null)
                    {
                        using var path = new GraphicsPath();
                        path.AddEllipse(rect);
                        g.SetClip(path);
                        g.DrawImage(_avatarImage, rect);
                        g.ResetClip();
                    }
                    else
                    {
                        using var brush = new SolidBrush(Color.FromArgb(0, 120, 215));
                        g.FillEllipse(brush, rect);

                        var initial = userInfo.DisplayInitial;
                        using var font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold);
                        var size = g.MeasureString(initial, font);
                        g.DrawString(initial, font, Brushes.White, 
                            (52 - size.Width) / 2, 
                            (52 - size.Height) / 2 + 2);
                    }
                }
            };

            // 昵称
            var nicknameLabel = new Label
            {
                Text = userInfo?.DisplayName ?? "未登录",
                Location = new Point(82, 22),
                Width = 150,
                AutoEllipsis = true,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            // 状态/ID
            var statusLabel = new Label
            {
                Text = "已连接云同步",
                Location = new Point(82, 46),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = Color.FromArgb(120, 120, 120)
            };

            // 分割线
            AddDivider(mainPanel, 85);

            // 账号管理按钮
            var manageBtn = new Button
            {
                Text = "⚙  个人资料设置",
                Size = new Size(224, 34),
                Location = new Point(18, 100),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Microsoft YaHei UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            manageBtn.FlatAppearance.BorderSize = 0;
            manageBtn.MouseEnter += (s, e) => manageBtn.BackColor = Color.FromArgb(245, 245, 245);
            manageBtn.MouseLeave += (s, e) => manageBtn.BackColor = Color.White;
            manageBtn.Click += (s, e) =>
            {
                if (IsDisposed) return;
                manageBtn.Enabled = false;
                BeginInvoke(new Action(() => {
                    if (!IsDisposed) Close();
                    MessageBox.Show("个人资料设置功能正在开发中...", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            };
            ApplyRoundedRegion(manageBtn, 6);

            // 退出按钮 (自定义样式)
            var logoutBtn = new Button
            {
                Text = "⏻  退出登录",
                Size = new Size(224, 34),
                Location = new Point(18, 140),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Microsoft YaHei UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            logoutBtn.FlatAppearance.BorderSize = 0;
            logoutBtn.MouseEnter += (s, e) => {
                logoutBtn.BackColor = Color.FromArgb(255, 240, 240);
                logoutBtn.ForeColor = Color.FromArgb(220, 50, 50);
            };
            logoutBtn.MouseLeave += (s, e) => {
                logoutBtn.BackColor = Color.White;
                logoutBtn.ForeColor = Color.FromArgb(80, 80, 80);
            };
            logoutBtn.Click += (s, e) =>
            {
                if (IsDisposed) return;
                logoutBtn.Enabled = false;
                BeginInvoke(new Action(() => {
                    if (!IsDisposed) Close();
                    _onLogoutClick?.Invoke();
                }));
            };
            ApplyRoundedRegion(logoutBtn, 6);

            mainPanel.Controls.Add(_avatarBox);
            mainPanel.Controls.Add(nicknameLabel);
            mainPanel.Controls.Add(statusLabel);
            mainPanel.Controls.Add(manageBtn);
            mainPanel.Controls.Add(logoutBtn);
        }
        else
        {
            var tipLabel = new Label
            {
                Text = "登录后同步您的书签和历史记录",
                Location = new Point(18, 22),
                Size = new Size(224, 45),
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            
            var loginBtn = new Button
            {
                Text = "立即登录",
                Size = new Size(224, 44),
                Location = new Point(18, 80),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            loginBtn.FlatAppearance.BorderSize = 0;
            
            loginBtn.MouseEnter += (s, e) => loginBtn.BackColor = Color.FromArgb(0, 100, 180);
            loginBtn.MouseLeave += (s, e) => loginBtn.BackColor = Color.FromArgb(0, 120, 215);
            loginBtn.MouseDown += (s, e) => loginBtn.BackColor = Color.FromArgb(0, 80, 150);
            loginBtn.MouseUp += (s, e) => loginBtn.BackColor = Color.FromArgb(0, 100, 180);

            loginBtn.Click += (s, e) =>
            {
                if (IsDisposed) return;
                loginBtn.Enabled = false;
                BeginInvoke(new Action(() => {
                    _onLoginClick?.Invoke();
                    if (!IsDisposed) Close();
                }));
            };

            ApplyRoundedRegion(loginBtn, 8);

            mainPanel.Controls.Add(tipLabel);
            mainPanel.Controls.Add(loginBtn);
        }

        Controls.Add(mainPanel);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0) return;
        
        // 包含小三角的完整路径
        using var path = CreateRoundedRectPath(new Rectangle(0, 6, Width, Height - 6), CornerRadius);
        
        int triangleWidth = DpiHelper.Scale(12);
        int centerX = Width / 2;
        Point[] triangle = new Point[]
        {
            new Point(centerX - triangleWidth / 2, 6),
            new Point(centerX, 0),
            new Point(centerX + triangleWidth / 2, 6)
        };
        path.AddPolygon(triangle);

        Region = new Region(path);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float penWidth = DpiHelper.Scale(1f);
        int triangleWidth = DpiHelper.Scale(12);
        int centerX = Width / 2;

        // 1. 填充背景（关键：显式填充白色，避免 Region 边缘出现杂色）
        using (var brush = new SolidBrush(BackColor))
        {
            using var fillPath = CreateRoundedRectPath(new RectangleF(0, 6, Width, Height - 6), CornerRadius);
            g.FillPath(brush, fillPath);
            
            PointF[] triangleFill = new PointF[]
            {
                new PointF(centerX - triangleWidth / 2f, 6),
                new PointF(centerX, 0),
                new PointF(centerX + triangleWidth / 2f, 6)
            };
            g.FillPolygon(brush, triangleFill);
        }

        // 2. 绘制圆角矩形边框 (缩进以确保抗锯齿边缘不被裁剪)
        using (var path = CreateRoundedRectPath(new RectangleF(penWidth / 2f, 6 + penWidth / 2f, Width - penWidth, Height - 6 - penWidth), CornerRadius))
        {
            using var pen = new Pen(Color.FromArgb(220, 220, 220), penWidth);
            g.DrawPath(pen, path);

            // 绘制顶部引导小三角边框
            PointF[] trianglePoints = new PointF[]
            {
                new PointF(centerX - triangleWidth / 2f, 6 + penWidth / 2f),
                new PointF(centerX, penWidth / 2f),
                new PointF(centerX + triangleWidth / 2f, 6 + penWidth / 2f)
            };
            
            g.DrawLine(pen, trianglePoints[0], trianglePoints[1]);
            g.DrawLine(pen, trianglePoints[1], trianglePoints[2]);
            
            // 擦除小三角底部的边框线，使其与主体融合
            using var erasePen = new Pen(BackColor, penWidth + 0.5f);
            g.DrawLine(erasePen, trianglePoints[0].X + penWidth, 6 + penWidth / 2f, trianglePoints[2].X - penWidth, 6 + penWidth / 2f);
        }
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var path = CreateRoundedRectPath(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region = new Region(path);
    }

    private static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        return CreateRoundedRectPath(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
    }
}
