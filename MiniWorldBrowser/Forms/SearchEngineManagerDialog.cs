using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 搜索引擎管理对话框
/// </summary>
public class SearchEngineManagerDialog : Form
{
    private readonly ISettingsService _settingsService;
    
    private ListView _defaultEnginesList = null!;
    private ListView _customEnginesList = null!;
    private TextBox _txtName = null!;
    private TextBox _txtKeyword = null!;
    private TextBox _txtUrl = null!;
    private Button _btnAdd = null!;
    private bool _isUpdating; // 防止递归更新
    
    // 默认搜索引擎
    private readonly (string Name, string Domain, string Url)[] _defaultEngines = new[]
    {
        (Localization.T("settings.search.options.google"), "google.com", "https://www.google.com/search?q=%s"),
        (Localization.T("settings.search.options.baidu"), "baidu.com", "https://www.baidu.com/s?wd=%s"),
        (Localization.T("settings.search.options.360"), "so.com", "https://www.so.com/s?q=%s&ie=utf-8"),
        (Localization.T("settings.search.options.bing"), "bing.com", "https://www.bing.com/search?q=%s")
    };
    
    public SearchEngineManagerDialog(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
    }
    
    private void InitializeComponent()
    {
        AppIconHelper.SetIcon(this);
        Text = Localization.Raw();
        Size = DpiHelper.Scale(new Size(700, 550));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        
        // 默认搜索设置标题
        var lblDefault = new Label
        {
            Text = Localization.Raw(),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(11F), FontStyle.Bold),
            Location = DpiHelper.Scale(new Point(20, 15)),
            AutoSize = true
        };
        
        // 默认搜索引擎列表
        _defaultEnginesList = new ListView
        {
            Location = DpiHelper.Scale(new Point(20, 45)),
            Size = DpiHelper.Scale(new Size(640, 130)),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle,
            CheckBoxes = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _defaultEnginesList.Columns.Add(Localization.Raw(), DpiHelper.Scale(180));
        _defaultEnginesList.Columns.Add(Localization.Raw(), DpiHelper.Scale(150));
        _defaultEnginesList.Columns.Add(Localization.Raw(), DpiHelper.Scale(280));
        _defaultEnginesList.ItemCheck += DefaultEnginesList_ItemCheck;
        
        // 加载默认搜索引擎数据
        LoadDefaultEngines();
        
        // 其他搜索引擎标题
        var lblOther = new Label
        {
            Text = Localization.Raw(),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(11F), FontStyle.Bold),
            Location = DpiHelper.Scale(new Point(20, 190)),
            AutoSize = true
        };
        
        // 添加新搜索引擎的输入框
        _txtName = new TextBox
        {
            Location = DpiHelper.Scale(new Point(20, 220)),
            Width = DpiHelper.Scale(150),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        SetPlaceholder(_txtName, Localization.Raw());
        
        _txtKeyword = new TextBox
        {
            Location = DpiHelper.Scale(new Point(180, 220)),
            Width = DpiHelper.Scale(120),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        SetPlaceholder(_txtKeyword, Localization.Raw());
        
        _txtUrl = new TextBox
        {
            Location = DpiHelper.Scale(new Point(310, 220)),
            Width = DpiHelper.Scale(280),
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F))
        };
        SetPlaceholder(_txtUrl, Localization.Raw());
        
