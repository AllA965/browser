using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 网站内容设置窗口 - 参考"世界之窗"浏览器的内容设置面板
/// 允许用户设置各种网站权限
/// </summary>
public class SiteSettingsForm : Form
{
    private readonly string _host;
    private Panel _contentPanel = null!;
    
    // 权限设置存储
    private readonly Dictionary<string, int> _permissions = new();
    
    public SiteSettingsForm(string url)
    {
        _host = GetHost(url);
        InitializeUI();
        LoadDefaultPermissions();
    }
    
    private static string GetHost(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return "所有网站";
            if (url.StartsWith("about:")) return url;
            var uri = new Uri(url);
            return uri.Host;
        }
        catch { return "所有网站"; }
    }
    
    private void InitializeUI()
    {
        AppIconHelper.SetIcon(this);
        Text = "内容设置";
        Size = DpiHelper.Scale(new Size(450, 650));
        MinimumSize = DpiHelper.Scale(new Size(400, 500));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F));
        
        // 关闭按钮
        var closeBtn = new Button
        {
            Text = "×",
            Size = DpiHelper.Scale(new Size(30, 30)),
            Location = new Point(Width - DpiHelper.Scale(45), DpiHelper.Scale(5)),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", DpiHelper.Scale(12F)),
            ForeColor = Color.Gray,
            Cursor = Cursors.Hand
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.Click += (s, e) => Close();
        
        // 滚动面板
        _contentPanel = new Panel
        {
            Location = DpiHelper.Scale(new Point(10, 10)),
            Size = new Size(Width - DpiHelper.Scale(40), Height - DpiHelper.Scale(80)),
            AutoScroll = true,
            BackColor = Color.White
        };
        
        CreateSettingsContent();
        
        // 底部按钮
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = DpiHelper.Scale(50),
            BackColor = Color.FromArgb(245, 245, 245)
        };
        
        var okBtn = new Button
        {
            Text = "确定",
            Size = DpiHelper.Scale(new Size(80, 30)),
            Location = new Point(Width - DpiHelper.Scale(200), DpiHelper.Scale(10)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        okBtn.FlatAppearance.BorderSize = 0;
        okBtn.Click += (s, e) => { SaveSettings(); Close(); };
        
        var cancelBtn = new Button
        {
            Text = "取消",
            Size = DpiHelper.Scale(new Size(80, 30)),
            Location = new Point(Width - DpiHelper.Scale(105), DpiHelper.Scale(10)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.Black,
            Cursor = Cursors.Hand
        };
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        cancelBtn.Click += (s, e) => Close();
        
        bottomPanel.Controls.Add(okBtn);
        bottomPanel.Controls.Add(cancelBtn);
        
        Controls.Add(closeBtn);
        Controls.Add(_contentPanel);
        Controls.Add(bottomPanel);
    }

    private void LoadDefaultPermissions()
    {
        _permissions["cookie"] = 0;      // 允许设置本地数据（推荐）
        _permissions["image"] = 0;       // 显示所有图片（推荐）
        _permissions["javascript"] = 0;  // 允许所有网站运行 JavaScript（推荐）
        _permissions["handler"] = 1;     // 允许网站要求成为协议默认处理程序（推荐）
        _permissions["popup"] = 1;       // 不允许任何网站显示弹出式窗口（推荐）
        _permissions["location"] = 1;    // 当网站要求跟踪我的地理位置时询问（推荐）
        _permissions["notification"] = 1; // 当网站要求显示桌面通知时询问（推荐）
        _permissions["fullscreen"] = 0;  // 允许所有网站进入全屏模式
        _permissions["mouselock"] = 1;   // 当网站要求锁定鼠标时询问
        _permissions["camera"] = 1;      // 当网站要求使用摄像头时询问
        _permissions["microphone"] = 1;  // 当网站要求使用麦克风时询问
        _permissions["midi"] = 1;        // 当网站要求使用 MIDI 设备时询问
        _permissions["usb"] = 1;         // 当网站要求使用 USB 设备时询问
        _permissions["download"] = 0;    // 允许所有网站自动下载多个文件
    }
    
    private void CreateSettingsContent()
    {
        int y = 0;
        
        // Cookie
        y = AddSettingSection("Cookie", y, new[]
        {
            ("允许设置本地数据（推荐）", 0),
            ("仅将本地数据保留到退出浏览器为止", 1),
            ("阻止网站设置任何数据", 2)
        }, "cookie", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("Cookie"))),
            ("所有 Cookie 和网站数据...", (Action)(() => ShowAllCookies()))
        });
        
        // 图片
        y = AddSettingSection("图片", y, new[]
        {
            ("显示所有图片（推荐）", 0),
            ("不显示任何图片", 1)
        }, "image", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("图片")))
        });
        
        // JavaScript
        y = AddSettingSection("JavaScript", y, new[]
        {
            ("允许所有网站运行 JavaScript（推荐）", 0),
            ("不允许任何网站运行 JavaScript", 1)
        }, "javascript", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("JavaScript")))
        });
        
        // 处理程序
        y = AddSettingSection("处理程序", y, new[]
        {
            ("允许网站要求成为协议默认处理程序（推荐）", 0),
            ("不允许任何网站处理协议", 1)
        }, "handler", new[]
        {
            ("管理处理程序...", (Action)(() => ShowHandlers()))
        });
        
        // 弹出式窗口
        y = AddSettingSection("弹出式窗口", y, new[]
        {
            ("允许所有网站显示弹出式窗口", 0),
            ("不允许任何网站显示弹出式窗口（推荐）", 1)
        }, "popup", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("弹出式窗口")))
        });
        
        // 位置
        y = AddSettingSection("位置", y, new[]
        {
            ("允许所有网站跟踪我的地理位置", 0),
            ("当网站要求跟踪我的地理位置时询问（推荐）", 1),
            ("不允许任何网站跟踪我的地理位置", 2)
        }, "location", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("位置")))
        });
        
        // 通知
        y = AddSettingSection("通知", y, new[]
        {
            ("允许所有网站显示桌面通知", 0),
            ("当网站要求显示桌面通知时询问（推荐）", 1),
            ("不允许任何网站显示桌面通知", 2)
        }, "notification", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("通知")))
        });
        
        // 全屏下载
        y = AddSettingSection("自动下载", y, new[]
        {
            ("允许所有网站自动下载多个文件", 0),
            ("当网站在自动下载第一个文件后尝试自动下载文件时询问（推荐）", 1),
            ("不允许任何网站自动下载多个文件", 2)
        }, "download", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("自动下载")))
        });
        
        // 常见的 MIDI 设备
        y = AddSettingSection("常见的 MIDI 设备", y, new[]
        {
            ("允许所有网站使用系统专有消息访问常见的 MIDI 设备", 0),
            ("当网站要求使用系统专有消息访问常见的 MIDI 设备时询问", 1),
            ("禁止任何网站使用系统专有消息访问常见的 MIDI 设备", 2)
        }, "midi", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("MIDI 设备")))
        });
        
        // 摄像头
        y = AddSettingSection("摄像头", y, new[]
        {
            ("允许所有网站使用摄像头", 0),
            ("当网站要求使用摄像头时询问（推荐）", 1),
            ("不允许任何网站使用摄像头", 2)
        }, "camera", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("摄像头")))
        });
        
        // 麦克风
        y = AddSettingSection("麦克风", y, new[]
        {
            ("允许所有网站使用麦克风", 0),
            ("当网站要求使用麦克风时询问（推荐）", 1),
            ("不允许任何网站使用麦克风", 2)
        }, "microphone", new[]
        {
            ("管理例外情况...", (Action)(() => ShowExceptions("麦克风")))
        });
    }

    private int AddSettingSection(string title, int y, (string text, int value)[] options, 
        string key, (string text, Action action)[]? buttons = null)
    {
        // 标题
        var titleLabel = new Label
        {
            Text = title,
            Location = new Point(DpiHelper.Scale(5), y + DpiHelper.Scale(10)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(10F), FontStyle.Bold),
            ForeColor = Color.Black
        };
        _contentPanel.Controls.Add(titleLabel);
        y += DpiHelper.Scale(35);
        
        // 选项
        var currentValue = _permissions.GetValueOrDefault(key, 0);
        foreach (var (text, value) in options)
        {
            var radio = new RadioButton
            {
                Text = text,
                Location = new Point(DpiHelper.Scale(15), y),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
                Checked = currentValue == value,
                Tag = (key, value)
            };
            radio.CheckedChanged += (s, e) =>
            {
                if (radio.Checked && radio.Tag is (string k, int v))
                    _permissions[k] = v;
            };
            _contentPanel.Controls.Add(radio);
            y += DpiHelper.Scale(24);
        }
        
        // 按钮
        if (buttons != null)
        {
            y += DpiHelper.Scale(5);
            foreach (var (text, action) in buttons)
            {
                var btn = new LinkLabel
                {
                    Text = text,
                    Location = new Point(DpiHelper.Scale(15), y),
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
                    LinkColor = Color.FromArgb(0, 102, 204)
                };
                btn.Click += (s, e) => action();
                _contentPanel.Controls.Add(btn);
                y += DpiHelper.Scale(22);
            }
        }
        
        // 分隔线
        y += DpiHelper.Scale(10);
        var separator = new Panel
        {
            Location = new Point(DpiHelper.Scale(5), y),
            Size = new Size(_contentPanel.Width - DpiHelper.Scale(30), 1), // 1px height usually fine, but can scale
            BackColor = Color.FromArgb(230, 230, 230)
        };
        _contentPanel.Controls.Add(separator);
        y += DpiHelper.Scale(15);
        
        return y;
    }
    
    private void ShowExceptions(string category)
    {
        using var dlg = new ExceptionsDialog(category);
        dlg.ShowDialog(this);
    }
    
    private void ShowAllCookies()
    {
        MessageBox.Show(
            "Cookie 和网站数据管理功能\n\n" +
            "此功能允许您查看和删除网站存储的 Cookie 和其他数据。\n\n" +
            "提示：清除 Cookie 可能会导致您从某些网站注销。",
            "Cookie 和网站数据",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
    
    private void ShowHandlers()
    {
        MessageBox.Show(
            "协议处理程序管理\n\n" +
            "此功能允许您管理哪些网站可以处理特定的协议（如 mailto:、tel: 等）。",
            "协议处理程序",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
    
    private void SaveSettings()
    {
        // 这里可以保存设置到配置文件
        // 目前只是显示一个确认消息
    }
}

/// <summary>
/// 例外情况管理对话框
/// </summary>
public class ExceptionsDialog : Form
{
    private readonly string _category;
    private ListView _listView = null!;
    
    public ExceptionsDialog(string category)
    {
        _category = category;
        InitializeUI();
    }
    
    private void InitializeUI()
    {
        Text = $"{_category} 例外情况";
        Size = DpiHelper.Scale(new Size(500, 400));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F));
        
        // 说明标签
        var descLabel = new Label
        {
            Text = $"您可以为特定网站设置 {_category} 权限。",
            Location = DpiHelper.Scale(new Point(15, 15)),
            AutoSize = true
        };
        
        // 添加区域
        var addPanel = new Panel
        {
            Location = DpiHelper.Scale(new Point(15, 45)),
            Size = new Size(Width - DpiHelper.Scale(45), DpiHelper.Scale(30)),
            BackColor = Color.White
        };
        
        var patternLabel = new Label
        {
            Text = "主机名模式:",
            Location = new Point(0, DpiHelper.Scale(5)),
            AutoSize = true
        };
        
        var patternBox = new TextBox
        {
            Location = new Point(DpiHelper.Scale(80), DpiHelper.Scale(2)),
            Size = DpiHelper.Scale(new Size(200, 24)),
            BorderStyle = BorderStyle.FixedSingle
        };
        
        var behaviorCombo = new ComboBox
        {
            Location = new Point(DpiHelper.Scale(290), DpiHelper.Scale(2)),
            Size = DpiHelper.Scale(new Size(80, 24)),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        behaviorCombo.Items.AddRange(new[] { "允许", "阻止", "询问" });
        behaviorCombo.SelectedIndex = 0;
        
        var addBtn = new Button
        {
            Text = "添加",
            Location = new Point(DpiHelper.Scale(380), DpiHelper.Scale(1)),
            Size = DpiHelper.Scale(new Size(60, 26)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White
        };
        addBtn.FlatAppearance.BorderSize = 0;
        addBtn.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(patternBox.Text))
            {
                _listView.Items.Add(new ListViewItem(new[] { patternBox.Text, behaviorCombo.Text }));
                patternBox.Clear();
            }
        };
        
        addPanel.Controls.AddRange(new Control[] { patternLabel, patternBox, behaviorCombo, addBtn });
        
        // 列表视图
        _listView = new ListView
        {
            Location = DpiHelper.Scale(new Point(15, 85)),
            Size = new Size(Width - DpiHelper.Scale(45), Height - DpiHelper.Scale(180)),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _listView.Columns.Add("主机名模式", DpiHelper.Scale(280));
        _listView.Columns.Add("行为", DpiHelper.Scale(150));
        
        // 删除按钮
        var removeBtn = new Button
        {
            Text = "删除",
            Location = new Point(DpiHelper.Scale(15), Height - DpiHelper.Scale(85)),
            Size = DpiHelper.Scale(new Size(80, 28)),
            FlatStyle = FlatStyle.Flat
        };
        removeBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        removeBtn.Click += (s, e) =>
        {
            foreach (ListViewItem item in _listView.SelectedItems)
                _listView.Items.Remove(item);
        };
        
        var removeAllBtn = new Button
        {
            Text = "全部删除",
            Location = new Point(DpiHelper.Scale(105), Height - DpiHelper.Scale(85)),
            Size = DpiHelper.Scale(new Size(80, 28)),
            FlatStyle = FlatStyle.Flat
        };
        removeAllBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        removeAllBtn.Click += (s, e) => _listView.Items.Clear();
        
        // 底部按钮
        var doneBtn = new Button
        {
            Text = "完成",
            Location = new Point(Width - DpiHelper.Scale(100), Height - DpiHelper.Scale(85)),
            Size = DpiHelper.Scale(new Size(70, 28)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            DialogResult = DialogResult.OK
        };
        doneBtn.FlatAppearance.BorderSize = 0;
        
        Controls.AddRange(new Control[] { descLabel, addPanel, _listView, removeBtn, removeAllBtn, doneBtn });
    }
}
