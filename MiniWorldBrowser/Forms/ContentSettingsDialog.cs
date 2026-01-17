using MiniWorldBrowser.Services.Interfaces;

using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 内容设置对话框 - 参考世界之窗浏览器（单页滚动式）
/// </summary>
public class ContentSettingsDialog : Form
{
    private readonly ISettingsService _settingsService;
    private Panel _scrollPanel = null!;

    public ContentSettingsDialog(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        Text = "内容设置";
        Size = new Size(600, 550);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;

        // 滚动面板
        _scrollPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(580, 460),
            AutoScroll = true,
            Dock = DockStyle.None
        };

        // 完成按钮
        var btnDone = new Button
        {
            Text = "完成",
            Location = new Point(490, 470),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.OK
        };

        Controls.Add(_scrollPanel);
        Controls.Add(btnDone);

        // 构建内容
        BuildContent();
    }

    private void BuildContent()
    {
        var y = 15;

        // Cookie
        AddSectionTitle("Cookie", ref y);
        AddRadioButton("允许设置本地数据（推荐）", "cookie", true, ref y);
        AddRadioButton("仅将本地数据保留到您退出浏览器为止", "cookie", false, ref y);
        AddRadioButton("阻止网站设置任何数据", "cookie", false, ref y);
        AddCheckBox("阻止第三方 Cookie 和网站数据", false, ref y);
        y += 5;
        AddButtonRow(new[] { ("管理例外情况...", "cookie_exception"), ("所有 Cookie 和网站数据...", "cookie_data") }, ref y);

        // 图片
        AddSectionTitle("图片", ref y);
        AddRadioButton("显示所有图片（推荐）", "image", true, ref y);
        AddRadioButton("不显示任何图片", "image", false, ref y);
        AddButton("管理例外情况...", "image_exception", ref y);

        // JavaScript
        AddSectionTitle("JavaScript", ref y);
        AddRadioButton("允许所有网站运行 JavaScript（推荐）", "js", true, ref y);
        AddRadioButton("不允许任何网站运行 JavaScript", "js", false, ref y);
        AddButton("管理例外情况...", "js_exception", ref y);

        // 处理程序
        AddSectionTitle("处理程序", ref y);
        AddRadioButton("允许网站要求成为协议的默认处理程序（推荐）", "handler", true, ref y);
        AddRadioButton("不允许任何网站处理协议", "handler", false, ref y);
        AddButton("管理处理程序...", "handler_manage", ref y);

        // 插件
        AddSectionTitle("插件", ref y);
        AddRadioButton("检测并运行重要插件内容（推荐）", "plugin", false, ref y);
        AddRadioButton("运行所有插件内容", "plugin", true, ref y);
        AddRadioButton("让我自行选择何时运行插件内容", "plugin", false, ref y);
        AddButton("管理例外情况...", "plugin_exception", ref y);

        // 广告过滤
        AddSectionTitle("广告过滤", ref y);
        AddButton("管理例外情况...", "adblock_exception", ref y);

        // 位置
        AddSectionTitle("位置", ref y);
        AddRadioButton("允许所有网站跟踪您所在的位置", "location", false, ref y);
        AddRadioButton("当网站要跟踪您所在的位置时询问您（推荐）", "location", true, ref y);
        AddRadioButton("不允许任何网站跟踪您所在的位置", "location", false, ref y);
        AddButton("管理例外情况...", "location_exception", ref y);

        // 通知
        AddSectionTitle("通知", ref y);
        AddRadioButton("允许所有网站显示通知", "notification", false, ref y);
        AddRadioButton("当网站要显示通知时询问您（推荐）", "notification", true, ref y);
        AddRadioButton("不允许任何网站显示通知", "notification", false, ref y);
        AddButton("管理例外情况...", "notification_exception", ref y);

        // 全屏
        AddSectionTitle("全屏", ref y);
        AddButton("管理例外情况...", "fullscreen_exception", ref y);

        // 鼠标指针
        AddSectionTitle("鼠标指针", ref y);
        AddRadioButton("允许所有网站隐藏鼠标指针", "mouselock", false, ref y);
        AddRadioButton("当网站要隐藏鼠标指针时询问您（推荐）", "mouselock", true, ref y);
        AddRadioButton("不允许任何网站隐藏鼠标指针", "mouselock", false, ref y);
        AddButton("管理例外情况...", "mouselock_exception", ref y);

        // 受保护的内容
        AddSectionTitle("受保护的内容", ref y);
        AddDescription("有些内容服务会使用机器标识符来标识您的个人身份，以便授予您访问受保护内容的权限。", ref y);
        AddCheckBox("允许将标识符用于受保护内容（可能需要重新启动计算机）", true, ref y);

        // 麦克风
        AddSectionTitle("麦克风", ref y);
        AddRadioButton("当网站要求使用您的麦克风时询问您（推荐）", "mic", true, ref y);
        AddRadioButton("不允许网站使用您的麦克风", "mic", false, ref y);
        AddButton("管理例外情况...", "mic_exception", ref y);

        // 摄像头
        AddSectionTitle("摄像头", ref y);
        AddComboBox("screen-recorder-dev", ref y);
        AddRadioButton("当网站要求使用您的摄像头时询问您（推荐）", "camera", true, ref y);
        AddRadioButton("不允许网站使用您的摄像头", "camera", false, ref y);
        AddButton("管理例外情况...", "camera_exception", ref y);

        // 未经过沙盒屏蔽的插件访问
        AddSectionTitle("未经过沙盒屏蔽的插件访问", ref y);
        AddRadioButton("允许所有网站使用插件访问您的计算机", "sandbox", false, ref y);
        AddRadioButton("当网站要使用插件访问您的计算机时询问您（推荐）", "sandbox", true, ref y);
        AddRadioButton("不允许任何网站使用插件访问您的计算机", "sandbox", false, ref y);
        AddButton("管理例外情况...", "sandbox_exception", ref y);

        // 自动下载
        AddSectionTitle("自动下载", ref y);
        AddRadioButton("允许所有网站自动下载多个文件", "download", false, ref y);
        AddRadioButton("当网站下载第一个文件后要自动下载更多文件时询问您（推荐）", "download", true, ref y);
        AddRadioButton("禁止任何网站自动下载多个文件", "download", false, ref y);
        AddButton("管理例外情况...", "download_exception", ref y);

        // 完全控制 MIDI 设备
        AddSectionTitle("完全控制 MIDI 设备", ref y);
        AddRadioButton("允许所有网站使用系统专有消息来访问 MIDI 设备", "midi", false, ref y);
        AddRadioButton("在网站想要使用系统专有消息访问 MIDI 设备时询问我（推荐）", "midi", true, ref y);
        AddRadioButton("禁止任何网站使用系统专有消息访问 MIDI 设备", "midi", false, ref y);
        AddButton("管理例外情况...", "midi_exception", ref y);

        // 缩放级别
        AddSectionTitle("缩放级别", ref y);
        AddButton("管理...", "zoom_manage", ref y);

        y += 20;
        // 设置滚动面板的内容高度
        _scrollPanel.AutoScrollMinSize = new Size(0, y);
    }

    private void AddSectionTitle(string text, ref int y)
    {
        y += 10;
        var lbl = new Label
        {
            Text = text,
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold)
        };
        _scrollPanel.Controls.Add(lbl);
        y += 30;
    }

    private void AddDescription(string text, ref int y)
    {
        var lbl = new Label
        {
            Text = text,
            Location = new Point(35, y),
            Size = new Size(500, 40),
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.Gray
        };
        _scrollPanel.Controls.Add(lbl);
        y += 45;
    }

    private RadioButton AddRadioButton(string text, string group, bool isChecked, ref int y)
    {
        var rb = new RadioButton
        {
            Text = text,
            Location = new Point(35, y),
            AutoSize = true,
            Checked = isChecked,
            Font = new Font("Microsoft YaHei UI", 9),
            Tag = group
        };
        _scrollPanel.Controls.Add(rb);
        y += 26;
        return rb;
    }

    private CheckBox AddCheckBox(string text, bool isChecked, ref int y)
    {
        var cb = new CheckBox
        {
            Text = text,
            Location = new Point(35, y),
            AutoSize = true,
            Checked = isChecked,
            Font = new Font("Microsoft YaHei UI", 9)
        };
        _scrollPanel.Controls.Add(cb);
        y += 26;
        return cb;
    }

    private void AddComboBox(string defaultValue, ref int y)
    {
        var cbo = new ComboBox
        {
            Location = new Point(35, y),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 9)
        };
        cbo.Items.Add(defaultValue);
        cbo.SelectedIndex = 0;
        _scrollPanel.Controls.Add(cbo);
        y += 32;
    }

    private Button AddButton(string text, string action, ref int y)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(35, y),
            AutoSize = true,
            MinimumSize = new Size(100, 26),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9),
            Tag = action
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btn.Click += OnButtonClick;
        _scrollPanel.Controls.Add(btn);
        y += 35;
        return btn;
    }

    private void AddButtonRow(IEnumerable<(string text, string action)> buttons, ref int y)
    {
        var x = 35;
        foreach (var (text, action) in buttons)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                MinimumSize = new Size(100, 26),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9),
                Tag = action
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btn.Click += OnButtonClick;
            _scrollPanel.Controls.Add(btn);
            x += btn.PreferredSize.Width + 10;
        }
        y += 35;
    }

    private void OnButtonClick(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string action) return;

        switch (action)
        {
            case "cookie_exception":
            case "image_exception":
            case "js_exception":
            case "plugin_exception":
            case "location_exception":
            case "notification_exception":
            case "fullscreen_exception":
            case "mouselock_exception":
            case "mic_exception":
            case "camera_exception":
            case "sandbox_exception":
            case "download_exception":
            case "midi_exception":
                var category = action.Replace("_exception", "");
                using (var dialog = new CookieExceptionDialog(_settingsService, category))
                {
                    dialog.ShowDialog(this);
                }
                break;

            case "adblock_exception":
                using (var dialog = new AdBlockExceptionDialog(_settingsService))
                {
                    dialog.ShowDialog(this);
                }
                break;

            case "cookie_data":
                using (var dialog = new CookieDataDialog())
                {
                    dialog.ShowDialog(this);
                }
                break;

            case "handler_manage":
                MessageBox.Show("此功能暂未实现", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;

            case "zoom_manage":
                using (var dialog = new ZoomLevelDialog(_settingsService))
                {
                    dialog.ShowDialog(this);
                }
                break;
        }
    }
}
