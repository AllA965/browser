using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MiniWorldBrowser.Constants;

namespace MiniWorldBrowser.Helpers;

/// <summary>
/// 默认浏览器设置辅助类
/// </summary>
public static class DefaultBrowserHelper
{
    private const string AppId = "KunQiongBrowser"; // 使用更唯一的 ID
    private static readonly string AppPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern void SHChangeNotify(long wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const long SHCNE_ASSOCCHANGED = 0x08000000L;
    private const uint SHCNF_IDLIST = 0x0000U;

    /// <summary>
    /// 检查当前应用是否为默认浏览器
    /// </summary>
    public static bool IsDefaultBrowser()
    {
        try
        {
            // 检查 HTTP 协议关联
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
            if (key != null)
            {
                var progId = key.GetValue("ProgId")?.ToString();
                return progId == AppId;
            }

            // 回退检查：检查旧式关联
            using var httpKey = Registry.ClassesRoot.OpenSubKey(@"http\shell\open\command");
            if (httpKey != null)
            {
                var command = httpKey.GetValue("")?.ToString();
                return !string.IsNullOrEmpty(command) && command.Contains(AppPath);
            }
        }
        catch
        {
            // 忽略读取错误
        }
        return false;
    }

    /// <summary>
    /// 将当前应用设为默认浏览器
    /// </summary>
    public static void SetAsDefaultBrowser()
    {
        try
        {
            RegisterBrowser();

            // 在 Windows 10+ 中，必须由用户在设置中手动选择
            // 我们只能引导用户到设置页面
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:defaultapps",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"设置默认浏览器失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 在注册表中注册浏览器能力
    /// </summary>
    private static void RegisterBrowser()
    {        if (string.IsNullOrEmpty(AppPath)) return;

        try
        {
            string exeName = System.IO.Path.GetFileName(AppPath);

            // 1. 注册 ProgID
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{AppId}"))
            {
                key.SetValue("", AppConstants.AppName);
                key.SetValue("FriendlyTypeName", AppConstants.AppName);
                key.SetValue("AppUserModelID", AppId);
                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                {                    iconKey.SetValue("", $"{AppPath},0");
                }
                using (var commandKey = key.CreateSubKey(@"shell\open\command"))
                {
                    commandKey.SetValue("", $"\"{AppPath}\" \"%1\"");
                }
            }

            // 2. 注册 Applications 项 (Open With 列表)
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{exeName}"))
            {
                key.SetValue("FriendlyAppName", AppConstants.AppName);
                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                {                    iconKey.SetValue("", $"{AppPath},0");
                }
                using (var shellKey = key.CreateSubKey(@"shell\open\command"))
                {
                    shellKey.SetValue("", $"\"{AppPath}\" \"%1\"");
                }
                using (var supportedTypes = key.CreateSubKey("SupportedTypes"))
                {
                    supportedTypes.SetValue(".htm", "");
                    supportedTypes.SetValue(".html", "");
                    supportedTypes.SetValue(".shtml", "");
                    supportedTypes.SetValue(".svg", "");
                    supportedTypes.SetValue(".webp", "");
                }
            }

            // 3. 注册扩展名的 OpenWithProgids
            string[] extensions = { ".htm", ".html", ".shtml", ".xht", ".xhtml", ".webp" };
            foreach (var ext in extensions)
            {
                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithProgids"))
                {
                    key.SetValue(AppId, "");
                }
            }

            // 4. 注册到 StartMenuInternet
            string rootPath = $@"Software\Clients\StartMenuInternet\{AppId}";
            using (var key = Registry.CurrentUser.CreateSubKey(rootPath))
            {
                key.SetValue("", AppConstants.AppName);
                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                {                    iconKey.SetValue("", $"{AppPath},0");
                }
                using (var shellKey = key.CreateSubKey(@"shell\open\command"))
                {
                    shellKey.SetValue("", $"\"{AppPath}\"");
                }

                using (var capKey = key.CreateSubKey("Capabilities"))
                {
                    capKey.SetValue("ApplicationDescription", $"{AppConstants.AppName} - 安全、智能的 AI 浏览器");
                    capKey.SetValue("ApplicationIcon", $"{AppPath},0");
                    capKey.SetValue("ApplicationName", AppConstants.AppName);

                    using (var assocKey = capKey.CreateSubKey("FileAssociations"))
                    {
                        assocKey.SetValue(".htm", AppId);
                        assocKey.SetValue(".html", AppId);
                        assocKey.SetValue(".shtml", AppId);
                        assocKey.SetValue(".xht", AppId);
                        assocKey.SetValue(".xhtml", AppId);
                        assocKey.SetValue(".webp", AppId);
                    }

                    using (var protoKey = capKey.CreateSubKey("URLAssociations"))
                    {
                        protoKey.SetValue("http", AppId);
                        protoKey.SetValue("https", AppId);
                        protoKey.SetValue("ftp", AppId);
                    }
                }
            }

            // 5. 注册为已安装的关联应用
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                key.SetValue(AppId, $@"Software\Clients\StartMenuInternet\{AppId}\Capabilities");
            }

            // 6. 注册旧式协议关联
            RegisterOldStyleAssociation("http");
            RegisterOldStyleAssociation("https");

            // 7. 通知系统关联已更改
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注册浏览器失败: {ex.Message}");
        }
    }

    private static void RegisterOldStyleAssociation(string protocol)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}");
            key.SetValue("URL Protocol", "");
            using var commandKey = key.CreateSubKey(@"shell\open\command");
            commandKey.SetValue("", $"\"{AppPath}\" \"%1\"");
        }
        catch { }
    }
}
