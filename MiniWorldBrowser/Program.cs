using MiniWorldBrowser.Forms;

namespace MiniWorldBrowser;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"ThreadException: {e.Exception}");
            MessageBox.Show($"发生错误: {e.Exception.Message}\n\n{e.Exception.StackTrace}", 
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"UnhandledException: {ex}");
            MessageBox.Show($"发生严重错误: {ex?.Message}\n\n{ex?.StackTrace}", 
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        
        ApplicationConfiguration.Initialize();
        
        // 设置全局默认字体为微软雅黑，使界面更现代化，接近 Edge 风格
        Application.SetDefaultFont(new Font("Microsoft YaHei UI", 9F));
        
        // 使用 ApplicationContext 来管理多窗口生命周期
        var context = new MultiWindowApplicationContext();
        context.ShowMainForm();
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
    
    public void ShowMainForm()
    {
        var form = new MainForm();
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
