using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 菜单工厂 - 为主窗口和隐身窗口提供统一的菜单创建逻辑
/// </summary>
public static class MenuFactory
{
    /// <summary>
    /// 创建主菜单
    /// </summary>
    public static ContextMenuStrip CreateMainMenu(
        ISettingsService settingsService,
        IBookmarkService bookmarkService,
        IAdBlockService adBlockService,
        BookmarkBar bookmarkBar,
        Action<string> onNavigate,
        Action onToggleBookmark,
        Action onSavePageAs,
        Action onOpenFindInPage,
        Action onPrintPage,
        Action onOpenDownloadDialog,
        Action onOpenDevTools,
        Action<string> onSetEncoding,
        Action onShowSettings,
        Action? onShowIncognitoInfo = null,
        bool isIncognito = false)
    {
        var menu = new ContextMenuStrip
        {
            Font = new Font("Microsoft YaHei UI", 9F),
            AutoClose = false,
            BackColor = isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249),
            ForeColor = isIncognito ? Color.White : Color.Black,
            ShowImageMargin = true,
            ImageScalingSize = new Size(20, 20),
            Padding = new Padding(0, 4, 0, 4)
        };
        menu.Renderer = new ModernMenuRenderer(isIncognito);

        // 新建标签页
        var newTab = CreateMenuItem("新建标签页(T)", "Ctrl+T", MenuIconDrawer.DrawNewTab);
        menu.Items.Add(newTab);

        // 新建窗口
        var newWindow = CreateMenuItem("新建窗口(N)", "Ctrl+N", MenuIconDrawer.DrawNewWindow);
        menu.Items.Add(newWindow);

        // 新建隐私窗口
        var newIncognito = CreateMenuItem("新建 InPrivate 窗口(I)", "Ctrl+Shift+N", MenuIconDrawer.DrawIncognito);
        menu.Items.Add(newIncognito);

        menu.Items.Add(new ToolStripSeparator());

        // 收藏夹
        var bookmarks = CreateMenuItem("收藏夹(B)", "Ctrl+Shift+O", MenuIconDrawer.DrawBookmark);
        bookmarks.DropDownDirection = ToolStripDropDownDirection.Left;
        bookmarks.DropDown.Renderer = new ModernMenuRenderer();

