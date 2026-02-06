using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 修改收藏夹对话框
/// </summary>
public class EditBookmarkDialog : Form
{
    private readonly IBookmarkService _bookmarkService;
    private readonly Bookmark _bookmark;
    
    private TextBox _txtName = null!;
    private TextBox _txtUrl = null!;
    private TreeView _treeView = null!;
    private Button _btnNewFolder = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;
    
    public EditBookmarkDialog(IBookmarkService bookmarkService, Bookmark bookmark)
    {
        _bookmarkService = bookmarkService;
        _bookmark = bookmark;
        
        InitializeComponent();
        LoadFolderTree();
        
        _txtName.Text = bookmark.Title;
        _txtUrl.Text = bookmark.Url ?? "";
        _txtName.SelectAll();
        
        SelectFolder(bookmark.ParentId);
    }
    
    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        Text = "修改收藏夹";
        Size = DpiHelper.Scale(new Size(400, 350));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;
        
        // 名字标签
        var lblName = new Label
        {
            Text = "名字:",
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            Location = DpiHelper.Scale(new Point(15, 20)),
            AutoSize = true
        };
        
        // 名字输入框
        _txtName = new TextBox
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            Location = DpiHelper.Scale(new Point(55, 17)),
            Width = DpiHelper.Scale(315),
            BorderStyle = BorderStyle.FixedSingle
        };
        
        // 网址标签
        var lblUrl = new Label
        {
            Text = "网址:",
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            Location = DpiHelper.Scale(new Point(15, 50)),
            AutoSize = true
        };
        
        // 网址输入框
        _txtUrl = new TextBox
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            Location = DpiHelper.Scale(new Point(55, 47)),
            Width = DpiHelper.Scale(315),
            BorderStyle = BorderStyle.FixedSingle
        };
        
        // 文件夹树形视图
        _treeView = new TreeView
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            Location = DpiHelper.Scale(new Point(15, 80)),
            Size = DpiHelper.Scale(new Size(355, 180)),
            BorderStyle = BorderStyle.FixedSingle,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            ImageList = CreateImageList()
        };
        
        // 新建文件夹按钮
        _btnNewFolder = new Button
        {
            Text = "新建文件夹",
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            Location = DpiHelper.Scale(new Point(15, 270)),
            Size = DpiHelper.Scale(new Size(90, 28)),
            FlatStyle = FlatStyle.System
        };
        _btnNewFolder.Click += BtnNewFolder_Click;
        
        // 保存按钮
        _btnSave = new Button
        {
            Text = "保存",
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            Location = DpiHelper.Scale(new Point(210, 270)),
            Size = DpiHelper.Scale(new Size(75, 28)),
            FlatStyle = FlatStyle.System
        };
        _btnSave.Click += BtnSave_Click;
        
        // 取消按钮
        _btnCancel = new Button
        {
            Text = "取消",
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9)),
            Location = DpiHelper.Scale(new Point(295, 270)),
            Size = DpiHelper.Scale(new Size(75, 28)),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.Cancel
        };
        
        Controls.AddRange(new Control[] 
        { 
            lblName, _txtName, lblUrl, _txtUrl, _treeView,
            _btnNewFolder, _btnSave, _btnCancel 
        });
        
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }
    
    private ImageList CreateImageList()
    {
        var imageList = new ImageList { ImageSize = DpiHelper.Scale(new Size(16, 16)) };
        
        // 创建文件夹图标
        var folderBmp = new Bitmap(DpiHelper.Scale(16), DpiHelper.Scale(16));
        using (var g = Graphics.FromImage(folderBmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            // 文件夹主体
            using var brush = new SolidBrush(Color.FromArgb(255, 200, 80));
            g.FillRectangle(brush, DpiHelper.Scale(1), DpiHelper.Scale(4), DpiHelper.Scale(14), DpiHelper.Scale(10));
            // 文件夹标签
            g.FillRectangle(brush, DpiHelper.Scale(1), DpiHelper.Scale(2), DpiHelper.Scale(6), DpiHelper.Scale(3));
        }
        imageList.Images.Add("folder", folderBmp);
        
        return imageList;
    }
    
    private void LoadFolderTree()
    {
        _treeView.Nodes.Clear();
        
        // 添加收藏栏根节点
        var rootNode = new TreeNode("收藏栏")
        {
            Tag = (string?)null,
            ImageKey = "folder",
            SelectedImageKey = "folder"
        };
        _treeView.Nodes.Add(rootNode);
        LoadFolderChildren(rootNode, null);
        rootNode.Expand();
        
        // 添加其他收藏节点
        var otherNode = new TreeNode("其他收藏")
        {
            Tag = "other",
            ImageKey = "folder",
            SelectedImageKey = "folder"
        };
        _treeView.Nodes.Add(otherNode);
        LoadFolderChildren(otherNode, "other");
    }
    
    private void LoadFolderChildren(TreeNode parentNode, string? parentId)
    {
        var items = parentId == null 
            ? _bookmarkService.GetBookmarkBarItems() 
            : _bookmarkService.GetChildren(parentId);
        
        foreach (var item in items.Where(i => i.IsFolder))
        {
            var node = new TreeNode(item.Title)
            {
                Tag = item.Id,
                ImageKey = "folder",
                SelectedImageKey = "folder"
            };
            parentNode.Nodes.Add(node);
            LoadFolderChildren(node, item.Id);
        }
    }
    
    private void SelectFolder(string? folderId)
    {
        if (folderId == null)
        {
            _treeView.SelectedNode = _treeView.Nodes[0];
            return;
        }
        
        var node = FindNode(_treeView.Nodes, folderId);
        if (node != null)
        {
            _treeView.SelectedNode = node;
            node.EnsureVisible();
        }
        else
        {
            _treeView.SelectedNode = _treeView.Nodes[0];
        }
    }
    
    private TreeNode? FindNode(TreeNodeCollection nodes, string folderId)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag as string == folderId)
                return node;
            
            var found = FindNode(node.Nodes, folderId);
            if (found != null)
                return found;
        }
        return null;
    }
    
    private void BtnNewFolder_Click(object? sender, EventArgs e)
    {
        using var inputDialog = new Form
        {
            Text = "新建文件夹",
            Size = DpiHelper.Scale(new Size(300, 130)),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9))
        };
        
        var lblName = new Label { Text = "名称:", Location = DpiHelper.Scale(new Point(15, 20)), AutoSize = true };
        var txtName = new TextBox { Text = "新建文件夹", Location = DpiHelper.Scale(new Point(60, 17)), Width = DpiHelper.Scale(210) };
        txtName.SelectAll();
        
        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = DpiHelper.Scale(new Point(110, 55)), Width = DpiHelper.Scale(75) };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = DpiHelper.Scale(new Point(195, 55)), Width = DpiHelper.Scale(75) };
        
        inputDialog.Controls.AddRange(new Control[] { lblName, txtName, btnOk, btnCancel });
        inputDialog.AcceptButton = btnOk;
        inputDialog.CancelButton = btnCancel;
        
        if (inputDialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txtName.Text))
        {
            var parentId = _treeView.SelectedNode?.Tag as string;
            var newFolder = _bookmarkService.AddFolder(txtName.Text.Trim(), parentId);
            
            // 刷新树并选中新文件夹
            LoadFolderTree();
            SelectFolder(newFolder.Id);
        }
    }
    
    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var title = string.IsNullOrWhiteSpace(_txtName.Text) ? "新书签" : _txtName.Text.Trim();
        var url = _txtUrl.Text.Trim();
        var parentId = _treeView.SelectedNode?.Tag as string;
        
        _bookmarkService.UpdateBookmark(_bookmark.Id, title, url, parentId ?? "");
        
        DialogResult = DialogResult.OK;
        Close();
    }
}
