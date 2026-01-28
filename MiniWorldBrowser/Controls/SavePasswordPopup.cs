namespace MiniWorldBrowser.Controls;

/// <summary>
/// 弹窗模式
/// </summary>
public enum PasswordPopupMode
{
    /// <summary>询问是否保存密码</summary>
    AskToSave,
    /// <summary>显示已保存的密码</summary>
    ShowSaved
}

/// <summary>
/// 保存密码提示弹窗（参考世界之窗浏览器样式）
/// </summary>
public class SavePasswordPopup : Form
{
    private readonly string _host;
    private readonly string _username;
    private readonly string _password;
    private readonly PasswordPopupMode _mode;
    private System.Windows.Forms.Timer? _autoCloseTimer;
    private bool _isManagePasswordsClicked;

    public bool ShouldSave { get; private set; }
    public bool NeverSave { get; private set; }

    // 管理密码点击事件
    public event EventHandler? ManagePasswordsClicked;

    public SavePasswordPopup(string host, string username, string password, PasswordPopupMode mode = PasswordPopupMode.AskToSave)
    {
        _host = host;
        _username = username;
        _password = password;
        _mode = mode;
        InitializeUI();
        StartAutoCloseTimer();

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

    private void InitializeUI()
    {
        Text = "";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);

        // 绘制边框
        Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(200, 200, 200), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        if (_mode == PasswordPopupMode.AskToSave)
        {
            InitializeAskToSaveUI();
        }
        else
        {
            InitializeShowSavedUI();
        }

        // 点击弹窗外部时关闭
        Deactivate += (s, e) =>
        {
            Task.Delay(150).ContinueWith(_ =>
            {
                if (!IsDisposed && !ShouldSave && !NeverSave && !_isManagePasswordsClicked)
                {
                    try { Invoke(() => Close()); }
                    catch { }
                }
            });
        };
    }

    /// <summary>
    /// 询问是否保存密码的 UI
    /// </summary>
    private void InitializeAskToSaveUI()
    {
        Size = new Size(380, 140);

        // 标题
        var titleLabel = new Label
        {
            Text = "希望鲲穹AI浏览器保存您在此网站上使用的密码吗？",
            Location = new Point(15, 15),
            Size = new Size(320, 20),
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        Controls.Add(titleLabel);

        // 用户名显示
        var usernameLabel = new Label
        {
            Text = _username,
            Location = new Point(15, 45),
            Size = new Size(150, 20),
            ForeColor = Color.FromArgb(51, 51, 51)
        };
        Controls.Add(usernameLabel);

        // 密码显示（星号）
        var passwordLabel = new Label
        {
            Text = new string('*', Math.Min(_password.Length, 12)),
            Location = new Point(180, 45),
            Size = new Size(150, 20),
            ForeColor = Color.Gray
        };
        Controls.Add(passwordLabel);

        // 关闭按钮（X）
        var closeBtn = new Label
        {
            Text = "×",
            Location = new Point(350, 10),
            Size = new Size(20, 20),
            Font = new Font("Microsoft YaHei UI", 12F),
            ForeColor = Color.Gray,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        closeBtn.Click += (s, e) => Close();
        closeBtn.MouseEnter += (s, e) => closeBtn.ForeColor = Color.FromArgb(180, 0, 0);
        closeBtn.MouseLeave += (s, e) => closeBtn.ForeColor = Color.Gray;
        Controls.Add(closeBtn);

        // 保存密码按钮
        var saveBtn = new Button
        {
            Text = "保存密码",
            Location = new Point(175, 85),
            Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.Click += (s, e) =>
        {
            ShouldSave = true;
            Close();
        };
        Controls.Add(saveBtn);

        // 一律不按钮
        var neverBtn = new Button
        {
            Text = "一律不",
            Location = new Point(270, 85),
            Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        neverBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        neverBtn.Click += (s, e) =>
        {
            NeverSave = true;
            Close();
        };
        Controls.Add(neverBtn);
    }

    /// <summary>
    /// 显示已保存密码的 UI
    /// </summary>
    private void InitializeShowSavedUI()
    {
        Size = new Size(360, 150);

        // 标题
        var titleLabel = new Label
        {
            Text = "已保存此网站的密码：",
            Location = new Point(15, 15),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F),
            ForeColor = Color.FromArgb(51, 51, 51)
        };
        Controls.Add(titleLabel);

        // 用户名显示
        var usernameLabel = new Label
        {
            Text = _username,
            Location = new Point(30, 50),
            Size = new Size(140, 20),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(51, 51, 51)
        };
        Controls.Add(usernameLabel);

        // 密码显示（星号）
        var passwordLabel = new Label
        {
            Text = new string('*', Math.Min(_password.Length, 10)),
            Location = new Point(180, 50),
            Size = new Size(100, 20),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.Gray
        };
        Controls.Add(passwordLabel);

        // 关闭按钮（X）
        var closeBtn = new Label
        {
            Text = "×",
            Location = new Point(330, 50),
            Size = new Size(20, 20),
            Font = new Font("Microsoft YaHei UI", 12F),
            ForeColor = Color.Gray,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        closeBtn.Click += (s, e) => Close();
        closeBtn.MouseEnter += (s, e) => closeBtn.ForeColor = Color.FromArgb(180, 0, 0);
        closeBtn.MouseLeave += (s, e) => closeBtn.ForeColor = Color.Gray;
        Controls.Add(closeBtn);

        // 管理已保存的密码链接
        var manageLink = new LinkLabel
        {
            Text = "管理已保存的密码",
            Location = new Point(15, 110),
            AutoSize = true,
            LinkColor = Color.FromArgb(0, 102, 204),
            ActiveLinkColor = Color.FromArgb(0, 80, 160),
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        manageLink.LinkClicked += (s, e) =>
        {
            _isManagePasswordsClicked = true;
            NeverSave = false;
            ShouldSave = false;
            OnManagePasswordsClicked();
            Close();
        };
        Controls.Add(manageLink);

        // 完成按钮
        var doneBtn = new Button
        {
            Text = "完成",
            Location = new Point(265, 105),
            Size = new Size(75, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 51, 51),
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        doneBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        doneBtn.FlatAppearance.BorderSize = 1;
        doneBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
        doneBtn.Click += (s, e) => Close();
        Controls.Add(doneBtn);
    }

    protected virtual void OnManagePasswordsClicked()
    {
        ManagePasswordsClicked?.Invoke(this, EventArgs.Empty);
    }

    private void StartAutoCloseTimer()
    {
        _autoCloseTimer = new System.Windows.Forms.Timer { Interval = 20000 };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _autoCloseTimer?.Stop();
            if (!IsDisposed) Close();
        };
        _autoCloseTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _autoCloseTimer?.Stop();
        _autoCloseTimer?.Dispose();
        base.OnFormClosed(e);
    }
}
