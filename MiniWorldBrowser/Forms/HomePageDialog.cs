using MiniWorldBrowser.Services.Interfaces;

using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 启动页设置对话框
/// </summary>
public class HomePageDialog : Form
{
    private readonly ISettingsService _settingsService;
    private TextBox _urlTextBox = null!;
    private Button _okBtn = null!;
    private Button _cancelBtn = null!;
    
    public HomePageDialog(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeUI();
        LoadSettings();
    }
    
    private void InitializeUI()
    {
        AppIconHelper.SetIcon(this);
        Text = "启动页";
        Size = DpiHelper.Scale(new Size(450, 180));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));
        
        // 添加新网页标签
        var label = new Label
        {
            Text = "添加新网页",
            Location = DpiHelper.Scale(new Point(25, 40)),
            AutoSize = true,
            ForeColor = Color.FromArgb(51, 51, 51)
        };
        Controls.Add(label);
        
        // URL 输入框
        _urlTextBox = new TextBox
        {
            Location = DpiHelper.Scale(new Point(130, 37)),
            Size = DpiHelper.Scale(new Size(280, 23)),
            BorderStyle = BorderStyle.FixedSingle
        };
        _urlTextBox.GotFocus += (s, e) =>
        {
            if (_urlTextBox.Text == "输入网址...")
            {
                _urlTextBox.Text = "";
                _urlTextBox.ForeColor = Color.Black;
            }
        };
        _urlTextBox.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_urlTextBox.Text))
            {
                _urlTextBox.Text = "输入网址...";
                _urlTextBox.ForeColor = Color.Gray;
            }
        };
        Controls.Add(_urlTextBox);
        
        // 确定按钮
        _okBtn = new Button
        {
            Text = "确定",
            Size = DpiHelper.Scale(new Size(75, 28)),
            Location = DpiHelper.Scale(new Point(255, 100)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            DialogResult = DialogResult.OK
        };
        _okBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        _okBtn.Click += OnOkClick;
        Controls.Add(_okBtn);
        
        // 取消按钮
        _cancelBtn = new Button
        {
            Text = "取消",
            Size = DpiHelper.Scale(new Size(75, 28)),
            Location = DpiHelper.Scale(new Point(340, 100)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            DialogResult = DialogResult.Cancel
        };
        _cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        Controls.Add(_cancelBtn);
        
        AcceptButton = _okBtn;
        CancelButton = _cancelBtn;
    }
    
    private void LoadSettings()
    {
        var homePage = _settingsService.Settings.HomePage;
        
        if (string.IsNullOrEmpty(homePage) || 
            homePage == "about:newtab" || 
            homePage == "about:blank")
        {
            _urlTextBox.Text = "输入网址...";
            _urlTextBox.ForeColor = Color.Gray;
        }
        else
        {
            _urlTextBox.Text = homePage;
            _urlTextBox.ForeColor = Color.Black;
        }
    }
    
    private void OnOkClick(object? sender, EventArgs e)
    {
        var url = _urlTextBox.Text.Trim();
        
        // 如果是占位符文本或空，设置为新标签页
        if (string.IsNullOrEmpty(url) || url == "输入网址...")
        {
            _settingsService.Settings.HomePage = "about:newtab";
            _settingsService.Save();
            return;
        }
        
        // 自动添加协议
        if (!url.Contains("://"))
        {
            url = "https://" + url;
        }
        
        _settingsService.Settings.HomePage = url;
        _settingsService.Save();
    }
    
    /// <summary>
    /// 获取主页显示文本
    /// </summary>
    public static string GetHomePageDisplayText(string homePage)
    {
        if (string.IsNullOrEmpty(homePage) || 
            homePage == "about:newtab" || 
            homePage == "about:blank")
        {
            return "打开新的标签页";
        }
        return homePage;
    }
}
