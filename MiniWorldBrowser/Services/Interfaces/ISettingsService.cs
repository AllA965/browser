using MiniWorldBrowser.Models;

namespace MiniWorldBrowser.Services.Interfaces;

/// <summary>
/// 设置服务接口
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 当前设置
    /// </summary>
    BrowserSettings Settings { get; }
    
    /// <summary>
    /// 加载设置
    /// </summary>
    BrowserSettings Load();
    
    /// <summary>
    /// 保存设置
    /// </summary>
    void Save();
    
    /// <summary>
    /// 重置为默认设置
    /// </summary>
    void Reset();
    
    /// <summary>
    /// 设置变更事件
    /// </summary>
    event Action? SettingsChanged;
}
