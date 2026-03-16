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
                        HandleSettingUpdate(tab, key, value);
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
                        // 鐐瑰嚮缃戦〉鍐呭鏃惰Е鍙戜簨浠讹紝鐢ㄤ簬鍏抽棴寮瑰嚭绐楀彛
                        WebViewClicked?.Invoke();
                    }
                    else if (action == "resetSettings")
                    {
                        // 鎭㈠榛樿璁剧疆
                        _settingsService?.Reset();
                        // 閲嶆柊瀵艰埅鍒拌缃〉闈互鍒锋柊鏄剧ず
                        tab.Navigate("about:settings");
                    }
                    else if (action == "browseDownloadPath")
                    {
                        // 鎵撳紑鏂囦欢澶归€夋嫨瀵硅瘽妗?
                        BrowseDownloadPath(tab);
                    }
                    else if (action == "openSearchEngineManager")
                    {
                        // 鎵撳紑鎼滅储寮曟搸绠＄悊瀵硅瘽妗?
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

    private void HandleSettingUpdate(BrowserTab tab, string? key, System.Text.Json.JsonElement value)
    {
        if (string.IsNullOrWhiteSpace(key) || _settingsService?.Settings == null)
        {
            PostSettingUpdateResult(tab, key ?? string.Empty, false, "invalid_key_or_settings");
            return;
        }

        var normalizedKey = key.Trim().ToLowerInvariant();
        try
        {
            var handled = true;
            switch (normalizedKey)
            {
                case "hidebookmarkbar":
                    var hideBookmarkBar = ReadBoolean(value);
                    _settingsService.Settings.AlwaysShowBookmarkBar = !hideBookmarkBar;
                    _settingsService.Save();
                    SettingChanged?.Invoke("hidebookmarkbar", hideBookmarkBar);
                    break;
                case "bookmarkbar":
                    var showBookmarkBar = ReadBoolean(value);
                    _settingsService.Settings.AlwaysShowBookmarkBar = showBookmarkBar;
                    _settingsService.Save();
                    SettingChanged?.Invoke("bookmarkbar", showBookmarkBar);
                    break;
                case "homebutton":
                    var showHomeButton = ReadBoolean(value);
                    _settingsService.Settings.ShowHomeButton = showHomeButton;
                    _settingsService.Save();
                    SettingChanged?.Invoke("homebutton", showHomeButton);
                    break;
                case "homepage":
                    _settingsService.Settings.HomePage = ReadString(value);
                    _settingsService.Save();
                    break;
                case "adblock":
                    var enableAdBlock = ReadBoolean(value);
                    _settingsService.Settings.EnableAdBlock = enableAdBlock;
                    _settingsService.Save();
                    SettingChanged?.Invoke("adblock", enableAdBlock);
                    break;
                case "adblockmode":
                    var adBlockMode = ReadInt(value, 2);
                    _settingsService.Settings.AdBlockMode = adBlockMode;
                    _settingsService.Settings.EnableAdBlock = adBlockMode > 0;
                    _settingsService.Save();
                    if (_adBlockService != null)
                    {
                        _adBlockService.Mode = adBlockMode;
                        _adBlockService.Enabled = adBlockMode > 0;
                    }
                    SettingChanged?.Invoke("adblockmode", adBlockMode);
                    break;
                case "gesture":
                    var enableGesture = ReadBoolean(value);
                    _settingsService.Settings.EnableMouseGesture = enableGesture;
                    _settingsService.Save();
                    SettingChanged?.Invoke("gesture", enableGesture);
                    break;
                case "superdrag":
                    var enableSuperDrag = ReadBoolean(value);
                    _settingsService.Settings.EnableSuperDrag = enableSuperDrag;
                    _settingsService.Save();
                    SettingChanged?.Invoke("superdrag", enableSuperDrag);
                    break;
                case "search":
                    var searchIndex = ReadInt(value, 1);
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
                    _settingsService.Settings.StartupBehavior = ReadInt(value, 0);
                    _settingsService.Save();
                    break;
                case "downloadpath":
                    _settingsService.Settings.DownloadPath = ReadString(value);
                    _settingsService.Save();
                    break;
                case "askdownload":
                    _settingsService.Settings.AskDownloadLocation = ReadBoolean(value);
                    _settingsService.Save();
                    break;
                case "crashupload":
                    _settingsService.Settings.EnableCrashUpload = ReadBoolean(value);
                    _settingsService.Save();
                    break;
                case "rightclickclosetab":
                    var rightClickClose = ReadBoolean(value);
                    _settingsService.Settings.RightClickCloseTab = rightClickClose;
                    _settingsService.Save();
                    foreach (var t in _tabs)
                    {
                        if (t.TabButton != null)
                            t.TabButton.RightClickToClose = rightClickClose;
                    }
                    SettingChanged?.Invoke("rightclickclosetab", rightClickClose);
                    break;
                case "openlinksbackground":
                    _settingsService.Settings.OpenLinksInBackground = ReadBoolean(value);
                    _settingsService.Save();
                    break;
                case "addressbarinput":
                    _settingsService.Settings.AddressBarInputMode = ReadInt(value, 0);
                    _settingsService.Save();
                    break;
                case "newtabposition":
                    _settingsService.Settings.NewTabPosition = ReadInt(value, 0);
                    _settingsService.Save();
                    break;
                case "smoothscrolling":
                    _settingsService.Settings.EnableSmoothScrolling = ReadBoolean(value);
                    _settingsService.Save();
                    break;
                case "enableautofill":
                    _settingsService.Settings.EnableAutofill = ReadBoolean(value);
                    _settingsService.Save();
                    break;
                case "savepasswords":
                    _settingsService.Settings.SavePasswords = ReadBoolean(value);
                    _settingsService.Save();
                    break;
                case "fontsize":
                    var fontSize = ReadInt(value, 2);
                    _settingsService.Settings.FontSize = fontSize;
                    _settingsService.Save();
                    ApplyFontSizeToAllTabs(fontSize);
                    break;
                case "pagezoom":
                    var pageZoom = ReadInt(value, 100);
                    _settingsService.Settings.PageZoom = pageZoom;
                    _settingsService.Save();
                    ApplyZoomToAllTabs(pageZoom);
                    break;
                case "aimode":
                    _settingsService.Settings.AiServiceMode = ReadInt(value, 0);
                    _settingsService.Save();
                    SettingChanged?.Invoke("aimode", _settingsService.Settings.AiServiceMode);
                    break;
                case "aiapikey":
                    _settingsService.Settings.AiApiKey = ReadString(value);
                    _settingsService.Save();
                    break;
                case "aiapibaseurl":
                    _settingsService.Settings.AiApiBaseUrl = ReadString(value);
                    _settingsService.Save();
                    break;
                case "aimodelname":
                    _settingsService.Settings.AiModelName = ReadString(value);
                    _settingsService.Save();
                    break;
                case "aicustomweburl":
                    _settingsService.Settings.AiCustomWebUrl = ReadString(value);
                    _settingsService.Save();
                    break;
                case "language":
                    var languageCode = ReadString(value, "auto");
                    _settingsService.Settings.LanguageCode = languageCode;
                    _settingsService.Save();
                    try
                    {
                        Localization.Initialize(string.Equals(languageCode, "auto", StringComparison.OrdinalIgnoreCase) ? null : languageCode);
                    }
                    catch { }
                    SettingChanged?.Invoke("language", languageCode);
                    RefreshAllSettingsPages();
                    break;
                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                PostSettingUpdateResult(tab, normalizedKey, true, null);
            }
        }
        catch (Exception ex)
        {
            LogSettingUpdateError(normalizedKey, value, ex);
            PostSettingUpdateResult(tab, normalizedKey, false, ex.Message);
        }
    }

    private static bool ReadBoolean(System.Text.Json.JsonElement value, bool fallback = false)
    {
        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Number => value.TryGetInt32(out var i) ? i != 0 : fallback,
            System.Text.Json.JsonValueKind.String => ParseBooleanString(value.GetString(), fallback),
            _ => fallback
        };
    }

    private static bool ParseBooleanString(string? text, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        if (bool.TryParse(text, out var b)) return b;
        if (int.TryParse(text, out var i)) return i != 0;
        return fallback;
    }

    private static int ReadInt(System.Text.Json.JsonElement value, int fallback = 0)
    {
        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Number => value.TryGetInt32(out var i) ? i : fallback,
            System.Text.Json.JsonValueKind.String => int.TryParse(value.GetString(), out var i) ? i : fallback,
            System.Text.Json.JsonValueKind.True => 1,
            System.Text.Json.JsonValueKind.False => 0,
            _ => fallback
        };
    }

    private static string ReadString(System.Text.Json.JsonElement value, string fallback = "")
    {
        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => value.GetString() ?? fallback,
            System.Text.Json.JsonValueKind.Number => value.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            _ => fallback
        };
    }

    private static void LogSettingUpdateError(string key, System.Text.Json.JsonElement value, Exception ex)
    {
        try
        {
            var rawValue = value.GetRawText();
            System.Diagnostics.Debug.WriteLine($"HandleSettingUpdate failed: key={key}, value={rawValue}, error={ex.Message}");
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"HandleSettingUpdate failed: key={key}, error={ex.Message}");
        }
    }

    private static void PostSettingUpdateResult(BrowserTab tab, string key, bool success, string? error)
    {
        try
        {
            if (tab.WebView?.CoreWebView2 == null) return;

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "settingUpdateResult",
                key,
                success,
                error = error ?? string.Empty
            });
            tab.WebView.CoreWebView2.PostWebMessageAsJson(payload);
        }
        catch { }
    }

    private void RefreshAllSettingsPages()
    {
        foreach (var t in _tabs)
        {
            if (string.Equals(t.Url, "about:settings", StringComparison.OrdinalIgnoreCase))
            {
                t.Navigate("about:settings");
            }
        }
    }

    private void HandleGesture(BrowserTab tab, string? gesture)
    {
        if (string.IsNullOrEmpty(gesture) || tab != _activeTab) return;
        
        switch (gesture)
        {
            case "L": // 宸︽粦 - 鍚庨€€
            case "UL": // 涓婂乏 - 鍚庨€€
                tab.GoBack();
                break;
            case "R": // 鍙虫粦 - 鍓嶈繘
            case "UR": // 涓婂彸 - 鍓嶈繘
                tab.GoForward();
                break;
            case "U": // 涓婃粦 - 婊氬姩鍒伴《閮?
                _ = tab.WebView?.CoreWebView2?.ExecuteScriptAsync("window.scrollTo(0, 0);");
                break;
            case "D": // 涓嬫粦 - 婊氬姩鍒板簳閮?
                _ = tab.WebView?.CoreWebView2?.ExecuteScriptAsync("window.scrollTo(0, document.body.scrollHeight);");
                break;
            case "UD": // 涓婁笅 - 鍒锋柊
                tab.Refresh();
                break;
            case "DR": // 涓嬪彸 - 鍏抽棴鏍囩椤?
            case "RD": // 鍙充笅 - 鍏抽棴鏍囩椤?
                CloseTab(tab);
                break;
            case "DU": // 涓嬩笂 - 鏂板缓鏍囩椤?
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
                // 妫€鏌TTP鍗忚鐨勯粯璁ゅ鐞嗙▼搴?
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
                if (key != null)
                {
                    var progId = key.GetValue("ProgId")?.ToString() ?? "";
                    // 妫€鏌ユ槸鍚︽槸鎴戜滑鐨勬祻瑙堝櫒锛堥€氳繃ProgId鍒ゆ柇锛?
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

