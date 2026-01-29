using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 弹出窗口 - 用于处理网站的 window.open 请求
/// </summary>
public class PopupWindow : Form
{
    public WebView2 WebView { get; private set; }
    private readonly CoreWebView2Environment _environment;
    private ProgressBar _progressBar = null!;

    private readonly CoreWebView2Deferral _deferral;

    public PopupWindow(CoreWebView2Environment environment, CoreWebView2NewWindowRequestedEventArgs args)
    {
        _environment = environment;
        _deferral = args.GetDeferral();
        WebView = new WebView2 { Dock = DockStyle.Fill };
        
        // 设置窗体基本属性
        this.Text = "正在加载...";
        this.ShowInTaskbar = true;
        this.Icon = AppIconHelper.AppIcon;
        this.StartPosition = FormStartPosition.Manual;
        
        // 设置窗口大小和位置
        int width = args.WindowFeatures.HasSize ? (int)args.WindowFeatures.Width : DpiHelper.Scale(800);
        int height = args.WindowFeatures.HasSize ? (int)args.WindowFeatures.Height : DpiHelper.Scale(600);
        
        this.ClientSize = new Size(width, height);

        if (args.WindowFeatures.HasPosition)
        {
            this.Location = new Point((int)args.WindowFeatures.Left, (int)args.WindowFeatures.Top);
        }
        else
        {
            this.StartPosition = FormStartPosition.CenterParent;
        }

        this.FormBorderStyle = FormBorderStyle.Sizable;

        InitializeUI();
        InitializeWebView(args);
    }

    private void InitializeUI()
    {
        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 2,
            Style = ProgressBarStyle.Marquee,
            Visible = true,
            MarqueeAnimationSpeed = 30
        };
        this.Controls.Add(_progressBar);
    }

    private async void InitializeWebView(CoreWebView2NewWindowRequestedEventArgs args)
    {
        try
        { 
            this.Controls.Add(WebView);
            WebView.BringToFront();

            await WebView.EnsureCoreWebView2Async(_environment);
            
            // 关键：将新窗口请求定向到此 WebView
            args.NewWindow = WebView.CoreWebView2;
            args.Handled = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PopupWindow Initialize Error: {ex.Message}");
        }
        finally
        {
            // 必须在异步操作完成后完成延时
            _deferral.Complete();
        }

        // 设置 User-Agent，增加 Edg/ 标识
        WebView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0";
        
        // 允许弹出窗口自己再打开弹出窗口（如扫码后的跳转）
        WebView.CoreWebView2.NewWindowRequested += (s, e) =>
        {
            e.Handled = false; // 让它在同一个窗口或者默认处理
        };

        WebView.CoreWebView2.DocumentTitleChanged += (s, e) =>
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(() => this.Text = WebView.CoreWebView2.DocumentTitle));
            else
                this.Text = WebView.CoreWebView2.DocumentTitle;
        };

        WebView.CoreWebView2.NavigationStarting += (s, e) =>
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(() => _progressBar.Visible = true));
            else
                _progressBar.Visible = true;
        };

        WebView.CoreWebView2.NavigationCompleted += (s, e) =>
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(() => _progressBar.Visible = false));
            else
                _progressBar.Visible = false;

            if (!e.IsSuccess)
            {
                Console.WriteLine($"PopupWindow Navigation Failed: {e.WebErrorStatus}, URL: {WebView.Source}");
                return;
            }

            // 获取当前 URL 并检查是否为已知的“成功后空白页”
            string url = WebView.Source.ToString().ToLower();
            
            // 某些登录流程成功后会跳转到 about:blank 或包含特定关键字的空白页
            // 如果页面标题包含“成功”、“完成”或“登录”且页面已经停止加载
            bool isSuccessPage = url.Contains("success") || url.Contains("callback") || url.Contains("complete") || url.Contains("finished") || url.Contains("done");
            
            // 额外的检测：如果页面 URL 包含登录成功后的重定向标识
            if (isSuccessPage || url == "about:blank" || url.Contains("login_success"))
            {
                // 检查页面内容是否为空，或者是那种典型的“正在跳转/请稍候”的简单提示
                WebView.CoreWebView2.ExecuteScriptAsync(@"
                    (function() {
                        const text = document.body.innerText.trim();
                        // 如果内容为空，或者只有极短的提示文字
                        if (text.length === 0 || text.length < 20) {
                            return true;
                        }
                        return false;
                    })()
                ").ContinueWith(task => {
                    if (task.Status == TaskStatus.RanToCompletion && task.Result?.ToLower() == "true")
                    {
                        // 页面内容为空或提示极短，可能是回调页，等待 1.2 秒后关闭
                        Task.Delay(1200).ContinueWith(_ => {
                            if (!this.IsDisposed)
                                this.Invoke(new Action(this.Close));
                        });
                    }
                });
            }
        };

        // 允许右键菜单，方便用户手动刷新
        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

        WebView.CoreWebView2.WindowCloseRequested += (s, e) =>
        {
            if (this.IsDisposed) return;
            this.Invoke(new Action(this.Close));
        };

        // 捕获脚本错误，防止白屏
        WebView.CoreWebView2.ProcessFailed += (s, e) =>
        {
            Console.WriteLine($"PopupWindow Process Failed: {e.ProcessFailedKind}");
            if (!this.IsDisposed)
                this.Invoke(new Action(this.Close));
        };
    }
}
