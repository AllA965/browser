using MiniWorldBrowser.Services.Interfaces;

using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 缩放级别管理对话框
/// </summary>
public class ZoomLevelDialog : Form
{
    private readonly ISettingsService _settingsService;
    private ListView _zoomList = null!;

    public ZoomLevelDialog(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        LoadZoomLevels();
    }

    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        Text = "缩放级别";
        Size = new Size(550, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;

        // 标题行
        var lblHost = new Label
        {
            Text = "主机名（可包含通配符）",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9)
        };

        var lblZoom = new Label
        {
            Text = "缩放",
            Location = new Point(380, 20),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9)
        };

        // 缩放级别列表
        _zoomList = new ListView
        {
            Location = new Point(20, 50),
            Size = new Size(490, 260),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        _zoomList.Columns.Add("主机名", 350);
        _zoomList.Columns.Add("缩放", 100);
        _zoomList.KeyDown += ZoomList_KeyDown;

        // 右键菜单
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("删除", null, (s, e) => DeleteSelected());
        contextMenu.Items.Add("全部删除", null, (s, e) => DeleteAll());
        _zoomList.ContextMenuStrip = contextMenu;

        // 完成按钮
        var btnDone = new Button
        {
            Text = "完成",
            Location = new Point(435, 325),
            Size = new Size(75, 28),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.OK
        };

        Controls.AddRange(new Control[]
        {
            lblHost, lblZoom,
            _zoomList, btnDone
        });

        AcceptButton = btnDone;
    }

    private void LoadZoomLevels()
    {
        _zoomList.Items.Clear();
        
        // 从设置中加载缩放级别
        // 这里使用示例数据，实际应从 BrowserSettings 中读取
        // 格式: "host|zoom" 如 "baidu.com|125%"
        
        // 如果有保存的缩放级别，加载它们
        // foreach (var zoom in _settingsService.Settings.ZoomLevels)
        // {
        //     var parts = zoom.Split('|');
        //     if (parts.Length == 2)
        //     {
        //         var item = new ListViewItem(parts[0]);
        //         item.SubItems.Add(parts[1]);
        //         _zoomList.Items.Add(item);
        //     }
        // }
    }

    private void ZoomList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    private void DeleteSelected()
    {
        if (_zoomList.SelectedItems.Count == 0) return;
        
        var item = _zoomList.SelectedItems[0];
        _zoomList.Items.Remove(item);
        
        // 保存更改到设置
        SaveZoomLevels();
    }

    private void DeleteAll()
    {
        if (_zoomList.Items.Count == 0) return;
        
        if (MessageBox.Show("确定要删除所有缩放级别设置吗？", "确认删除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _zoomList.Items.Clear();
            SaveZoomLevels();
        }
    }

    private void SaveZoomLevels()
    {
        // 保存缩放级别到设置
        // var zoomLevels = new List<string>();
        // foreach (ListViewItem item in _zoomList.Items)
        // {
        //     zoomLevels.Add($"{item.Text}|{item.SubItems[1].Text}");
        // }
        // _settingsService.Settings.ZoomLevels = zoomLevels;
        // _settingsService.Save();
    }

    /// <summary>
    /// 添加或更新缩放级别（供外部调用）
    /// </summary>
    public static void AddZoomLevel(ISettingsService settingsService, string host, int zoomPercent)
    {
        // 实现添加缩放级别的逻辑
        // var zoomLevels = settingsService.Settings.ZoomLevels ?? new List<string>();
        // 
        // // 移除已存在的相同主机
        // zoomLevels.RemoveAll(z => z.StartsWith(host + "|"));
        // 
        // // 添加新的缩放级别
        // if (zoomPercent != 100)
        // {
        //     zoomLevels.Add($"{host}|{zoomPercent}%");
        // }
        // 
        // settingsService.Settings.ZoomLevels = zoomLevels;
        // settingsService.Save();
    }
}
