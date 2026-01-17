using System.Text.Json;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Services;

/// <summary>
/// 设置服务实现
/// </summary>
public class SettingsService : ISettingsService
{
    private BrowserSettings _settings = new();
    
    public BrowserSettings Settings => _settings;
    
    public event Action? SettingsChanged;
    
    public SettingsService()
    {
        Load();
    }
    
    public BrowserSettings Load()
    {
        try
        {
            if (File.Exists(AppConstants.SettingsFile))
            {
                var json = File.ReadAllText(AppConstants.SettingsFile);
                _settings = JsonSerializer.Deserialize<BrowserSettings>(json) ?? new BrowserSettings();
            }
        }
        catch
        {
            _settings = new BrowserSettings();
        }
        return _settings;
    }
    
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(AppConstants.SettingsFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            
            // 使用临时文件保存，防止写入中断损坏文件
            var tempFile = AppConstants.SettingsFile + ".tmp";
            File.WriteAllText(tempFile, json);
            
            if (File.Exists(AppConstants.SettingsFile))
                File.Delete(AppConstants.SettingsFile);
                
            File.Move(tempFile, AppConstants.SettingsFile);
            
            SettingsChanged?.Invoke();
            System.Diagnostics.Debug.WriteLine($"Settings saved successfully to {AppConstants.SettingsFile}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            // 如果是在开发环境下，可以考虑弹窗提示，但作为服务层，通常只记录日志
        }
    }
    
    public void Reset()
    {
        _settings = new BrowserSettings();
        Save();
    }
}
