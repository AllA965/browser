using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Services;
using MiniWorldBrowser.Services.Interfaces;
using MiniWorldBrowser.Helpers;

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
        Action onShowAbout,
        Action? onShowIncognitoInfo = null,
        bool isIncognito = false)
    {
        var menu = new ContextMenuStrip
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            AutoClose = false,
            BackColor = isIncognito ? Color.FromArgb(45, 45, 45) : Color.FromArgb(249, 249, 249),
            ForeColor = isIncognito ? Color.White : Color.Black,
            ShowImageMargin = true,
            ImageScalingSize = DpiHelper.Scale(new Size(20, 20)),
            Padding = DpiHelper.Scale(new Padding(0, 4, 0, 4))
        };
        menu.Renderer = new ModernMenuRenderer(isIncognito);

        // 新建标签页
        var newTab = CreateMenuItem(Localization.Raw(), "Ctrl+T", MenuIconDrawer.DrawNewTab);
        menu.Items.Add(newTab);

        // 新建窗口
        var newWindow = CreateMenuItem(Localization.Raw(), "Ctrl+N", MenuIconDrawer.DrawNewWindow);
        menu.Items.Add(newWindow);

        // 新建隐私窗口
        var newIncognito = CreateMenuItem(Localization.Raw(), "Ctrl+Shift+N", MenuIconDrawer.DrawIncognito);
        menu.Items.Add(newIncognito);

        menu.Items.Add(new ToolStripSeparator());

        // 收藏夹
        var bookmarks = CreateMenuItem(Localization.Raw(), "Ctrl+Shift+O", MenuIconDrawer.DrawBookmark);
        bookmarks.DropDownDirection = ToolStripDropDownDirection.Left;
        bookmarks.DropDown.Renderer = new ModernMenuRenderer();

        var showBar = new ToolStripMenuItem(Localization.Raw())
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+B",
            Checked = bookmarkBar.Visible,
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };
        showBar.Click += (s, e) =>
        {
            bookmarkBar.Visible = !bookmarkBar.Visible;
            showBar.Checked = bookmarkBar.Visible;
        };
        bookmarks.DropDownItems.Add(showBar);

        bookmarks.DropDownItems.Add(new ToolStripSeparator());

        var addBookmark = new ToolStripMenuItem(Localization.Raw())
        {
            ShortcutKeyDisplayString = "Ctrl+D",
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };
        addBookmark.Click += (s, e) => onToggleBookmark();
        bookmarks.DropDownItems.Add(addBookmark);

        bookmarks.DropDownItems.Add(new ToolStripSeparator());

        var barItems = bookmarkService.GetBookmarkBarItems();
        foreach (var item in barItems.Take(10))
        {
            var bmItem = new ToolStripMenuItem(item.IsFolder ? "📁 " + item.Title : item.Title)
            {
                Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
            };
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
        var history = CreateMenuItem(Localization.Raw(), "Ctrl+H", MenuIconDrawer.DrawHistory);
        history.DropDownDirection = ToolStripDropDownDirection.Left;
        history.DropDown.Renderer = new ModernMenuRenderer();

        var showHistory = new ToolStripMenuItem(Localization.Raw())
        {
            ShortcutKeyDisplayString = "Ctrl+H",
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };
        showHistory.Click += (s, e) => onShowSettings();
        history.DropDownItems.Add(showHistory);

        history.DropDownItems.Add(new ToolStripSeparator());

        menu.Items.Add(history);

        // 下载
        var download = CreateMenuItem(Localization.Raw(), "Ctrl+J", MenuIconDrawer.DrawDownload);
        download.Click += (s, e) => onOpenDownloadDialog();
        menu.Items.Add(download);

        menu.Items.Add(new ToolStripSeparator());

        // 清除浏览数据
        var clearData = CreateMenuItem(Localization.Raw(), "Ctrl+Shift+Delete", MenuIconDrawer.DrawClear);
        menu.Items.Add(clearData);

        // 打印
        var print = CreateMenuItem(Localization.Raw(), "Ctrl+P", MenuIconDrawer.DrawPrint);
        print.Click += (s, e) => onPrintPage();
        menu.Items.Add(print);

        menu.Items.Add(new ToolStripSeparator());

        // 网页另存为
        var saveAs = CreateMenuItem(Localization.Raw(), "Ctrl+S", MenuIconDrawer.DrawSave);
        saveAs.Click += (s, e) => onSavePageAs();
        menu.Items.Add(saveAs);

        // 在页面上查找
        var find = CreateMenuItem(Localization.Raw(), "Ctrl+F", MenuIconDrawer.DrawFind);
        find.Click += (s, e) => onOpenFindInPage();
        menu.Items.Add(find);

        // 更多工具
        var tools = CreateMenuItem(Localization.Raw(), null, MenuIconDrawer.DrawTools);
        tools.DropDownDirection = ToolStripDropDownDirection.Left;
        tools.DropDown.Renderer = new ModernMenuRenderer();

        var encoding = new ToolStripMenuItem(Localization.Raw())
        {
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };
        encoding.DropDownDirection = ToolStripDropDownDirection.Left;
        var encodingAuto = new ToolStripMenuItem(Localization.Raw()) { Checked = true, Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6)) };
        encodingAuto.Click += (s, e) => onSetEncoding("auto");
        encoding.DropDownItems.Add(encodingAuto);
        encoding.DropDownItems.Add(new ToolStripSeparator());

        foreach (var (name, code) in new[] { (Localization.Raw(), "UTF-8"), (Localization.Raw(), "GBK"),
            (Localization.Raw(), "GB2312"), (Localization.Raw(), "Big5"),
            (Localization.Raw(), "Shift_JIS"), (Localization.Raw(), "EUC-KR") })
        {
            var encItem = new ToolStripMenuItem(name)
            {
                Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
            };
            encItem.Click += (s, e) => onSetEncoding(code);
            encoding.DropDownItems.Add(encItem);
        }
        tools.DropDownItems.Add(encoding);
        tools.DropDownItems.Add(new ToolStripSeparator());

        var devTools = new ToolStripMenuItem(Localization.Raw())
        {
            ShortcutKeyDisplayString = "F12",
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };
        devTools.Click += (s, e) => onOpenDevTools();
        tools.DropDownItems.Add(devTools);

        menu.Items.Add(tools);

        menu.Items.Add(new ToolStripSeparator());

        // 广告过滤
        var adBlock = CreateMenuItem(Localization.Raw(), null, MenuIconDrawer.DrawAdBlock);
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
            var aboutIncognito = new ToolStripMenuItem(Localization.Raw())
            {
                Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
            };
            aboutIncognito.Click += (s, e) => onShowIncognitoInfo();
            menu.Items.Add(aboutIncognito);
        }

        // 设置
        var settings = CreateMenuItem(Localization.Raw(), null, MenuIconDrawer.DrawSettings);
        settings.Click += (s, e) => onShowSettings();
        menu.Items.Add(settings);

        // 关于
        var about = CreateMenuItem(Localization.Raw(), null, MenuIconDrawer.DrawAbout);
        about.Click += (s, e) => onShowAbout();
        menu.Items.Add(about);

        // 反馈
        var feedback = CreateMenuItem(Localization.Raw(), null, MenuIconDrawer.DrawFeedback);
        feedback.Click += (s, e) => onNavigate("https://www.kunqiongai.com/feedback?soft_number=10014");
        menu.Items.Add(feedback);

        menu.Items.Add(new ToolStripSeparator());

        // 退出/关闭
        var exit = new ToolStripMenuItem(isIncognito ? Localization.T("menu.exit_incognito") : Localization.T("menu.exit_normal"))
        {
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };
        menu.Items.Add(exit);

        return menu;
    }

    private static ToolStripMenuItem CreateMenuItem(string text, string? shortcut, Action<Graphics, Rectangle>? iconDrawer)
    {
        var item = new ToolStripMenuItem(text)
        {
            ShortcutKeyDisplayString = shortcut,
            Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
        };

        if (iconDrawer != null)
        {
            var iconSize = DpiHelper.Scale(20);
            var iconBitmap = new Bitmap(iconSize, iconSize);
            using (var g = Graphics.FromImage(iconBitmap))
            {
                g.Clear(Color.Transparent);
                iconDrawer(g, new Rectangle(0, 0, iconSize, iconSize));
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
            var item = new ToolStripMenuItem(child.IsFolder ? "📁 " + child.Title : child.Title)
            {
                Padding = DpiHelper.Scale(new Padding(8, 6, 8, 6))
            };
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
