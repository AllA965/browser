using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Models;

namespace MiniWorldBrowser.Services;

/// <summary>
/// 密码管理服务
/// </summary>
public class PasswordService
{
    private static readonly string PasswordFile = Path.Combine(AppConstants.AppDataFolder, "passwords.dat");
    private static readonly string NeverSaveFile = Path.Combine(AppConstants.AppDataFolder, "neversave.json");
    
    private List<SavedPassword> _passwords = new();
    private List<NeverSavePassword> _neverSave = new();
    
    public IReadOnlyList<SavedPassword> Passwords => _passwords.AsReadOnly();
    public IReadOnlyList<NeverSavePassword> NeverSaveList => _neverSave.AsReadOnly();
    
    public PasswordService()
    {
        Load();
    }
    
    public void Load()
    {
        LoadPasswords();
        LoadNeverSave();
    }
    
    private void LoadPasswords()
    {
        try
        {
            if (File.Exists(PasswordFile))
            {
                var encrypted = File.ReadAllBytes(PasswordFile);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                _passwords = JsonSerializer.Deserialize<List<SavedPassword>>(json) ?? new();
            }
        }
        catch
        {
            _passwords = new();
        }
    }
    
    private void LoadNeverSave()
    {
        try
        {
            if (File.Exists(NeverSaveFile))
            {
                var json = File.ReadAllText(NeverSaveFile);
                _neverSave = JsonSerializer.Deserialize<List<NeverSavePassword>>(json) ?? new();
            }
        }
        catch
        {
            _neverSave = new();
        }
    }
    
    public void Save()
    {
        SavePasswords();
        SaveNeverSave();
    }
    
    private void SavePasswords()
    {
        try
        {
            var dir = Path.GetDirectoryName(PasswordFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var json = JsonSerializer.Serialize(_passwords);
            var data = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(PasswordFile, encrypted);
        }
        catch { }
    }
    
    private void SaveNeverSave()
    {
        try
        {
            var dir = Path.GetDirectoryName(NeverSaveFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var json = JsonSerializer.Serialize(_neverSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(NeverSaveFile, json);
        }
        catch { }
    }
    
    /// <summary>
    /// 保存密码
    /// </summary>
    public void SavePassword(string host, string username, string password)
    {
        // 检查是否已存在
        var existing = _passwords.FirstOrDefault(p => p.Host == host && p.Username == username);
        if (existing != null)
        {
            existing.Password = password;
            existing.SavedTime = DateTime.Now;
        }
        else
        {
            _passwords.Add(new SavedPassword
            {
                Host = host,
                Username = username,
                Password = password
            });
        }
        Save();
    }
    
    /// <summary>
    /// 获取网站的保存密码（支持同一根域名下的子域名匹配）
    /// </summary>
    public List<SavedPassword> GetPasswordsForHost(string host)
    {
        // 提取根域名（如 4399.com）
        var rootDomain = GetRootDomain(host);
        
        return _passwords.Where(p => 
        {
            // 精确匹配
            if (p.Host == host) return true;
            // 同一根域名下的子域名匹配
            var pRootDomain = GetRootDomain(p.Host);
            return rootDomain == pRootDomain;
        }).ToList();
    }
    
    /// <summary>
    /// 提取根域名（如 www.4399.com -> 4399.com, ptlogin.4399.com -> 4399.com）
    /// </summary>
    private static string GetRootDomain(string host)
    {
        if (string.IsNullOrEmpty(host)) return host;
        
        var parts = host.Split('.');
        if (parts.Length <= 2) return host;
        
        // 返回最后两部分（如 4399.com）
        return string.Join(".", parts.Skip(parts.Length - 2));
    }
    
    /// <summary>
    /// 检查密码是否已经保存（相同的host、username和password）
    /// </summary>
    public bool IsPasswordAlreadySaved(string host, string username, string password)
    {
        // 提取根域名进行匹配
        var rootDomain = GetRootDomain(host);
        
        return _passwords.Any(p =>
        {
            // 用户名必须完全匹配
            if (p.Username != username) return false;
            // 密码必须完全匹配
            if (p.Password != password) return false;
            // host精确匹配或同一根域名
            if (p.Host == host) return true;
            var pRootDomain = GetRootDomain(p.Host);
            return rootDomain == pRootDomain;
        });
    }
    
    /// <summary>
    /// 删除密码
    /// </summary>
    public void DeletePassword(string id)
    {
        _passwords.RemoveAll(p => p.Id == id);
        Save();
    }
    
    /// <summary>
    /// 清除所有密码
    /// </summary>
    public void ClearAll()
    {
        _passwords.Clear();
        Save();
    }
    
    /// <summary>
    /// 添加到一律不保存列表
    /// </summary>
    public void AddToNeverSave(string host)
    {
        if (!_neverSave.Any(n => n.Host == host))
        {
            _neverSave.Add(new NeverSavePassword { Host = host });
            Save();
        }
    }
    
    /// <summary>
    /// 检查是否在一律不保存列表中
    /// </summary>
    public bool IsNeverSave(string host)
    {
        return _neverSave.Any(n => n.Host == host);
    }
    
    /// <summary>
    /// 从一律不保存列表移除
    /// </summary>
    public void RemoveFromNeverSave(string host)
    {
        _neverSave.RemoveAll(n => n.Host == host);
        Save();
    }
    
    /// <summary>
    /// 搜索密码
    /// </summary>
    public List<SavedPassword> Search(string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
            return _passwords.ToList();
        
        return _passwords.Where(p => 
            p.Host.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            p.Username.Contains(keyword, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }
}
