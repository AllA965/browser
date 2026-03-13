using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniWorldBrowser.Constants;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Services;

public class UpdateInfo
{
    [JsonPropertyName("has_update")]
    public bool HasUpdate { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("update_log")]
    public string? UpdateLog { get; set; }

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("package_size")]
    public long PackageSize { get; set; }

    [JsonPropertyName("package_hash")]
    public string? PackageHash { get; set; }

    [JsonPropertyName("is_mandatory")]
    public bool IsMandatory { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }
}

public class UpdateService
{
    private const string UpdateApiUrl = "http://software.kunqiongai.com:8000/api/v1/updates/check/";

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient();
            var url = $"{UpdateApiUrl}?software={AppConstants.AppId}&version={AppConstants.AppVersion}";
            var info = await client.GetFromJsonAsync<UpdateInfo>(url);
            return info;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
            return null;
        }
    }

    public async Task CheckAndPromptUpdateAsync(Form owner)
    {
        try
        {
            var info = await CheckForUpdatesAsync();
            if (info != null && info.HasUpdate)
            {
                // 确保在 UI 线程执行
                owner.Invoke(new Action(() =>
                {
                    var msg = Localization.T("about.update_found_message", new Dictionary<string, string> { { "version", info.Version ?? "" }, { "log", info.UpdateLog ?? "" } });
                    var title = Localization.T("about.update_found_title");
                    
                    if (info.IsMandatory)
                    {
                        msg = Localization.T("about.update_found_message", new Dictionary<string, string> { { "version", info.Version ?? "" }, { "log", info.UpdateLog ?? "" } });
                        title = Localization.T("about.update_found_title");
                        MessageBox.Show(owner, msg, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        StartUpdate(info);
                        Application.Exit(); // 强制更新启动后退出
                    }
                    else
                    {
                        var result = MessageBox.Show(owner, msg, title, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (result == DialogResult.Yes)
                        {
                            StartUpdate(info);
                        }
                    }
                }));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Auto update check failed: {ex.Message}");
        }
    }

    public void StartUpdate(UpdateInfo updateInfo)
    {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl)) return;

        string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.exe");
        if (!File.Exists(updaterPath))
        {
            MessageBox.Show(Localization.T("update.updater_missing"), Localization.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        // 去掉末尾的反斜杠，避免命令行参数解析问题
        if (appDir.EndsWith("\\"))
        {
            appDir = appDir.Substring(0, appDir.Length - 1);
        }

        string exeName = "鲲穹AI浏览器.exe";
        int pid = Process.GetCurrentProcess().Id;

        // 恢复为文档要求的 -- 标记参数格式
        string args = $"--url \"{updateInfo.DownloadUrl}\" " +
                      $"--hash \"{updateInfo.PackageHash}\" " +
                      $"--dir \"{appDir}\" " +
                      $"--exe \"{exeName}\" " +
                      $"--pid {pid}";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = args,
                UseShellExecute = true, // 必须为 true 以独立启动
                CreateNoWindow = false
            };

            Process.Start(startInfo);
            Application.Exit(); // 立即退出
        }
        catch (Exception ex)
        {
            MessageBox.Show(Localization.T("clear.failed", new Dictionary<string, string> { { "msg", ex.Message } }), Localization.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
