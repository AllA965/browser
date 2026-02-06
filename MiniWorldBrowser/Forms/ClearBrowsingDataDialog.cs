using Microsoft.Web.WebView2.Core;
using MiniWorldBrowser.Browser;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 清除浏览数据对话框
/// </summary>
public class ClearBrowsingDataDialog : Form
{
    private ComboBox _timeRangeCombo = null!;
    private CheckBox _historyCheck = null!;
    private CheckBox _downloadsCheck = null!;
    private CheckBox _cookiesCheck = null!;
    private CheckBox _cacheCheck = null!;
    private CheckBox _passwordsCheck = null!;
    private CheckBox _formDataCheck = null!;
    private CheckBox _hostedAppDataCheck = null!;
    private CheckBox _contentLicensesCheck = null!;
    private CheckBox _clearOnExitCheck = null!;
    private Button _clearBtn = null!;
    private Button _cancelBtn = null!;
    
    private readonly BrowserTabManager _tabManager;
    private readonly IHistoryService? _historyService;
    private readonly PasswordService? _passwordService;
    private long _cacheSize = 0;
    
    public ClearBrowsingDataDialog(BrowserTabManager tabManager, IHistoryService? historyService, PasswordService? passwordService)
    {
        _tabManager = tabManager;
        _historyService = historyService;
        _passwordService = passwordService;
        
        CalculateCacheSize();
        InitializeUI();
    }
    
    private void CalculateCacheSize()
    {
        try
        {
            var cacheFolder = Path.Combine(AppConstants.UserDataFolder, "EBWebView", "Default", "Cache");
            if (Directory.Exists(cacheFolder))
            {
                var dirInfo = new DirectoryInfo(cacheFolder);
                _cacheSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
        }
        catch { _cacheSize = 0; }
    }
    
    private void InitializeUI()
    {
        AppIconHelper.SetIcon(this);
        Text = "清除浏览数据";
        Size = DpiHelper.Scale(new Size(450, 480));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));
        
        var y = DpiHelper.Scale(20);
        
        // 时间范围
        var timeLabel = new Label { Text = "清除指定时间段内的数据:", Location = new Point(DpiHelper.Scale(20), y), AutoSize = true };
        Controls.Add(timeLabel);
        
        _timeRangeCombo = new ComboBox
        {
            Location = new Point(DpiHelper.Scale(180), y - DpiHelper.Scale(3)),
            Width = DpiHelper.Scale(120),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _timeRangeCombo.Items.AddRange(new object[]
        {
            "过去一小时",
            "过去24小时",
            "过去7天",
            "过去4周",
            "全部时间"
        });
        _timeRangeCombo.SelectedIndex = 0;
        Controls.Add(_timeRangeCombo);
        y += DpiHelper.Scale(40);

        // 分隔线
        var separator = new Panel { Location = new Point(DpiHelper.Scale(20), y), Size = new Size(DpiHelper.Scale(390), DpiHelper.Scale(1)), BackColor = Color.FromArgb(220, 220, 220) };
        Controls.Add(separator);
        y += DpiHelper.Scale(15);
        
        // 选项
        _historyCheck = CreateCheckBox("清除浏览记录", "", y, true);
        y += DpiHelper.Scale(30);
        
        _downloadsCheck = CreateCheckBox("清除下载记录", "", y, true);
        y += DpiHelper.Scale(30);
        
        _cookiesCheck = CreateCheckBox("Cookie 及其他网站数据和插件数据", "", y, true);
        y += DpiHelper.Scale(30);
        
        var cacheSizeText = _cacheSize > 0 ? $"- 不到 {FormatSize(_cacheSize)}" : "";
        _cacheCheck = CreateCheckBox("清空缓存", cacheSizeText, y, true);
        y += DpiHelper.Scale(30);
        
        _passwordsCheck = CreateCheckBox("清除已保存的密码", "", y, false);
        y += DpiHelper.Scale(30);
        
        _formDataCheck = CreateCheckBox("清除已保存的自动填充表单数据", "", y, false);
        y += DpiHelper.Scale(30);
        
        _hostedAppDataCheck = CreateCheckBox("清除托管应用中数据", "", y, false);
        y += DpiHelper.Scale(30);
        
        _contentLicensesCheck = CreateCheckBox("内容许可", "", y, false);
        y += DpiHelper.Scale(45);
        
        // 分隔线
        var separator2 = new Panel { Location = new Point(DpiHelper.Scale(20), y), Size = new Size(DpiHelper.Scale(390), DpiHelper.Scale(1)), BackColor = Color.FromArgb(220, 220, 220) };
        Controls.Add(separator2);
        y += DpiHelper.Scale(15);
        
        // 退出时自动清除选项
        _clearOnExitCheck = new CheckBox
        {
            Text = "退出浏览器时自动清除勾选数据",
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true
        };
        Controls.Add(_clearOnExitCheck);
        
        // 按钮
        _cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(DpiHelper.Scale(330), DpiHelper.Scale(400)),
            Size = DpiHelper.Scale(new Size(85, 30)),
            FlatStyle = FlatStyle.Flat
        };
        _cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(_cancelBtn);
        
