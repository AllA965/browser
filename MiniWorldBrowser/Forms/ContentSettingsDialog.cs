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
    private readonly List<Control> _inputControls = new();

    public ContentSettingsDialog(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        Text = "内容设置";
        Size = DpiHelper.Scale(new Size(600, 650));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F));

        // 使用 TableLayoutPanel 确保布局稳定
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Color.White
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DpiHelper.Scale(55)));

        // 滚动面板
        _scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White,
            Padding = new Padding(0, 0, DpiHelper.Scale(10), DpiHelper.Scale(10))
        };
        
        // 底部按钮面板
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding = new Padding(0, 0, DpiHelper.Scale(15), 0)
        };

        // 完成按钮
        var btnDone = new Button
        {
            Text = "完成",
            Size = DpiHelper.Scale(new Size(85, 32)),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.OK,
            Top = DpiHelper.Scale(11),
            Left = bottomPanel.Width - DpiHelper.Scale(105)
        };
        btnDone.Click += (s, e) => SaveSettings();
        bottomPanel.Controls.Add(btnDone);

        mainLayout.Controls.Add(_scrollPanel, 0, 0);
        mainLayout.Controls.Add(bottomPanel, 0, 1);
        
        Controls.Add(mainLayout);

        // 构建内容
        BuildContent();
    }

    private void SaveSettings()
    {
        var settings = _settingsService.Settings;
        foreach (var control in _inputControls)
        {
            if (control is RadioButton rb && rb.Checked && rb.Tag is ValueTuple<string, int> radioTag)
            {
                var propName = radioTag.Item1;
                var value = radioTag.Item2;
                var prop = settings.GetType().GetProperty(propName);
                prop?.SetValue(settings, value);
            }
            else if (control is CheckBox cb && cb.Tag is string cbPropName)
            {
                var prop = settings.GetType().GetProperty(cbPropName);
                prop?.SetValue(settings, cb.Checked);
            }
        }
        _settingsService.Save();
    }

    private void BuildContent()
    {
        _scrollPanel.Controls.Clear();
        _inputControls.Clear();
        
        var flowLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(DpiHelper.Scale(15), DpiHelper.Scale(5), DpiHelper.Scale(10), DpiHelper.Scale(5)),
            BackColor = Color.White
        };
        _scrollPanel.Controls.Add(flowLayout);

        var settings = _settingsService.Settings;

        // Cookie
        AddSection(flowLayout, "Cookie", new Control[] {
            CreateRadioButton("允许设置本地数据（推荐）", "CookieSetting", 0, settings.CookieSetting == 0),
            CreateRadioButton("仅将本地数据保留到您退出浏览器为止", "CookieSetting", 1, settings.CookieSetting == 1),
            CreateRadioButton("阻止网站设置任何数据", "CookieSetting", 2, settings.CookieSetting == 2),
            CreateCheckBox("阻止第三方 Cookie 和网站数据", "BlockThirdPartyCookies", settings.BlockThirdPartyCookies)
        }, new[] {
            ("管理例外情况...", "cookie_exception"),
            ("所有 Cookie 和网站数据...", "cookie_data")
        });

        // 图片
        AddSection(flowLayout, "图片", new Control[] {
            CreateRadioButton("显示所有图片（推荐）", "ImageSetting", 0, settings.ImageSetting == 0),
            CreateRadioButton("不显示任何图片", "ImageSetting", 1, settings.ImageSetting == 1)
        }, new[] { ("管理例外情况...", "image_exception") });

        // JavaScript
        AddSection(flowLayout, "JavaScript", new Control[] {
            CreateRadioButton("允许所有网站运行 JavaScript（推荐）", "JavaScriptSetting", 0, settings.JavaScriptSetting == 0),
            CreateRadioButton("不允许任何网站运行 JavaScript", "JavaScriptSetting", 1, settings.JavaScriptSetting == 1)
        }, new[] { ("管理例外情况...", "js_exception") });

        // 处理程序
        AddSection(flowLayout, "处理程序", new Control[] {
            CreateRadioButton("允许网站要求成为协议的默认处理程序（推荐）", "HandlerSetting", 0, settings.HandlerSetting == 0),
            CreateRadioButton("不允许任何网站处理协议", "HandlerSetting", 1, settings.HandlerSetting == 1)
        }, new[] { ("管理处理程序...", "handler_manage") });

        // 插件
        AddSection(flowLayout, "插件", new Control[] {
            CreateRadioButton("检测并运行重要插件内容（推荐）", "PluginSetting", 0, settings.PluginSetting == 0),
            CreateRadioButton("运行所有插件内容", "PluginSetting", 1, settings.PluginSetting == 1),
            CreateRadioButton("让我自行选择何时运行插件内容", "PluginSetting", 2, settings.PluginSetting == 2)
        }, new[] { ("管理例外情况...", "plugin_exception") });

        // 广告过滤
        AddSection(flowLayout, "广告过滤", null, new[] { ("管理例外情况...", "adblock_exception") });

        // 位置
        AddSection(flowLayout, "位置", new Control[] {
            CreateRadioButton("允许所有网站跟踪您所在的位置", "LocationSetting", 0, settings.LocationSetting == 0),
            CreateRadioButton("当网站要跟踪您所在的位置时询问您（推荐）", "LocationSetting", 1, settings.LocationSetting == 1),
            CreateRadioButton("不允许任何网站跟踪您所在的位置", "LocationSetting", 2, settings.LocationSetting == 2)
        }, new[] { ("管理例外情况...", "location_exception") });

        // 通知
        AddSection(flowLayout, "通知", new Control[] {
            CreateRadioButton("允许所有网站显示通知", "NotificationSetting", 0, settings.NotificationSetting == 0),
            CreateRadioButton("当网站要显示通知时询问您（推荐）", "NotificationSetting", 1, settings.NotificationSetting == 1),
            CreateRadioButton("不允许任何网站显示通知", "NotificationSetting", 2, settings.NotificationSetting == 2)
        }, new[] { ("管理例外情况...", "notification_exception") });

        // 全屏
        AddSection(flowLayout, "全屏", null, new[] { ("管理例外情况...", "fullscreen_exception") });

        // 鼠标指针
        AddSection(flowLayout, "鼠标指针", new Control[] {
            CreateRadioButton("允许所有网站隐藏鼠标指针", "MouseLockSetting", 0, settings.MouseLockSetting == 0),
            CreateRadioButton("当网站要隐藏鼠标指针时询问您（推荐）", "MouseLockSetting", 1, settings.MouseLockSetting == 1),
            CreateRadioButton("不允许任何网站隐藏鼠标指针", "MouseLockSetting", 2, settings.MouseLockSetting == 2)
        }, new[] { ("管理例外情况...", "mouselock_exception") });

        // 受保护的内容
        AddSection(flowLayout, "受保护的内容", new Control[] {
            CreateDescription("有些内容服务会使用机器标识符来标识您的个人身份，以便授予您访问受保护内容的权限。"),
            CreateCheckBox("允许将标识符用于受保护内容（可能需要重新启动计算机）", "ProtectedContentSetting", settings.ProtectedContentSetting)
        }, null);

        // 麦克风
        AddSection(flowLayout, "麦克风", new Control[] {
            CreateRadioButton("当网站要求使用您的麦克风时询问您（推荐）", "MicSetting", 0, settings.MicSetting == 0),
            CreateRadioButton("不允许网站使用您的麦克风", "MicSetting", 1, settings.MicSetting == 1)
        }, new[] { ("管理例外情况...", "mic_exception") });

        // 摄像头
        AddSection(flowLayout, "摄像头", new Control[] {
            CreateComboBox("screen-recorder-dev"),
            CreateRadioButton("当网站要求使用您的摄像头时询问您（推荐）", "CameraSetting", 0, settings.CameraSetting == 0),
            CreateRadioButton("不允许网站使用您的摄像头", "CameraSetting", 1, settings.CameraSetting == 1)
        }, new[] { ("管理例外情况...", "camera_exception") });

        // 未经过沙盒屏蔽的插件访问
        AddSection(flowLayout, "未经过沙盒屏蔽的插件访问", new Control[] {
            CreateRadioButton("允许所有网站使用插件访问您的计算机", "UnsandboxedPluginSetting", 0, settings.UnsandboxedPluginSetting == 0),
            CreateRadioButton("当网站要使用插件访问您的计算机时询问您（推荐）", "UnsandboxedPluginSetting", 1, settings.UnsandboxedPluginSetting == 1),
            CreateRadioButton("不允许任何网站使用插件访问您的计算机", "UnsandboxedPluginSetting", 2, settings.UnsandboxedPluginSetting == 2)
        }, new[] { ("管理例外情况...", "sandbox_exception") });

        // 自动下载
        AddSection(flowLayout, "自动下载", new Control[] {
            CreateRadioButton("允许所有网站自动下载多个文件", "AutomaticDownloadSetting", 0, settings.AutomaticDownloadSetting == 0),
            CreateRadioButton("当网站下载第一个文件后要自动下载更多文件时询问您（推荐）", "AutomaticDownloadSetting", 1, settings.AutomaticDownloadSetting == 1),
            CreateRadioButton("禁止任何网站自动下载多个文件", "AutomaticDownloadSetting", 2, settings.AutomaticDownloadSetting == 2)
        }, new[] { ("管理例外情况...", "download_exception") });

        // 完全控制 MIDI 设备
        AddSection(flowLayout, "完全控制 MIDI 设备", new Control[] {
            CreateRadioButton("允许所有网站使用系统专有消息来访问 MIDI 设备", "MidiSetting", 0, settings.MidiSetting == 0),
            CreateRadioButton("在网站想要使用系统专有消息访问 MIDI 设备时询问我（推荐）", "MidiSetting", 1, settings.MidiSetting == 1),
            CreateRadioButton("禁止任何网站使用系统专有消息访问 MIDI 设备", "MidiSetting", 2, settings.MidiSetting == 2)
        }, new[] { ("管理例外情况...", "midi_exception") });

        // 缩放级别
        AddSection(flowLayout, "缩放级别", null, new[] { ("管理...", "zoom_manage") });
    }

    private void AddSection(Control parent, string title, Control[]? contentControls, (string text, string action)[]? buttons)
    {
        var sectionPanel = new Panel
        {
            Width = DpiHelper.Scale(540),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, DpiHelper.Scale(8)),
            BackColor = Color.White
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(10F), FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 32, 32),
            AutoSize = true,
            Location = new Point(0, 0),
            Margin = Padding.Empty
        };
        sectionPanel.Controls.Add(lblTitle);

        var currentY = lblTitle.Bottom + DpiHelper.Scale(2);

        if (contentControls != null)
        {
            var contentPanel = new Panel
            {
                Location = new Point(DpiHelper.Scale(12), currentY),
                Width = sectionPanel.Width - DpiHelper.Scale(20),
                AutoSize = true,
                Margin = Padding.Empty
            };

            int innerY = 0;
            foreach (var ctrl in contentControls)
            {
                ctrl.Location = new Point(0, innerY);
                contentPanel.Controls.Add(ctrl);
                innerY += ctrl.Height;
            }
            sectionPanel.Controls.Add(contentPanel);
            currentY = contentPanel.Bottom;
        }

        if (buttons != null && buttons.Length > 0)
        {
            var btnPanel = new FlowLayoutPanel
            {
                Location = new Point(DpiHelper.Scale(12), currentY),
                Width = sectionPanel.Width - DpiHelper.Scale(20),
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            foreach (var (text, action) in buttons)
            {
                var link = new LinkLabel
                {
                    Text = text,
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
                    LinkColor = Color.FromArgb(0, 120, 212),
                    ActiveLinkColor = Color.FromArgb(0, 102, 204),
                    VisitedLinkColor = Color.FromArgb(0, 120, 212),
                    LinkBehavior = LinkBehavior.HoverUnderline,
                    Margin = new Padding(0, DpiHelper.Scale(1), DpiHelper.Scale(15), DpiHelper.Scale(1)),
                    Tag = action
                };
                link.Click += OnButtonClick;
                btnPanel.Controls.Add(link);
            }
            sectionPanel.Controls.Add(btnPanel);
        }

        parent.Controls.Add(sectionPanel);
    }

    private RadioButton CreateRadioButton(string text, string propertyName, int value, bool isChecked)
    {
        var rb = new RadioButton
        {
            Text = text,
            AutoSize = true,
            Checked = isChecked,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9.5F)),
            Tag = (propertyName, value),
            ForeColor = Color.FromArgb(64, 64, 64),
            Margin = new Padding(0, 0, 0, 0),
            Padding = new Padding(0, DpiHelper.Scale(1), 0, DpiHelper.Scale(1))
        };
        _inputControls.Add(rb);
        return rb;
    }

    private CheckBox CreateCheckBox(string text, string propertyName, bool isChecked)
    {
        var cb = new CheckBox
        {
            Text = text,
            AutoSize = true,
            Checked = isChecked,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9.5F)),
            Tag = propertyName,
            ForeColor = Color.FromArgb(64, 64, 64),
            Margin = new Padding(0, 0, 0, 0),
            Padding = new Padding(0, DpiHelper.Scale(1), 0, DpiHelper.Scale(1))
        };
        _inputControls.Add(cb);
        return cb;
    }

    private Label CreateDescription(string text)
    {
        return new Label
        {
            Text = text,
            Width = DpiHelper.Scale(500),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            ForeColor = Color.Gray,
            Margin = new Padding(0, 0, 0, DpiHelper.Scale(2))
        };
    }

    private ComboBox CreateComboBox(string defaultValue)
    {
        var cbo = new ComboBox
        {
            Size = new Size(DpiHelper.Scale(200), DpiHelper.Scale(25)),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F))
        };
        cbo.Items.Add(defaultValue);
        cbo.SelectedIndex = 0;
        return cbo;
    }

    private void AddSectionTitle(string text, ref int y) { }
    private void AddDescription(string text, ref int y) { }
    private RadioButton AddRadioButton(string text, string propertyName, int value, bool isChecked, ref int y) { return null!; }
    private CheckBox AddCheckBox(string text, string propertyName, bool isChecked, ref int y) { return null!; }
    private void AddComboBox(string defaultValue, ref int y) { }
    private Button AddButton(string text, string action, ref int y) { return null!; }
    private void AddButtonRow(IEnumerable<(string text, string action)> buttons, ref int y) { }

    private void OnButtonClick(object? sender, EventArgs e)
    {
        string? action = null;
        if (sender is Button btn) action = btn.Tag as string;
        else if (sender is LinkLabel link) action = link.Tag as string;

        if (string.IsNullOrEmpty(action)) return;

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
