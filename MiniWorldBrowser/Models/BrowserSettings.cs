using MiniWorldBrowser.Constants;

namespace MiniWorldBrowser.Models;

/// <summary>
/// 浏览器设置模型 - 参考世界之窗浏览器
/// </summary>
public class BrowserSettings
{
    #region 基本设置
    
    /// <summary>
    /// 主页地址
    /// </summary>
    public string HomePage { get; set; } = AppConstants.DefaultHomePage;
    
    /// <summary>
    /// 搜索引擎 URL
    /// </summary>
    public string SearchEngine { get; set; } = AppConstants.DefaultSearchEngine;
    
    #endregion
    
    #region 登录状态

    /// <summary>
    /// 登录令牌
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// 用户信息
    /// </summary>
    public UserInfo? UserInfo { get; set; }

    #endregion

    #region 启动时
    
    /// <summary>
    /// 启动时行为: 0=打开新标签页, 1=继续浏览上次打开的网页, 2=打开特定网页
    /// </summary>
    public int StartupBehavior { get; set; } = 0;
    
    /// <summary>
    /// 启动时打开的特定网页列表
    /// </summary>
    public List<string> StartupPages { get; set; } = new();
    
    /// <summary>
    /// 上次会话的标签页URL列表（用于"继续浏览上次"功能）
    /// </summary>
    public List<string> LastSessionUrls { get; set; } = new();
    
    #endregion
    
    #region 广告过滤
    
    /// <summary>
    /// 广告过滤模式: 0=不过滤, 1=仅弹窗, 2=强力过滤, 3=自定义规则
    /// </summary>
    public int AdBlockMode { get; set; } = 2;
    
    /// <summary>
    /// 启用广告过滤
    /// </summary>
    public bool EnableAdBlock { get; set; } = true;
    
    /// <summary>
    /// 自定义过滤规则文件路径
    /// </summary>
    public string CustomAdBlockRulesPath { get; set; } = "";
    
    /// <summary>
    /// 广告过滤例外网站列表（格式：host|action，action为allow或block）
    /// </summary>
    public List<string> AdBlockExceptions { get; set; } = new();
    
    #endregion

    #region AI 设置

    /// <summary>
    /// AI 服务模式: 0=内置网页(DeepSeek), 1=自定义 API (OpenAI 兼容)
    /// </summary>
    public int AiServiceMode { get; set; } = 0;

    /// <summary>
    /// AI API Key
    /// </summary>
    public string AiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// AI API 代理地址
    /// </summary>
    public string AiApiBaseUrl { get; set; } = "https://api.deepseek.com/v1";

    /// <summary>
    /// AI 模型名称
    /// </summary>
    public string AiModelName { get; set; } = "deepseek-chat";

    /// <summary>
    /// 自定义 AI 网页地址
    /// </summary>
    public string AiCustomWebUrl { get; set; } = "https://chat.deepseek.com/";

    #endregion
    
    #region 标签设置
    
    /// <summary>
    /// 在地址栏显示完整URL
    /// </summary>
    public bool ShowFullUrlInAddressBar { get; set; } = false;
    
    /// <summary>
    /// 单击地址栏时全选URL
    /// </summary>
    public bool SelectAllOnAddressBarClick { get; set; } = true;
    
    /// <summary>
    /// 地址栏输入方式: 0=智能选择打开方式(推荐), 1=在当前标签打开, 2=在新标签打开
    /// </summary>
    public int AddressBarInputMode { get; set; } = 0;
    
    /// <summary>
    /// 新标签页打开方式: 0=当前标签右侧打开, 1=所有标签右侧打开
    /// </summary>
    public int NewTabPosition { get; set; } = 0;
    
    /// <summary>
    /// 在新标签页中打开链接
    /// </summary>
    public bool OpenLinksInNewTab { get; set; } = true;
    
    /// <summary>
    /// 双击关闭标签页
    /// </summary>
    public bool DoubleClickCloseTab { get; set; } = true;
    
    /// <summary>
    /// 右击关闭对应标签（按住Shift右击可显示菜单）
    /// </summary>
    public bool RightClickCloseTab { get; set; } = false;
    
