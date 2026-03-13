using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Browser;
using System.Diagnostics;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 任务管理器窗体 - 显示浏览器进程和标签页资源占用
/// </summary>
public class TaskManagerForm : Form
{
    private readonly BrowserTabManager _tabManager;
    private readonly ListView _listView;
    private readonly Button _endProcessBtn;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    
    public TaskManagerForm(BrowserTabManager tabManager)
    {
        _tabManager = tabManager;
        AppIconHelper.SetIcon(this);
        
        Text = Localization.Raw();
        Size = DpiHelper.Scale(new Size(600, 400));
        MinimumSize = DpiHelper.Scale(new Size(500, 300));
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));
        
        // 创建 ListView
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        
        _listView.Columns.Add(Localization.Raw(), DpiHelper.Scale(250));
        _listView.Columns.Add(Localization.Raw(), DpiHelper.Scale(80), HorizontalAlignment.Right);
        _listView.Columns.Add(Localization.Raw(), DpiHelper.Scale(60), HorizontalAlignment.Right);
        _listView.Columns.Add(Localization.Raw(), DpiHelper.Scale(60), HorizontalAlignment.Right);
        _listView.Columns.Add(Localization.Raw(), DpiHelper.Scale(80), HorizontalAlignment.Right);
        
        // 底部面板
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = DpiHelper.Scale(50),
            Padding = DpiHelper.Scale(new Padding(10))
        };
        
        _endProcessBtn = new Button
        {
            Text = Localization.Raw(),
            Size = DpiHelper.Scale(new Size(90, 30)),
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _endProcessBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _endProcessBtn.Click += OnEndProcessClick;
        
        bottomPanel.Controls.Add(_endProcessBtn);
        
        Controls.Add(_listView);
        Controls.Add(bottomPanel);
        
        _listView.SelectedIndexChanged += (s, e) => 
            _endProcessBtn.Enabled = _listView.SelectedItems.Count > 0;
        
        // 定时刷新
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _refreshTimer.Tick += (s, e) => RefreshProcessList();
        _refreshTimer.Start();
        
        Load += (s, e) => RefreshProcessList();
        FormClosed += (s, e) => _refreshTimer.Stop();
    }
    
    private void RefreshProcessList()
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();
        
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            
            // 浏览器主进程
            var browserItem = new ListViewItem(new[]
            {
                Localization.Raw(),
                FormatMemory(currentProcess.WorkingSet64),
                "0",
                Localization.Raw(),
                currentProcess.Id.ToString()
            });
            browserItem.ImageIndex = 0;
            _listView.Items.Add(browserItem);

            // GPU 进程（模拟）
            var gpuItem = new ListViewItem(new[]
            {
                Localization.Raw(),
                Localization.Raw(),
                "0",
                Localization.Raw(),
                GetGpuProcessId()
            });
            _listView.Items.Add(gpuItem);
            
            // 各个标签页
            foreach (var tab in _tabManager.Tabs)
            {
                var title = tab.Title;
                if (title.Length > 30) title = title[..27] + "...";
                
                var tabItem = new ListViewItem(new[]
                {
                    Localization.T("task_manager.tab_title", new Dictionary<string, string> { { "title", title } }),
                    Localization.Raw(),
                    "0",
                    Localization.Raw(),
                    GetTabProcessId(tab)
                });
                tabItem.Tag = tab;
                _listView.Items.Add(tabItem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshProcessList failed: {ex.Message}");
        }
        
        _listView.EndUpdate();
    }
    
    private string FormatMemory(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
    
    private string GetGpuProcessId()
    {
        try
        {
            // 尝试获取 WebView2 相关的 GPU 进程
            var processes = Process.GetProcessesByName("msedgewebview2");
            if (processes.Length > 1)
            {
                return processes[1].Id.ToString();
            }
        }
        catch { }
        return "N/A";
    }
    
    private string GetTabProcessId(BrowserTab tab)
    {
        try
        {
            // WebView2 的每个标签页可能有独立的渲染进程
            var processes = Process.GetProcessesByName("msedgewebview2");
            if (processes.Length > 0)
            {
                // 返回一个相关进程的 ID（简化实现）
                var index = _tabManager.Tabs.ToList().IndexOf(tab);
                if (index >= 0 && index < processes.Length)
                {
                    return processes[index].Id.ToString();
                }
                return processes[0].Id.ToString();
            }
        }
        catch { }
        return "N/A";
    }
    
    private void OnEndProcessClick(object? sender, EventArgs e)
    {
        if (_listView.SelectedItems.Count == 0) return;
        
        var selectedItem = _listView.SelectedItems[0];
        var tab = selectedItem.Tag as BrowserTab;
        
        if (tab != null)
        {
            var result = MessageBox.Show(
                Localization.Raw(),
                Localization.Raw(),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                _tabManager.CloseTab(tab);
                RefreshProcessList();
            }
        }
        else
        {
            MessageBox.Show(Localization.Raw(), Localization.T("common.info"), 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
