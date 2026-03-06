using System.Drawing.Drawing2D;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Browser;

namespace MiniWorldBrowser.Controls;

/// <summary>
/// 自定义下载面板 - 显示所有下载任务及其进度
/// </summary>
public class DownloadPanel : UserControl
{
    private readonly BrowserTabManager _tabManager;
    private readonly FlowLayoutPanel _listPanel;
    private readonly Label _titleLabel;
    private readonly System.Windows.Forms.Timer _updateTimer;

    public DownloadPanel(BrowserTabManager tabManager)
    {
        _tabManager = tabManager;
        
        this.Size = DpiHelper.Scale(new Size(350, 450));
        this.BackColor = Color.White;
        this.Padding = new Padding(1);
        
        // 标题栏
        var header = new Panel { Dock = DockStyle.Top, Height = DpiHelper.Scale(45), BackColor = Color.FromArgb(250, 250, 250) };
        _titleLabel = new Label 
        { 
            Text = "下载", 
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(11F), FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 30),
            Location = DpiHelper.Scale(new Point(15, 12)),
            AutoSize = true
        };
        
        var closeBtn = new Button
        {
            Text = "✕",
            Size = DpiHelper.Scale(new Size(30, 30)),
            Location = new Point(this.Width - DpiHelper.Scale(40), DpiHelper.Scale(7)),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Gray,
            Cursor = Cursors.Hand
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.Click += (s, e) => this.Hide();
        
        header.Controls.Add(_titleLabel);
        header.Controls.Add(closeBtn);
        
        // 列表容器
        _listPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 5, 0, 5)
        };
        
        this.Controls.Add(_listPanel);
        this.Controls.Add(header);
        
        // 边框
        this.Paint += (s, e) => {
            e.Graphics.DrawRectangle(new Pen(Color.FromArgb(200, 200, 200)), 0, 0, Width - 1, Height - 1);
        };

        // 定时刷新进度
        _updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _updateTimer.Tick += (s, e) => RefreshList();
        _updateTimer.Start();
        
        RefreshList();
    }

    public void RefreshList()
    {
        var downloads = _tabManager.GetDownloads();
        
        // 简单的差异化更新逻辑
        _listPanel.SuspendLayout();
        
        // 如果数量不对，清空重绘 (简单处理)
        if (_listPanel.Controls.Count != downloads.Count)
        {
            _listPanel.Controls.Clear();
            foreach (var item in downloads.AsEnumerable().Reverse()) // 最新的在上面
            {
                _listPanel.Controls.Add(new DownloadItemControl(item));
            }
        }
        else
        {
            // 更新现有控件
            for (int i = 0; i < downloads.Count; i++)
            {
                var ctrl = _listPanel.Controls[downloads.Count - 1 - i] as DownloadItemControl;
                ctrl?.UpdateStatus();
            }
        }
        
        _listPanel.ResumeLayout();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) RefreshList();
    }
}

/// <summary>
/// 单个下载项的显示控件
/// </summary>
internal class DownloadItemControl : UserControl
{
    private readonly DownloadItem _item;
    private readonly ProgressBar _progressBar;
    private readonly Label _nameLabel;
    private readonly Label _statusLabel;

    public DownloadItemControl(DownloadItem item)
    {
        _item = item;
        this.Size = new Size(320, DpiHelper.Scale(65));
        this.Margin = new Padding(5, 2, 5, 2);
        this.BackColor = Color.White;

        _nameLabel = new Label
        {
            Text = item.FileName,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F)),
            Location = DpiHelper.Scale(new Point(10, 8)),
            Size = DpiHelper.Scale(new Size(280, 20)),
            AutoEllipsis = true
        };

        _progressBar = new ProgressBar
        {
            Location = DpiHelper.Scale(new Point(10, 32)),
            Size = DpiHelper.Scale(new Size(250, 4)),
            Maximum = 100,
            Value = (int)item.Progress
        };

        _statusLabel = new Label
        {
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(8F)),
            ForeColor = Color.Gray,
            Location = DpiHelper.Scale(new Point(10, 42)),
            Size = DpiHelper.Scale(new Size(220, 15)),
            Text = GetStatusText()
        };

        var folderBtn = new Button
        {
            Text = "📁",
            Size = DpiHelper.Scale(new Size(24, 24)),
            Location = new Point(this.Width - DpiHelper.Scale(35), DpiHelper.Scale(30)),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        folderBtn.FlatAppearance.BorderSize = 0;
        folderBtn.Click += (s, e) => {
            if (!string.IsNullOrEmpty(_item.FilePath)) {
                try {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_item.FilePath}\"");
                } catch {
                    try {
                        System.Diagnostics.Process.Start("explorer.exe", Path.GetDirectoryName(_item.FilePath) ?? "");
                    } catch { }
                }
            }
        };

        this.Controls.Add(_nameLabel);
        this.Controls.Add(_progressBar);
        this.Controls.Add(_statusLabel);
        this.Controls.Add(folderBtn);
        
        // 悬停效果
        this.MouseEnter += (s, e) => this.BackColor = Color.FromArgb(245, 245, 245);
        this.MouseLeave += (s, e) => this.BackColor = Color.White;
        
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        _nameLabel.Text = _item.FileName;
        _progressBar.Value = (int)_item.Progress;
        _statusLabel.Text = GetStatusText();
        _progressBar.Visible = _item.Status == DownloadStatus.Downloading;
    }

    private string GetStatusText()
    {
        switch (_item.Status)
        {
            case DownloadStatus.Downloading:
                string sizeStr = _item.TotalBytes > 0 ? $" / {FormatSize(_item.TotalBytes)}" : "";
                return $"正在下载: {FormatSize(_item.ReceivedBytes)}{sizeStr} ({_item.Progress:F1}%)";
            case DownloadStatus.Completed:
                return "已完成";
            case DownloadStatus.Cancelled:
                return "已取消";
            case DownloadStatus.Failed:
                return "下载失败";
            default:
                return "等待中...";
        }
    }

    private string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F1} {units[unitIndex]}";
    }
}
