using MiniWorldBrowser.Helpers.Extensions;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 收藏栏控件
/// </summary>
public class BookmarkBar : Panel
{
    private readonly IBookmarkService _bookmarkService;
    private readonly FlowLayoutPanel _container;
    private readonly Button _overflowBtn;
    private readonly BookmarkButton _otherBookmarksBtn;
    private readonly ContextMenuStrip _overflowMenu;
    private ContextMenuStrip? _folderMenu;
    private ContextMenuStrip? _activeContextMenu;
    private bool _isShowingContextMenu = false;
    private bool _isIncognito = false;

    public bool IsIncognito
    {
        get => _isIncognito;
        set
        {
            _isIncognito = value;
            _otherBookmarksBtn.IsIncognito = value;
            _overflowMenu.Renderer = new ModernMenuRenderer(value);
            RefreshBookmarks();
        }
    }
    
    public event Action<string>? BookmarkClicked;
    public event Action<string, bool>? BookmarkMiddleClicked;
    public event Action? AddBookmarkRequested;
    
    /// <summary>
    /// 关闭所有下拉菜单和右键菜单
    /// </summary>
    public void CloseDropdowns()
    {
        // 强制关闭右键菜单
        if (_activeContextMenu != null)
        {
            try
            {
                _activeContextMenu.Hide();
                _activeContextMenu.Close();
                _activeContextMenu.Dispose();
            }
            catch { }
            _activeContextMenu = null;
        }
        
        // 关闭文件夹下拉菜单
        if (_folderMenu != null)
        {
            _folderMenu.AutoClose = true;
            _folderMenu.Close();
        }
        
        _overflowMenu?.Close();
        _isShowingContextMenu = false;
    }
    
    public BookmarkBar(IBookmarkService bookmarkService)
    {
        _bookmarkService = bookmarkService;
        _bookmarkService.BookmarksChanged += RefreshBookmarks;
        
        Height = DpiHelper.Scale(40);
        Dock = DockStyle.Top;
        BackColor = Color.FromArgb(245, 245, 245);
        Padding = DpiHelper.Scale(new Padding(4, 6, 4, 6));
        
        _container = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            BackColor = Color.Transparent
        };
        
