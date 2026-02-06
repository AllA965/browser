using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Services;

namespace MiniWorldBrowser.Forms;

public partial class AboutForm : Form
{
    private readonly UpdateService _updateService;
    private Button _btnCheckUpdate;
    private Label _lblStatus;

    public AboutForm()
    {
        _updateService = new UpdateService();
        InitializeComponent();
    }

    private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "关于 " + AppConstants.AppName;
            this.Size = DpiHelper.Scale(new Size(420, 360));
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White; // 统一背景颜色为白色，消除分层

            // Logo
            var pbLogo = new PictureBox();
            try {
                string iconPath = @"C:\Users\admin\Desktop\ff\鲲穹AI浏览器源码\鲲穹AI浏览器.ico";
                if (File.Exists(iconPath)) {
                    pbLogo.Image = new Icon(iconPath, 128, 128).ToBitmap();
                } else {
                    pbLogo.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap();
                }
            } catch {
                try { pbLogo.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap(); } catch {}
            }
            pbLogo.SizeMode = PictureBoxSizeMode.Zoom;
            pbLogo.Size = DpiHelper.Scale(new Size(80, 80));
            pbLogo.Location = DpiHelper.Scale(new Point((420 - 80) / 2, 40));
            this.Controls.Add(pbLogo);

            // App Name
            var lblName = new Label();
            lblName.Text = AppConstants.AppName;
            lblName.Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(18), FontStyle.Bold);
            lblName.AutoSize = false;
            lblName.TextAlign = ContentAlignment.MiddleCenter;
            lblName.Size = DpiHelper.Scale(new Size(400, 40));
            lblName.Location = DpiHelper.Scale(new Point(10, 130));
            this.Controls.Add(lblName);

            // Version
            var lblVersion = new Label();
            lblVersion.Text = "版本 " + AppConstants.AppVersion;
            lblVersion.Font = new Font("Segoe UI", DpiHelper.ScaleFont(11));
            lblVersion.ForeColor = Color.Gray;
            lblVersion.AutoSize = false;
            lblVersion.TextAlign = ContentAlignment.MiddleCenter;
            lblVersion.Size = DpiHelper.Scale(new Size(400, 25));
            lblVersion.Location = DpiHelper.Scale(new Point(10, 170));
            this.Controls.Add(lblVersion);

            // Check Update Button
            _btnCheckUpdate = new Button();
            _btnCheckUpdate.Text = "检查更新";
            _btnCheckUpdate.Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10));
            _btnCheckUpdate.Size = DpiHelper.Scale(new Size(120, 38));
            _btnCheckUpdate.Location = DpiHelper.Scale(new Point((420 - 120) / 2, 220));
            _btnCheckUpdate.FlatStyle = FlatStyle.Flat;
            _btnCheckUpdate.FlatAppearance.BorderSize = 1;
            _btnCheckUpdate.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 215);
            _btnCheckUpdate.BackColor = Color.White;
            _btnCheckUpdate.ForeColor = Color.FromArgb(0, 120, 215);
            _btnCheckUpdate.Cursor = Cursors.Hand;
            _btnCheckUpdate.Click += BtnCheckUpdate_Click;
            this.Controls.Add(_btnCheckUpdate);

            // Status Label
            _lblStatus = new Label();
            _lblStatus.Text = "";
            _lblStatus.Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9));
            _lblStatus.ForeColor = Color.FromArgb(100, 100, 100);
            _lblStatus.AutoSize = false;
            _lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            _lblStatus.Size = DpiHelper.Scale(new Size(400, 20));
            _lblStatus.Location = DpiHelper.Scale(new Point(10, 265));
            this.Controls.Add(_lblStatus);

            // Copyright
            var lblCopyright = new Label();
            lblCopyright.Text = "© 2026 鲲穹AI. All rights reserved.";
            lblCopyright.Font = new Font("Segoe UI", DpiHelper.ScaleFont(8.5f));
            lblCopyright.ForeColor = Color.DarkGray;
            lblCopyright.AutoSize = false;
            lblCopyright.TextAlign = ContentAlignment.MiddleCenter;
            lblCopyright.Size = DpiHelper.Scale(new Size(400, 20));
            lblCopyright.Location = DpiHelper.Scale(new Point(10, 300));
            this.Controls.Add(lblCopyright);

            this.ResumeLayout(false);
        }

    private async void BtnCheckUpdate_Click(object? sender, EventArgs e)
    {
        _btnCheckUpdate.Enabled = false;
        _btnCheckUpdate.Text = "检查中...";
        _lblStatus.Text = "正在连接更新服务器...";
        _lblStatus.ForeColor = Color.Blue;

        try
        {
            var info = await _updateService.CheckForUpdatesAsync();
            if (info == null)
            {
                _lblStatus.Text = "检查更新失败，请稍后重试。";
                _lblStatus.ForeColor = Color.Red;
                _btnCheckUpdate.Text = "检查更新";
                _btnCheckUpdate.Enabled = true;
                return;
            }

            if (info.HasUpdate)
            {
                _lblStatus.Text = $"发现新版本: {info.Version}";
                _lblStatus.ForeColor = Color.Green;
                
                var result = MessageBox.Show(
                    $"发现新版本 {info.Version}\n\n更新内容:\n{info.UpdateLog}\n\n是否立即更新？", 
                    "发现更新", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    _updateService.StartUpdate(info);
                }
            }
            else
            {
                _lblStatus.Text = "当前已是最新版本。";
                _lblStatus.ForeColor = Color.Green;
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "发生错误: " + ex.Message;
            _lblStatus.ForeColor = Color.Red;
        }
        finally
        {
            _btnCheckUpdate.Text = "检查更新";
            _btnCheckUpdate.Enabled = true;
        }
    }
}