    /// <summary>
    /// 点击链接在后台标签打开
    /// </summary>
    public bool OpenLinksInBackground { get; set; } = false;
    
    #endregion
    
    #region 地址栏搜索引擎
    
    /// <summary>
    /// 地址栏搜索引擎: 0=360, 1=百度, 2=必应, 3=Google, 4+=自定义
    /// </summary>
    public int AddressBarSearchEngine { get; set; } = 1;
    
    /// <summary>
    /// 自定义搜索引擎列表
    /// </summary>
    public List<CustomSearchEngine> CustomSearchEngines { get; set; } = new();
    
    #endregion
    
    #region 用户数据
    
    /// <summary>
    /// 用户数据目录
    /// </summary>
    public string UserDataPath { get; set; } = "";
    
    #endregion
    
    #region 网页设置
    
    /// <summary>
    /// 启用网页平滑滚动效果（重启浏览器后生效）
    /// </summary>
    public bool EnableSmoothScrolling { get; set; } = true;
    
    #endregion
    
    #region 自定义缓存
    
    /// <summary>
    /// 自定义缓存目录位置
    /// </summary>
    public string CustomCachePath { get; set; } = "";
    
    /// <summary>
    /// 是否使用自定义缓存目录
    /// </summary>
    public bool UseCustomCachePath { get; set; } = false;
    
    #endregion
    
    #region 外观
    
    /// <summary>
    /// 显示主页按钮
    /// </summary>
    public bool ShowHomeButton { get; set; } = true;
    
    /// <summary>
    /// 总是显示书签栏
    /// </summary>
    public bool AlwaysShowBookmarkBar { get; set; } = true;
    
    /// <summary>
    /// 夜间模式
    /// </summary>
    public bool DarkMode { get; set; } = false;
    
    #endregion
    
    #region 下载内容
    
    /// <summary>
    /// 使用内置下载器
    /// </summary>
    public bool UseBuiltInDownloader { get; set; } = true;
    
    /// <summary>
    /// 下载保存路径
    /// </summary>
    public string DownloadPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    
    /// <summary>
    /// 下载前询问保存位置
    /// </summary>
    public bool AskDownloadLocation { get; set; } = false;
    
    #endregion
    
    #region 功能开关
    
    /// <summary>
    /// 启用鼠标手势
    /// </summary>
    public bool EnableMouseGesture { get; set; } = true;
    
    /// <summary>
    /// 启用超级拖拽
    /// </summary>
    public bool EnableSuperDrag { get; set; } = true;
    
    /// <summary>
    /// 后台标签内存释放时间（分钟）
    /// </summary>
    public int MemoryReleaseMinutes { get; set; } = AppConstants.MemoryReleaseDefaultMinutes;
    
    #endregion
    
    #region 隐私设置
    
    /// <summary>
    /// 退出时清除浏览历史
    /// </summary>
    public bool ClearHistoryOnExit { get; set; } = false;
    
    /// <summary>
    /// 退出时清除下载记录
    /// </summary>
    public bool ClearDownloadsOnExit { get; set; } = false;
    
    /// <summary>
    /// 退出时清除缓存
    /// </summary>
    public bool ClearCacheOnExit { get; set; } = false;
    
    /// <summary>
    /// 退出时清除Cookies
    /// </summary>
    public bool ClearCookiesOnExit { get; set; } = false;
    
    /// <summary>
    /// 发送Do Not Track请求
    /// </summary>
    public bool SendDoNotTrack { get; set; } = true;
    
    /// <summary>
    /// 开启崩溃上传
    /// </summary>
    public bool EnableCrashUpload { get; set; } = true;
    
    #endregion
    
    #region 密码和表单
    
    /// <summary>
    /// 启用自动填充功能
    /// </summary>
    public bool EnableAutofill { get; set; } = true;
    
    /// <summary>
    /// 提示保存密码
    /// </summary>
    public bool SavePasswords { get; set; } = true;
    
    #endregion
    
    #region 网络内容
    
    /// <summary>
    /// 字号: 0=极小, 1=小, 2=中, 3=大, 4=极大
    /// </summary>
    public int FontSize { get; set; } = 2;
    
    /// <summary>
    /// 网页缩放百分比 (50-200)
    /// </summary>
    public int PageZoom { get; set; } = 100;
    
