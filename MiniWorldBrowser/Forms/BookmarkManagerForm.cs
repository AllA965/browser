using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 收藏管理器窗体
/// </summary>
public class BookmarkManagerForm : Form
{
    private readonly IBookmarkService _bookmarkService;
    private readonly Action<string>? _onNavigate;
    
    private TextBox _txtName = null!;
    private TextBox _txtUrl = null!;
    private TreeView _treeView = null!;
    private ListView _listView = null!;
    private SplitContainer _splitContainer = null!;
    private ImageList _treeImageList = null!;
    private ImageList _listImageList = null!;
    
    private Bookmark? _selectedBookmark;
    private string? _selectedFolderId;
    private bool _isUpdating;
    
    public BookmarkManagerForm(IBookmarkService bookmarkService, Action<string>? onNavigate = null)
    {
        _bookmarkService = bookmarkService;
        _onNavigate = onNavigate;
        
        InitializeComponent();
        LoadFolderTree();
        
        _bookmarkService.BookmarksChanged += OnBookmarksChanged;
    }
    
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _bookmarkService.BookmarksChanged -= OnBookmarksChanged;
        base.OnFormClosed(e);
    }
    
    private void OnBookmarksChanged()
    {
        if (InvokeRequired)
        {
            Invoke(OnBookmarksChanged);
            return;
        }
        
        var selectedFolderId = _selectedFolderId;
        LoadFolderTree();
        SelectFolderInTree(selectedFolderId);
    }

    
    private void InitializeComponent()
    {
        Text = "收藏管理器";
        Size = DpiHelper.Scale(new Size(900, 550));
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = DpiHelper.Scale(new Size(700, 400));
        BackColor = Color.White;
        
        // 设置窗口图标
        AppIconHelper.SetIcon(this);
        
        CreateImageLists();
        
        // 名字标签和输入框
        var lblName = new Label
        {
            Text = "名字:",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            Location = DpiHelper.Scale(new Point(15, 18)),
            AutoSize = true
        };
        
        _txtName = new TextBox
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            Location = DpiHelper.Scale(new Point(55, 15)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtName.Width = ClientSize.Width - DpiHelper.Scale(70);
        _txtName.Leave += TxtName_Leave;
        _txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SaveNameChange(); e.Handled = true; } };
        
        // 网址标签和输入框
        var lblUrl = new Label
        {
            Text = "网址:",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            Location = DpiHelper.Scale(new Point(15, 48)),
            AutoSize = true
        };
        
        _txtUrl = new TextBox
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            Location = DpiHelper.Scale(new Point(55, 45)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtUrl.Width = ClientSize.Width - DpiHelper.Scale(70);
        _txtUrl.Leave += TxtUrl_Leave;
        _txtUrl.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SaveUrlChange(); e.Handled = true; } };
        
        // 分割容器 - 调整比例约 1:2
        _splitContainer = new SplitContainer
        {
            Location = DpiHelper.Scale(new Point(15, 80)),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle,
            SplitterWidth = DpiHelper.Scale(4)
        };
        _splitContainer.Size = new Size(ClientSize.Width - DpiHelper.Scale(30), ClientSize.Height - DpiHelper.Scale(135));
        _splitContainer.SplitterDistance = (int)(_splitContainer.Width * 0.28); // 左侧约28%
        
        // 左侧文件夹树
        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            BorderStyle = BorderStyle.None,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            ImageList = _treeImageList
        };
        _treeView.AfterSelect += TreeView_AfterSelect;
        _splitContainer.Panel1.Controls.Add(_treeView);
        
        // 右侧书签列表
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            BorderStyle = BorderStyle.None,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            SmallImageList = _listImageList
        };
        _listView.Columns.Add("名称", DpiHelper.Scale(300));
        _listView.Columns.Add("网址", DpiHelper.Scale(350));
        _listView.SelectedIndexChanged += ListView_SelectedIndexChanged;
        _listView.DoubleClick += ListView_DoubleClick;
        _listView.KeyDown += ListView_KeyDown;
        
        // 右键菜单
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("打开", null, (s, e) => OpenSelectedBookmark());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("编辑", null, (s, e) => EditSelectedBookmark());
        contextMenu.Items.Add("删除", null, (s, e) => DeleteSelectedBookmark());
        _listView.ContextMenuStrip = contextMenu;
        
        _splitContainer.Panel2.Controls.Add(_listView);
        
        // 底部按钮
        var btnNewFolder = new Button
        {
            Text = "新建文件夹",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            Location = new Point(DpiHelper.Scale(15), ClientSize.Height - DpiHelper.Scale(45)),
            Size = DpiHelper.Scale(new Size(100, 30)),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            FlatStyle = FlatStyle.System
        };
        btnNewFolder.Click += BtnNewFolder_Click;
        
        var btnExport = new Button
        {
            Text = "导出收藏到HTML文件",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            Location = new Point(DpiHelper.Scale(125), ClientSize.Height - DpiHelper.Scale(45)),
            Size = DpiHelper.Scale(new Size(140, 30)),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            FlatStyle = FlatStyle.System
        };
        btnExport.Click += BtnExport_Click;
        
        var btnClose = new Button
        {
            Text = "关闭",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F)),
            Location = new Point(ClientSize.Width - DpiHelper.Scale(90), ClientSize.Height - DpiHelper.Scale(45)),
            Size = DpiHelper.Scale(new Size(75, 30)),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.Cancel
        };
        
        Controls.AddRange(new Control[] { lblName, _txtName, lblUrl, _txtUrl, _splitContainer, btnNewFolder, btnExport, btnClose });
        CancelButton = btnClose;
    }
    
    private void CreateImageLists()
    {
        _treeImageList = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        _listImageList = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        
        var folderBmp = CreateFolderIcon();
        _treeImageList.Images.Add("folder", folderBmp);
        _listImageList.Images.Add("folder", folderBmp);
        
        var pageBmp = Helpers.FaviconHelper.DefaultIcon;
        _listImageList.Images.Add("page", pageBmp);
    }
    
    private Bitmap CreateFolderIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(255, 200, 80));
        g.FillRectangle(brush, 1, 4, 14, 10);
        g.FillRectangle(brush, 1, 2, 6, 3);
        return bmp;
    }

    
    private void LoadFolderTree()
    {
        _treeView.Nodes.Clear();
        
        var rootNode = new TreeNode("收藏栏")
        {
            Tag = (string?)null,
            ImageKey = "folder",
            SelectedImageKey = "folder"
        };
        _treeView.Nodes.Add(rootNode);
        LoadFolderChildren(rootNode, null);
        
        var otherNode = new TreeNode("其它收藏")
        {
            Tag = "other",
            ImageKey = "folder",
            SelectedImageKey = "folder"
        };
        _treeView.Nodes.Add(otherNode);
        LoadFolderChildren(otherNode, "other");
        
        rootNode.Expand();
        _treeView.SelectedNode = rootNode;
    }
    
    private void LoadFolderChildren(TreeNode parentNode, string? parentId)
    {
        List<Bookmark> items;
        if (parentId == null)
            items = _bookmarkService.GetBookmarkBarItems();
        else if (parentId == "other")
            items = _bookmarkService.GetOtherBookmarks();
        else
            items = _bookmarkService.GetChildren(parentId);
        
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
    
    private void SelectFolderInTree(string? folderId)
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
    
    private void TreeView_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        _selectedFolderId = e.Node?.Tag as string;
        _selectedBookmark = null;
        
        _isUpdating = true;
        _txtName.Text = e.Node?.Text ?? "";
        _txtUrl.Text = "";
        _txtUrl.Enabled = false;
        _isUpdating = false;
        
        LoadBookmarkList(_selectedFolderId);
    }
    
    private void LoadBookmarkList(string? folderId)
    {
        _listView.Items.Clear();
        
        List<Bookmark> items;
        if (folderId == null)
            items = _bookmarkService.GetBookmarkBarItems();
        else if (folderId == "other")
            items = _bookmarkService.GetOtherBookmarks();
        else
            items = _bookmarkService.GetChildren(folderId);
        
        foreach (var item in items)
        {
            var listItem = new ListViewItem(item.Title)
            {
                Tag = item,
                ImageKey = item.IsFolder ? "folder" : "page"
            };
            listItem.SubItems.Add(item.Url ?? "");
            _listView.Items.Add(listItem);
            
            // 异步加载网址图标
            if (!item.IsFolder && !string.IsNullOrEmpty(item.Url))
            {
                LoadListItemFaviconAsync(listItem, item.Url);
            }
        }
    }
    
    private async void LoadListItemFaviconAsync(ListViewItem listItem, string url)
    {
        try
        {
            var icon = await Helpers.FaviconHelper.GetFaviconAsync(url);
            if (icon != null && !_listView.IsDisposed && _listView.Items.Contains(listItem))
            {
                var key = $"url_{url.GetHashCode()}";
                if (!_listImageList.Images.ContainsKey(key))
                {
                    _listImageList.Images.Add(key, icon);
                }
                BeginInvoke(() =>
                {
                    if (_listView.Items.Contains(listItem))
                        listItem.ImageKey = key;
                });
            }
        }
        catch { }
    }
    
    private void ListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _isUpdating = true;
        if (_listView.SelectedItems.Count > 0)
        {
            _selectedBookmark = _listView.SelectedItems[0].Tag as Bookmark;
            if (_selectedBookmark != null)
            {
                _txtName.Text = _selectedBookmark.Title;
                _txtUrl.Text = _selectedBookmark.Url ?? "";
                _txtUrl.Enabled = !_selectedBookmark.IsFolder;
            }
        }
        else
        {
            _selectedBookmark = null;
            var node = _treeView.SelectedNode;
            _txtName.Text = node?.Text ?? "";
            _txtUrl.Text = "";
            _txtUrl.Enabled = false;
        }
        _isUpdating = false;
    }
    
    private void ListView_DoubleClick(object? sender, EventArgs e)
    {
        if (_selectedBookmark != null)
        {
            if (_selectedBookmark.IsFolder)
                SelectFolderInTree(_selectedBookmark.Id);
            else
                OpenSelectedBookmark();
        }
    }
    
    private void ListView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelectedBookmark();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            OpenSelectedBookmark();
            e.Handled = true;
        }
    }

    
    private void TxtName_Leave(object? sender, EventArgs e)
    {
        SaveNameChange();
    }
    
    private void TxtUrl_Leave(object? sender, EventArgs e)
    {
        SaveUrlChange();
    }
    
    private void SaveNameChange()
    {
        if (_isUpdating || _selectedBookmark == null) return;
        if (_selectedBookmark.Title == _txtName.Text) return;
        
        _bookmarkService.UpdateBookmark(_selectedBookmark.Id, _txtName.Text);
        
        foreach (ListViewItem item in _listView.Items)
        {
            if (item.Tag == _selectedBookmark)
            {
                item.Text = _txtName.Text;
                break;
            }
        }
    }
    
    private void SaveUrlChange()
    {
        if (_isUpdating || _selectedBookmark == null || _selectedBookmark.IsFolder) return;
        if (_selectedBookmark.Url == _txtUrl.Text) return;
        
        _bookmarkService.UpdateBookmark(_selectedBookmark.Id, null, _txtUrl.Text);
        
        foreach (ListViewItem item in _listView.Items)
        {
            if (item.Tag == _selectedBookmark)
            {
                item.SubItems[1].Text = _txtUrl.Text;
                // 重新加载图标
                LoadListItemFaviconAsync(item, _txtUrl.Text);
                break;
            }
        }
    }
    
    private void OpenSelectedBookmark()
    {
        if (_selectedBookmark != null && !_selectedBookmark.IsFolder && !string.IsNullOrEmpty(_selectedBookmark.Url))
        {
            _onNavigate?.Invoke(_selectedBookmark.Url);
            Close();
        }
    }
    
    private void EditSelectedBookmark()
    {
        if (_selectedBookmark != null)
        {
            using var dialog = new EditBookmarkDialog(_bookmarkService, _selectedBookmark);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadBookmarkList(_selectedFolderId);
            }
        }
    }
    
    private void DeleteSelectedBookmark()
    {
        if (_selectedBookmark != null)
        {
            var msg = _selectedBookmark.IsFolder 
                ? $"确定要删除文件夹 \"{_selectedBookmark.Title}\" 及其所有内容吗？" 
                : $"确定要删除 \"{_selectedBookmark.Title}\" 吗？";
            
            if (MessageBox.Show(msg, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _bookmarkService.Delete(_selectedBookmark.Id);
                _selectedBookmark = null;
                LoadBookmarkList(_selectedFolderId);
            }
        }
    }
    
    private void BtnNewFolder_Click(object? sender, EventArgs e)
    {
        using var inputDialog = new Form
        {
            Text = "新建文件夹",
            Size = new Size(300, 130),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        
        var lblName = new Label { Text = "名称:", Location = new Point(15, 20), AutoSize = true };
        var txtName = new TextBox { Text = "新建文件夹", Location = new Point(60, 17), Width = 210 };
        txtName.SelectAll();
        
        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(110, 55), Width = 75 };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(195, 55), Width = 75 };
        
        inputDialog.Controls.AddRange(new Control[] { lblName, txtName, btnOk, btnCancel });
        inputDialog.AcceptButton = btnOk;
        inputDialog.CancelButton = btnCancel;
        
        if (inputDialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txtName.Text))
        {
            _bookmarkService.AddFolder(txtName.Text.Trim(), _selectedFolderId);
            LoadFolderTree();
            SelectFolderInTree(_selectedFolderId);
            LoadBookmarkList(_selectedFolderId);
        }
    }
    
    private void BtnExport_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "导出收藏",
            Filter = "HTML 文件 (*.html)|*.html",
            FileName = "bookmarks.html",
            DefaultExt = "html"
        };
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ExportToHtml(dialog.FileName);
                MessageBox.Show("导出成功！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    
    private void ExportToHtml(string filePath)
    {
        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        writer.WriteLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        writer.WriteLine("<TITLE>Bookmarks</TITLE>");
        writer.WriteLine("<H1>Bookmarks</H1>");
        writer.WriteLine("<DL><p>");
        ExportFolder(writer, null, 1);
        writer.WriteLine("</DL><p>");
    }
    
    private void ExportFolder(StreamWriter writer, string? parentId, int indent)
    {
        var items = parentId == null ? _bookmarkService.GetBookmarkBarItems() : _bookmarkService.GetChildren(parentId);
        var indentStr = new string(' ', indent * 4);
        
        foreach (var item in items)
        {
            if (item.IsFolder)
            {
                writer.WriteLine($"{indentStr}<DT><H3>{System.Web.HttpUtility.HtmlEncode(item.Title)}</H3>");
                writer.WriteLine($"{indentStr}<DL><p>");
                ExportFolder(writer, item.Id, indent + 1);
                writer.WriteLine($"{indentStr}</DL><p>");
            }
            else
            {
                writer.WriteLine($"{indentStr}<DT><A HREF=\"{System.Web.HttpUtility.HtmlEncode(item.Url)}\">{System.Web.HttpUtility.HtmlEncode(item.Title)}</A>");
            }
        }
    }
}
