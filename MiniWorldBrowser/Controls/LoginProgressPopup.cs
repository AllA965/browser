using System;
using System.Drawing;
using System.Windows.Forms;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 登录进度弹出窗口
/// </summary>
public class LoginProgressPopup : Form
{
    private readonly Action _onCancel;
    private readonly System.Windows.Forms.Timer _timeoutTimer;
    private int _remainingSeconds = 300;
    private Label _timerLabel = null!;
    private const int CornerRadius = 12;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    public LoginProgressPopup(Action onCancel)
    {
        _onCancel = onCancel;

        InitializeUI();

        _timeoutTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timeoutTimer.Tick += (s, e) =>
        {
            _remainingSeconds--;
            if (_remainingSeconds <= 0)
            {
                _timeoutTimer.Stop();
                _onCancel?.Invoke();
                Close();
            }
            else
            {
                _timerLabel.Text = $"正在等待网页登录... ({_remainingSeconds}s)";
            }
        };
        _timeoutTimer.Start();
    }

    private void InitializeUI()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = DpiHelper.Scale(new Size(360, 170));
        BackColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = DpiHelper.Scale(new Padding(18)) };

        var icon = new Label
        {
            Text = "⏳",
            Location = DpiHelper.Scale(new Point(18, 18)),
            Size = DpiHelper.Scale(new Size(36, 36)),
            Font = new Font("Segoe UI Symbol", DpiHelper.ScaleFont(18F)),
            ForeColor = Color.FromArgb(0, 120, 215),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var titleLabel = new Label
        {
            Text = "正在登录",
            Location = DpiHelper.Scale(new Point(62, 20)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(12F), FontStyle.Bold),
            ForeColor = Color.Black
        };

        _timerLabel = new Label
        {
            Text = $"正在等待网页登录... ({_remainingSeconds}s)",
            Location = DpiHelper.Scale(new Point(62, 48)),
            Size = DpiHelper.Scale(new Size(280, 20)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = Color.Gray
        };

        var progress = new ModernProgressBar
        {
            Location = DpiHelper.Scale(new Point(62, 74)),
            Size = DpiHelper.Scale(new Size(280, 10)),
            IsMarquee = true
        };

        var cancelBtn = new Button
        {
            Text = "取消登录",
            Size = DpiHelper.Scale(new Size(324, 38)),
            Location = DpiHelper.Scale(new Point(18, 112)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(248, 249, 250),
            ForeColor = Color.FromArgb(60, 60, 60),
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        cancelBtn.FlatAppearance.BorderSize = 1;
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(230, 230, 230);
        cancelBtn.MouseEnter += (s, e) => cancelBtn.BackColor = Color.FromArgb(240, 240, 240);
        cancelBtn.MouseLeave += (s, e) => cancelBtn.BackColor = Color.FromArgb(248, 249, 250);
        cancelBtn.Click += (s, e) =>
        {
            _timeoutTimer.Stop();
            _onCancel?.Invoke();
            Close();
        };

        ApplyRoundedRegion(cancelBtn, DpiHelper.Scale(10));

        mainPanel.Controls.Add(icon);
        mainPanel.Controls.Add(titleLabel);
        mainPanel.Controls.Add(_timerLabel);
        mainPanel.Controls.Add(progress);
        mainPanel.Controls.Add(cancelBtn);

        Controls.Add(mainPanel);

    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0) return;
        using var path = CreateRoundedRectPath(new Rectangle(0, 0, Width, Height), DpiHelper.Scale(CornerRadius));
        Region = new Region(path);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = CreateRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), DpiHelper.Scale(CornerRadius));
        using var pen = new Pen(Color.FromArgb(220, 220, 220), DpiHelper.Scale(1f));
        e.Graphics.DrawPath(pen, path);
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var path = CreateRoundedRectPath(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region = new Region(path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timeoutTimer?.Stop();
        _timeoutTimer?.Dispose();
        base.OnFormClosed(e);
    }
}
