using MiniWorldBrowser.Browser;
using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Helpers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.IO;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// MainForm - 菜单和书签部分
/// </summary>
public partial class MainForm
{
    #region 主菜单

    private System.Windows.Forms.Timer? _menuCloseTimer;
    private Panel? _zoomPanel;
    
    // 缩放弹窗相关
    private Panel? _zoomPopup;
    private Label? _zoomPopupLabel;
    private System.Windows.Forms.Timer? _zoomPopupTimer;

    private void CloseMainMenu()
    {
        _reopenMenuAfterZoom = false; // 确保关闭菜单时不会因为之前的缩放操作而重新打开
        StopMenuCloseTimer();
        if (_mainMenu != null && _mainMenu.Visible)
        {
            _mainMenu.AutoClose = true;
            _mainMenu.Close();
        }
    }

    private void ClosePopups()
    {
        CloseMainMenu();
        _addressDropdown?.Hide();
        CloseDownloadDialog();
        _bookmarkBar?.CloseDropdowns();
        _tabOverflowPanel?.HidePanel();
        CloseAISidePanel();
        
        CloseUserInfoPopup();
    }

    private void StartMenuCloseTimer()
    {
        StopMenuCloseTimer();
        _menuCloseTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _menuCloseTimer.Tick += OnMenuCloseTimerTick;
        _menuCloseTimer.Start();
    }

    private void StopMenuCloseTimer()
    {
        if (_menuCloseTimer != null)
        {
            _menuCloseTimer.Stop();
            _menuCloseTimer.Dispose();
            _menuCloseTimer = null;
        }
    }

    private bool _isMouseDownInMenu = false;
    private Point _lastMouseDownPos = Point.Empty;
    private bool _reopenMenuAfterZoom = false;   // 标记是否需要在缩放后重新打开菜单

    private void OnMenuCloseTimerTick(object? sender, EventArgs e)
    {
        if (_mainMenu == null || !_mainMenu.Visible)
        {
            StopMenuCloseTimer();
            return;
        }

        // 如果需要重新打开菜单，不关闭
        if (_reopenMenuAfterZoom)
            return;

        var mousePos = Control.MousePosition;
        var isMouseDown = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;

        // 检查鼠标是否在菜单区域内（包括缩放面板）
        bool inMenuArea = IsMouseInMenuArea(mousePos);

        if (isMouseDown && !_isMouseDownInMenu)
        {
            _lastMouseDownPos = mousePos;
            _isMouseDownInMenu = true;
            _lastMouseDownInMenuArea = inMenuArea;
            
            // 如果在菜单区域外按下鼠标，立即关闭菜单（用于拖动窗口等操作）
            if (!inMenuArea)
            {
                CloseMainMenu();
                return;
            }
        }

        if (!isMouseDown && _isMouseDownInMenu)
        {
            _isMouseDownInMenu = false;
        }
    }
    
    private bool _lastMouseDownInMenuArea = false;

    private bool IsMouseInMenuArea(Point screenPos)
    {
        if (_mainMenu == null) return false;

        var menuBounds = _mainMenu.Bounds;
        menuBounds.Inflate(5, 5);
        if (menuBounds.Contains(screenPos))
            return true;

        // 检查缩放面板 - 使用IsHandleCreated而不是Visible
        if (_zoomPanel != null && _zoomPanel.IsHandleCreated)
        {
            try
            {
                var panelScreen = _zoomPanel.PointToScreen(Point.Empty);
                var panelBounds = new Rectangle(panelScreen, _zoomPanel.Size);
                panelBounds.Inflate(5, 5);
                if (panelBounds.Contains(screenPos))
                    return true;
            }
            catch { }
        }

        if (CheckDropDownMenus(_mainMenu.Items, screenPos))
            return true;

        return false;
    }