        // 其他收藏按钮（右侧）
        _otherBookmarksBtn = new BookmarkButton
        {
            Text = Localization.T("bookmarks.other"),
            IsFolder = true,
            Icon = CreateFolderIcon(),
            Dock = DockStyle.Right,
            Visible = false,
            Margin = DpiHelper.Scale(new Padding(2, 0, 2, 0))
        };
        _otherBookmarksBtn.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OnOtherBookmarksClick(s, e);
        };
        
        _overflowBtn = new Button
        {
            Text = "»",
            Size = DpiHelper.Scale(new Size(20, 20)),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", DpiHelper.ScaleFont(9F), FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false,
            Dock = DockStyle.Right
        };
        _overflowBtn.FlatAppearance.BorderSize = 0;
        _overflowBtn.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
        _overflowBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 220, 220);
        _overflowBtn.Click += OnOverflowClick;
        
        _overflowMenu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(_isIncognito) };
        
        // 按照正确的停靠顺序添加：先添加 Dock != Fill 的，最后添加 Dock = Fill 的
        Controls.Add(_overflowBtn);
        Controls.Add(_otherBookmarksBtn);
        Controls.Add(_container);
        
        _container.Resize += (s, e) => UpdateOverflow();
        
        RefreshBookmarks();
    }
    
    public void RefreshBookmarks()
    {
        _container.Controls.Clear();
        var items = _bookmarkService.GetBookmarkBarItems();
        
        foreach (var item in items)
        {
            var btn = CreateBookmarkButton(item);
            _container.Controls.Add(btn);
        }
        
        // 更新"其他收藏"按钮的可见性
        var otherItems = _bookmarkService.GetOtherBookmarks();
        _otherBookmarksBtn.Visible = otherItems.Count > 0;
        
        UpdateOverflow();
    }
    
    private void OnOtherBookmarksClick(object? sender, EventArgs e)
    {
        _folderMenu?.Dispose();
        _folderMenu = new ContextMenuStrip { AutoClose = false, Renderer = new ModernMenuRenderer(_isIncognito) };
        _folderMenu.Closing += OnFolderMenuClosing;
        
        var otherItems = _bookmarkService.GetOtherBookmarks();
            if (otherItems.Count == 0)
            {
                _folderMenu.Items.Add(Localization.T("common.empty")).Enabled = false;
            }
        else
        {
            foreach (var child in otherItems)
            {
                if (child.IsFolder)
                {
                    var subMenu = new ToolStripMenuItem("📁 " + child.Title);
                    PopulateFolderMenuWithContextMenu(subMenu, child.Id);
                    AddFolderContextMenu(subMenu, child);
                    _folderMenu.Items.Add(subMenu);
                }
                else
                {
                    var item = new ToolStripMenuItem(child.Title);
                    item.Image = Helpers.FaviconHelper.GetCachedFavicon(child.Url);
                    LoadMenuItemFaviconAsync(item, child.Url);
                    item.Click += (s, ev) => 
                    {
                        if (Control.MouseButtons != MouseButtons.Right)
                            BookmarkClicked?.Invoke(child.Url);
                    };
                    item.ToolTipText = child.Url;
                    AddBookmarkContextMenu(item, child);
                    _folderMenu.Items.Add(item);
                }
            }
        }
        
        _folderMenu.Show(_otherBookmarksBtn, new Point(0, _otherBookmarksBtn.Height));
    }
    
    private void OnFolderMenuClosing(object? sender, ToolStripDropDownClosingEventArgs e)
    {
        // 如果正在显示右键菜单，阻止下拉菜单关闭
        if (_isShowingContextMenu && e.CloseReason == ToolStripDropDownCloseReason.AppClicked)
        {
            e.Cancel = true;
        }
    }
    
    private void AddBookmarkContextMenu(ToolStripMenuItem menuItem, Bookmark bookmark)
    {
        menuItem.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowBookmarkRightClickMenu(bookmark);
            }
        };
    }
    
    private void AddFolderContextMenu(ToolStripMenuItem menuItem, Bookmark folder)
    {
        menuItem.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowFolderRightClickMenu(folder);
            }
        };
    }
    
    private void ShowBookmarkRightClickMenu(Bookmark bookmark)
    {
        // 先关闭并释放之前的右键菜单
        if (_activeContextMenu != null)
        {
            try
            {
                _activeContextMenu.Hide();
                _activeContextMenu.Dispose();
            }
            catch { }
            _activeContextMenu = null;
        }
        
        _isShowingContextMenu = true;
        
        _activeContextMenu = new ContextMenuStrip { AutoClose = true, Renderer = new ModernMenuRenderer(_isIncognito) };
        _activeContextMenu.Items.Add(Localization.T("bookmark_menu.open"), null, (cs, ce) => { CloseAllMenus(); BookmarkClicked?.Invoke(bookmark.Url); });
        _activeContextMenu.Items.Add(Localization.T("bookmark_menu.open_in_new_tab"), null, (cs, ce) => { CloseAllMenus(); BookmarkMiddleClicked?.Invoke(bookmark.Url, true); });
        _activeContextMenu.Items.Add(new ToolStripSeparator());
        _activeContextMenu.Items.Add(Localization.T("actions.edit"), null, (cs, ce) => { CloseAllMenus(); EditBookmark(bookmark); });
        _activeContextMenu.Items.Add(Localization.T("actions.delete"), null, (cs, ce) => { CloseAllMenus(); _bookmarkService.Delete(bookmark.Id); });
        _activeContextMenu.Items.Add(new ToolStripSeparator());
        _activeContextMenu.Items.Add(Localization.T("actions.copy_link"), null, (cs, ce) => { Clipboard.SetText(bookmark.Url); });
        _activeContextMenu.Closed += OnContextMenuClosed;
        
        // 直接在鼠标位置显示
        _activeContextMenu.Show(Cursor.Position);
    }
    
    private void ShowFolderRightClickMenu(Bookmark folder)
    {
        // 先关闭并释放之前的右键菜单
        if (_activeContextMenu != null)
        {
            try
            {
                _activeContextMenu.Hide();
                _activeContextMenu.Dispose();
            }
            catch { }
            _activeContextMenu = null;
        }
        
        _isShowingContextMenu = true;
        
        _activeContextMenu = new ContextMenuStrip { AutoClose = true, Renderer = new ModernMenuRenderer(_isIncognito) };
        _activeContextMenu.Items.Add(Localization.T("bookmark_menu.open_all_bookmarks"), null, (cs, ce) => { CloseAllMenus(); OpenAllInFolder(folder.Id); });
        _activeContextMenu.Items.Add(new ToolStripSeparator());
        _activeContextMenu.Items.Add(Localization.T("actions.rename"), null, (cs, ce) => { CloseAllMenus(); EditBookmark(folder); });
        _activeContextMenu.Items.Add(Localization.T("actions.delete"), null, (cs, ce) => { CloseAllMenus(); DeleteWithConfirm(folder); });
        _activeContextMenu.Closed += OnContextMenuClosed;
        
        // 直接在鼠标位置显示
        _activeContextMenu.Show(Cursor.Position);
    }
    
    private void OnContextMenuClosed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        _isShowingContextMenu = false;
        // 右键菜单关闭后，恢复下拉菜单的自动关闭功能
        if (_folderMenu != null)
        {
            _folderMenu.AutoClose = true;
        }
    }
    
    private void CloseAllMenus()
    {
        _isShowingContextMenu = false;
        _activeContextMenu?.Close();
        if (_folderMenu != null)
        {
            _folderMenu.AutoClose = true;
            _folderMenu.Close();
        }
    }
    
    private async void LoadMenuItemFaviconAsync(ToolStripMenuItem menuItem, string url)
    {
        try
        {
            var icon = await Helpers.FaviconHelper.GetFaviconAsync(url);
            if (icon != null && !menuItem.IsDisposed)
            {
                menuItem.Image = icon;
            }
        }
        catch { }
    }
    
    private Control CreateBookmarkButton(Bookmark bookmark)
    {
        var btn = new BookmarkButton
        {
            Text = bookmark.Title.Truncate(14),
            Tag = bookmark,
            IsFolder = bookmark.IsFolder,
            IsIncognito = _isIncognito, // 应用隐身模式
            Margin = DpiHelper.Scale(new Padding(2, 0, 2, 0))
        };
        
        // 设置图标
        if (bookmark.IsFolder)
        {
            btn.Icon = CreateFolderIcon();
            btn.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ShowFolderMenu(btn, bookmark);
            };
        }
        else
        {
            // 异步加载 favicon
            btn.Icon = Helpers.FaviconHelper.GetCachedFavicon(bookmark.Url);
            LoadFaviconAsync(btn, bookmark.Url);
            
            btn.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    BookmarkClicked?.Invoke(bookmark.Url);
            };
            btn.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Middle)
                    BookmarkMiddleClicked?.Invoke(bookmark.Url, true);
            };
        }
        
        btn.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
                ShowContextMenu(btn, bookmark, e.Location);
        };
        
        var tip = new ToolTip();
        tip.SetToolTip(btn, bookmark.IsFolder ? bookmark.Title : $"{bookmark.Title}\n{bookmark.Url}");
        
        return btn;
    }
    
    private async void LoadFaviconAsync(BookmarkButton btn, string url)
    {
        try
        {
            var icon = await Helpers.FaviconHelper.GetFaviconAsync(url);
            if (icon != null && !btn.IsDisposed && btn.IsHandleCreated)
            {
                try
                {
                    btn.Invoke(() => 
                    { 
                        if (!btn.IsDisposed)
                        {
                            btn.Icon = icon; 
                            btn.Invalidate(); 
                        }
                    });
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }
        }
        catch { }
    }
    
    private static Image CreateFolderIcon()
    {
        int size = DpiHelper.Scale(16);
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(255, 193, 7));
        
        // 比例缩放绘制
        float ratio = size / 16f;
        g.FillRectangle(brush, 1 * ratio, 4 * ratio, 14 * ratio, 10 * ratio);
        g.FillRectangle(brush, 1 * ratio, 2 * ratio, 6 * ratio, 3 * ratio);
        return bmp;
    }
    
    private void ShowFolderMenu(Control btn, Bookmark folder)
    {
        _folderMenu?.Dispose();
        _folderMenu = new ContextMenuStrip { AutoClose = false, Renderer = new ModernMenuRenderer(_isIncognito) };
        _folderMenu.Closing += OnFolderMenuClosing;
        
        var children = _bookmarkService.GetChildren(folder.Id);
        if (children.Count == 0)
        {
            _folderMenu.Items.Add(Localization.T("common.empty")).Enabled = false;
        }
        else
        {
            foreach (var child in children)
            {
                if (child.IsFolder)
                {
                    var subMenu = new ToolStripMenuItem("📁 " + child.Title);
                    PopulateFolderMenuWithContextMenu(subMenu, child.Id);
                    AddFolderContextMenu(subMenu, child);
                    _folderMenu.Items.Add(subMenu);
                }
                else
                {
                    var item = new ToolStripMenuItem(child.Title);
                    item.Image = Helpers.FaviconHelper.GetCachedFavicon(child.Url);
                    LoadMenuItemFaviconAsync(item, child.Url);
                    item.Click += (s, e) => 
                    {
                        if (Control.MouseButtons != MouseButtons.Right)
                            BookmarkClicked?.Invoke(child.Url);
                    };
                    item.ToolTipText = child.Url;
                    AddBookmarkContextMenu(item, child);
                    _folderMenu.Items.Add(item);
                }
            }
        }
        
        _folderMenu.Items.Add(new ToolStripSeparator());
        var openAll = new ToolStripMenuItem("打开所有书签");
        openAll.Click += (s, e) => { _folderMenu.AutoClose = true; _folderMenu.Close(); OpenAllInFolder(folder.Id); };
        _folderMenu.Items.Add(openAll);
        
        _folderMenu.Show(btn, new Point(0, btn.Height));
    }
    
    private void PopulateFolderMenu(ToolStripMenuItem menu, string folderId)
    {
        var children = _bookmarkService.GetChildren(folderId);
        if (children.Count == 0)
        {
            menu.DropDownItems.Add(Localization.T("common.empty")).Enabled = false;
            return;
        }
        
        foreach (var child in children)
        {
            if (child.IsFolder)
            {
                var subMenu = new ToolStripMenuItem("📁 " + child.Title);
                PopulateFolderMenu(subMenu, child.Id);
                menu.DropDownItems.Add(subMenu);
            }
            else
            {
                var item = new ToolStripMenuItem(child.Title);
                item.Click += (s, e) => 
                {
                    if (Control.MouseButtons != MouseButtons.Right)
                        BookmarkClicked?.Invoke(child.Url);
                };
                menu.DropDownItems.Add(item);
            }
        }
    }
    
    private void PopulateFolderMenuWithContextMenu(ToolStripMenuItem menu, string folderId)
    {
        var children = _bookmarkService.GetChildren(folderId);
        if (children.Count == 0)
        {
            menu.DropDownItems.Add(Localization.T("common.empty")).Enabled = false;
            return;
        }
        
        foreach (var child in children)
        {
            if (child.IsFolder)
            {
                var subMenu = new ToolStripMenuItem("📁 " + child.Title);
                PopulateFolderMenuWithContextMenu(subMenu, child.Id);
                AddFolderContextMenu(subMenu, child);
                menu.DropDownItems.Add(subMenu);
            }
            else
                {
                    var item = new ToolStripMenuItem(child.Title);
                    item.Image = Helpers.FaviconHelper.GetCachedFavicon(child.Url);
                    LoadMenuItemFaviconAsync(item, child.Url);
                    item.Click += (s, e) => 
                    {
                        if (Control.MouseButtons != MouseButtons.Right)
                            BookmarkClicked?.Invoke(child.Url);
                    };
                    item.ToolTipText = child.Url;
                    AddBookmarkContextMenu(item, child);
                    menu.DropDownItems.Add(item);
                }
        }
    }
    
    private void OpenAllInFolder(string folderId)
    {
        var children = _bookmarkService.GetChildren(folderId);
        foreach (var child in children.Where(c => !c.IsFolder))
        {
            BookmarkMiddleClicked?.Invoke(child.Url, true);
        }
    }
    
    private void ShowContextMenu(Control anchor, Bookmark bookmark, Point location)
    {
        var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(_isIncognito) };
        
        if (bookmark.IsFolder)
        {
            menu.Items.Add(Localization.T("bookmark_menu.open_all_bookmarks"), null, (s, e) => OpenAllInFolder(bookmark.Id));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Localization.T("actions.rename"), null, (s, e) => EditBookmark(bookmark));
            menu.Items.Add(Localization.T("actions.delete"), null, (s, e) => DeleteWithConfirm(bookmark));
        }
        else
        {
            menu.Items.Add(Localization.T("bookmark_menu.open"), null, (s, e) => BookmarkClicked?.Invoke(bookmark.Url));
            menu.Items.Add(Localization.T("bookmark_menu.open_in_new_tab"), null, (s, e) => BookmarkMiddleClicked?.Invoke(bookmark.Url, true));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Localization.T("actions.edit"), null, (s, e) => EditBookmark(bookmark));
            menu.Items.Add(Localization.T("actions.delete"), null, (s, e) => _bookmarkService.Delete(bookmark.Id));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Localization.T("actions.copy_link"), null, (s, e) => Clipboard.SetText(bookmark.Url));
        }
        
        menu.Show(anchor, location);
    }
    
    private void EditBookmark(Bookmark bookmark)
    {
        using var dlg = new BookmarkEditDialog(bookmark);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _bookmarkService.UpdateBookmark(bookmark.Id, dlg.BookmarkTitle, dlg.BookmarkUrl);
        }
    }
    
    private void DeleteWithConfirm(Bookmark folder)
    {
        var result = MessageBox.Show(
            Localization.T("bookmark_manager.confirm_delete_folder", new Dictionary<string, string> { { "name", folder.Title } }),
            Localization.T("confirm.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        
        if (result == DialogResult.Yes)
            _bookmarkService.Delete(folder.Id);
    }
    
    private void OnOverflowClick(object? sender, EventArgs e)
    {
        _overflowMenu.Items.Clear();
        
        foreach (Control ctrl in _container.Controls)
        {
            if (ctrl.Right > _container.Width - DpiHelper.Scale(25))
            {
                var bookmark = ctrl.Tag as Bookmark;
                if (bookmark == null) continue;
                
                if (bookmark.IsFolder)
                {
                    var subMenu = new ToolStripMenuItem("📁 " + bookmark.Title);
                    PopulateFolderMenu(subMenu, bookmark.Id);
                    _overflowMenu.Items.Add(subMenu);
                }
                else
                {
                    var item = new ToolStripMenuItem(bookmark.Title);
                    item.Click += (s, ev) => 
                    {
                        if (Control.MouseButtons != MouseButtons.Right)
                            BookmarkClicked?.Invoke(bookmark.Url);
                    };
                    _overflowMenu.Items.Add(item);
                }
            }
        }
        
        if (_overflowMenu.Items.Count > 0)
            _overflowMenu.Show(_overflowBtn, new Point(0, _overflowBtn.Height));
    }
    
    private void UpdateOverflow()
    {
        bool hasOverflow = false;
        int overflowThreshold = DpiHelper.Scale(25);
        foreach (Control ctrl in _container.Controls)
        {
            bool visible = ctrl.Right <= _container.Width - overflowThreshold;
            ctrl.Visible = visible;
            if (!visible) hasOverflow = true;
        }
        _overflowBtn.Visible = hasOverflow;
    }
    
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
        {
            var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(_isIncognito) };
            menu.Items.Add("添加书签", null, (s, ev) => AddBookmarkRequested?.Invoke());
            menu.Items.Add("添加文件夹", null, (s, ev) => AddFolder());
            menu.Show(this, e.Location);
        }
    }
    
    private void AddFolder()
    {
        using var dlg = new BookmarkEditDialog(null, true);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _bookmarkService.AddFolder(dlg.BookmarkTitle);
        }
    }
}

