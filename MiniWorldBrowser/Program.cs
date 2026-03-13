using MiniWorldBrowser.Forms;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string logPath = Path.Combine(Path.GetTempPath(), "KunQiongBrowser_startup.log");
        try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] start\n"); } catch { }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"ThreadException: {e.Exception}");
            try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] thread-ex: {e.Exception}\n"); } catch { }
            MessageBox.Show(Localization.Raw(),
                Localization.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"UnhandledException: {ex}");
            try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] unhandled-ex: {ex}\n"); } catch { }
            MessageBox.Show(Localization.Raw(),
                Localization.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();
        Localization.Initialize();
        
        // 设置全局默认字体为微软雅黑，使界面更现代化，接近 Edge 风格
        Application.SetDefaultFont(new Font("Microsoft YaHei UI", 9F));
        
        try
        {
            PythonBridgeManager.StartBridge();
            try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] bridge-started\n"); } catch { }
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] bridge-failed: {ex}\n"); } catch { }
        }
        
        // 解析启动参数
        string? initialUrl = null;
        if (args.Length > 0)
        {
            initialUrl = args[0];
        }
        
        // 使用 ApplicationContext 来管理多窗口生命周期
        var context = new MultiWindowApplicationContext();
        try
        {
            context.ShowMainForm(initialUrl);
            try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] mainform-shown\n"); } catch { }
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] show-failed: {ex}\n"); } catch { }
            MessageBox.Show(Localization.Raw(), Localization.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Application.Run(context);
    }
}

/// <summary>
/// 多窗口应用程序上下文 - 只有当所有窗口都关闭时才退出应用
/// </summary>
public class MultiWindowApplicationContext : ApplicationContext
{
    private int _formCount = 0;
    private readonly object _lock = new();
    
    public void ShowMainForm(string? initialUrl = null)
    {
        var form = new MainForm(false);
        RegisterForm(form);
        form.Show();
    }
    
    /// <summary>
    /// 注册窗口，当窗口关闭时检查是否需要退出应用
    /// </summary>
    public void RegisterForm(Form form)
    {
        lock (_lock)
        {
            _formCount++;
        }
        
        form.FormClosed += (s, e) =>
        {
            lock (_lock)
            {
                _formCount--;
                
                // 当所有窗口都关闭时，退出应用
                if (_formCount <= 0)
                {
                    // 停止 Python Bridge 服务
                    PythonBridgeManager.StopBridge();
                    
                    ExitThread();
                }
            }
        };
    }
    
    /// <summary>
    /// 获取当前实例（用于其他地方注册窗口）
    /// </summary>
    public static MultiWindowApplicationContext? Current { get; private set; }
    
    public MultiWindowApplicationContext()
    {
        Current = this;
    }
}
