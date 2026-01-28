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
        
        Text = "任务管理器 - 鲲穹AI浏览器";
        Size = DpiHelper.Scale(new Size(600, 400));
        MinimumSize = DpiHelper.Scale(new Size(500, 300));
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F));
        
        // 创建 ListView
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        
        _listView.Columns.Add("任务", DpiHelper.Scale(250));
        _listView.Columns.Add("内存", DpiHelper.Scale(80), HorizontalAlignment.Right);
        _listView.Columns.Add("CPU", DpiHelper.Scale(60), HorizontalAlignment.Right);
        _listView.Columns.Add("网络", DpiHelper.Scale(60), HorizontalAlignment.Right);
        _listView.Columns.Add("进程 ID", DpiHelper.Scale(80), HorizontalAlignment.Right);
        
        // 底部面板
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = DpiHelper.Scale(50),
            Padding = DpiHelper.Scale(new Padding(10))
        };
        
        _endProcessBtn = new Button
        {
            Text = "结束进程",
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
                "● 浏览器",
                FormatMemory(currentProcess.WorkingSet64),
                "0",
                "无",
                currentProcess.Id.ToString()
            });
            browserItem.ImageIndex = 0;
            _listView.Items.Add(browserItem);

            // GPU 进程（模拟）
            var gpuItem = new ListViewItem(new[]
            {
                "● GPU 进程",
                "无",
                "0",
                "无",
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
                    $"● 标签页: {title}",
                    "无",
                    "0",
                    "无",
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
                $"确定要结束标签页 \"{tab.Title}\" 吗？",
                "结束进程",
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
            MessageBox.Show("无法结束此进程。只能结束标签页进程。", "提示", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
