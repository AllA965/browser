using MiniWorldBrowser.Services.Interfaces;

using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 广告过滤例外网站管理对话框
/// </summary>
public class AdBlockExceptionDialog : Form
{
    private readonly ISettingsService _settingsService;
    private ListView _exceptionList = null!;
    private TextBox _txtHost = null!;
    private ComboBox _cboAction = null!;
    private Button _btnAdd = null!;

    public AdBlockExceptionDialog(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        LoadExceptions();
    }

    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        Text = "广告过滤例外情况";
        Size = new Size(550, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;

        // 标题
        var lblTitle = new Label
        {
            Text = "主机名（可包含通配符）",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9)
        };

        var lblAction = new Label
        {
            Text = "行为",
            Location = new Point(380, 20),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9)
        };

        // 输入框
        _txtHost = new TextBox
        {
            Location = new Point(20, 45),
            Size = new Size(350, 25),
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
            Location = new Point(380, 43),
            Size = new Size(80, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 9)
        };
        _cboAction.Items.AddRange(new[] { "允许", "阻止" });
        _cboAction.SelectedIndex = 0;

        // 添加按钮
        _btnAdd = new Button
        {
            Text = "+",
            Location = new Point(470, 42),
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
            Size = new Size(490, 250),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        _exceptionList.Columns.Add("主机名", 350);
        _exceptionList.Columns.Add("行为", 100);
        _exceptionList.KeyDown += ExceptionList_KeyDown;

        // 右键菜单
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("删除", null, (s, e) => DeleteSelected());
        _exceptionList.ContextMenuStrip = contextMenu;

        // 完成按钮
        var btnDone = new Button
        {
            Text = "完成",
            Location = new Point(435, 345),
            Size = new Size(75, 28),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.OK
        };

        Controls.AddRange(new Control[]
        {
            lblTitle, lblAction,
            _txtHost, _cboAction, _btnAdd,
            _exceptionList, btnDone
        });

        AcceptButton = btnDone;
    }

    private void LoadExceptions()
    {
        _exceptionList.Items.Clear();
        foreach (var exception in _settingsService.Settings.AdBlockExceptions)
        {
            var parts = exception.Split('|');
            var host = parts[0];
            var action = parts.Length > 1 && parts[1] == "block" ? "阻止" : "允许";

            var item = new ListViewItem(host);
            item.SubItems.Add(action);
            item.Tag = exception;
            _exceptionList.Items.Add(item);
        }
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var host = _txtHost.Text.Trim();
        if (string.IsNullOrEmpty(host) || host == "[*.]example.com")
        {
            MessageBox.Show("请输入主机名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var action = _cboAction.SelectedIndex == 0 ? "allow" : "block";
        var exception = $"{host}|{action}";

        // 检查是否已存在
        foreach (ListViewItem item in _exceptionList.Items)
        {
            if (item.Text.Equals(host, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("该主机名已存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        _settingsService.Settings.AdBlockExceptions.Add(exception);
        _settingsService.Save();

        LoadExceptions();

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

        var item = _exceptionList.SelectedItems[0];
        var exception = item.Tag as string;

        if (exception != null)
        {
            _settingsService.Settings.AdBlockExceptions.Remove(exception);
            _settingsService.Save();
            LoadExceptions();
        }
    }
}
