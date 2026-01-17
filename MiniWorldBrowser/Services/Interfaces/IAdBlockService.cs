namespace MiniWorldBrowser.Services.Interfaces;

/// <summary>
/// 广告过滤服务接口
/// </summary>
public interface IAdBlockService
{
    /// <summary>
    /// 是否启用广告过滤
    /// </summary>
    bool Enabled { get; set; }
    
    /// <summary>
    /// 过滤模式: 0=不过滤, 1=仅弹窗, 2=强力过滤, 3=自定义规则
    /// </summary>
    int Mode { get; set; }
    
    /// <summary>
    /// 判断 URL 是否应该被阻止
    /// </summary>
    bool ShouldBlock(string url);
    
    /// <summary>
    /// 判断是否应该阻止弹窗
    /// </summary>
    bool ShouldBlockPopup(string url);
    
    /// <summary>
    /// 添加阻止的域名
    /// </summary>
    void AddBlockedDomain(string domain);
    
    /// <summary>
    /// 添加阻止的 URL 模式
    /// </summary>
    void AddBlockedPattern(string pattern);
    
    /// <summary>
    /// 加载自定义规则文件
    /// </summary>
    void LoadCustomRules(string filePath);
    
    /// <summary>
    /// 设置例外网站列表
    /// </summary>
    void SetExceptions(List<string> exceptions);
    
    /// <summary>
    /// 检查网站是否在例外列表中（允许广告）
    /// </summary>
    bool IsExcepted(string url);
    
    /// <summary>
    /// 获取已阻止的请求数量
    /// </summary>
    int BlockedCount { get; }
}
