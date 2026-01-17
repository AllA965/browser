using System.Text.Json;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Services;

/// <summary>
/// 书签服务实现
/// </summary>
public class BookmarkService : IBookmarkService
{
    private readonly List<Bookmark> _bookmarks = new();
    private static readonly object _lock = new();
    
    public event Action? BookmarksChanged;
    
    public BookmarkService()
    {
        Load();
    }
    
    public List<Bookmark> GetBookmarkBarItems()
    {
        lock (_lock)
        {
            return _bookmarks
                .Where(b => b.ParentId == null && b.ParentId != "other")
                .OrderBy(b => b.Order)
                .ToList();
        }
    }
    
    public List<Bookmark> GetOtherBookmarks()
    {
        lock (_lock)
        {
            return _bookmarks
                .Where(b => b.ParentId == "other")
                .OrderBy(b => b.Order)
                .ToList();
        }
    }
    
    public List<Bookmark> GetChildren(string folderId)
    {
        lock (_lock)
        {
            return _bookmarks
                .Where(b => b.ParentId == folderId)
                .OrderBy(b => b.Order)
                .ToList();
        }
    }
    
    public Bookmark AddBookmark(string title, string url, string? parentId = null, string? faviconUrl = null)
    {
        lock (_lock)
        {
            var maxOrder = _bookmarks
                .Where(b => b.ParentId == parentId)
                .Select(b => b.Order)
                .DefaultIfEmpty(-1)
                .Max();
            
            var bookmark = new Bookmark
            {
                Title = title,
                Url = url,
                ParentId = parentId,
                FaviconUrl = faviconUrl,
                IsFolder = false,
                Order = maxOrder + 1
            };
            
            _bookmarks.Add(bookmark);
            Save();
            BookmarksChanged?.Invoke();
            return bookmark;
        }
    }
    
    public Bookmark AddFolder(string title, string? parentId = null)
    {
        lock (_lock)
        {
            var maxOrder = _bookmarks
                .Where(b => b.ParentId == parentId)
                .Select(b => b.Order)
                .DefaultIfEmpty(-1)
                .Max();
            
            var folder = new Bookmark
            {
                Title = title,
                IsFolder = true,
                ParentId = parentId,
                Order = maxOrder + 1
            };
            
            _bookmarks.Add(folder);
            Save();
            BookmarksChanged?.Invoke();
            return folder;
        }
    }
    
    public void UpdateBookmark(string id, string? title = null, string? url = null, string? parentId = null)
    {
        lock (_lock)
        {
            var bookmark = _bookmarks.FirstOrDefault(b => b.Id == id);
            if (bookmark != null)
            {
                if (title != null) bookmark.Title = title;
                if (url != null) bookmark.Url = url;
                if (parentId != null) bookmark.ParentId = parentId == "" ? null : parentId;
                bookmark.ModifiedAt = DateTime.Now;
                Save();
                BookmarksChanged?.Invoke();
            }
        }
    }
    
    public void Delete(string id)
    {
        lock (_lock)
        {
            var item = _bookmarks.FirstOrDefault(b => b.Id == id);
            if (item == null) return;
            
            if (item.IsFolder)
            {
                // 递归删除子项
                var children = _bookmarks.Where(b => b.ParentId == id).ToList();
                foreach (var child in children)
                {
                    Delete(child.Id);
                }
            }
            
            _bookmarks.Remove(item);
            Save();
            BookmarksChanged?.Invoke();
        }
    }
    
    public void Move(string id, string? newParentId, int newOrder)
    {
        lock (_lock)
        {
            var item = _bookmarks.FirstOrDefault(b => b.Id == id);
            if (item == null) return;
            
            // 更新同级其他项的顺序
            var siblings = _bookmarks
                .Where(b => b.ParentId == newParentId && b.Id != id)
                .OrderBy(b => b.Order)
                .ToList();
            
            for (int i = 0; i < siblings.Count; i++)
            {
                siblings[i].Order = i >= newOrder ? i + 1 : i;
            }
            
            item.ParentId = newParentId;
            item.Order = newOrder;
            item.ModifiedAt = DateTime.Now;
            Save();
            BookmarksChanged?.Invoke();
        }
    }
    
    public Bookmark? FindByUrl(string url)
    {
        lock (_lock)
        {
            return _bookmarks.FirstOrDefault(b => !b.IsFolder && b.Url == url);
        }
    }
    
    public List<Bookmark> Search(string keyword)
    {
        lock (_lock)
        {
            return _bookmarks
                .Where(b => !b.IsFolder &&
                    (b.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                     b.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }
    
    private void Load()
    {
        try
        {
            if (File.Exists(AppConstants.BookmarksFile))
            {
                var json = File.ReadAllText(AppConstants.BookmarksFile);
                var loaded = JsonSerializer.Deserialize<List<Bookmark>>(json);
                if (loaded != null)
                {
                    _bookmarks.Clear();
                    _bookmarks.AddRange(loaded);
                }
            }
        }
        catch
        {
            // 忽略加载错误
        }
    }
    
    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(AppConstants.BookmarksFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_bookmarks, options);
            File.WriteAllText(AppConstants.BookmarksFile, json);
        }
        catch
        {
            // 忽略保存错误
        }
    }
}