    private bool CheckDropDownMenus(ToolStripItemCollection items, Point screenPos)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripMenuItem menuItem && menuItem.DropDown.Visible)
            {
                var bounds = menuItem.DropDown.Bounds;
                bounds.Inflate(5, 5);
                if (bounds.Contains(screenPos))
                    return true;

                if (CheckDropDownMenus(menuItem.DropDown.Items, screenPos))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 创建带图标的菜单项
    /// </summary>
    private ToolStripMenuItem CreateMenuItem(string text, string? shortcut, Action<Graphics, Rectangle>? iconDrawer, Action? onClick = null)
    {
        var item = new ToolStripMenuItem(text)
        {
            ShortcutKeyDisplayString = shortcut,
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };

        if (iconDrawer != null)
        {
            // 创建图标图像
            int iconSize = DpiHelper.Scale(20);
            var iconBitmap = new Bitmap(iconSize, iconSize);
            using (var g = Graphics.FromImage(iconBitmap))
            {
                g.Clear(Color.Transparent);
                iconDrawer(g, new Rectangle(0, 0, iconSize, iconSize));
            }

            // 如果是隐身模式，将图标颜色转换为白色
            if (_isIncognito)
            {
                var newBitmap = new Bitmap(iconSize, iconSize);
                using (var g = Graphics.FromImage(newBitmap))
                {
                    // 将所有非透明像素转换为白色
                    var matrix = new System.Drawing.Imaging.ColorMatrix(new[]
                    {
                        new float[] { 0, 0, 0, 0, 0 }, // R 乘数
                        new float[] { 0, 0, 0, 0, 0 }, // G 乘数
                        new float[] { 0, 0, 0, 0, 0 }, // B 乘数
                        new float[] { 0, 0, 0, 1, 0 }, // A 乘数 (保持原样)
                        new float[] { 1, 1, 1, 0, 1 }  // 偏移量 (R,G,B 都加 1，即变成白色)
                    });
                    
                    var attributes = new System.Drawing.Imaging.ImageAttributes();
                    attributes.SetColorMatrix(matrix);
                    
                    g.DrawImage(iconBitmap, new Rectangle(0, 0, iconSize, iconSize),
                        0, 0, iconSize, iconSize, GraphicsUnit.Pixel, attributes);
                }
                iconBitmap.Dispose();
                iconBitmap = newBitmap;
            }

            item.Image = iconBitmap;
            item.ImageScaling = ToolStripItemImageScaling.None;
        }

        if (onClick != null)
            item.Click += (s, e) => onClick();

        return item;
    }

    private void ShowMainMenu()
    {
        _mainMenu?.Close();
        
        // 重置状态
        _isMouseDownInMenu = false;
        _lastMouseDownInMenuArea = false;
        
        _mainMenu = new ContextMenuStrip
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            AutoClose = false,
            BackColor = _isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249),
            ForeColor = _isIncognito ? Color.White : Color.Black,
            ShowImageMargin = true,
            ImageScalingSize = DpiHelper.Scale(new Size(20, 20)),
            Padding = DpiHelper.Scale(new Padding(0, 4, 0, 4))
        };
        var menu = _mainMenu;

        // 应用 Edge 风格渲染器
        menu.Renderer = new ModernMenuRenderer(_isIncognito);

        // 菜单关闭时的处理
        menu.Closed += (s, e) => 
        {
            StopMenuCloseTimer();
            // 如果需要重新打开菜单（点击了缩放按钮）
            if (_reopenMenuAfterZoom)
            {
                _reopenMenuAfterZoom = false;
                BeginInvoke(() => ShowMainMenu());
            }
        };

        // 新建标签页
        menu.Items.Add(CreateMenuItem("新建标签页(T)", "Ctrl+T", MenuIconDrawer.DrawNewTab,
            async () => { CloseMainMenu(); await CreateNewTabWithProtection("about:newtab"); }));

        // 新建窗口
        menu.Items.Add(CreateMenuItem("新建窗口(N)", "Ctrl+N", MenuIconDrawer.DrawNewWindow,
            () => { CloseMainMenu(); System.Diagnostics.Process.Start(Application.ExecutablePath); }));

        // 新建隐私窗口
        menu.Items.Add(CreateMenuItem("新建 InPrivate 窗口(I)", "Ctrl+Shift+N", MenuIconDrawer.DrawIncognito,
            () => { CloseMainMenu(); OpenIncognitoWindow(); }));

        menu.Items.Add(new ToolStripSeparator());

        // 缩放
        var zoomItem = CreateZoomMenuItem();
        menu.Items.Add(zoomItem);

        menu.Items.Add(new ToolStripSeparator());

        // 收藏夹
        var bookmarks = CreateMenuItem("收藏夹(B)", null, MenuIconDrawer.DrawBookmark);
        bookmarks.DropDownDirection = ToolStripDropDownDirection.Left;
        bookmarks.DropDown.Renderer = new ModernMenuRenderer(_isIncognito);
        bookmarks.DropDown.BackColor = _isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249);
        bookmarks.DropDown.ForeColor = _isIncognito ? Color.White : Color.Black;

        // 显示收藏栏 - 切换开关，不关闭菜单
        var showBar = new ToolStripMenuItem("显示收藏栏(S)")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+B",
            Checked = _settingsService.Settings.AlwaysShowBookmarkBar
        };
        showBar.Click += (s, e) => 
        {
            _settingsService.Settings.AlwaysShowBookmarkBar = !_settingsService.Settings.AlwaysShowBookmarkBar;
            _settingsService.Save();
            // 注意：Save() 会触发 SettingsChanged，进而触发 UpdateBookmarkBarVisibility()
            showBar.Checked = _settingsService.Settings.AlwaysShowBookmarkBar;
        };
        bookmarks.DropDownItems.Add(showBar);

        // 收藏夹管理器
        var bookmarkManager = new ToolStripMenuItem("收藏夹管理器(B)")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+O"
        };
        bookmarkManager.Click += (s, e) => { CloseMainMenu(); ShowBookmarkManager(); };
        bookmarks.DropDownItems.Add(bookmarkManager);

        // 导入收藏和设置
        var importBookmarks = new ToolStripMenuItem("导入收藏和设置...");
        importBookmarks.Click += (s, e) => { CloseMainMenu(); ImportBookmarks(); };
        bookmarks.DropDownItems.Add(importBookmarks);

        bookmarks.DropDownItems.Add(new ToolStripSeparator());

        // 为此网页添加收藏
        var addBookmark = new ToolStripMenuItem("为此网页添加收藏...")
        {
            ShortcutKeyDisplayString = "Ctrl+D"
        };
        addBookmark.Click += (s, e) => { CloseMainMenu(); ShowAddBookmarkDialog(); };
        bookmarks.DropDownItems.Add(addBookmark);

        // 为打开的网页添加收藏（批量收藏所有标签页）
        var addAllBookmarks = new ToolStripMenuItem("为打开的网页添加收藏...")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+D"
        };
        addAllBookmarks.Click += (s, e) => { CloseMainMenu(); AddAllTabsToBookmarks(); };
        bookmarks.DropDownItems.Add(addAllBookmarks);

        bookmarks.DropDownItems.Add(new ToolStripSeparator());

        // 收藏栏书签列表
        var barItems = _bookmarkService.GetBookmarkBarItems();
        if (barItems.Count > 0)
        {
            foreach (var item in barItems.Take(15))
            {
                var bmItem = new ToolStripMenuItem(item.IsFolder ? "📁 " + item.Title : item.Title);
                if (item.IsFolder)
                {
                    AddBookmarkFolderItems(bmItem, item.Id);
                }
                else
                {
                    bmItem.Image = Helpers.FaviconHelper.GetCachedFavicon(item.Url);
                    LoadMenuItemFaviconAsync(bmItem, item.Url, item.FaviconUrl);
                    var itemUrl = item.Url;
                    bmItem.Click += (s, e) => 
                    { 
                        if (Control.MouseButtons != MouseButtons.Right)
                        {
                            CloseMainMenu(); 
                            _tabManager.ActiveTab?.Navigate(itemUrl); 
                        }
                    };
                }
                bookmarks.DropDownItems.Add(bmItem);
            }
            
            if (barItems.Count > 15)
            {
                bookmarks.DropDownItems.Add(new ToolStripSeparator());
                var moreBookmarks = new ToolStripMenuItem($"更多收藏 ({barItems.Count - 15})...");
                moreBookmarks.Click += (s, e) => { CloseMainMenu(); ShowBookmarkManager(); };
                bookmarks.DropDownItems.Add(moreBookmarks);
            }
        }
        else
        {
            var emptyItem = new ToolStripMenuItem("暂无收藏") { Enabled = false };
            bookmarks.DropDownItems.Add(emptyItem);
        }

        menu.Items.Add(bookmarks);

        // 历史记录
        var history = CreateMenuItem("历史记录(H)", "Ctrl+H", MenuIconDrawer.DrawHistory);
        history.DropDownDirection = ToolStripDropDownDirection.Left;
        history.DropDown.Renderer = new ModernMenuRenderer(_isIncognito);
        history.DropDown.BackColor = _isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249);
        history.DropDown.ForeColor = _isIncognito ? Color.White : Color.Black;

        var showHistory = new ToolStripMenuItem("显示全部历史记录")
        {
            ShortcutKeyDisplayString = "Ctrl+H"
        };
        showHistory.Click += (s, e) => { CloseMainMenu(); _ = CreateNewTabWithProtection("about:settings"); };
        history.DropDownItems.Add(showHistory);

        history.DropDownItems.Add(new ToolStripSeparator());

        var recentHistory = _historyService.GetHistory(10);
        if (recentHistory.Count > 0)
        {
            foreach (var item in recentHistory)
            {
                var title = string.IsNullOrEmpty(item.Title) ? item.Url : item.Title;
                if (title.Length > 40) title = title[..40] + "...";
                var historyItem = new ToolStripMenuItem(title);
                var url = item.Url;
                historyItem.Image = Helpers.FaviconHelper.GetCachedFavicon(url);
                LoadMenuItemFaviconAsync(historyItem, url);
                historyItem.Click += (s, e) => { CloseMainMenu(); _tabManager.ActiveTab?.Navigate(url); };
                history.DropDownItems.Add(historyItem);
            }
        }
        else
        {
            var emptyItem = new ToolStripMenuItem("暂无历史记录") { Enabled = false };
            history.DropDownItems.Add(emptyItem);
        }

        history.DropDownItems.Add(new ToolStripSeparator());

        var clearHistory = new ToolStripMenuItem("清除浏览历史记录");
        clearHistory.Click += (s, e) =>
        {
            CloseMainMenu();
            if (MessageBox.Show("确定要清除所有历史记录吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _historyService.Clear();
                _statusLabel.Text = "历史记录已清除";
            }
        };
        history.DropDownItems.Add(clearHistory);

        menu.Items.Add(history);

        // 下载
        menu.Items.Add(CreateMenuItem("下载(D)", "Ctrl+J", MenuIconDrawer.DrawDownload,
            () => { CloseMainMenu(); OpenDownloadDialog(); }));

        // 媒体提取
        var mediaMenu = CreateMenuItem("媒体提取", null, MenuIconDrawer.DrawAi); // 借用 AI 图标或以后自定义
        mediaMenu.DropDownDirection = ToolStripDropDownDirection.Left;
        mediaMenu.DropDown.Renderer = new ModernMenuRenderer(_isIncognito);
        mediaMenu.DropDown.BackColor = _isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249);
        mediaMenu.DropDown.ForeColor = _isIncognito ? Color.White : Color.Black;

        mediaMenu.DropDownItems.Add(new ToolStripMenuItem("提取本页图片", null, (s, e) => { CloseMainMenu(); _ = ExtractImagesAsync(); }));
        mediaMenu.DropDownItems.Add(new ToolStripMenuItem("提取本页视频", null, (s, e) => { CloseMainMenu(); _ = ExtractVideosAsync(); }));
        
        menu.Items.Add(mediaMenu);

        menu.Items.Add(new ToolStripSeparator());

        // 清除浏览数据
        menu.Items.Add(CreateMenuItem("删除浏览数据", "Ctrl+Shift+Delete", MenuIconDrawer.DrawClear,
            () => { CloseMainMenu(); ShowClearBrowsingDataDialog(); }));

        // 打印
        menu.Items.Add(CreateMenuItem("打印(P)", "Ctrl+P", MenuIconDrawer.DrawPrint,
            () => { CloseMainMenu(); PrintPage(); }));

        menu.Items.Add(new ToolStripSeparator());

        // 网页另存为
        menu.Items.Add(CreateMenuItem("网页另存为(A)...", "Ctrl+S", MenuIconDrawer.DrawSave,
            () => { CloseMainMenu(); SavePageAs(); }));

        // 在页面上查找
        menu.Items.Add(CreateMenuItem("在页面上查找", "Ctrl+F", MenuIconDrawer.DrawFind,
            () => { CloseMainMenu(); OpenFindInPage(); }));

        // 更多工具
        var tools = CreateMenuItem("更多工具", null, MenuIconDrawer.DrawTools);
        tools.DropDownDirection = ToolStripDropDownDirection.Left;
        tools.DropDown.Renderer = new ModernMenuRenderer(_isIncognito);
        tools.DropDown.BackColor = _isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249);
        tools.DropDown.ForeColor = _isIncognito ? Color.White : Color.Black;

        var taskManager = new ToolStripMenuItem("任务管理器(T)")
        {
            ShortcutKeyDisplayString = "Shift+Esc"
        };
        taskManager.Click += (s, e) => { CloseMainMenu(); ShowTaskManager(); };
        tools.DropDownItems.Add(taskManager);

        var encoding = new ToolStripMenuItem("编码(E)");
        encoding.DropDownDirection = ToolStripDropDownDirection.Left;
        encoding.DropDown.Renderer = new ModernMenuRenderer(_isIncognito);
        encoding.DropDown.BackColor = _isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249);
        encoding.DropDown.ForeColor = _isIncognito ? Color.White : Color.Black;
        
        var encodingAuto = new ToolStripMenuItem("自动检测") { Checked = true };
        encodingAuto.Click += (s, e) => { CloseMainMenu(); SetEncoding("auto"); };
        encoding.DropDownItems.Add(encodingAuto);
        encoding.DropDownItems.Add(new ToolStripSeparator());

        foreach (var (name, code) in new[] { ("Unicode (UTF-8)", "UTF-8"), ("简体中文 (GBK)", "GBK"),
            ("简体中文 (GB2312)", "GB2312"), ("繁体中文 (Big5)", "Big5"),
            ("日语 (Shift_JIS)", "Shift_JIS"), ("韩语 (EUC-KR)", "EUC-KR") })
        {
            var encItem = new ToolStripMenuItem(name);
            var encCode = code;
            encItem.Click += (s, e) => { CloseMainMenu(); SetEncoding(encCode); };
            encoding.DropDownItems.Add(encItem);
        }
        tools.DropDownItems.Add(encoding);
        tools.DropDownItems.Add(new ToolStripSeparator());

        var devTools = new ToolStripMenuItem("开发者工具(D)")
        {
            ShortcutKeyDisplayString = "F12"
        };
        devTools.Click += (s, e) => { CloseMainMenu(); OpenDevTools(); };
        tools.DropDownItems.Add(devTools);

        var resourceLog = new ToolStripMenuItem("查看资源加载日志(L)")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+L"
        };
        resourceLog.Click += (s, e) => { CloseMainMenu(); ShowResourceLog(); };
        tools.DropDownItems.Add(resourceLog);

        menu.Items.Add(tools);

        menu.Items.Add(new ToolStripSeparator());

        // 广告过滤
        var adBlock = CreateMenuItem("广告过滤(G)", null, _adBlockService.Enabled ? MenuIconDrawer.DrawAdBlockEnabled : MenuIconDrawer.DrawAdBlock);
        adBlock.Checked = _adBlockService.Enabled;
        adBlock.Click += (s, e) =>
        {
            _adBlockService.Enabled = !_adBlockService.Enabled;
            _settingsService.Settings.EnableAdBlock = _adBlockService.Enabled;
            _settingsService.Save();
            adBlock.Checked = _adBlockService.Enabled;
            // 更新图标
            int iconSize = DpiHelper.Scale(20);
            var iconBitmap = new Bitmap(iconSize, iconSize);
            using (var g = Graphics.FromImage(iconBitmap))
            {
                g.Clear(Color.Transparent);
                if (_adBlockService.Enabled)
                    MenuIconDrawer.DrawAdBlockEnabled(g, new Rectangle(0, 0, iconSize, iconSize));
                else
                    MenuIconDrawer.DrawAdBlock(g, new Rectangle(0, 0, iconSize, iconSize));
            }
            adBlock.Image = iconBitmap;
        };
        menu.Items.Add(adBlock);

        menu.Items.Add(new ToolStripSeparator());

        // 设置
        menu.Items.Add(CreateMenuItem("设置(S)", null, MenuIconDrawer.DrawSettings,
            () => { CloseMainMenu(); ShowSettings(); }));

        // 反馈
        menu.Items.Add(CreateMenuItem("反馈", null, MenuIconDrawer.DrawFeedback,
            () => { CloseMainMenu(); _tabManager.ActiveTab?.Navigate("https://www.kunqiongai.com/feedback?soft_number=10014"); }));

        // 关于
        menu.Items.Add(CreateMenuItem("关于鲲穹AI浏览器", null, MenuIconDrawer.DrawAbout,
            () => { CloseMainMenu(); new AboutForm().ShowDialog(); }));

        if (_isIncognito)
        {
            menu.Items.Add(CreateMenuItem("关于 InPrivate 浏览", null, MenuIconDrawer.DrawIncognito,
                () => { CloseMainMenu(); ShowIncognitoInfo(); }));
        }

        menu.Items.Add(new ToolStripSeparator());

        // 退出
        var exit = new ToolStripMenuItem(_isIncognito ? "关闭隐身窗口" : "关闭鲲穹AI浏览器")
        {
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };
        exit.Click += (s, e) => { CloseMainMenu(); Close(); };
        menu.Items.Add(exit);

        menu.Show(_settingsBtn, new Point(_settingsBtn.Width - menu.Width, _settingsBtn.Height));

        StartMenuCloseTimer();
    }

    private void AddBookmarkFolderItems(ToolStripMenuItem parent, string folderId)
    {
        parent.DropDown.Renderer = new ModernMenuRenderer(_isIncognito);
        parent.DropDown.BackColor = _isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249);
        parent.DropDown.ForeColor = _isIncognito ? Color.White : Color.Black;

        var children = _bookmarkService.GetChildren(folderId);
        foreach (var child in children)
        {
            var item = new ToolStripMenuItem(child.IsFolder ? "📁 " + child.Title : child.Title);
            if (child.IsFolder)
            {
                AddBookmarkFolderItems(item, child.Id);
            }
            else
            {
                item.Image = Helpers.FaviconHelper.GetCachedFavicon(child.Url);
                LoadMenuItemFaviconAsync(item, child.Url, child.FaviconUrl);
                var childUrl = child.Url;
                item.Click += (s, e) => { CloseMainMenu(); _tabManager.ActiveTab?.Navigate(childUrl); };
            }
            parent.DropDownItems.Add(item);
        }
    }

    private ToolStripControlHost CreateZoomMenuItem()
    {
        var host = new ToolStripControlHost(CreateZoomPanel())
        {
            AutoSize = false,
            Size = DpiHelper.Scale(new Size(280, 36))
        };
        return host;
    }

    private Label? _zoomLevelLabel;
    
    private Panel CreateZoomPanel()
    {
        _zoomPanel = new Panel { Size = DpiHelper.Scale(new Size(280, 34)), BackColor = Color.Transparent };

        // 缩放图标
        int iconSize = DpiHelper.Scale(20);
        var iconPanel = new Panel
        {
            Size = new Size(iconSize, iconSize),
            Location = DpiHelper.Scale(new Point(12, 7)),
            BackColor = Color.Transparent
        };
        iconPanel.Paint += (s, e) => MenuIconDrawer.DrawZoom(e.Graphics, new Rectangle(0, 0, iconSize, iconSize));

        var lblZoom = new Label
        {
            Text = "缩放",
            Location = DpiHelper.Scale(new Point(40, 9)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = _isIncognito ? Color.White : Color.FromArgb(32, 32, 32)
        };

        var btnMinus = CreateZoomButton("—", DpiHelper.Scale(new Point(120, 5)), DpiHelper.Scale(new Size(32, 24)), () => { ZoomOut(); UpdateZoomLabel(); });

        _zoomLevelLabel = new Label
        {
            Text = $"{(int)(_zoomLevel * 100)}%",
            Size = DpiHelper.Scale(new Size(50, 24)),
            Location = DpiHelper.Scale(new Point(154, 7)),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            ForeColor = _isIncognito ? Color.White : Color.FromArgb(32, 32, 32)
        };

        var btnPlus = CreateZoomButton("+", DpiHelper.Scale(new Point(206, 5)), DpiHelper.Scale(new Size(32, 24)), () => { ZoomIn(); UpdateZoomLabel(); });

        var btnFullscreen = CreateZoomButton("⛶", DpiHelper.Scale(new Point(244, 5)), DpiHelper.Scale(new Size(28, 24)), () =>
        {
            _reopenMenuAfterZoom = false;  // 确保不会重新打开菜单
            CloseMainMenu();
            _fullscreenManager.Toggle();
        }, keepMenuOpen: false);
        btnFullscreen.Font = new Font("Segoe UI Symbol", DpiHelper.ScaleFont(11F));

        _zoomPanel.Controls.AddRange(new Control[] { iconPanel, lblZoom, btnMinus, _zoomLevelLabel, btnPlus, btnFullscreen });
        return _zoomPanel;
    }

    private Label CreateZoomButton(string text, Point location, Size size, Action? onClick = null, bool keepMenuOpen = true)
    {
        var btn = new Label
        {
            Text = text,
            Size = size,
            Location = location,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", DpiHelper.ScaleFont(10F)),
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            ForeColor = _isIncognito ? Color.White : Color.FromArgb(32, 32, 32)
        };

        btn.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
            using var pen = new Pen(_isIncognito ? Color.FromArgb(100, 100, 100) : Color.FromArgb(180, 180, 180));
            using var path = CreateRoundedRect(rect, DpiHelper.Scale(4));
            g.DrawPath(pen, path);
        };

        btn.MouseEnter += (s, e) => btn.BackColor = _isIncognito ? Color.FromArgb(70, 70, 70) : Color.FromArgb(232, 232, 232);
        btn.MouseLeave += (s, e) => btn.BackColor = Color.Transparent;
        btn.MouseDown += (s, e) => 
        {
            btn.BackColor = _isIncognito ? Color.FromArgb(90, 90, 90) : Color.FromArgb(210, 210, 210);
            // 立即执行操作
            onClick?.Invoke();
        };
        btn.MouseUp += (s, e) =>
        {
            btn.BackColor = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position))
                ? (_isIncognito ? Color.FromArgb(70, 70, 70) : Color.FromArgb(232, 232, 232)) : Color.Transparent;
            // 如果不需要保持菜单打开，在MouseUp时执行操作
            if (!keepMenuOpen && btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position)))
                onClick?.Invoke();
        };

        return btn;
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
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

    private void UpdateZoomLabel()
    {
        if (_zoomLevelLabel != null)
            _zoomLevelLabel.Text = $"{(int)(_zoomLevel * 100)}%";
    }

    private void ZoomIn()
    {
        if (_zoomLevel < 3.0)
        {
            _zoomLevel += 0.1;
            ApplyZoom();
            ShowZoomPopup();
        }
    }

    private void ZoomOut()
    {
        if (_zoomLevel > 0.25)
        {
            _zoomLevel -= 0.1;
            ApplyZoom();
            ShowZoomPopup();
        }
    }
    
    private void ResetZoom()
    {
        _zoomLevel = 1.0;
        ApplyZoom();
        UpdateZoomLabel();
        ShowZoomPopup();
    }

    private void ApplyZoom()
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 != null)
            _tabManager.ActiveTab.WebView.ZoomFactor = _zoomLevel;
    }
    
    private void ShowZoomPopup()
    {
        // 更新菜单栏中的缩放比例
        UpdateZoomLabel();
        
        // 显示/隐藏放大镜按钮（缩放不是100%时显示）
        UpdateZoomButtonVisibility();
        
        // 创建或更新缩放弹窗
        if (_zoomPopup == null)
        {
            _zoomPopup = new Panel
            {
                Size = DpiHelper.Scale(new Size(140, 72)),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None, // 移除系统边框
                Padding = DpiHelper.Scale(new Padding(1))
            };
            
            // 启用圆角绘制
            _zoomPopup.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, _zoomPopup.Width - 1, _zoomPopup.Height - 1);
                using var pen = new Pen(Color.FromArgb(220, 220, 220));
                using var path = CreateRoundedRect(rect, DpiHelper.Scale(8));
                g.DrawPath(pen, path);
            };

            // 缩放百分比标签
            _zoomPopupLabel = new Label
            {
                Text = $"{(int)(_zoomLevel * 100)}%",
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(11F), FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 32, 32),
                Location = DpiHelper.Scale(new Point(2, 10)), // 增加 2px 偏移避免遮挡边框
                Size = DpiHelper.Scale(new Size(136, 24)),   // 减小宽度避免遮挡
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent                // 透明背景
            };
            _zoomPopup.Controls.Add(_zoomPopupLabel);
            
            // 重置按钮美化
            var resetBtn = new Label
            {
                Text = "重置",
                Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
                Location = DpiHelper.Scale(new Point(20, 38)),
                Size = DpiHelper.Scale(new Size(100, 26)),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(245, 245, 245),
                ForeColor = Color.FromArgb(64, 64, 64)
            };

            resetBtn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, resetBtn.Width - 1, resetBtn.Height - 1);
                using var path = CreateRoundedRect(rect, DpiHelper.Scale(4));
                using var pen = new Pen(Color.FromArgb(210, 210, 210));
                g.DrawPath(pen, path);
            };

            resetBtn.MouseEnter += (s, e) => { resetBtn.BackColor = Color.FromArgb(235, 235, 235); };
            resetBtn.MouseLeave += (s, e) => { resetBtn.BackColor = Color.FromArgb(245, 245, 245); };
            resetBtn.MouseDown += (s, e) => { resetBtn.BackColor = Color.FromArgb(225, 225, 225); };
            resetBtn.MouseUp += (s, e) => { resetBtn.BackColor = Color.FromArgb(235, 235, 235); };
            
            resetBtn.Click += (s, e) => { ResetZoom(); HideZoomPopup(); };
            _zoomPopup.Controls.Add(resetBtn);
            
            Controls.Add(_zoomPopup);
            _zoomPopup.BringToFront();
        }
        
        // 更新标签文本
        if (_zoomPopupLabel != null)
            _zoomPopupLabel.Text = $"{(int)(_zoomLevel * 100)}%";
        
        // 定位到锚点按钮下方
        Control? anchorBtn = null;
        if (_zoomBtn != null && _zoomBtn.Visible) anchorBtn = _zoomBtn;
        else if (_downloadBtn != null) anchorBtn = _downloadBtn;
        else if (_settingsBtn != null) anchorBtn = _settingsBtn;

        if (anchorBtn != null)
        {
            var btnScreenPos = anchorBtn.PointToScreen(Point.Empty);
            var formPos = PointToClient(btnScreenPos);
            var x = formPos.X + (anchorBtn.Width / 2) - (_zoomPopup.Width / 2);
            var y = formPos.Y + anchorBtn.Height + DpiHelper.Scale(8); // 增加一点间距
            
            // 确保不超出窗体边界
            if (x < 0) x = 5;
            if (x + _zoomPopup.Width > ClientSize.Width) x = ClientSize.Width - _zoomPopup.Width - 5;

            _zoomPopup.Location = new Point(x, y);
            _zoomPopup.Visible = true;
        }
        
        // 设置自动隐藏定时器
        _zoomPopupTimer?.Stop();
        _zoomPopupTimer?.Dispose();
        _zoomPopupTimer = new System.Windows.Forms.Timer { Interval = 2500 };
        _zoomPopupTimer.Tick += (s, e) => HideZoomPopup();
        _zoomPopupTimer.Start();
    }
    
    private void UpdateZoomButtonVisibility()
    {
        // 缩放不是100%时显示放大镜按钮
        var isNotDefault = Math.Abs(_zoomLevel - 1.0) > 0.01;
        if (_zoomBtn != null)
            _zoomBtn.Visible = isNotDefault;
    }
    
    private void HideZoomPopup()
    {
        _zoomPopupTimer?.Stop();
        _zoomPopupTimer?.Dispose();
        _zoomPopupTimer = null;
        
        if (_zoomPopup != null)
            _zoomPopup.Visible = false;
    }
    
    private void OnTabZoomChanged(BrowserTab tab, double zoomFactor)
    {
        // 更新内部缩放级别
        _zoomLevel = zoomFactor;
        
        // 更新菜单栏中的缩放比例
        UpdateZoomLabel();
        
        // 更新放大镜按钮可见性
        UpdateZoomButtonVisibility();
        
        // 显示缩放弹窗
        ShowZoomPopup();
    }

    #endregion

    #region 书签操作

    private void ToggleBookmark()
    {
        ShowAddBookmarkDialog();
    }

    private void AddCurrentPageToBookmarks()
    {
        var url = _tabManager.ActiveTab?.Url;
        var title = _tabManager.ActiveTab?.Title ?? "新书签";

        if (string.IsNullOrEmpty(url) || url == "about:blank") return;

        if (_bookmarkService.FindByUrl(url) != null)
        {
            MessageBox.Show("已收藏", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _bookmarkService.AddBookmark(title, url, null, _tabManager.ActiveTab?.FaviconUrl);
        UpdateBookmarkButton(true);
    }

    /// <summary>
    /// 显示添加收藏对话框
    /// </summary>
    private void ShowAddBookmarkDialog()
    {
        if (_tabManager.ActiveTab == null) return;
        
        var url = _tabManager.ActiveTab.Url;
        var title = _tabManager.ActiveTab.Title ?? "新书签";

        if (string.IsNullOrEmpty(url) || url.StartsWith("about:")) 
        {
            MessageBox.Show("无法收藏此页面", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try 
        {
            var existing = _bookmarkService.FindByUrl(url);
            
            using var dialog = new AddBookmarkDialog(
                _bookmarkService, 
                title, 
                url, 
                _tabManager.ActiveTab?.FaviconUrl,
                existing);
            
            if (_bookmarkBtn != null && _bookmarkBtn.IsHandleCreated)
            {
                var btnLocation = _bookmarkBtn.PointToScreen(new Point(_bookmarkBtn.Width, _bookmarkBtn.Height));
                dialog.SetAnchorPoint(btnLocation);
            }
            
            var result = dialog.ShowDialog(this);
            
            UpdateBookmarkButton(result != DialogResult.Abort);
            
            if (result == DialogResult.Abort)
                _statusLabel.Text = "已取消收藏";
            else if (result == DialogResult.Retry)
                ShowBookmarkManager();
        }
        catch (Exception ex)
         {
             Debug.WriteLine($"显示添加书签对话框失败: {ex.Message}");
             MessageBox.Show("操作失败，请重试", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
    }

    /// <summary>
    /// 批量收藏所有打开的标签页
    /// </summary>
    private void AddAllTabsToBookmarks()
    {
        var tabs = _tabManager.Tabs.Where(t => 
            !string.IsNullOrEmpty(t.Url) && 
            !t.Url.StartsWith("about:")).ToList();

        if (tabs.Count == 0)
        {
            MessageBox.Show("没有可收藏的标签页", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"将为 {tabs.Count} 个标签页创建收藏夹\n\n是否继续？",
            "批量添加收藏",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        // 创建一个文件夹来存放这些书签
        var folderName = $"标签页 {DateTime.Now:MM-dd HH:mm}";
        var folder = _bookmarkService.AddFolder(folderName);

        var addedCount = 0;
        foreach (var tab in tabs)
        {
            if (_bookmarkService.FindByUrl(tab.Url) == null)
            {
                _bookmarkService.AddBookmark(tab.Title, tab.Url, folder.Id, tab.FaviconUrl);
                addedCount++;
            }
        }

        _statusLabel.Text = $"已添加 {addedCount} 个收藏到文件夹 \"{folderName}\"";
        MessageBox.Show($"已将 {addedCount} 个标签页添加到收藏夹 \"{folderName}\"", 
            "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// 显示收藏夹管理器
    /// </summary>
    private void ShowBookmarkManager()
    {
        using var manager = new BookmarkManagerForm(_bookmarkService, url => _tabManager.ActiveTab?.Navigate(url));
        manager.ShowDialog(this);
    }

    /// <summary>
    /// 导入收藏
    /// </summary>
    private void ImportBookmarks()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入收藏",
            Filter = "HTML 文件 (*.html;*.htm)|*.html;*.htm|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var content = File.ReadAllText(dialog.FileName);
            var importedCount = ImportBookmarksFromHtml(content);
            
            _statusLabel.Text = $"已导入 {importedCount} 个收藏";
            MessageBox.Show($"成功导入 {importedCount} 个收藏", "导入完成", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 从 HTML 文件解析并导入书签
    /// </summary>
    private int ImportBookmarksFromHtml(string html)
    {
        var count = 0;
        
        // 简单解析 HTML 书签格式 (Netscape Bookmark File Format)
        // 匹配 <A HREF="url">title</A>
        var regex = new System.Text.RegularExpressions.Regex(
            @"<A\s+HREF=""([^""]+)""[^>]*>([^<]+)</A>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var matches = regex.Matches(html);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var url = match.Groups[1].Value;
            var title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value);

            // 跳过已存在的书签
            if (_bookmarkService.FindByUrl(url) != null) continue;

            // 跳过 javascript: 和 place: 等特殊链接
            if (url.StartsWith("javascript:") || url.StartsWith("place:")) continue;

            _bookmarkService.AddBookmark(title, url);
            count++;
        }

        return count;
    }

    #endregion

    #region 媒体提取
    
    private void OnMediaExtractionRequested(string type)
    {
        if (type == "image")
            _ = ExtractImagesAsync();
        else if (type == "video")
            _ = ExtractVideosAsync();
    }
    
    private async Task ExtractImagesAsync()
    {
        var activeTab = _tabManager.ActiveTab;
        if (activeTab?.WebView?.CoreWebView2 == null) return;

        _statusLabel.Text = "正在提取图片资源...";
        try
        {
            var imageUrls = await _mediaDownloadService.ExtractImageUrlsAsync(activeTab.WebView.CoreWebView2);
            if (imageUrls == null || imageUrls.Count == 0)
            {
                _statusLabel.Text = "未发现图片资源";
                MessageBox.Show("当前页面未发现可供提取的图片资源。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 使用设置中的下载目录（仅展示给用户，不在此修改）
            string defaultPath = _settingsService.Settings.DownloadPath;
            if (string.IsNullOrWhiteSpace(defaultPath))
                defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(defaultPath))
                Directory.CreateDirectory(defaultPath);

            using (var dialog = new ImageSelectionDialog(imageUrls, defaultPath))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var urlsToDownload = dialog.SelectedUrls;
                    _statusLabel.Text = $"已提交 {urlsToDownload.Count} 个图片下载任务";
                    
                    var chosenPath = dialog.SelectedSavePath;
                    foreach (var imgUrl in urlsToDownload)
                    {
                        try { _tabManager.RegisterDownloadPathOverride(imgUrl, chosenPath); } catch { }
                        StartWebViewDownload(activeTab, imgUrl, referer: activeTab.Url ?? "");
                    }
                    
                    // 呼出默认下载面板方便查看
                    OpenDownloadDialog();
                }
                else
                {
                    _statusLabel.Text = "已取消图片提取";
                }
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "图片提取失败";
            MessageBox.Show($"提取失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ExtractVideosAsync()
    {
        var activeTab = _tabManager.ActiveTab;
        if (activeTab == null) return;
        
        var url = activeTab.Url;
        if (string.IsNullOrEmpty(url) || url.StartsWith("about:"))
        {
            MessageBox.Show("当前页面不支持视频提取", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _statusLabel.Text = "正在解析视频信息...";
        try
        {
            string extractionResult = url;
            // 针对不同页面，尝试提取实际视频地址或数据
            if (activeTab.WebView?.CoreWebView2 != null)
            {
                extractionResult = await _mediaDownloadService.GetEffectiveVideoUrlAsync(activeTab, url);
            }

            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            
            // 解析提取结果
            object infoRequest;
            JsonElement? directData = null;
            string cookies = null;
            string userAgent = null;
            string effectiveUrl = url;

            try {
                using (var doc = JsonDocument.Parse(extractionResult)) {
                    var root = doc.RootElement;
                    
                    // 提取 Cookie 和 UA
                    if (root.TryGetProperty("cookies", out var cookieProp)) {
                        cookies = cookieProp.GetString();
                    }
                    if (root.TryGetProperty("ua", out var uaProp)) {
                        userAgent = uaProp.GetString();
                    }

                    if (root.TryGetProperty("type", out var type) && type.GetString() == "direct_data") {
                        directData = root.Clone();
                        // 默认仍然使用当前页 URL 作为解析入口，避免将某些站点的 CDN 直链
                        // （例如 bilivideo.com 的 m4s 分片）错误地当成解析入口。
                        if (root.TryGetProperty("url", out var dUrl)) {
                            var extractedUrl = dUrl.GetString();
                            // 对于非 bilibili 直链，仍然允许覆盖为提取出的详情页地址
                            if (!string.IsNullOrEmpty(extractedUrl) && !extractedUrl.Contains("bilivideo.com", StringComparison.OrdinalIgnoreCase)) {
                                effectiveUrl = extractedUrl;
                            }
                        }
                        
                        infoRequest = new { 
                            url = effectiveUrl, 
                            direct_data = directData,
                            cookies = cookies,
                            user_agent = userAgent
                        };
                    } else if (root.TryGetProperty("url", out var extractedUrl)) {
                        effectiveUrl = extractedUrl.GetString();
                        infoRequest = new { 
                            url = effectiveUrl,
                            cookies = cookies,
                            user_agent = userAgent
                        };
                    } else {
                        infoRequest = new { 
                            url = url,
                            cookies = cookies,
                            user_agent = userAgent
                        };
                    }
                }
            } catch {
                infoRequest = new { url = url };
            }

            // 针对抖音进行最后一道检查：如果是首页链接且没有 direct_data，说明提取失败
            if (effectiveUrl.Contains("douyin.com") && 
                (effectiveUrl.EndsWith("/") || effectiveUrl.Contains("?recommend=") || effectiveUrl.Contains("/discover")) && 
                directData == null)
            {
                _statusLabel.Text = "提取失败";
                MessageBox.Show("未能识别到当前播放的视频，请尝试：\n1. 确保视频正在播放\n2. 刷新页面后重试\n3. 点击视频进入详情页后再提取", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var infoContent = new StringContent(JsonSerializer.Serialize(infoRequest), Encoding.UTF8, "application/json");
            var infoResponse = await client.PostAsync("http://localhost:8000/video/info", infoContent);
            
            if (infoResponse.IsSuccessStatusCode)
            {
                var infoJson = await infoResponse.Content.ReadAsStringAsync();
                
                // 检查返回的 JSON 中是否有有效的视频格式
                bool hasFormats = false;
                try
                {
                    using (var doc = JsonDocument.Parse(infoJson))
                    {
                        if (doc.RootElement.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array && formats.GetArrayLength() > 0)
                        {
                            hasFormats = true;
                        }
                    }
                }
                catch { }

                if (!hasFormats)
                {
                    _statusLabel.Text = "未识别到视频资源";
                    ShowModernMessage(
                        "未识别到视频资源",
                        "当前页面未检测到可供提取的视频资源。\n\n说明：该网页可能未内嵌视频，或使用了加密/DRM 播放器，因此暂无法直接提取。\n\n建议：请确认这是具体的视频播放页面，并在视频开始播放后再次尝试提取。",
                        ModernDialogIcon.Warning);
                    return;
                }

                // 使用设置中的下载目录
                string defaultPath = _settingsService.Settings.DownloadPath;
                if (string.IsNullOrWhiteSpace(defaultPath))
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(defaultPath))
                    Directory.CreateDirectory(defaultPath);

                using (var selectionDialog = new VideoSelectionDialog(infoJson, defaultPath))
                {
                    if (selectionDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        string formatId = selectionDialog.SelectedFormatId;
                        string chosenPath = selectionDialog.SelectedSavePath;
                        
                        // 优先尝试使用可直接下载的直链，通过 WebView2 发起以显示在默认下载面板
                        string? directDownloadUrl = null;
                        try
                        {
                            using var docAll = JsonDocument.Parse(infoJson);
                            if (docAll.RootElement.TryGetProperty("formats", out var fmts) && fmts.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var f in fmts.EnumerateArray())
                                {
                                    if (f.TryGetProperty("format_id", out var idProp) && idProp.GetString() == formatId)
                                    {
                                        if (f.TryGetProperty("url", out var urlProp))
                                        {
                                            directDownloadUrl = urlProp.GetString();
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                        
                        // 统一走后端下载，避免直链在标签页导航导致不可访问
                        _statusLabel.Text = "视频下载已开始...";
                        
                        var effectiveFormatId = !string.IsNullOrEmpty(directDownloadUrl) ? "direct" : formatId;
                        var downloadRequest = new
                        {
                            url = url,
                            save_path = chosenPath,
                            format_id = effectiveFormatId,
                            direct_data = directData,
                            cookies = cookies,
                            user_agent = userAgent
                        };
                        
                        var downloadContent = new StringContent(JsonSerializer.Serialize(downloadRequest), Encoding.UTF8, "application/json");
                        var downloadResponse = await client.PostAsync("http://localhost:8000/video/download", downloadContent);
                        
                        if (downloadResponse.IsSuccessStatusCode)
                        {
                            var respJson = await downloadResponse.Content.ReadAsStringAsync();
                            var taskInfo = JsonDocument.Parse(respJson);
                            string taskId = taskInfo.RootElement.GetProperty("task_id").GetString() ?? "";
                            
                            _statusLabel.Text = "视频下载已开始（外部任务）";
                            
                            var downloadItem = new MiniWorldBrowser.Models.DownloadItem
                            {
                                Id = taskId,
                                FileName = "正在初始化...",
                                Status = MiniWorldBrowser.Models.DownloadStatus.Downloading,
                                Url = url,
                                FilePath = chosenPath
                            };
                            _tabManager.AddExternalDownload(downloadItem);
                            
                            // 显示右下角进度条
                            BeginInvoke(new Action(() =>
                            {
                                _progressBar.IsMarquee = false;
                                _progressBar.Maximum = 100;
                                _progressBar.Value = 0;
                                _progressBar.Visible = true;
                            }));
                            
                            _ = Task.Run(async () => {
                                while (true) {
                                    await Task.Delay(1000);
                                    var progress = await _mediaDownloadService.GetDownloadProgressAsync(taskId);
                                    if (progress != null) {
                                        downloadItem.ReceivedBytes = progress.DownloadedBytes;
                                        downloadItem.TotalBytes = progress.TotalBytes;
                                        downloadItem.FileName = progress.Filename;
                                        
                                        if (progress.Status == "completed") {
                                            downloadItem.Status = MiniWorldBrowser.Models.DownloadStatus.Completed;
                                            downloadItem.EndTime = DateTime.Now;
                                            BeginInvoke(new Action(() => {
                                                _statusLabel.Text = $"下载完成: {progress.Filename}";
                                                _progressBar.Value = 100;
                                                var t = new System.Windows.Forms.Timer { Interval = 1200 };
                                                t.Tick += (s2, e2) => { t.Stop(); t.Dispose(); _progressBar.Visible = false; };
                                                t.Start();
                                            }));
                                            break;
                                        } else if (progress.Status == "failed") {
                                            downloadItem.Status = MiniWorldBrowser.Models.DownloadStatus.Failed;
                                            BeginInvoke(new Action(() => {
                                                _statusLabel.Text = "下载失败";
                                                _progressBar.Visible = false;
                                            }));
                                            break;
                                        }
                                        
                                        BeginInvoke(new Action(() => {
                                            _statusLabel.Text = $"正在下载: {progress.Filename}";
                                            var pct = Math.Max(0, Math.Min(100, (int)Math.Round(progress.Progress)));
                                            _progressBar.Value = pct;
                                        }));
                                    }
                                }
                            });
                        }
                        else
                        {
                            var error = await downloadResponse.Content.ReadAsStringAsync();
                            string displayError = error;
                            try {
                                using (var doc = JsonDocument.Parse(error)) {
                                    if (doc.RootElement.TryGetProperty("detail", out var detail)) {
                                        displayError = detail.GetString() ?? error;
                                    }
                                }
                            } catch { }
                            _statusLabel.Text = "视频下载失败";
                            MessageBox.Show($"下载失败: {displayError}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        _statusLabel.Text = "已取消下载";
                    }
                }
            }
            else
            {
                var error = await infoResponse.Content.ReadAsStringAsync();
                string displayError = error;
                try
                {
                    using (var doc = JsonDocument.Parse(error))
                    {
                        if (doc.RootElement.TryGetProperty("detail", out var detail))
                        {
                            displayError = detail.GetString() ?? error;
                        }
                    }
                }
                catch { }

                var lowerError = displayError?.ToLowerInvariant() ?? string.Empty;

                if (lowerError.Contains("不支持的 url") || lowerError.Contains("unsupported url"))
                {
                    _statusLabel.Text = "未识别到视频资源";
                    ShowModernMessage(
                        "未识别到视频资源",
                        "当前页面未能解析出可提取的视频资源，或不在支持的网站范围内。\n\n建议：\n• 确认已打开具体的视频播放页面，而非列表或个人主页；\n• 如为短视频站点，请进入单个视频详情页后再尝试提取。",
                        ModernDialogIcon.Warning);
                }
                else if (lowerError.Contains("未能获取视频信息") || lowerError.Contains("yt-dlp"))
                {
                    _statusLabel.Text = "未识别到视频资源";
                    ShowModernMessage(
                        "未识别到视频资源",
                        "未能从当前页面解析出视频流。\n\n可能原因：页面尚未完全加载，或视频通过加密/DRM 播放器播放。\n\n建议：刷新页面，等待视频开始播放后再次尝试提取。",
                        ModernDialogIcon.Warning);
                }
                else
                {
                    _statusLabel.Text = "解析视频失败";
                    ShowModernMessage(
                        "解析失败",
                        $"解析失败：{displayError}\n\n请确保 Python 桥接服务已启动且已安装 yt-dlp。",
                        ModernDialogIcon.Error);
                }
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "操作失败";
            MessageBox.Show($"操作失败: {ex.Message}\n请确保 Python 桥接服务已启动且安装了 yt-dlp。\n\n提示：您需要先在命令行运行 python_bridge/main.py", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StartWebViewDownload(BrowserTab tab, string url, string? cookies = null, string? userAgent = null, string? referer = null)
    {
        try
        {
            var core = tab.WebView?.CoreWebView2;
            if (core == null) return;
            var headers = new StringBuilder();
            if (!string.IsNullOrEmpty(cookies)) headers.Append("Cookie: ").Append(cookies).Append("\r\n");
            if (!string.IsNullOrEmpty(userAgent)) headers.Append("User-Agent: ").Append(userAgent).Append("\r\n");
            if (!string.IsNullOrEmpty(referer)) headers.Append("Referer: ").Append(referer).Append("\r\n");
            var req = core.Environment.CreateWebResourceRequest(url, "GET", Stream.Null, headers.ToString());
            core.NavigateWithWebResourceRequest(req);
        }
        catch { }
    }

    #endregion

    #region 设置和其他窗口

    private void ShowSettings()
    {
        _ = CreateNewTabWithProtection("about:settings");
    }

    private void ShowSettingsDialog()
    {
        using var dlg = new SettingsForm(_settingsService, _bookmarkService);
        dlg.ShowDialog();
        _adBlockService.Enabled = _settingsService.Settings.EnableAdBlock;
        UpdateBookmarkBarVisibility();
    }

    private void OpenIncognitoWindow()
    {
        var incognitoForm = new MainForm(true);
        // 注册到多窗口上下文，确保生命周期正确管理
        MultiWindowApplicationContext.Current?.RegisterForm(incognitoForm);
        incognitoForm.Show();
    }

    private async void SavePageAs()
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null)
        {
            MessageBox.Show("没有可保存的网页", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var webView = _tabManager.ActiveTab.WebView.CoreWebView2;
        var pageTitle = webView.DocumentTitle ?? "网页";
        var safeTitle = string.Join("_", pageTitle.Split(Path.GetInvalidFileNameChars()));
        if (safeTitle.Length > 50) safeTitle = safeTitle[..50];

        using var saveDialog = new SaveFileDialog
        {
            Title = "网页另存为",
            FileName = safeTitle,
            Filter = "网页，仅HTML (*.html)|*.html|网页，完整 (*.html)|*.html|MHTML文件 (*.mhtml)|*.mhtml|PDF文档 (*.pdf)|*.pdf",
            FilterIndex = 1,
            DefaultExt = "html",
            AddExtension = true
        };

        if (saveDialog.ShowDialog() != DialogResult.OK) return;

        var filePath = saveDialog.FileName;
        var filterIndex = saveDialog.FilterIndex;
        var pageSaveService = new PageSaveService();

        try
        {
            _statusLabel.Text = "正在保存网页...";
            _progressBar.Visible = true;

            switch (filterIndex)
            {
                case 1: await pageSaveService.SaveAsHtmlOnlyAsync(webView, filePath); break;
                case 2: await pageSaveService.SaveAsHtmlCompleteAsync(webView, filePath); break;
                case 3: await pageSaveService.SaveAsMhtmlAsync(webView, filePath); break;
                case 4: await pageSaveService.SaveAsPdfAsync(webView, filePath); break;
            }

            _statusLabel.Text = "保存完成";
            MessageBox.Show($"网页已保存到:\n{filePath}", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "保存失败";
            MessageBox.Show($"保存网页时出错:\n{ex.Message}", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progressBar.Visible = false;
        }
    }

    private void OpenFindInPage()
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null) return;

        try
        {
            var webView = _tabManager.ActiveTab.WebView;
            webView.Focus();
            SendKeys.Send("^f");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenFindInPage failed: {ex.Message}");
        }
    }

    private async void PrintPage()
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null)
        {
            MessageBox.Show("没有可打印的网页", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            _statusLabel.Text = "正在准备打印...";
            await _tabManager.ActiveTab.WebView.CoreWebView2.ExecuteScriptAsync("window.print()");
            _statusLabel.Text = "就绪";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "打印失败";
            System.Diagnostics.Debug.WriteLine($"PrintPage failed: {ex.Message}");
            MessageBox.Show($"打印时出错:\n{ex.Message}", "打印失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public void ShowClearBrowsingDataDialog()
    {
        var tab = _tabManager.ActiveTab;
        if (tab == null) return;

        _tabManager.OpenClearBrowsingData(tab);
        _tabManager.ActiveTab?.Refresh();
        _statusLabel.Text = "浏览数据已清除";
    }

    private void ShowTaskManager()
    {
        var taskManagerForm = new TaskManagerForm(_tabManager);
        taskManagerForm.Show();
    }

    private async void SetEncoding(string encoding)
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null) return;

        try
        {
            if (encoding == "auto")
            {
                _tabManager.ActiveTab.Refresh();
            }
            else
            {
                var script = $"document.charset = '{encoding}';";
                await _tabManager.ActiveTab.WebView.CoreWebView2.ExecuteScriptAsync(script);
                _tabManager.ActiveTab.Refresh();
            }
            _statusLabel.Text = $"编码已设置为: {encoding}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetEncoding failed: {ex.Message}");
        }
    }

    private void OpenDevTools()
    {
        if (_tabManager.ActiveTab?.WebView?.CoreWebView2 == null) return;

        try
        {
            _tabManager.ActiveTab.WebView.CoreWebView2.OpenDevToolsWindow();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenDevTools failed: {ex.Message}");
        }
    }

    private async void LoadMenuItemFaviconAsync(ToolStripMenuItem menuItem, string url, string? faviconUrl = null)
    {
        try
        {
            var icon = await Helpers.FaviconHelper.GetFaviconAsync(url, faviconUrl);
            if (icon != null && !menuItem.IsDisposed)
            {
                BeginInvoke(() => menuItem.Image = icon);
            }
        }
        catch { }
    }

    #endregion
}
