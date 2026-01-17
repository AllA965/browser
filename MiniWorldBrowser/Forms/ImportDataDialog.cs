using MiniWorldBrowser.Services.Interfaces;

using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 导入收藏和设置对话框
/// </summary>
public class ImportDataDialog : Form
{
    private readonly IBookmarkService _bookmarkService;
    private ComboBox _sourceCombo = null!;
    private CheckBox _bookmarksCheck = null!;
    private Button _importBtn = null!;
    private Button _cancelBtn = null!;
    
    public ImportDataDialog(IBookmarkService bookmarkService)
    {
        _bookmarkService = bookmarkService;
        InitializeUI();
    }
    
    private void InitializeUI()
    {
        AppIconHelper.SetIcon(this);
        Text = "导入收藏和设置";
        Size = new Size(400, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Microsoft YaHei UI", 9F);
        
        // 来源标签
        var sourceLabel = new Label
        {
            Text = "来源：",
            Location = new Point(20, 25),
            AutoSize = true
        };
        Controls.Add(sourceLabel);
        
        // 来源下拉框
        _sourceCombo = new ComboBox
        {
            Location = new Point(80, 22),
            Size = new Size(280, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _sourceCombo.Items.AddRange(new object[]
        {
            "Microsoft Internet Explorer",
            "Microsoft Edge",
            "Google Chrome",
            "Mozilla Firefox",
            "以前导出的收藏夹（HTML文件）"
        });
        _sourceCombo.SelectedIndex = 0;
        Controls.Add(_sourceCombo);
        
        // 选择要导入的内容标签
        var selectLabel = new Label
        {
            Text = "选择要导入的内容：",
            Location = new Point(20, 65),
            AutoSize = true
        };
        Controls.Add(selectLabel);
        
        // 收藏夹/书签复选框
        _bookmarksCheck = new CheckBox
        {
            Text = "收藏夹/书签",
            Location = new Point(40, 90),
            AutoSize = true,
            Checked = true
        };
        Controls.Add(_bookmarksCheck);
        
        // 导入按钮
        _importBtn = new Button
        {
            Text = "导入",
            Size = new Size(75, 28),
            Location = new Point(210, 140),
            DialogResult = DialogResult.OK
        };
        _importBtn.Click += OnImportClick;
        Controls.Add(_importBtn);
        
        // 取消按钮
        _cancelBtn = new Button
        {
            Text = "取消",
            Size = new Size(75, 28),
            Location = new Point(295, 140),
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(_cancelBtn);
        
        AcceptButton = _importBtn;
        CancelButton = _cancelBtn;
    }
    
    private void OnImportClick(object? sender, EventArgs e)
    {
        if (!_bookmarksCheck.Checked)
        {
            MessageBox.Show("请至少选择一项要导入的内容。", "提示", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.None;
            return;
        }
        
        try
        {
            int importedCount = 0;
            
            switch (_sourceCombo.SelectedIndex)
            {
                case 0: // IE
                    importedCount = ImportFromIE();
                    break;
                case 1: // Edge
                    importedCount = ImportFromEdge();
                    break;
                case 2: // Chrome
                    importedCount = ImportFromChrome();
                    break;
                case 3: // Firefox
                    importedCount = ImportFromFirefox();
                    break;
                case 4: // HTML文件
                    importedCount = ImportFromHtmlFile();
                    break;
            }
            
            if (importedCount > 0)
            {
                MessageBox.Show($"成功导入 {importedCount} 个收藏。", "导入完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (importedCount == 0)
            {
                MessageBox.Show("没有找到可导入的收藏。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
    
    private int ImportFromIE()
    {
        var favoritesPath = Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
        if (!Directory.Exists(favoritesPath))
        {
            MessageBox.Show("未找到 Internet Explorer 收藏夹。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        
        return ImportFromFavoritesFolder(favoritesPath, null);
    }
    
    private int ImportFromEdge()
    {
        // Edge 使用 Chromium 内核，书签存储在 JSON 文件中
        var edgePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data", "Default", "Bookmarks");
        
        if (!File.Exists(edgePath))
        {
            MessageBox.Show("未找到 Microsoft Edge 书签文件。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        
        return ImportFromChromiumBookmarks(edgePath);
    }
    
    private int ImportFromChrome()
    {
        var chromePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data", "Default", "Bookmarks");
        
        if (!File.Exists(chromePath))
        {
            MessageBox.Show("未找到 Google Chrome 书签文件。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        
        return ImportFromChromiumBookmarks(chromePath);
    }
    
    private int ImportFromFirefox()
    {
        // Firefox 书签存储在 SQLite 数据库中，这里简化处理
        var firefoxPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox", "Profiles");
        
        if (!Directory.Exists(firefoxPath))
        {
            MessageBox.Show("未找到 Firefox 配置文件夹。\n\n建议：请先从 Firefox 导出书签为 HTML 文件，然后选择\"以前导出的收藏夹（HTML文件）\"进行导入。", 
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        
        MessageBox.Show("Firefox 书签导入需要先从 Firefox 导出为 HTML 文件。\n\n请在 Firefox 中：\n1. 按 Ctrl+Shift+O 打开书签管理器\n2. 点击\"导入和备份\" > \"导出书签到 HTML\"\n3. 然后选择\"以前导出的收藏夹（HTML文件）\"进行导入", 
            "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }
    
    private int ImportFromHtmlFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择书签 HTML 文件",
            Filter = "HTML 文件 (*.html;*.htm)|*.html;*.htm|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };
        
        if (dialog.ShowDialog() != DialogResult.OK)
            return -1; // 用户取消
        
        return ImportFromHtmlBookmarks(dialog.FileName);
    }

    
    /// <summary>
    /// 从 IE 收藏夹文件夹导入
    /// </summary>
    private int ImportFromFavoritesFolder(string folderPath, string? parentId)
    {
        int count = 0;
        
        // 导入文件夹中的 .url 文件
        foreach (var file in Directory.GetFiles(folderPath, "*.url"))
        {
            try
            {
                var url = ReadUrlFromShortcut(file);
                if (!string.IsNullOrEmpty(url))
                {
                    var title = Path.GetFileNameWithoutExtension(file);
                    _bookmarkService.AddBookmark(title, url, parentId);
                    count++;
                }
            }
            catch { }
        }
        
        // 递归导入子文件夹
        foreach (var dir in Directory.GetDirectories(folderPath))
        {
            var folderName = Path.GetFileName(dir);
            // 跳过系统文件夹
            if (folderName.StartsWith(".") || folderName == "Links")
                continue;
            
            var folder = _bookmarkService.AddFolder(folderName, parentId);
            count += ImportFromFavoritesFolder(dir, folder.Id);
        }
        
        return count;
    }
    
    /// <summary>
    /// 从 .url 快捷方式文件读取 URL
    /// </summary>
    private static string? ReadUrlFromShortcut(string filePath)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    return line[4..].Trim();
                }
            }
        }
        catch { }
        return null;
    }
    
    /// <summary>
    /// 从 Chromium 内核浏览器（Chrome/Edge）的 JSON 书签文件导入
    /// </summary>
    private int ImportFromChromiumBookmarks(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            int count = 0;
            
            if (root.TryGetProperty("roots", out var roots))
            {
                // 导入书签栏
                if (roots.TryGetProperty("bookmark_bar", out var bookmarkBar))
                {
                    count += ImportChromiumFolder(bookmarkBar, null);
                }
                
                // 导入其他书签
                if (roots.TryGetProperty("other", out var other))
                {
                    count += ImportChromiumFolder(other, "other");
                }
            }
            
            return count;
        }
        catch (Exception ex)
        {
            throw new Exception($"解析书签文件失败：{ex.Message}");
        }
    }
    
    private int ImportChromiumFolder(System.Text.Json.JsonElement element, string? parentId)
    {
        int count = 0;
        
        if (element.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                var type = child.GetProperty("type").GetString();
                var name = child.GetProperty("name").GetString() ?? "未命名";
                
                if (type == "folder")
                {
                    var folder = _bookmarkService.AddFolder(name, parentId);
                    count += ImportChromiumFolder(child, folder.Id);
                }
                else if (type == "url")
                {
                    var url = child.GetProperty("url").GetString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        _bookmarkService.AddBookmark(name, url, parentId);
                        count++;
                    }
                }
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// 从 HTML 书签文件导入（Netscape 书签格式）
    /// </summary>
    private int ImportFromHtmlBookmarks(string filePath)
    {
        try
        {
            var html = File.ReadAllText(filePath);
            return ParseHtmlBookmarks(html, null);
        }
        catch (Exception ex)
        {
            throw new Exception($"解析 HTML 文件失败：{ex.Message}");
        }
    }
    
    private int ParseHtmlBookmarks(string html, string? parentId)
    {
        int count = 0;
        
        // 简单的 HTML 解析，处理 Netscape 书签格式
        // <DT><A HREF="url">title</A>
        // <DT><H3>folder name</H3>
        // <DL><p>...children...</DL>
        
        var lines = html.Split('\n');
        string? currentFolderId = parentId;
        var folderStack = new Stack<string?>();
        folderStack.Push(parentId);
        
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            
            // 检测文件夹开始
            if (line.Contains("<H3", StringComparison.OrdinalIgnoreCase))
            {
                var folderName = ExtractTextContent(line, "H3");
                if (!string.IsNullOrEmpty(folderName))
                {
                    var folder = _bookmarkService.AddFolder(folderName, currentFolderId);
                    folderStack.Push(currentFolderId);
                    currentFolderId = folder.Id;
                }
            }
            // 检测书签链接
            else if (line.Contains("<A ", StringComparison.OrdinalIgnoreCase) && 
                     line.Contains("HREF=", StringComparison.OrdinalIgnoreCase))
            {
                var url = ExtractHref(line);
                var title = ExtractTextContent(line, "A");
                
                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
                {
                    _bookmarkService.AddBookmark(title, url, currentFolderId);
                    count++;
                }
            }
            // 检测文件夹结束
            else if (line.Contains("</DL>", StringComparison.OrdinalIgnoreCase))
            {
                if (folderStack.Count > 0)
                {
                    currentFolderId = folderStack.Pop();
                }
            }
        }
        
        return count;
    }
    
    private static string? ExtractHref(string line)
    {
        var hrefIndex = line.IndexOf("HREF=", StringComparison.OrdinalIgnoreCase);
        if (hrefIndex < 0) return null;
        
        var start = hrefIndex + 5;
        if (start >= line.Length) return null;
        
        char quote = line[start];
        if (quote != '"' && quote != '\'')
        {
            // 没有引号的情况
            var end = line.IndexOfAny(new[] { ' ', '>', '\t' }, start);
            return end > start ? line[start..end] : null;
        }
        
        start++;
        var endQuote = line.IndexOf(quote, start);
        return endQuote > start ? line[start..endQuote] : null;
    }
    
    private static string? ExtractTextContent(string line, string tagName)
    {
        // 查找开始标签的结束位置
        var tagStart = line.IndexOf($"<{tagName}", StringComparison.OrdinalIgnoreCase);
        if (tagStart < 0) return null;
        
        var contentStart = line.IndexOf('>', tagStart);
        if (contentStart < 0) return null;
        contentStart++;
        
        // 查找结束标签
        var contentEnd = line.IndexOf($"</{tagName}>", contentStart, StringComparison.OrdinalIgnoreCase);
        if (contentEnd < 0)
        {
            // 可能没有结束标签，查找下一个 < 
            contentEnd = line.IndexOf('<', contentStart);
            if (contentEnd < 0) contentEnd = line.Length;
        }
        
        if (contentEnd <= contentStart) return null;
        
        var text = line[contentStart..contentEnd].Trim();
        // 解码 HTML 实体
        text = System.Net.WebUtility.HtmlDecode(text);
        return text;
    }
}