        var showBar = new ToolStripMenuItem("显示收藏栏(S)")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+B",
            Checked = bookmarkBar.Visible
        };
        showBar.Click += (s, e) =>
        {
            bookmarkBar.Visible = !bookmarkBar.Visible;
            showBar.Checked = bookmarkBar.Visible;
        };
        bookmarks.DropDownItems.Add(showBar);

        bookmarks.DropDownItems.Add(new ToolStripSeparator());

        var addBookmark = new ToolStripMenuItem("为此页添加收藏...")
        {
            ShortcutKeyDisplayString = "Ctrl+D"
        };
        addBookmark.Click += (s, e) => onToggleBookmark();
        bookmarks.DropDownItems.Add(addBookmark);

        bookmarks.DropDownItems.Add(new ToolStripSeparator());

        var barItems = bookmarkService.GetBookmarkBarItems();
        foreach (var item in barItems.Take(10))
        {
            var bmItem = new ToolStripMenuItem(item.IsFolder ? "📁 " + item.Title : item.Title);
            if (item.IsFolder)
            {
                AddBookmarkFolderItems(bmItem, item.Id, bookmarkService, onNavigate);
            }
            else
            {
                bmItem.Click += (s, e) => 
                {
                    if (Control.MouseButtons != MouseButtons.Right)
                        onNavigate(item.Url);
                };
            }
            bookmarks.DropDownItems.Add(bmItem);
        }

        menu.Items.Add(bookmarks);

        // 历史记录
        var history = CreateMenuItem("历史记录(H)", "Ctrl+H", MenuIconDrawer.DrawHistory);
        history.DropDownDirection = ToolStripDropDownDirection.Left;
        history.DropDown.Renderer = new ModernMenuRenderer();

        var showHistory = new ToolStripMenuItem("显示全部历史记录")
        {
            ShortcutKeyDisplayString = "Ctrl+H"
        };
        showHistory.Click += (s, e) => onShowSettings();
        history.DropDownItems.Add(showHistory);

        history.DropDownItems.Add(new ToolStripSeparator());

        menu.Items.Add(history);

        // 下载
        var download = CreateMenuItem("下载(D)", "Ctrl+J", MenuIconDrawer.DrawDownload);
        download.Click += (s, e) => onOpenDownloadDialog();
        menu.Items.Add(download);

        menu.Items.Add(new ToolStripSeparator());

        // 清除浏览数据
        var clearData = CreateMenuItem("删除浏览数据", "Ctrl+Shift+Delete", MenuIconDrawer.DrawClear);
        menu.Items.Add(clearData);

        // 打印
        var print = CreateMenuItem("打印(P)", "Ctrl+P", MenuIconDrawer.DrawPrint);
        print.Click += (s, e) => onPrintPage();
        menu.Items.Add(print);

        menu.Items.Add(new ToolStripSeparator());

        // 网页另存为
        var saveAs = CreateMenuItem("网页另存为(A)...", "Ctrl+S", MenuIconDrawer.DrawSave);
        saveAs.Click += (s, e) => onSavePageAs();
        menu.Items.Add(saveAs);

        // 在页面上查找
        var find = CreateMenuItem("在页面上查找", "Ctrl+F", MenuIconDrawer.DrawFind);
        find.Click += (s, e) => onOpenFindInPage();
        menu.Items.Add(find);

        // 更多工具
        var tools = CreateMenuItem("更多工具", null, MenuIconDrawer.DrawTools);
        tools.DropDownDirection = ToolStripDropDownDirection.Left;
        tools.DropDown.Renderer = new ModernMenuRenderer();

        var encoding = new ToolStripMenuItem("编码(E)");
        encoding.DropDownDirection = ToolStripDropDownDirection.Left;
        var encodingAuto = new ToolStripMenuItem("自动检测") { Checked = true };
        encodingAuto.Click += (s, e) => onSetEncoding("auto");
        encoding.DropDownItems.Add(encodingAuto);
        encoding.DropDownItems.Add(new ToolStripSeparator());

        foreach (var (name, code) in new[] { ("Unicode (UTF-8)", "UTF-8"), ("简体中文 (GBK)", "GBK"),
            ("简体中文 (GB2312)", "GB2312"), ("繁体中文 (Big5)", "Big5"),
            ("日语 (Shift_JIS)", "Shift_JIS"), ("韩语 (EUC-KR)", "EUC-KR") })
        {
            var encItem = new ToolStripMenuItem(name);
            encItem.Click += (s, e) => onSetEncoding(code);
            encoding.DropDownItems.Add(encItem);
        }
        tools.DropDownItems.Add(encoding);
        tools.DropDownItems.Add(new ToolStripSeparator());

        var devTools = new ToolStripMenuItem("开发者工具(D)")
        {
            ShortcutKeyDisplayString = "F12"
        };
        devTools.Click += (s, e) => onOpenDevTools();
        tools.DropDownItems.Add(devTools);

        menu.Items.Add(tools);

        menu.Items.Add(new ToolStripSeparator());

        // 广告过滤
        var adBlock = CreateMenuItem("广告过滤(G)", null, MenuIconDrawer.DrawAdBlock);
        adBlock.Checked = adBlockService.Enabled;
        adBlock.Click += (s, e) =>
        {
            adBlockService.Enabled = !adBlockService.Enabled;
            settingsService.Settings.EnableAdBlock = adBlockService.Enabled;
            settingsService.Save();
            adBlock.Checked = adBlockService.Enabled;
        };
        menu.Items.Add(adBlock);

        menu.Items.Add(new ToolStripSeparator());

        // 隐身模式相关菜单项
        if (isIncognito && onShowIncognitoInfo != null)
        {
            var aboutIncognito = new ToolStripMenuItem("关于隐身浏览");
            aboutIncognito.Click += (s, e) => onShowIncognitoInfo();
            menu.Items.Add(aboutIncognito);
        }

        // 设置
        var settings = CreateMenuItem("设置(S)", null, MenuIconDrawer.DrawSettings);
        settings.Click += (s, e) => onShowSettings();
        menu.Items.Add(settings);

        // 关于
        var about = CreateMenuItem("关于鲲穹AI浏览器", null, MenuIconDrawer.DrawAbout);
        menu.Items.Add(about);

        menu.Items.Add(new ToolStripSeparator());

        // 退出/关闭
        var exit = new ToolStripMenuItem(isIncognito ? "关闭隐身窗口" : "关闭鲲穹AI浏览器")
        {
            Padding = new Padding(8, 6, 8, 6)
        };
        menu.Items.Add(exit);

        return menu;
    }

    private static ToolStripMenuItem CreateMenuItem(string text, string? shortcut, Action<Graphics, Rectangle>? iconDrawer)
    {
        var item = new ToolStripMenuItem(text)
        {
            ShortcutKeyDisplayString = shortcut,
            Padding = new Padding(8, 6, 8, 6)
        };

        if (iconDrawer != null)
        {
            var iconBitmap = new Bitmap(20, 20);
            using (var g = Graphics.FromImage(iconBitmap))
            {
                g.Clear(Color.Transparent);
                iconDrawer(g, new Rectangle(0, 0, 20, 20));
            }
            item.Image = iconBitmap;
            item.ImageScaling = ToolStripItemImageScaling.None;
        }

        return item;
    }

    private static void AddBookmarkFolderItems(ToolStripMenuItem parent, string folderId, IBookmarkService bookmarkService, Action<string> onNavigate)
    {
        var children = bookmarkService.GetChildren(folderId);
        foreach (var child in children)
        {
            var item = new ToolStripMenuItem(child.IsFolder ? "📁 " + child.Title : child.Title);
            if (child.IsFolder)
            {
                AddBookmarkFolderItems(item, child.Id, bookmarkService, onNavigate);
            }
            else
            {
                item.Click += (s, e) => 
                {
                    if (Control.MouseButtons != MouseButtons.Right)
                        onNavigate(child.Url);
                };
            }
            parent.DropDownItems.Add(item);
        }
    }
}