/// <summary>
/// 自定义书签按钮控件 - 支持图标和文字垂直居中对齐
/// </summary>
public class BookmarkButton : Control
{
    private Image? _icon;
    private bool _isHovered;
    private bool _isIncognito;

    public bool IsIncognito
    {
        get => _isIncognito;
        set { _isIncognito = value; Invalidate(); }
    }

    public Image? Icon
    {
        get => _icon;
        set { _icon = value; UpdateWidth(); Invalidate(); }
    }
    public bool IsFolder { get; set; }

    public BookmarkButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", DpiHelper.ScaleFont(9F));
        Height = DpiHelper.Scale(28);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        UpdateWidth();
    }

    private void UpdateWidth()
    {
        int iconSize = DpiHelper.Scale(16);
        int padding = DpiHelper.Scale(12); // 进一步增加总内边距
        int iconTextGap = DpiHelper.Scale(4); // 减小图标文字间距

        int width = padding; // 左边距
        if (_icon != null)
            width += iconSize + iconTextGap;

        if (!string.IsNullOrEmpty(Text))
        {
            // 使用 TextRenderer 测量，并指定没有边距的标志
            var size = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            width += size.Width + DpiHelper.Scale(2); // 额外增加 2 像素缓冲，防止渲染截断
        }
        width += padding; // 右边距

        if (Width != width)
        {
            Width = width;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 绘制背景 - 悬停时显示圆角背景
        if (_isHovered)
        {
            Color hoverColor;
            if (_isIncognito)
            {
                hoverColor = Color.FromArgb(80, 255, 255, 255);
            }
            else
            {
                hoverColor = Color.FromArgb(20, 0, 0, 0);
            }

            using var hoverBrush = new SolidBrush(hoverColor);
            var rect = new Rectangle(0, DpiHelper.Scale(2), Width, Height - DpiHelper.Scale(4));
            using var path = CreateRoundedRectangle(rect, DpiHelper.Scale(4));
            g.FillPath(hoverBrush, path);
        }

        int iconSize = DpiHelper.Scale(16);
        int padding = DpiHelper.Scale(12);
        int iconTextGap = DpiHelper.Scale(4);

        int x = padding;
        int centerY = (Height - iconSize) / 2;

        // 绘制图标
        if (_icon != null)
        {
            try
            {
                if (_icon.Width > 0 && _icon.Height > 0)
                {
                    g.DrawImage(_icon, x, centerY, iconSize, iconSize);
                }
            }
            catch { }
            x += iconSize + iconTextGap;
        }

        // 绘制文字
        if (!string.IsNullOrEmpty(Text))
        {
            Color textColor = _isIncognito ? Color.FromArgb(240, 240, 240) : Color.FromArgb(32, 32, 32);
            // 使用 TextRenderer 绘制，并指定相同的 TextFormatFlags
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Left;
            // 确保绘制区域足够大
            var textRect = new Rectangle(x, 0, Width - x - padding + DpiHelper.Scale(2), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, textColor, flags);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }
}

/// <summary>
/// 书签编辑对话框
/// </summary>
public class BookmarkEditDialog : Form
{
    private readonly TextBox _titleBox;
    private readonly TextBox? _urlBox;
    
    public string BookmarkTitle => _titleBox.Text;
    public string BookmarkUrl => _urlBox?.Text ?? "";
    
    public BookmarkEditDialog(Bookmark? bookmark, bool isFolder = false)
    {
        Text = Localization.Raw();
        Size = DpiHelper.Scale(new Size(400, isFolder ? 120 : 160));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));
        
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = DpiHelper.Scale(new Padding(10)),
            RowCount = isFolder ? 2 : 3,
            ColumnCount = 2
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DpiHelper.Scale(60)));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        
        panel.Controls.Add(new Label { Text = Localization.Raw(), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _titleBox = new TextBox { Dock = DockStyle.Fill, Text = bookmark?.Title ?? "" };
        panel.Controls.Add(_titleBox, 1, 0);
        
        if (!isFolder)
        {
            panel.Controls.Add(new Label { Text = Localization.Raw(), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _urlBox = new TextBox { Dock = DockStyle.Fill, Text = bookmark?.Url ?? "" };
            panel.Controls.Add(_urlBox, 1, 1);
        }
        
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var cancelBtn = new Button { Text = Localization.T("confirm.cancel"), DialogResult = DialogResult.Cancel };
        var okBtn = new Button { Text = Localization.T("confirm.ok"), DialogResult = DialogResult.OK };
        btnPanel.Controls.Add(cancelBtn);
        btnPanel.Controls.Add(okBtn);
        panel.Controls.Add(btnPanel, 1, isFolder ? 1 : 2);
        
        Controls.Add(panel);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }
}