        _clearBtn = new Button
        {
            Text = "清除浏览数据",
            Location = new Point(DpiHelper.Scale(220), DpiHelper.Scale(400)),
            Size = DpiHelper.Scale(new Size(100, 30)),
            FlatStyle = FlatStyle.Flat
        };
        _clearBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _clearBtn.Click += OnClearData;
        Controls.Add(_clearBtn);
        
        AcceptButton = _clearBtn;
        CancelButton = _cancelBtn;
    }
    
    private CheckBox CreateCheckBox(string text, string suffix, int y, bool isChecked)
    {
        var check = new CheckBox
        {
            Text = string.IsNullOrEmpty(suffix) ? text : $"{text}  {suffix}",
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true,
            Checked = isChecked
        };
        Controls.Add(check);
        return check;
    }
    
    private string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
    
    private async void OnClearData(object? sender, EventArgs e)
    {
        var items = new List<string>();
        if (_historyCheck.Checked) items.Add("浏览历史记录");
        if (_downloadsCheck.Checked) items.Add("下载记录");
        if (_cookiesCheck.Checked) items.Add("Cookie 及其他网站数据");
        if (_cacheCheck.Checked) items.Add("缓存");
        if (_passwordsCheck.Checked) items.Add("密码");
        if (_formDataCheck.Checked) items.Add("表单数据");
        if (_hostedAppDataCheck.Checked) items.Add("托管应用数据");
        if (_contentLicensesCheck.Checked) items.Add("内容许可");
        
        if (items.Count == 0)
        {
            MessageBox.Show("请至少选择一项要清除的数据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        
        _clearBtn.Enabled = false;
        _clearBtn.Text = "清除中...";
        
        try
        {
            // 计算起始时间
            DateTime? startTime = null;
            switch (_timeRangeCombo.SelectedIndex)
            {
                case 0: startTime = DateTime.Now.AddHours(-1); break;
                case 1: startTime = DateTime.Now.AddDays(-1); break;
                case 2: startTime = DateTime.Now.AddDays(-7); break;
                case 3: startTime = DateTime.Now.AddDays(-28); break;
                case 4: startTime = null; break; // 全部时间
            }

            // 执行清除操作
            var webViewDataKinds = (CoreWebView2BrowsingDataKinds)0;
            
            if (_historyCheck.Checked)
            {
                _historyService?.Clear(startTime);
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.BrowsingHistory;
            }
            
            if (_downloadsCheck.Checked)
            {
                _tabManager.ClearDownloads(startTime);
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.DownloadHistory;
            }
            
            if (_cookiesCheck.Checked)
            {
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.Cookies;
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.LocalStorage;
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.IndexedDb;
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.FileSystems;
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.WebSql;
            }
            
            if (_cacheCheck.Checked)
            {
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.DiskCache;
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.CacheStorage;
            }
            
            if (_passwordsCheck.Checked)
            {
                _passwordService?.Clear(startTime);
            }
            
            if (_formDataCheck.Checked)
            {
            }
            
            if (_hostedAppDataCheck.Checked)
            {
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.AllDomStorage;
            }
            
            if (_contentLicensesCheck.Checked)
            {
                webViewDataKinds |= CoreWebView2BrowsingDataKinds.Settings;
            }

            // 调用 WebView2 清除数据
            if (webViewDataKinds != 0)
            {
                await _tabManager.ClearWebView2Data(webViewDataKinds, startTime);
            }
            
            MessageBox.Show($"已成功清除以下数据：\n• {string.Join("\n• ", items)}", 
                "清除完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"清除数据时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _clearBtn.Enabled = true;
            _clearBtn.Text = "清除浏览数据";
        }
    }
}
