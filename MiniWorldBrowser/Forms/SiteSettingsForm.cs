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
        Text = Localization.Raw();
        Size = DpiHelper.Scale(new Size(480, 680));
        MinimumSize = DpiHelper.Scale(new Size(400, 500));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10F));

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
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White,
            Padding = DpiHelper.Scale(new Padding(10))
        };
        
        // 底部按钮面板
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding = DpiHelper.Scale(new Padding(0, 0, 15, 0))
        };

        // 确定按钮
        var okBtn = new Button
        {
            Text = Localization.T("confirm.ok"),
            Size = DpiHelper.Scale(new Size(85, 32)),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Top = DpiHelper.Scale(11),
            Left = DpiHelper.Scale(280) // 480 - 15 - 85 - 15 - 85 = 280
        };
        okBtn.FlatAppearance.BorderSize = 0;
        okBtn.Click += (s, e) => { SaveSettings(); Close(); };
        
        // 取消按钮
        var cancelBtn = new Button
        {
            Text = Localization.T("confirm.cancel"),
            Size = DpiHelper.Scale(new Size(85, 32)),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.Black,
            Cursor = Cursors.Hand,
            Top = DpiHelper.Scale(11),
            Left = DpiHelper.Scale(380) // 480 - 15 - 85 = 380
        };
        cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        cancelBtn.Click += (s, e) => Close();

        bottomPanel.Controls.Add(okBtn);
        bottomPanel.Controls.Add(cancelBtn);

        mainLayout.Controls.Add(_contentPanel, 0, 0);
        mainLayout.Controls.Add(bottomPanel, 0, 1);
        
        Controls.Add(mainLayout);

        CreateSettingsContent();
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
        _contentPanel.Controls.Clear();
        
        // 使用 FlowLayoutPanel 代替手动计算坐标
        var flowLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(DpiHelper.Scale(15), DpiHelper.Scale(5), DpiHelper.Scale(10), DpiHelper.Scale(5)),
            BackColor = Color.White
        };
        _contentPanel.Controls.Add(flowLayout);

        // 绑定数据到 UI
        AddSection(flowLayout, Localization.Raw(), "cookie", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1),
            (Localization.Raw(), 2)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("Cookie"))),
            (Localization.Raw(), (Action)(() => ShowAllCookies()))
        });

        AddSection(flowLayout, Localization.Raw(), "image", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("图片")))
        });

        AddSection(flowLayout, Localization.Raw(), "javascript", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("JavaScript")))
        });

        AddSection(flowLayout, Localization.Raw(), "handler", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowHandlers()))
        });

        AddSection(flowLayout, Localization.Raw(), "popup", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("弹出式窗口")))
        });

        AddSection(flowLayout, Localization.Raw(), "location", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1),
            (Localization.Raw(), 2)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("位置")))
        });

        AddSection(flowLayout, Localization.Raw(), "notification", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1),
            (Localization.Raw(), 2)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("通知")))
        });

        AddSection(flowLayout, Localization.Raw(), "download", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1),
            (Localization.Raw(), 2)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("自动下载")))
        });

        AddSection(flowLayout, Localization.Raw(), "midi", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1),
            (Localization.Raw(), 2)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("MIDI 设备")))
        });

        AddSection(flowLayout, Localization.Raw(), "camera", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1),
            (Localization.Raw(), 2)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("摄像头")))
        });

        AddSection(flowLayout, Localization.Raw(), "microphone", new[]
        {
            (Localization.Raw(), 0),
            (Localization.Raw(), 1),
            (Localization.Raw(), 2)
        }, new[]
        {
            (Localization.Raw(), (Action)(() => ShowExceptions("麦克风")))
        });
    }

    private void AddSection(Control parent, string title, string key, (string text, int value)[] options, (string text, Action action)[]? buttons)
    {
        var sectionPanel = new Panel
        {
            Width = DpiHelper.Scale(420),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, DpiHelper.Scale(8)),
            BackColor = Color.White
        };

        // 标题
        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10F), FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 32, 32),
            AutoSize = true,
            Location = new Point(0, 0),
            Margin = Padding.Empty
        };
        sectionPanel.Controls.Add(lblTitle);

        // 选项容器
        var optionsPanel = new Panel
        {
            Location = new Point(DpiHelper.Scale(12), lblTitle.Bottom + DpiHelper.Scale(2)),
            Width = sectionPanel.Width - DpiHelper.Scale(20),
            AutoSize = true,
            Margin = Padding.Empty
        };

        int currentY = 0;
        int selectedValue = _permissions.GetValueOrDefault(key, 0);

        foreach (var (text, value) in options)
        {
            var rb = new RadioButton
            {
                Text = text,
                Location = new Point(0, currentY),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9.5F)),
                Checked = selectedValue == value,
                Tag = (key, value),
                ForeColor = Color.FromArgb(64, 64, 64),
                Margin = Padding.Empty,
                Padding = new Padding(0, DpiHelper.Scale(1), 0, DpiHelper.Scale(1))
            };
            rb.CheckedChanged += (s, e) =>
            {
                if (rb.Checked && rb.Tag is (string k, int v))
                    _permissions[k] = v;
            };
            optionsPanel.Controls.Add(rb);
            currentY += rb.PreferredSize.Height;
        }
        sectionPanel.Controls.Add(optionsPanel);

        // 按钮容器
        if (buttons != null && buttons.Length > 0)
        {
            var btnPanel = new FlowLayoutPanel
            {
                Location = new Point(DpiHelper.Scale(12), optionsPanel.Bottom),
                Width = sectionPanel.Width - DpiHelper.Scale(20),
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            foreach (var (text, action) in buttons)
            {
                var btn = new LinkLabel
                {
                    Text = text,
                    AutoSize = true,
                    Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
                    LinkColor = Color.FromArgb(0, 120, 212),
                    ActiveLinkColor = Color.FromArgb(0, 102, 204),
                    VisitedLinkColor = Color.FromArgb(0, 120, 212),
                    LinkBehavior = LinkBehavior.HoverUnderline,
                    Margin = new Padding(0, DpiHelper.Scale(1), DpiHelper.Scale(15), DpiHelper.Scale(1))
                };
                btn.Click += (s, e) => action?.Invoke();
                btnPanel.Controls.Add(btn);
            }
            sectionPanel.Controls.Add(btnPanel);
        }

        parent.Controls.Add(sectionPanel);
    }

    private int AddSettingSection(string title, int y, (string text, int value)[] options, 
        string key, (string text, Action action)[]? buttons = null)
    {
        // 此方法已废弃，保留签名以防万一，但逻辑已移至 AddSection
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
            Localization.Raw() + "\n\n" +
            Localization.Raw() + "\n\n" +
            Localization.Raw(),
            Localization.Raw(),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
    
    private void ShowHandlers()
    {
        MessageBox.Show(
            Localization.Raw() + "\n\n" +
            Localization.Raw(),
            Localization.Raw(),
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
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));
        
        // 说明标签
        var descLabel = new Label
        {
            Text = Localization.T("site_settings.desc", new Dictionary<string, string> { { "category", _category } }),
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
            Text = Localization.T("site_settings.host_pattern"),
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
        behaviorCombo.Items.AddRange(new[] { Localization.T("permissions.allow"), Localization.T("permissions.block"), Localization.T("permissions.ask") });
        behaviorCombo.SelectedIndex = 0;
        
        var addBtn = new Button
        {
            Text = Localization.T("actions.add"),
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
        _listView.Columns.Add(Localization.T("site_settings.columns.host_pattern"), DpiHelper.Scale(280));
        _listView.Columns.Add(Localization.T("site_settings.columns.behavior"), DpiHelper.Scale(150));
        
        // 删除按钮
        var removeBtn = new Button
        {
            Text = Localization.T("actions.delete"),
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
            Text = Localization.T("actions.delete_all"),
            Location = new Point(DpiHelper.Scale(105), Height - DpiHelper.Scale(85)),
            Size = DpiHelper.Scale(new Size(80, 28)),
            FlatStyle = FlatStyle.Flat
        };
        removeAllBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        removeAllBtn.Click += (s, e) => _listView.Items.Clear();
        
        // 底部按钮
        var doneBtn = new Button
        {
            Text = Localization.T("actions.done"),
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
