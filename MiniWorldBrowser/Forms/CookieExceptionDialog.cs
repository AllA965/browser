using MiniWorldBrowser.Services.Interfaces;

using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// Cookie和网站数据例外情况对话框
/// </summary>
public class CookieExceptionDialog : Form
{
    private readonly ISettingsService _settingsService;
    private readonly string _category;
    private TextBox _txtHost = null!;
    private ComboBox _cboAction = null!;
    private Button _btnAdd = null!;
    private ListView _exceptionList = null!;

    public CookieExceptionDialog(ISettingsService settingsService, string category = "cookies")
    {
        _settingsService = settingsService;
        _category = category;
        InitializeComponent();
        LoadExceptions();
    }

    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        var title = _category switch
        {
            "cookies" => "Cookie 和网站数据例外情况",
            "images" => "图片例外情况",
            "javascript" => "JavaScript 例外情况",
            "popups" => "弹出式窗口例外情况",
            "location" => "位置例外情况",
            "notifications" => "通知例外情况",
            "media" => "媒体例外情况",
            _ => "例外情况"
        };

        Text = title;
        Size = new Size(650, 450);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;

        // 主机名标签
        var lblHost = new Label
        {
            Text = "主机名（可包含通配符）",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9)
        };

        // 行为标签
        var lblAction = new Label
        {
            Text = "行为",
            Location = new Point(450, 20),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9)
        };

        // 主机名输入框
        _txtHost = new TextBox
        {
            Location = new Point(20, 45),
            Size = new Size(420, 25),
            Font = new Font("Microsoft YaHei UI", 9)
        };
        _txtHost.Text = "[*.]example.com";
        _txtHost.ForeColor = Color.Gray;
        _txtHost.GotFocus += (s, e) =>
        {
            if (_txtHost.Text == "[*.]example.com")
            {
                _txtHost.Text = "";
                _txtHost.ForeColor = Color.Black;
            }
        };
        _txtHost.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_txtHost.Text))
            {
                _txtHost.Text = "[*.]example.com";
                _txtHost.ForeColor = Color.Gray;
            }
        };

        // 行为下拉框
        _cboAction = new ComboBox
        {
            Location = new Point(450, 43),
            Size = new Size(100, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 9)
        };
        _cboAction.Items.AddRange(new[] { "允许", "阻止", "仅会话" });
        _cboAction.SelectedIndex = 0;

        // 添加按钮
        _btnAdd = new Button
        {
            Text = "+",
            Location = new Point(560, 42),
            Size = new Size(30, 26),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold)
        };
        _btnAdd.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _btnAdd.Click += BtnAdd_Click;

        // 例外列表
        _exceptionList = new ListView
        {
            Location = new Point(20, 80),
            Size = new Size(590, 280),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        _exceptionList.Columns.Add("主机名", 400);
        _exceptionList.Columns.Add("行为", 150);
        _exceptionList.KeyDown += ExceptionList_KeyDown;

        // 右键菜单
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("删除", null, (s, e) => DeleteSelected());
        _exceptionList.ContextMenuStrip = contextMenu;

        // 完成按钮
        var btnDone = new Button
        {
            Text = "完成",
            Location = new Point(535, 375),
            Size = new Size(75, 28),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.OK
        };

        Controls.AddRange(new Control[]
        {
            lblHost, lblAction,
            _txtHost, _cboAction, _btnAdd,
            _exceptionList, btnDone
        });

        AcceptButton = btnDone;
    }

    private string GetSettingsKey()
    {
        return _category switch
        {
            "cookies" => "CookieExceptions",
            "images" => "ImageExceptions",
            "javascript" => "JavaScriptExceptions",
            "popups" => "PopupExceptions",
            "location" => "LocationExceptions",
            "notifications" => "NotificationExceptions",
            "media" => "MediaExceptions",
            _ => "CookieExceptions"
        };
    }

    private List<string> GetExceptionList()
    {
        // 这里简化处理，实际应该根据 category 获取对应的例外列表
        // 目前使用 AdBlockExceptions 作为示例存储
        return _settingsService.Settings.AdBlockExceptions;
    }

    private void LoadExceptions()
    {
        _exceptionList.Items.Clear();
        // 这里可以从设置中加载对应分类的例外列表
        // 目前为空列表，实际使用时需要扩展 BrowserSettings
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var host = _txtHost.Text.Trim();
        if (string.IsNullOrEmpty(host) || host == "[*.]example.com")
        {
            MessageBox.Show("请输入主机名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var actionText = _cboAction.SelectedItem?.ToString() ?? "允许";

        // 检查是否已存在
        foreach (ListViewItem item in _exceptionList.Items)
        {
            if (item.Text.Equals(host, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("该主机名已存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        var listItem = new ListViewItem(host);
        listItem.SubItems.Add(actionText);
        _exceptionList.Items.Add(listItem);

        // 清空输入
        _txtHost.Text = "[*.]example.com";
        _txtHost.ForeColor = Color.Gray;
        _cboAction.SelectedIndex = 0;
    }

    private void ExceptionList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    private void DeleteSelected()
    {
        if (_exceptionList.SelectedItems.Count == 0) return;
        _exceptionList.Items.Remove(_exceptionList.SelectedItems[0]);
    }
}
