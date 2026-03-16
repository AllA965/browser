using System.Text;
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
    private readonly object _saveLock = new();

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
        lock (_saveLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(AppConstants.SettingsFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);

                Exception? lastError = null;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    var tempFile = AppConstants.SettingsFile + ".tmp." + Guid.NewGuid().ToString("N");
                    try
                    {
                        File.WriteAllText(tempFile, json, Encoding.UTF8);
                        File.Move(tempFile, AppConstants.SettingsFile, true);

                        SettingsChanged?.Invoke();
                        System.Diagnostics.Debug.WriteLine($"Settings saved successfully to {AppConstants.SettingsFile}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        try
                        {
                            if (File.Exists(tempFile))
                                File.Delete(tempFile);
                        }
                        catch { }

                        if (attempt < 2)
                            Thread.Sleep(35);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Failed to save settings after retries: {lastError?.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }

    public void Reset()
    {
        _settings = new BrowserSettings();
        Save();
    }
}
