using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Helpers.Extensions;

namespace MiniWorldBrowser.Browser;

public partial class BrowserTabManager
{
    private void SetupWebMessageHandler(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;
            
            tab.WebView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    var rawMsg = e.WebMessageAsJson;
                    var msg = System.Text.Json.JsonDocument.Parse(rawMsg);
                    var action = msg.RootElement.GetProperty("action").GetString();
                    
                    if (action == "search")
                    {
                        var text = msg.RootElement.GetProperty("text").GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            var searchEngine = _settingsService?.Settings?.SearchEngine 
                                ?? MiniWorldBrowser.Constants.AppConstants.DefaultSearchEngine;
                            NewWindowRequested?.Invoke(searchEngine + Uri.EscapeDataString(text));
                        }
                    }
                    else if (action == "openLink")
                    {
                        var linkUrl = msg.RootElement.GetProperty("url").GetString();
                        if (!string.IsNullOrEmpty(linkUrl))
                            NewWindowRequested?.Invoke(linkUrl);
                    }
                    else if (action == "updateSetting")
                    {
                        var key = msg.RootElement.GetProperty("key").GetString();
                        var value = msg.RootElement.GetProperty("value");
                        HandleSettingUpdate(key, value);
                    }
                    else if (action == "getHistory")
                    {
                        SendHistoryData(tab);
                    }
                    else if (action == "searchHistory")
                    {
                        var keyword = msg.RootElement.GetProperty("keyword").GetString() ?? "";
                        SendHistoryData(tab, keyword);
                    }
                    else if (action == "clearHistory")
                    {
                        _historyService?.Clear();
                        SendHistoryData(tab);
                    }
                    else if (action == "navigate")
                    {
                        var url = msg.RootElement.GetProperty("url").GetString();
                        if (!string.IsNullOrEmpty(url))
                            tab.Navigate(url);
                    }
                    else if (action == "gesture")
                    {
                        var gesture = msg.RootElement.GetProperty("gesture").GetString();
                        HandleGesture(tab, gesture);
                    }
                    else if (action == "click")
                    {
                        // 点击网页内容时触发事件，用于关闭弹出窗口
                        WebViewClicked?.Invoke();
                    }
                    else if (action == "resetSettings")
                    {
                        // 恢复默认设置
                        _settingsService?.Reset();
                        // 重新导航到设置页面以刷新显示
                        tab.Navigate("about:settings");
                    }
                    else if (action == "browseDownloadPath")
                    {
                        // 打开文件夹选择对话框
                        BrowseDownloadPath(tab);
                    }
                    else if (action == "openSearchEngineManager")
                    {
                        // 打开搜索引擎管理对话框
                        OpenSearchEngineManager(tab);
                    }
                    else if (action == "getBookmarks")
                    {
                        var folderId = msg.RootElement.TryGetProperty("folderId", out var folderIdProp) 
                            ? folderIdProp.GetString() : null;
                        SendBookmarksData(tab, folderId);
                    }
                    else if (action == "searchBookmarks")
                    {
                        var keyword = msg.RootElement.GetProperty("keyword").GetString() ?? "";
                        SendBookmarksSearchData(tab, keyword);
                    }
                    else if (action == "updateBookmark")
                    {
                        var id = msg.RootElement.GetProperty("id").GetString();
                        var title = msg.RootElement.GetProperty("title").GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            _bookmarkService?.UpdateBookmark(id, title);
                            SendBookmarksData(tab, null);
                        }
                    }
                    else if (action == "deleteBookmark")
                    {
                        var id = msg.RootElement.GetProperty("id").GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            _bookmarkService?.Delete(id);
                            SendBookmarksData(tab, null);
                        }
                    }
                    else if (action == "addFolder")
                    {
                        var title = msg.RootElement.GetProperty("title").GetString();
                        var parentId = msg.RootElement.TryGetProperty("parentId", out var parentProp) 
                            ? parentProp.GetString() : null;
                        if (!string.IsNullOrEmpty(title))
                        {
                            _bookmarkService?.AddFolder(title, parentId);
                            SendBookmarksData(tab, parentId);
                        }
                    }
                    else if (action == "exportBookmarks")
                    {
                        ExportBookmarks(tab);
                    }
                    else if (action == "openAdBlockExceptions")
                    {
                        OpenAdBlockExceptions(tab);
                    }
                    else if (action == "openAdBlockRulesFolder")
                    {
                        OpenAdBlockRulesFolder();
                    }
                    else if (action == "openContentSettings")
                    {
                        OpenContentSettings(tab);
                    }
                    else if (action == "openClearBrowsingData")
                    {
                        OpenClearBrowsingData(tab);
                    }
                    else if (action == "openImportData")
                    {
                        OpenImportData(tab);
                    }
                    else if (action == "openHomePageDialog")
                    {
                        OpenHomePageDialog(tab);
                    }
                    else if (action == "changeCachePath")
                    {
                        ChangeCachePath(tab);
                    }
                    else if (action == "openCacheDir")
                    {
                        OpenCacheDir();
                    }
                    else if (action == "resetCachePath")
                    {
                        ResetCachePath(tab);
                    }
                    else if (action == "openAutofillSettings")
                    {
                        OpenAutofillSettings(tab);
                    }
                    else if (action == "openPasswordManager")
                    {
                        OpenPasswordManager(tab);
                    }
                    else if (action == "setAsDefaultBrowser")
                    {
                        SetAsDefaultBrowser(tab);
                    }
                    else if (action == "checkDefaultBrowser")
                    {
                        CheckDefaultBrowser(tab);
                    }
                    else if (action == "openFontSettings")
                    {
                        OpenFontSettings(tab);
                    }
                    else if (action == "openProxySettings")
                    {
                        OpenProxySettings();
                    }
                    else if (action == "openCertificateManager")
                    {
                        OpenCertificateManager();
                    }
                    else if (action == "passwordDetected")
                    {
                        var host = msg.RootElement.GetProperty("host").GetString() ?? "";
                        var username = msg.RootElement.GetProperty("username").GetString() ?? "";
                        var password = msg.RootElement.GetProperty("password").GetString() ?? "";
                        ShowSavePasswordPrompt(tab, host, username, password);
                    }
                    else if (action == "requestSavedPasswords")
                    {
                        var host = msg.RootElement.GetProperty("host").GetString() ?? "";
                        SendSavedPasswords(tab, host);
                    }
                }
                catch { }
            };
        }
        catch { }
    }

    private void HandleSettingUpdate(string? key, System.Text.Json.JsonElement value)
    {
        if (string.IsNullOrEmpty(key) || _settingsService?.Settings == null) return;
        
        try
        {
            switch (key)
            {
                case "hidebookmarkbar":
                    var hideBookmarkBar = value.GetBoolean();
                    _settingsService.Settings.AlwaysShowBookmarkBar = !hideBookmarkBar;
                    _settingsService.Save();
                    SettingChanged?.Invoke("hidebookmarkbar", hideBookmarkBar);
                    break;
                case "bookmarkbar":
                    var showBookmarkBar = value.GetBoolean();
                    _settingsService.Settings.AlwaysShowBookmarkBar = showBookmarkBar;
                    _settingsService.Save();
                    SettingChanged?.Invoke("bookmarkbar", showBookmarkBar);
                    break;
                case "homebutton":
                    _settingsService.Settings.ShowHomeButton = value.GetBoolean();
                    _settingsService.Save();
                    SettingChanged?.Invoke("homebutton", value.GetBoolean());
                    break;
                case "homepage":
                    _settingsService.Settings.HomePage = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "adblock":
                    _settingsService.Settings.EnableAdBlock = value.GetBoolean();
                    _settingsService.Save();
                    SettingChanged?.Invoke("adblock", value.GetBoolean());
                    break;
                case "adblockmode":
                    var adBlockMode = int.Parse(value.GetString() ?? "2");
                    _settingsService.Settings.AdBlockMode = adBlockMode;
                    _settingsService.Settings.EnableAdBlock = adBlockMode > 0;
                    _settingsService.Save();
                    // 更新 AdBlockService
                    if (_adBlockService != null)
                    {
                        _adBlockService.Mode = adBlockMode;
                        _adBlockService.Enabled = adBlockMode > 0;
                    }
                    SettingChanged?.Invoke("adblockmode", adBlockMode);
                    break;
                case "gesture":
                    _settingsService.Settings.EnableMouseGesture = value.GetBoolean();
                    _settingsService.Save();
                    SettingChanged?.Invoke("gesture", value.GetBoolean());
                    break;
                case "superdrag":
                    _settingsService.Settings.EnableSuperDrag = value.GetBoolean();
                    _settingsService.Save();
                    SettingChanged?.Invoke("superdrag", value.GetBoolean());
                    break;
                case "search":
                    var searchIndex = int.Parse(value.GetString() ?? "1");
                    _settingsService.Settings.AddressBarSearchEngine = searchIndex;
                    _settingsService.Settings.SearchEngine = searchIndex switch
                    {
                        0 => "https://www.so.com/s?q=",
                        1 => "https://www.baidu.com/s?wd=",
                        2 => "https://www.bing.com/search?q=",
                        3 => "https://www.google.com/search?q=",
                        _ => "https://www.baidu.com/s?wd="
                    };
                    _settingsService.Save();
                    SettingChanged?.Invoke("search", searchIndex);
                    break;
                case "startup":
                    var startupIndex = int.Parse(value.GetString() ?? "0");
                    _settingsService.Settings.StartupBehavior = startupIndex;
                    _settingsService.Save();
                    break;
                case "downloadpath":
                    _settingsService.Settings.DownloadPath = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "askdownload":
                    _settingsService.Settings.AskDownloadLocation = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "crashupload":
                    _settingsService.Settings.EnableCrashUpload = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "rightclickclosetab":
                    var rightClickClose = value.GetBoolean();
                    _settingsService.Settings.RightClickCloseTab = rightClickClose;
                    _settingsService.Save();
                    // 更新所有标签的右击关闭设置
                    foreach (var t in _tabs)
                    {
                        if (t.TabButton != null)
                            t.TabButton.RightClickToClose = rightClickClose;
                    }
                    SettingChanged?.Invoke("rightclickclosetab", rightClickClose);
                    break;
                case "openlinksbackground":
                    _settingsService.Settings.OpenLinksInBackground = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "addressbarinput":
                    _settingsService.Settings.AddressBarInputMode = int.Parse(value.GetString() ?? "0");
                    _settingsService.Save();
                    break;
                case "newtabposition":
                    _settingsService.Settings.NewTabPosition = int.Parse(value.GetString() ?? "0");
                    _settingsService.Save();
                    break;
                case "smoothscrolling":
                    _settingsService.Settings.EnableSmoothScrolling = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "enableautofill":
                    _settingsService.Settings.EnableAutofill = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "savepasswords":
                    _settingsService.Settings.SavePasswords = value.GetBoolean();
                    _settingsService.Save();
                    break;
                case "fontsize":
                    var fontSize = int.Parse(value.GetString() ?? "2");
                    _settingsService.Settings.FontSize = fontSize;
                    _settingsService.Save();
                    // 应用字体大小到所有标签页
                    ApplyFontSizeToAllTabs(fontSize);
                    break;
                case "pagezoom":
                    var pageZoom = int.Parse(value.GetString() ?? "100");
                    _settingsService.Settings.PageZoom = pageZoom;
                    _settingsService.Save();
                    // 应用缩放到所有标签页
                    ApplyZoomToAllTabs(pageZoom);
                    break;
                case "aimode":
                    _settingsService.Settings.AiServiceMode = int.Parse(value.GetString() ?? "0");
                    _settingsService.Save();
                    SettingChanged?.Invoke("aimode", _settingsService.Settings.AiServiceMode);
                    break;
                case "aiapikey":
                    _settingsService.Settings.AiApiKey = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "aiapibaseurl":
                    _settingsService.Settings.AiApiBaseUrl = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "aimodelname":
                    _settingsService.Settings.AiModelName = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
                case "aicustomweburl":
                    _settingsService.Settings.AiCustomWebUrl = value.GetString() ?? "";
                    _settingsService.Save();
                    break;
            }
        }
        catch { }
    }

    private void HandleGesture(BrowserTab tab, string? gesture)
    {
        if (string.IsNullOrEmpty(gesture) || tab != _activeTab) return;
        
        switch (gesture)
        {
            case "L": // 左滑 - 后退
            case "UL": // 上左 - 后退
                tab.GoBack();
                break;
            case "R": // 右滑 - 前进
            case "UR": // 上右 - 前进
                tab.GoForward();
                break;
            case "U": // 上滑 - 滚动到顶部
                _ = tab.WebView?.CoreWebView2?.ExecuteScriptAsync("window.scrollTo(0, 0);");
                break;
            case "D": // 下滑 - 滚动到底部
                _ = tab.WebView?.CoreWebView2?.ExecuteScriptAsync("window.scrollTo(0, document.body.scrollHeight);");
                break;
            case "UD": // 上下 - 刷新
                tab.Refresh();
                break;
            case "DR": // 下右 - 关闭标签页
            case "RD": // 右下 - 关闭标签页
                CloseTab(tab);
                break;
            case "DU": // 下上 - 新建标签页
                _ = CreateTabAsync(_settingsService?.Settings?.HomePage ?? "about:newtab");
                break;
        }
    }

    private void CheckDefaultBrowser(BrowserTab tab)
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;
            
            bool isDefault = false;
            try
            {
                // 检查HTTP协议的默认处理程序
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
                if (key != null)
                {
                    var progId = key.GetValue("ProgId")?.ToString() ?? "";
                    // 检查是否是我们的浏览器（通过ProgId判断）
                    isDefault = progId.Contains("MiniWorld", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "defaultBrowserStatus",
                isDefault = isDefault
            });
            tab.WebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch { }
    }

    private static string GetFaviconUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=16";
        }
        catch
        {
            return "";
        }
    }
}
