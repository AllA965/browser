using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// Cookie和网站数据对话框 - 显示所有网站的Cookie和本地存储数据
/// </summary>
public class CookieDataDialog : Form
{
    private ListView _siteList = null!;
    private Panel _detailPanel = null!;
    private TextBox _searchBox = null!;
    private Button _btnDeleteAll = null!;
    private FlowLayoutPanel _cookieTagsPanel = null!;
    private Panel _cookieDetailPanel = null!;
    private string? _selectedSite;
    private string? _selectedCookie;

    // 模拟数据
    private readonly List<SiteData> _siteDataList = new();

    public CookieDataDialog()
    {
        InitializeComponent();
        LoadSampleData();
        RefreshSiteList();
    }

    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        Text = "Cookie 和网站数据";
        Size = DpiHelper.Scale(new Size(850, 600));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;

        // 标题行
        var lblSite = new Label
        {
            Text = "网站",
            Location = DpiHelper.Scale(new Point(20, 20)),
            Size = DpiHelper.Scale(new Size(200, 20)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F), FontStyle.Bold)
        };

        var lblData = new Label
        {
            Text = "本地存储的数据",
            Location = DpiHelper.Scale(new Point(230, 20)),
            Size = DpiHelper.Scale(new Size(300, 20)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F), FontStyle.Bold)
        };

        // 全部删除按钮
        _btnDeleteAll = new Button
        {
            Text = "全部删除",
            Location = DpiHelper.Scale(new Point(620, 15)),
            Size = DpiHelper.Scale(new Size(80, 28)),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        _btnDeleteAll.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _btnDeleteAll.Click += BtnDeleteAll_Click;

        // 搜索框
        _searchBox = new TextBox
        {
            Location = DpiHelper.Scale(new Point(710, 17)),
            Size = DpiHelper.Scale(new Size(110, 25)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        _searchBox.Text = "搜索 Cookie";
        _searchBox.ForeColor = Color.Gray;
        _searchBox.GotFocus += (s, e) =>
        {
            if (_searchBox.Text == "搜索 Cookie")
            {
                _searchBox.Text = "";
                _searchBox.ForeColor = Color.Black;
            }
        };
        _searchBox.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_searchBox.Text))
            {
                _searchBox.Text = "搜索 Cookie";
                _searchBox.ForeColor = Color.Gray;
            }
        };
        _searchBox.TextChanged += SearchBox_TextChanged;

        // 网站列表
        _siteList = new ListView
        {
            Location = DpiHelper.Scale(new Point(20, 50)),
            Size = DpiHelper.Scale(new Size(800, 450)),
            View = View.Details,
            FullRowSelect = true,
            BorderStyle = BorderStyle.FixedSingle,
            HeaderStyle = ColumnHeaderStyle.None,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        _siteList.Columns.Add("网站", DpiHelper.Scale(200));
        _siteList.Columns.Add("数据", DpiHelper.Scale(400));
        _siteList.Columns.Add("大小", DpiHelper.Scale(80));
        _siteList.SelectedIndexChanged += SiteList_SelectedIndexChanged;
        _siteList.DoubleClick += SiteList_DoubleClick;

        // 详情面板（初始隐藏）
        _detailPanel = new Panel
        {
            Location = DpiHelper.Scale(new Point(230, 50)),
            Size = DpiHelper.Scale(new Size(590, 450)),
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(245, 245, 245),
            Visible = false
        };

        // Cookie标签面板
        _cookieTagsPanel = new FlowLayoutPanel
        {
            Location = DpiHelper.Scale(new Point(10, 30)),
            Size = DpiHelper.Scale(new Size(570, 80)),
            AutoScroll = true,
            WrapContents = true
        };
        _detailPanel.Controls.Add(_cookieTagsPanel);

        // Cookie详情面板
        _cookieDetailPanel = new Panel
        {
            Location = DpiHelper.Scale(new Point(10, 120)),
            Size = DpiHelper.Scale(new Size(570, 280)),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            AutoScroll = true
        };
        _detailPanel.Controls.Add(_cookieDetailPanel);

        // 完成按钮
        var btnDone = new Button
        {
            Text = "完成",
            Location = DpiHelper.Scale(new Point(740, 520)),
            Size = DpiHelper.Scale(new Size(80, 28)),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.OK,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };

        Controls.AddRange(new Control[]
        {
            lblSite, lblData,
            _btnDeleteAll, _searchBox,
            _siteList, _detailPanel, btnDone
        });

        AcceptButton = btnDone;
    }

    private void LoadSampleData()
    {
        // 加载示例数据（实际应从WebView2获取）
        _siteDataList.Add(new SiteData("360.cn", new[] { "Cookie1" }, 0));
        _siteDataList.Add(new SiteData("baidu.com", new[] { "BAIDUID", "BAIDUID_BFESS", "BA_HECTOR", "BDORZ", "BIDUPSID", "H_PS_PSSID", "H_WISE_SIDS", "H_WISE_SIDS_BFESS", "PSTM", "ZFY", "_bid_n" }, 0));
        _siteDataList.Add(new SiteData("baijiahao.baidu.com", new string[0], 25395, "数据库存储, 本地存储"));
        _siteDataList.Add(new SiteData("www.baidu.com", new[] { "Cookie1" }, 1024, "本地存储"));
        _siteDataList.Add(new SiteData("bilibili.com", new[] { "Cookie1", "Cookie2" }, 0));
        _siteDataList.Add(new SiteData("doubleclick.net", new string[0], 0, "版本 ID"));
        _siteDataList.Add(new SiteData("mediav.com", new[] { "C1", "C2", "C3", "C4", "C5" }, 0));
        _siteDataList.Add(new SiteData("static-ssl.mediav.com", new string[0], 0, "本地存储"));
        _siteDataList.Add(new SiteData("so.com", new[] { "C1", "C2", "C3", "C4" }, 0));
        _siteDataList.Add(new SiteData("www.so.com", new[] { "C1", "C2", "C3" }, 0));
        _siteDataList.Add(new SiteData("www.theworld.cn", new[] { "C1", "C2" }, 0));
        _siteDataList.Add(new SiteData("youku.com", new[] { "Cookie1" }, 0));
    }

    private void RefreshSiteList(string? filter = null)
    {
        _siteList.Items.Clear();
        _detailPanel.Visible = false;

        foreach (var site in _siteDataList)
        {
            if (!string.IsNullOrEmpty(filter) && 
                !site.Domain.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var item = new ListViewItem(site.Domain);
            item.SubItems.Add(site.GetDataDescription());
            item.SubItems.Add(site.Size > 0 ? FormatSize(site.Size) : "");
            item.Tag = site;
            _siteList.Items.Add(item);
        }
    }

    private void SiteList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_siteList.SelectedItems.Count == 0)
        {
            _detailPanel.Visible = false;
            return;
        }

        var site = _siteList.SelectedItems[0].Tag as SiteData;
        if (site == null || site.Cookies.Length == 0)
        {
            _detailPanel.Visible = false;
            return;
        }

        _selectedSite = site.Domain;
        ShowSiteDetail(site);
    }

    private void SiteList_DoubleClick(object? sender, EventArgs e)
    {
        // 双击展开/折叠详情
        if (_siteList.SelectedItems.Count == 0) return;
        
        var site = _siteList.SelectedItems[0].Tag as SiteData;
        if (site == null || site.Cookies.Length == 0) return;

        _detailPanel.Visible = !_detailPanel.Visible;
        if (_detailPanel.Visible)
        {
            ShowSiteDetail(site);
        }
    }

    private void ShowSiteDetail(SiteData site)
    {
        _detailPanel.Visible = true;
        _cookieTagsPanel.Controls.Clear();
        _cookieDetailPanel.Controls.Clear();

        // 添加Cookie标签按钮
        foreach (var cookie in site.Cookies)
        {
            var btn = new Button
            {
                Text = cookie,
                AutoSize = true,
                MinimumSize = DpiHelper.Scale(new Size(60, 28)),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
                Margin = DpiHelper.Scale(new Padding(3)),
                Tag = cookie
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btn.Click += CookieTag_Click;
            _cookieTagsPanel.Controls.Add(btn);
        }

        // 默认选中第一个Cookie
        if (site.Cookies.Length > 0)
        {
            _selectedCookie = site.Cookies[0];
            ShowCookieDetail(site.Domain, site.Cookies[0]);
            HighlightCookieTag(site.Cookies[0]);
        }
    }

    private void CookieTag_Click(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cookieName)
        {
            _selectedCookie = cookieName;
            ShowCookieDetail(_selectedSite!, cookieName);
            HighlightCookieTag(cookieName);
        }
    }

    private void HighlightCookieTag(string cookieName)
    {
        foreach (Control ctrl in _cookieTagsPanel.Controls)
        {
            if (ctrl is Button btn)
            {
                btn.BackColor = btn.Tag?.ToString() == cookieName 
                    ? Color.FromArgb(230, 240, 255) 
                    : Color.White;
            }
        }
    }

    private void ShowCookieDetail(string domain, string cookieName)
    {
        _cookieDetailPanel.Controls.Clear();
        var y = 15;

        // 模拟Cookie详情
        AddDetailRow("名字:", cookieName, ref y);
        AddDetailRow("内容:", GenerateFakeCookieValue(), ref y);
        AddDetailRow("域:", "." + domain, ref y);
        AddDetailRow("路径:", "/", ref y);
        AddDetailRow("发送用途:", "各种连接", ref y);
        AddDetailRow("脚本可访问:", "是", ref y);
        AddDetailRow("创建时间:", DateTime.Now.AddDays(-7).ToString("yyyy年M月d日dddd 下午h:mm:ss"), ref y);
        AddDetailRow("过期时间:", DateTime.Now.AddYears(1).ToString("yyyy年M月d日dddd 下午h:mm:ss"), ref y);

        // 删除按钮
        var btnDelete = new Button
        {
            Text = "删除",
            Location = DpiHelper.Scale(new Point(15, y + 10)),
            Size = DpiHelper.Scale(new Size(60, 26)),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9))
        };
        btnDelete.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btnDelete.Click += (s, e) => DeleteCookie(domain, cookieName);
        _cookieDetailPanel.Controls.Add(btnDelete);
    }

    private void AddDetailRow(string label, string value, ref int y)
    {
        var lblName = new Label
        {
            Text = label,
            Location = DpiHelper.Scale(new Point(15, y)),
            Size = DpiHelper.Scale(new Size(80, 20)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            ForeColor = Color.Gray
        };

        var lblValue = new Label
        {
            Text = value,
            Location = DpiHelper.Scale(new Point(100, y)),
            Size = DpiHelper.Scale(new Size(450, 20)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            AutoEllipsis = true
        };
        
        _cookieDetailPanel.Controls.Add(lblName);
        _cookieDetailPanel.Controls.Add(lblValue);
        y += (int)Math.Round(25 * DpiHelper.GetControlDpiScale(this));
    }

    private string GenerateFakeCookieValue()
    {
        return Guid.NewGuid().ToString("N").ToUpper() + ":FG=1";
    }

    private void DeleteCookie(string domain, string cookieName)
    {
        var site = _siteDataList.FirstOrDefault(s => s.Domain == domain);
        if (site != null)
        {
            var cookies = site.Cookies.ToList();
            cookies.Remove(cookieName);
            site.Cookies = cookies.ToArray();

            if (site.Cookies.Length == 0)
            {
                _detailPanel.Visible = false;
            }
            else
            {
                ShowSiteDetail(site);
            }
            RefreshSiteList();
        }
    }

    private void BtnDeleteAll_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("确定要删除所有 Cookie 和网站数据吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _siteDataList.Clear();
            RefreshSiteList();
            _detailPanel.Visible = false;
        }
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        var filter = _searchBox.Text;
        if (filter == "搜索 Cookie") filter = null;
        RefreshSiteList(filter);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    private class SiteData
    {
        public string Domain { get; set; }
        public string[] Cookies { get; set; }
        public long Size { get; set; }
        public string? OtherData { get; set; }

        public SiteData(string domain, string[] cookies, long size, string? otherData = null)
        {
            Domain = domain;
            Cookies = cookies;
            Size = size;
            OtherData = otherData;
        }

        public string GetDataDescription()
        {
            var parts = new List<string>();
            if (Cookies.Length > 0)
                parts.Add($"{Cookies.Length} 个 Cookie");
            if (!string.IsNullOrEmpty(OtherData))
                parts.Add(OtherData);
            return string.Join(", ", parts);
        }
    }
}