        _btnAdd = new Button
        {
            Text = "+",
            Location = DpiHelper.Scale(new Point(600, 218)),
            Size = DpiHelper.Scale(new Size(30, 25)),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(10F), FontStyle.Bold)
        };
        _btnAdd.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _btnAdd.Click += BtnAdd_Click;
        
        // 自定义搜索引擎列表
        _customEnginesList = new ListView
        {
            Location = DpiHelper.Scale(new Point(20, 255)),
            Size = DpiHelper.Scale(new Size(640, 200)),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _customEnginesList.Columns.Add(Localization.Raw(), DpiHelper.Scale(150));
        _customEnginesList.Columns.Add(Localization.Raw(), DpiHelper.Scale(120));
        _customEnginesList.Columns.Add(Localization.Raw(), DpiHelper.Scale(340));
        _customEnginesList.KeyDown += CustomEnginesList_KeyDown;
        
        // 右键菜单
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(Localization.Raw(), null, (s, e) => SetAsDefault());
        contextMenu.Items.Add(Localization.Raw(), null, (s, e) => DeleteSelected());
        _customEnginesList.ContextMenuStrip = contextMenu;
        
        // 完成按钮
        var btnDone = new Button
        {
            Text = Localization.Raw(),
            Location = DpiHelper.Scale(new Point(585, 470)),
            Size = DpiHelper.Scale(new Size(75, 30)),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.OK
        };
        
        Controls.AddRange(new Control[]
        {
            lblDefault, _defaultEnginesList,
            lblOther, _txtName, _txtKeyword, _txtUrl, _btnAdd,
            _customEnginesList, btnDone
        });
        
        AcceptButton = btnDone;
        
        // 加载自定义搜索引擎
        LoadCustomEngines();
    }
    
    private void LoadDefaultEngines()
    {
        _isUpdating = true;
        try
        {
            _defaultEnginesList.Items.Clear();
            var currentDefault = _settingsService.Settings.AddressBarSearchEngine;
            
            for (int i = 0; i < _defaultEngines.Length; i++)
            {
                var engine = _defaultEngines[i];
                var displayName = engine.Name;
                if (i == currentDefault)
                    displayName += Localization.Raw();
                
                var item = new ListViewItem(displayName);
                item.SubItems.Add(engine.Domain);
                item.SubItems.Add(engine.Url);
                item.Tag = i;
                item.Checked = i == currentDefault;
                _defaultEnginesList.Items.Add(item);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }
    
    private void LoadCustomEngines()
    {
        _customEnginesList.Items.Clear();
        foreach (var engine in _settingsService.Settings.CustomSearchEngines)
        {
            var item = new ListViewItem(engine.Name);
            item.SubItems.Add(engine.Keyword);
            item.SubItems.Add(engine.Url);
            item.Tag = engine;
            _customEnginesList.Items.Add(item);
        }
    }
    
    private void SetPlaceholder(TextBox textBox, string placeholder)
    {
        textBox.Text = placeholder;
        textBox.ForeColor = Color.Gray;
        
        textBox.GotFocus += (s, e) =>
        {
            if (textBox.Text == placeholder)
            {
                textBox.Text = "";
                textBox.ForeColor = Color.Black;
            }
        };
        
        textBox.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = placeholder;
                textBox.ForeColor = Color.Gray;
            }
        };
    }
    
    private void RefreshLists()
    {
        LoadDefaultEngines();
        LoadCustomEngines();
    }
    
    private void DefaultEnginesList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_isUpdating) return;
        
        if (e.NewValue == CheckState.Checked)
        {
            // 设置为默认
            _settingsService.Settings.AddressBarSearchEngine = e.Index;
            _settingsService.Save();
            
            // 更新显示（延迟执行避免在事件中修改列表）
            BeginInvoke(() => RefreshLists());
        }
        else if (e.CurrentValue == CheckState.Checked)
        {
            // 不允许取消当前默认
            e.NewValue = CheckState.Checked;
        }
    }
    
    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var name = _txtName.Text;
        var keyword = _txtKeyword.Text;
        var url = _txtUrl.Text;
        
        // 验证输入
        if (string.IsNullOrWhiteSpace(name) || name == Localization.Raw())
        {
            MessageBox.Show(Localization.Raw(), Localization.Raw(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(url) || url == Localization.Raw())
        {
            MessageBox.Show(Localization.Raw(), Localization.Raw(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        
        if (!url.Contains("%s"))
        {
            MessageBox.Show(Localization.Raw(), Localization.Raw(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        
        // 添加新搜索引擎
        var engine = new CustomSearchEngine
        {
            Name = name,
            Keyword = keyword == Localization.Raw() ? "" : keyword,
            Url = url
        };
        
        _settingsService.Settings.CustomSearchEngines.Add(engine);
        _settingsService.Save();
        
        // 刷新列表
        LoadCustomEngines();
        
        // 清空输入框
        _txtName.Text = Localization.Raw();
        _txtName.ForeColor = Color.Gray;
        _txtKeyword.Text = Localization.Raw();
        _txtKeyword.ForeColor = Color.Gray;
        _txtUrl.Text = Localization.Raw();
        _txtUrl.ForeColor = Color.Gray;
    }
    
    private void CustomEnginesList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }
    
    private void SetAsDefault()
    {
        if (_customEnginesList.SelectedItems.Count == 0) return;
        
        var engine = _customEnginesList.SelectedItems[0].Tag as CustomSearchEngine;
        if (engine == null) return;
        
        // 设置自定义搜索引擎为默认（索引从4开始）
        var index = _settingsService.Settings.CustomSearchEngines.IndexOf(engine);
        _settingsService.Settings.AddressBarSearchEngine = 4 + index;
        _settingsService.Save();
        
        RefreshLists();
    }
    
    private void DeleteSelected()
    {
        if (_customEnginesList.SelectedItems.Count == 0) return;
        
        var engine = _customEnginesList.SelectedItems[0].Tag as CustomSearchEngine;
        if (engine == null) return;
        
        if (MessageBox.Show(Localization.T("search_engine.confirm_delete", new Dictionary<string, string> { { "name", engine.Name } }), Localization.T("confirm.title"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _settingsService.Settings.CustomSearchEngines.Remove(engine);
            _settingsService.Save();
            LoadCustomEngines();
        }
    }
}