    /// <summary>
    /// 标准字体
    /// </summary>
    public string StandardFont { get; set; } = "Microsoft YaHei";
    
    /// <summary>
    /// 标准字体大小 (9-72)
    /// </summary>
    public int StandardFontSize { get; set; } = 16;
    
    /// <summary>
    /// Serif 字体
    /// </summary>
    public string SerifFont { get; set; } = "Times New Roman";
    
    /// <summary>
    /// Sans-serif 字体
    /// </summary>
    public string SansSerifFont { get; set; } = "Arial";
    
    /// <summary>
    /// 等宽字体（宽度固定的字体）
    /// </summary>
    public string FixedWidthFont { get; set; } = "Consolas";
    
    /// <summary>
    /// 最小字号 (6-24)
    /// </summary>
    public int MinimumFontSize { get; set; } = 12;
    
    #endregion
    
    #region 内容设置 (Global Content Settings)

    /// <summary>
    /// Cookie 设置: 0=允许设置本地数据, 1=仅保留到退出, 2=阻止网站设置任何数据
    /// </summary>
    public int CookieSetting { get; set; } = 0;

    /// <summary>
    /// 阻止第三方 Cookie
    /// </summary>
    public bool BlockThirdPartyCookies { get; set; } = false;

    /// <summary>
    /// 图片设置: 0=显示所有图片, 1=不显示任何图片
    /// </summary>
    public int ImageSetting { get; set; } = 0;

    /// <summary>
    /// JavaScript 设置: 0=允许所有网站运行, 1=不允许任何网站运行
    /// </summary>
    public int JavaScriptSetting { get; set; } = 0;

    /// <summary>
    /// 处理程序设置: 0=允许网站要求成为默认处理程序, 1=不允许任何网站处理协议
    /// </summary>
    public int HandlerSetting { get; set; } = 0;

    /// <summary>
    /// 插件设置: 0=检测并运行重要内容, 1=运行所有插件内容, 2=让我自行选择
    /// </summary>
    public int PluginSetting { get; set; } = 1;

    /// <summary>
    /// 位置设置: 0=允许所有网站跟踪, 1=询问（推荐）, 2=不允许任何网站跟踪
    /// </summary>
    public int LocationSetting { get; set; } = 1;

    /// <summary>
    /// 通知设置: 0=允许所有网站显示通知, 1=询问（推荐）, 2=不允许任何网站显示通知
    /// </summary>
    public int NotificationSetting { get; set; } = 1;

    /// <summary>
    /// 鼠标锁定设置: 0=允许所有网站隐藏鼠标指针, 1=询问（推荐）, 2=不允许任何网站隐藏鼠标指针
    /// </summary>
    public int MouseLockSetting { get; set; } = 1;

    /// <summary>
    /// 允许将标识符用于受保护内容
    /// </summary>
    public bool ProtectedContentSetting { get; set; } = true;

    /// <summary>
    /// 麦克风设置: 0=询问（推荐）, 1=不允许网站使用麦克风
    /// </summary>
    public int MicSetting { get; set; } = 0;

    /// <summary>
    /// 摄像头设置: 0=询问（推荐）, 1=不允许网站使用摄像头
    /// </summary>
    public int CameraSetting { get; set; } = 0;

    /// <summary>
    /// 未经过沙盒屏蔽的插件访问: 0=允许所有网站使用插件访问, 1=询问（推荐）, 2=不允许任何网站使用插件访问
    /// </summary>
    public int UnsandboxedPluginSetting { get; set; } = 1;

    /// <summary>
    /// 自动下载设置: 0=允许所有网站自动下载多个文件, 1=询问（推荐）, 2=禁止任何网站自动下载多个文件
    /// </summary>
    public int AutomaticDownloadSetting { get; set; } = 1;

    /// <summary>
    /// MIDI 设备设置: 0=允许所有网站访问, 1=询问（推荐）, 2=禁止任何网站访问
    /// </summary>
    public int MidiSetting { get; set; } = 1;

    #endregion

    #region 收藏夹（兼容旧版）
    
    public List<string> Favorites { get; set; } = new();
    
    #endregion
}
