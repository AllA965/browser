using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;
using System.Runtime.InteropServices;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 添加收藏对话框 - 弹出式样式，显示在收藏按钮下方
/// </summary>
public class AddBookmarkDialog : Form
{
    private readonly IBookmarkService _bookmarkService;
    private readonly string _url;
    private readonly string? _faviconUrl;
    private Bookmark? _existingBookmark;
    
    private TextBox _txtName = null!;
    private ComboBox _cmbFolder = null!;
    private Button _btnDelete = null!;
    private Button _btnEdit = null!;
    private Button _btnDone = null!;
    private Label _lblTitle = null!;
    
    private List<FolderItem> _folders = new();
    private Point _anchorPoint;
    private bool _isNewBookmark;
    
    // 阴影效果
    private const int CS_DROPSHADOW = 0x00020000;
    
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }
    
    public AddBookmarkDialog(IBookmarkService bookmarkService, string title, string url, string? faviconUrl, Bookmark? existingBookmark = null)
    {
        _bookmarkService = bookmarkService;
        _url = url;
        _faviconUrl = faviconUrl;
        _existingBookmark = existingBookmark;
        _isNewBookmark = existingBookmark == null;
        
        // 如果是新书签，先添加到收藏
        if (_isNewBookmark)
        {
            _existingBookmark = _bookmarkService.AddBookmark(title, url, null, faviconUrl);
        }
        
        InitializeComponent();
        LoadFolders();
        
        _txtName.Text = _existingBookmark?.Title ?? title;
        _txtName.SelectAll();
        
        if (_existingBookmark != null)
        {
            SelectFolder(_existingBookmark.ParentId);
        }
    }
    
    public void SetAnchorPoint(Point screenPoint)
    {
        _anchorPoint = screenPoint;
        StartPosition = FormStartPosition.Manual;
    }
    
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        
        if (StartPosition == FormStartPosition.Manual && _anchorPoint != Point.Empty)
        {
            var x = _anchorPoint.X - Width + 30;
            var y = _anchorPoint.Y;
            
            var screen = Screen.FromPoint(_anchorPoint).WorkingArea;
            if (x < screen.Left) x = screen.Left + 5;
            if (x + Width > screen.Right) x = screen.Right - Width - 5;
            if (y + Height > screen.Bottom) y = _anchorPoint.Y - Height - 30;
            
            Location = new Point(x, y);
        }
    }
    
    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(240, 240, 240); // 背景改为内容色
        Size = new Size(400, 175);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        
        // 移除 Region 裁剪，改用 OnPaint 手动绘制平滑边缘
        this.Paint += AddBookmarkDialog_Paint;
        
        // 主内容面板
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent, // 改为透明
            Padding = new Padding(2) // 为边框留出空间
        };
        
        // 标题
        _lblTitle = new Label
        {
            Text = "已添加收藏！",
            Font = new Font("Microsoft YaHei UI", 12, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 51, 51),
            Location = new Point(19, 14),
            AutoSize = true
        };
        
        // 名字标签
        var lblName = new Label
        {
            Text = "名字:",
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(51, 51, 51),
            Location = new Point(19, 54),
            AutoSize = true
        };
        
        // 名字输入框
        _txtName = new TextBox
        {
            Font = new Font("Microsoft YaHei UI", 9),
            Location = new Point(79, 51),
            Width = 295,
            BorderStyle = BorderStyle.FixedSingle
        };
        
        // 文件夹标签
        var lblFolder = new Label
        {
            Text = "文件夹:",
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(51, 51, 51),
            Location = new Point(19, 89),
            AutoSize = true
        };
        
        // 文件夹下拉框
        _cmbFolder = new ComboBox
        {
            Font = new Font("Microsoft YaHei UI", 9),
            Location = new Point(79, 86),
            Width = 295,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        
        // 删除按钮
        _btnDelete = CreateButton("删除", 80);
        _btnDelete.Location = new Point(114, 129);
        _btnDelete.Click += BtnDelete_Click;
        
        // 修改按钮
        _btnEdit = CreateButton("修改...", 80);
        _btnEdit.Location = new Point(204, 129);
        _btnEdit.Click += BtnEdit_Click;
        
        // 完成按钮
        _btnDone = CreateButton("完成", 80);
        _btnDone.Location = new Point(294, 129);
        _btnDone.Click += BtnDone_Click;
        
        contentPanel.Controls.AddRange(new Control[] 
        { 
            _lblTitle, lblName, _txtName, lblFolder, _cmbFolder, 
            _btnDelete, _btnEdit, _btnDone 
        });
        
        Controls.Add(contentPanel);
        
        AcceptButton = _btnDone;
        Deactivate += (s, e) => Close();
    }
    
    private Button CreateButton(string text, int width)
    {
        return new Button
        {
            Text = text,
            Font = new Font("Microsoft YaHei UI", 9),
            Size = new Size(width, 28),
            FlatStyle = FlatStyle.System,
            UseVisualStyleBackColor = true
        };
    }
    
    private void AddBookmarkDialog_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = 8;

        // 绘制背景
        using (var path = FormExtensions.GetRoundedRectanglePath(rect, radius))
        {
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
            
            // 绘制平滑边框
            using (var pen = new Pen(Color.FromArgb(180, 180, 180), 1))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
        
        // 应用裁剪区域（虽然 Region 仍有微小锯齿，但配合抗锯齿边框绘图，视觉上会非常平滑）
        this.Region = FormExtensions.CreateRoundedRegion(new Rectangle(0, 0, Width, Height), radius);
    }

    private void LoadFolders()
    {
        _folders.Clear();
        _folders.Add(new FolderItem { Id = null, Name = "收藏栏", Level = 0 });
        LoadFoldersRecursive(null, 1);
        
        // 添加"其他收藏"
        _folders.Add(new FolderItem { Id = "other", Name = "其他收藏", Level = 0 });
        LoadFoldersRecursive("other", 1);
        
        _cmbFolder.Items.Clear();
        foreach (var folder in _folders)
        {
            _cmbFolder.Items.Add(folder.DisplayName);
        }
        _cmbFolder.SelectedIndex = 0;
    }
    
    private void LoadFoldersRecursive(string? parentId, int level)
    {
        var items = parentId == null 
            ? _bookmarkService.GetBookmarkBarItems() 
            : _bookmarkService.GetChildren(parentId);
        
        foreach (var item in items.Where(i => i.IsFolder))
        {
            _folders.Add(new FolderItem { Id = item.Id, Name = item.Title, Level = level });
            LoadFoldersRecursive(item.Id, level + 1);
        }
    }
    
    private void SelectFolder(string? folderId)
    {
        for (int i = 0; i < _folders.Count; i++)
        {
            if (_folders[i].Id == folderId)
            {
                _cmbFolder.SelectedIndex = i;
                return;
            }
        }
        _cmbFolder.SelectedIndex = 0;
    }
    
    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_existingBookmark != null)
        {
            _bookmarkService.Delete(_existingBookmark.Id);
            DialogResult = DialogResult.Abort;
            Close();
        }
    }
    
    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_existingBookmark == null) return;
        
        // 先保存当前修改
        SaveChanges();
        
        // 打开修改对话框
        using var editDialog = new EditBookmarkDialog(_bookmarkService, _existingBookmark);
        editDialog.ShowDialog(Owner);
        
        DialogResult = DialogResult.OK;
        Close();
    }
    
    private void BtnDone_Click(object? sender, EventArgs e)
    {
        SaveChanges();
        DialogResult = DialogResult.OK;
        Close();
    }
    
    private void SaveChanges()
    {
        if (_existingBookmark == null) return;
        
        var title = string.IsNullOrWhiteSpace(_txtName.Text) ? "新书签" : _txtName.Text.Trim();
        var selectedFolder = _cmbFolder.SelectedIndex >= 0 ? _folders[_cmbFolder.SelectedIndex] : null;
        var parentId = selectedFolder?.Id;
        
        _bookmarkService.UpdateBookmark(_existingBookmark.Id, title, null, parentId ?? "");
    }
    
    private class FolderItem
    {
        public string? Id { get; set; }
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public string DisplayName => new string(' ', Level * 2) + Name;
    }
}
