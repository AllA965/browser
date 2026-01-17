namespace MiniWorldBrowser.Models;

/// <summary>
/// 自定义搜索引擎模型
/// </summary>
public class CustomSearchEngine
{
    /// <summary>
    /// 搜索引擎名称
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 关键字（用于快速搜索）
    /// </summary>
    public string Keyword { get; set; } = "";
    
    /// <summary>
    /// 搜索URL（用 %s 代替搜索字词）
    /// </summary>
    public string Url { get; set; } = "";
}
