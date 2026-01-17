namespace MiniWorldBrowser.Constants;

/// <summary>
/// 应用程序常量定义
/// </summary>
public static class AppConstants
{
    public const string AppName = "鲲穹AI浏览器";
    public const string AppVersion = "1.0.0";
    
    // 默认设置
    public const string DefaultHomePage = "about:newtab";
    public const string DefaultSearchEngine = "https://www.baidu.com/s?wd=";
    
    // 限制
    public const int MaxTabCount = 50;
    public const int MaxHistoryItems = 100;
    public const int MaxUrlHistoryItems = 100;
    public const int MemoryReleaseDefaultMinutes = 10;
    
    // 路径
    public static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MiniWorldBrowser");
    
    public static readonly string UserDataFolder = Path.Combine(AppDataFolder, "UserData");
    public static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings.json");
    public static readonly string BookmarksFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MiniWorldBrowser", "bookmarks.json");
    public static readonly string DefaultCacheFolder = Path.Combine(AppDataFolder, "Cache");
    
    // 搜索引擎配置
    public static readonly Dictionary<string, string> SearchEngines = new()
    {
        { "百度", "https://www.baidu.com/s?wd=" },
        { "必应", "https://www.bing.com/search?q=" },
        { "Google", "https://www.google.com/search?q=" }
    };
}
