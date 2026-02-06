using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Services;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 清除已保存的密码对话框
/// </summary>
public class PasswordManagerDialog : Form
{
    private readonly PasswordService _passwordService;
    private TextBox _searchBox = null!;
    private ListView _savedPasswordsList = null!;
    private ListView _neverSaveList = null!;
    private Button _doneBtn = null!;

    // 记录哪些密码已经显示（通过密码ID）
    private readonly HashSet<string> _revealedPasswords = new();
    // 是否已通过 Windows 验证
    private bool _isWindowsAuthenticated = false;

    public PasswordManagerDialog(PasswordService passwordService)
    {
        _passwordService = passwordService;
        InitializeUI();
        LoadData();
    }

    private void InitializeUI()
    {
        AppIconHelper.SetIcon(this);
        Text = "清除已保存的密码";
        Size = DpiHelper.Scale(new Size(600, 520));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));

        var y = DpiHelper.Scale(20);

        // 已保存的密码标题和搜索框
        var savedLabel = new Label
        {
            Text = "已保存的密码",
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10F), FontStyle.Bold)
        };
        Controls.Add(savedLabel);

        _searchBox = new TextBox
        {
            Location = new Point(DpiHelper.Scale(400), y - DpiHelper.Scale(3)),
            Width = DpiHelper.Scale(170),
            Text = "搜索密码"
        };
        _searchBox.GotFocus += (s, e) =>
        {
            if (_searchBox.Text == "搜索密码")
            {
                _searchBox.Text = "";
                _searchBox.ForeColor = Color.Black;
            }
        };
        _searchBox.LostFocus += (s, e) =>
        {
            if (string.IsNullOrEmpty(_searchBox.Text))
            {
                _searchBox.Text = "搜索密码";
                _searchBox.ForeColor = Color.Gray;
            }
        };
        _searchBox.ForeColor = Color.Gray;
        Controls.Add(_searchBox);
        y += DpiHelper.Scale(30);

        // 已保存的密码列表
        _savedPasswordsList = new ListView
        {
            Location = new Point(DpiHelper.Scale(20), y),
            Size = DpiHelper.Scale(new Size(550, 180)),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle,
            OwnerDraw = true
        };
        _savedPasswordsList.Columns.Add("网站", DpiHelper.Scale(180));
        _savedPasswordsList.Columns.Add("用户名", DpiHelper.Scale(130));
        _savedPasswordsList.Columns.Add("密码", DpiHelper.Scale(120));
        _savedPasswordsList.Columns.Add("", DpiHelper.Scale(70));  // 显示/隐藏按钮
        _savedPasswordsList.Columns.Add("", DpiHelper.Scale(30));  // 删除按钮

        // 自定义绘制
        _savedPasswordsList.DrawColumnHeader += OnDrawColumnHeader;
        _savedPasswordsList.DrawSubItem += OnDrawSubItem;
        _savedPasswordsList.MouseClick += OnListViewMouseClick;

        Controls.Add(_savedPasswordsList);
        y += DpiHelper.Scale(195);

        // 一律不保存标题
        var neverSaveLabel = new Label
        {
            Text = "一律不保存",
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10F), FontStyle.Bold)
        };
        Controls.Add(neverSaveLabel);
        y += DpiHelper.Scale(30);

        // 一律不保存列表
        _neverSaveList = new ListView
        {
            Location = new Point(DpiHelper.Scale(20), y),
            Size = DpiHelper.Scale(new Size(550, 130)),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _neverSaveList.Columns.Add("网站", DpiHelper.Scale(500));

        Controls.Add(_neverSaveList);

        // 完成按钮
        _doneBtn = new Button
        {
            Text = "完成",
            Location = DpiHelper.Scale(new Point(490, 450)),
            Size = DpiHelper.Scale(new Size(80, 28)),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        _doneBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        Controls.Add(_doneBtn);

        AcceptButton = _doneBtn;

        // 搜索功能
        _searchBox.TextChanged += (s, e) =>
        {
            if (_searchBox.Text != "搜索密码")
                LoadData(_searchBox.Text);
        };
    }

    private void OnDrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.DrawDefault = true;
    }

    private void OnDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item == null) return;

        // 绘制选中背景
        if (e.Item.Selected)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(204, 232, 255)), e.Bounds);
        }
        else
        {
            e.Graphics.FillRectangle(Brushes.White, e.Bounds);
        }

        var id = e.Item.Tag as string;
        var isRevealed = id != null && _revealedPasswords.Contains(id);

        // 显示/隐藏按钮列
        if (e.ColumnIndex == 3 && id != null)
        {
            var btnRect = new Rectangle(e.Bounds.X + DpiHelper.Scale(5), e.Bounds.Y + DpiHelper.Scale(2), DpiHelper.Scale(55), e.Bounds.Height - DpiHelper.Scale(4));
            var btnText = isRevealed ? "隐藏" : "显示";

            // 绘制按钮背景
            using var btnBrush = new SolidBrush(Color.FromArgb(0, 102, 204));
            e.Graphics.FillRectangle(btnBrush, btnRect);

            // 绘制按钮文字
            using var textBrush = new SolidBrush(Color.White);
            var textSize = e.Graphics.MeasureString(btnText, Font);
            var textX = btnRect.X + (btnRect.Width - textSize.Width) / 2;
            var textY = btnRect.Y + (btnRect.Height - textSize.Height) / 2;
            e.Graphics.DrawString(btnText, Font, textBrush, textX, textY);
        }
        // 删除按钮列
        else if (e.ColumnIndex == 4 && id != null)
        {
            var deleteText = "×";
            using var deleteBrush = new SolidBrush(Color.FromArgb(200, 50, 50));
            using var deleteFont = new Font(Font.FontFamily, DpiHelper.ScaleFont(12F), FontStyle.Bold);
            var textSize = e.Graphics.MeasureString(deleteText, deleteFont);
            var textX = e.Bounds.X + (e.Bounds.Width - textSize.Width) / 2;
            var textY = e.Bounds.Y + (e.Bounds.Height - textSize.Height) / 2;
            e.Graphics.DrawString(deleteText, deleteFont, deleteBrush, textX, textY);
        }
        else
        {
            // 普通文本
            var text = e.SubItem?.Text ?? "";
            using var textBrush = new SolidBrush(e.Item.ForeColor);
            var textY = e.Bounds.Y + (e.Bounds.Height - Font.Height) / 2;
            e.Graphics.DrawString(text, Font, textBrush, e.Bounds.X + DpiHelper.Scale(5), textY);
        }
    }

    private void OnListViewMouseClick(object? sender, MouseEventArgs e)
    {
        var hitTest = _savedPasswordsList.HitTest(e.Location);
        if (hitTest.Item == null) return;

        var id = hitTest.Item.Tag as string;
        if (string.IsNullOrEmpty(id)) return;

        var columnIndex = hitTest.Item.SubItems.IndexOf(hitTest.SubItem);

        // 点击显示/隐藏按钮
        if (columnIndex == 3)
        {
            TogglePasswordVisibility(id, hitTest.Item);
        }
        // 点击删除按钮
        else if (columnIndex == 4)
        {
            DeletePassword(id);
        }
    }

    private void TogglePasswordVisibility(string id, ListViewItem item)
    {
        if (_revealedPasswords.Contains(id))
        {
            // 隐藏密码
            _revealedPasswords.Remove(id);
            item.SubItems[2].Text = "••••••••";
            _savedPasswordsList.Invalidate();
        }
        else
        {
            // 显示密码 - 需要验证 Windows 密码
            if (!_isWindowsAuthenticated)
            {
                if (!WindowsCredentialHelper.ShowPasswordDialog(this))
                {
                    return; // 验证失败或取消
                }
                _isWindowsAuthenticated = true;
            }

            // 获取真实密码
            var pwd = _passwordService.Passwords.FirstOrDefault(p => p.Id == id);
            if (pwd != null)
            {
                _revealedPasswords.Add(id);
                item.SubItems[2].Text = pwd.Password;
                _savedPasswordsList.Invalidate();
            }
        }
    }

    private void DeletePassword(string id)
    {
        var result = MessageBox.Show("确定要删除此密码吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _passwordService.DeletePassword(id);
            _revealedPasswords.Remove(id);
            LoadData(_searchBox.Text == "搜索密码" ? null : _searchBox.Text);
        }
    }

    private void LoadData(string? keyword = null)
    {
        _savedPasswordsList.Items.Clear();
        _neverSaveList.Items.Clear();

        // 加载已保存的密码
        var passwords = string.IsNullOrEmpty(keyword)
            ? _passwordService.Passwords.ToList()
            : _passwordService.Search(keyword);

        if (passwords.Count == 0)
        {
            var emptyItem = new ListViewItem("您保存过的密码将会显示在此处。");
            emptyItem.ForeColor = Color.Gray;
            _savedPasswordsList.Items.Add(emptyItem);
        }
        else
        {
            foreach (var pwd in passwords)
            {
                var isRevealed = _revealedPasswords.Contains(pwd.Id);
                var displayPassword = isRevealed ? pwd.Password : "••••••••";
                var item = new ListViewItem(new[] { pwd.Host, pwd.Username, displayPassword, "", "" });
                item.Tag = pwd.Id;
                _savedPasswordsList.Items.Add(item);
            }
        }

        // 加载一律不保存列表
        var neverSave = _passwordService.NeverSaveList;
        if (neverSave.Count == 0)
        {
            var emptyItem = new ListViewItem("一律不保存密码的网站会显示在此处。");
            emptyItem.ForeColor = Color.Gray;
            _neverSaveList.Items.Add(emptyItem);
        }
        else
        {
            foreach (var ns in neverSave)
            {
                _neverSaveList.Items.Add(new ListViewItem(ns.Host));
            }
        }
    }
}
