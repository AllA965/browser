using MiniWorldBrowser.Models;

namespace MiniWorldBrowser.Services.Interfaces;

/// <summary>
/// 书签服务接口
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// 获取收藏栏根目录的书签
    /// </summary>
    List<Bookmark> GetBookmarkBarItems();
    
    /// <summary>
    /// 获取"其他收藏"文件夹中的书签
    /// </summary>
    List<Bookmark> GetOtherBookmarks();
    
    /// <summary>
    /// 获取指定文件夹下的书签
    /// </summary>
    List<Bookmark> GetChildren(string folderId);
    
    /// <summary>
    /// 添加书签
    /// </summary>
    Bookmark AddBookmark(string title, string url, string? parentId = null, string? faviconUrl = null);
    
    /// <summary>
    /// 添加文件夹
    /// </summary>
    Bookmark AddFolder(string title, string? parentId = null);
    
    /// <summary>
    /// 更新书签
    /// </summary>
    void UpdateBookmark(string id, string? title = null, string? url = null, string? parentId = null);
    
    /// <summary>
    /// 删除书签或文件夹
    /// </summary>
    void Delete(string id);
    
    /// <summary>
    /// 移动书签
    /// </summary>
    void Move(string id, string? newParentId, int newOrder);
    
    /// <summary>
    /// 根据 URL 查找书签
    /// </summary>
    Bookmark? FindByUrl(string url);
    
    /// <summary>
    /// 搜索书签
    /// </summary>
    List<Bookmark> Search(string keyword);
    
    /// <summary>
    /// 书签变更事件
    /// </summary>
    event Action? BookmarksChanged;
}
