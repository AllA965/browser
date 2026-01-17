namespace MiniWorldBrowser.Models;

/// <summary>
/// 书签数据模型
/// </summary>
public class Bookmark
{
    /// <summary>
    /// 唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 书签标题
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// 书签 URL（文件夹时为空）
    /// </summary>
    public string Url { get; set; } = "";
    
    /// <summary>
    /// 网站图标 URL
    /// </summary>
    public string? FaviconUrl { get; set; }
    
    /// <summary>
    /// 父文件夹 ID，null 表示在收藏栏根目录
    /// </summary>
    public string? ParentId { get; set; }
    
    /// <summary>
    /// 是否为文件夹
    /// </summary>
    public bool IsFolder { get; set; }
    
    /// <summary>
    /// 排序顺序
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
}
