using MiniWorldBrowser.Services.Interfaces;
using MiniWorldBrowser.Helpers;
using System.Drawing.Text;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 字体和编码设置对话框
/// </summary>
public class FontSettingsDialog : Form
{
    private readonly ISettingsService _settingsService;
    private readonly ComboBox _standardFontCombo;
    private readonly TrackBar _standardFontSizeSlider;
    private readonly Label _standardPreview;
    private readonly ComboBox _serifFontCombo;
    private readonly Label _serifPreview;
    private readonly ComboBox _sansSerifFontCombo;
    private readonly Label _sansSerifPreview;
    private readonly ComboBox _fixedWidthFontCombo;
    private readonly Label _fixedWidthPreview;
    private readonly TrackBar _minFontSizeSlider;
    private readonly Label _minFontSizePreview;
    private readonly Label _minFontSizeValue;
    
    private readonly List<string> _fontNames = new();
    
    public FontSettingsDialog(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        
        Text = "字体和编码";
        Size = DpiHelper.Scale(new Size(550, 580));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        
        // 加载系统字体
        LoadSystemFonts();
        
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = DpiHelper.Scale(new Padding(20))
        };
        
        int y = DpiHelper.Scale(20);
        
        // 标准字体
        var standardLabel = new Label
        {
            Text = "标准字体",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(10F), FontStyle.Bold),
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true
        };
        panel.Controls.Add(standardLabel);
        y += DpiHelper.Scale(30);

        _standardFontCombo = CreateFontComboBox(DpiHelper.Scale(20), y, _settingsService.Settings.StandardFont);
        panel.Controls.Add(_standardFontCombo);
        
        _standardPreview = new Label
        {
            Text = "16: Lorem ipsum dolor sit amet,\nconsectetur adipiscing elit.",
            Location = new Point(DpiHelper.Scale(200), y),
            Size = DpiHelper.Scale(new Size(300, 50)),
            Font = new Font(_settingsService.Settings.StandardFont, DpiHelper.Scale((float)_settingsService.Settings.StandardFontSize))
        };
        panel.Controls.Add(_standardPreview);
        y += DpiHelper.Scale(35);
        
        // 字号滑块
        _standardFontSizeSlider = new TrackBar
        {
            Location = new Point(DpiHelper.Scale(20), y),
            Size = DpiHelper.Scale(new Size(150, 30)),
            Minimum = 9,
            Maximum = 72,
            Value = _settingsService.Settings.StandardFontSize,
            TickFrequency = 10
        };
        _standardFontSizeSlider.ValueChanged += (s, e) => UpdateStandardPreview();
        panel.Controls.Add(_standardFontSizeSlider);
        
        var minLabel = new Label { Text = "最小", Location = new Point(DpiHelper.Scale(20), y + DpiHelper.Scale(30)), AutoSize = true, ForeColor = Color.Gray };
        var maxLabel = new Label { Text = "最大", Location = new Point(DpiHelper.Scale(145), y + DpiHelper.Scale(30)), AutoSize = true, ForeColor = Color.Gray };
        panel.Controls.Add(minLabel);
        panel.Controls.Add(maxLabel);
        y += DpiHelper.Scale(70);
        
        // Serif 字体
        var serifLabel = new Label
        {
            Text = "Serif 字体",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(10F), FontStyle.Bold),
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true
        };
        panel.Controls.Add(serifLabel);
        y += DpiHelper.Scale(30);
        
        _serifFontCombo = CreateFontComboBox(DpiHelper.Scale(20), y, _settingsService.Settings.SerifFont);
        panel.Controls.Add(_serifFontCombo);
        
        _serifPreview = new Label
        {
            Text = "16: Lorem ipsum dolor sit amet,\nconsectetur adipiscing elit.",
            Location = new Point(DpiHelper.Scale(200), y),
            Size = DpiHelper.Scale(new Size(300, 50)),
            Font = new Font(_settingsService.Settings.SerifFont, DpiHelper.Scale(16F))
        };
        panel.Controls.Add(_serifPreview);
        y += DpiHelper.Scale(70);
        
        // Sans-serif 字体
        var sansSerifLabel = new Label
        {
            Text = "Sans-serif 字体",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(10F), FontStyle.Bold),
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true
        };
        panel.Controls.Add(sansSerifLabel);
        y += DpiHelper.Scale(30);
        
        _sansSerifFontCombo = CreateFontComboBox(DpiHelper.Scale(20), y, _settingsService.Settings.SansSerifFont);
        panel.Controls.Add(_sansSerifFontCombo);
        
        _sansSerifPreview = new Label
        {
            Text = "16: Lorem ipsum dolor sit amet,\nconsectetur adipiscing elit.",
            Location = new Point(DpiHelper.Scale(200), y),
            Size = DpiHelper.Scale(new Size(300, 50)),
            Font = new Font(_settingsService.Settings.SansSerifFont, DpiHelper.Scale(16F))
        };
        panel.Controls.Add(_sansSerifPreview);
        y += DpiHelper.Scale(70);

        // 宽度固定的字体
        var fixedWidthLabel = new Label
        {
            Text = "宽度固定的字体",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(10F), FontStyle.Bold),
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true
        };
        panel.Controls.Add(fixedWidthLabel);
        y += DpiHelper.Scale(30);
        
        _fixedWidthFontCombo = CreateFontComboBox(DpiHelper.Scale(20), y, _settingsService.Settings.FixedWidthFont);
        panel.Controls.Add(_fixedWidthFontCombo);
        
        _fixedWidthPreview = new Label
        {
            Text = "Lorem ipsum dolor sit amet,\nconsectetur adipiscing elit.",
            Location = new Point(DpiHelper.Scale(200), y),
            Size = DpiHelper.Scale(new Size(300, 50)),
            Font = new Font(_settingsService.Settings.FixedWidthFont, DpiHelper.Scale(14F))
        };
        panel.Controls.Add(_fixedWidthPreview);
        y += DpiHelper.Scale(70);
        
        // 最小字号
        var minFontSizeLabel = new Label
        {
            Text = "最小字号",
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(10F), FontStyle.Bold),
            Location = new Point(DpiHelper.Scale(20), y),
            AutoSize = true
        };
        panel.Controls.Add(minFontSizeLabel);
        y += DpiHelper.Scale(30);
        
        _minFontSizeSlider = new TrackBar
        {
            Location = new Point(DpiHelper.Scale(20), y),
            Size = DpiHelper.Scale(new Size(150, 30)),
            Minimum = 6,
            Maximum = 24,
            Value = _settingsService.Settings.MinimumFontSize,
            TickFrequency = 2
        };
        _minFontSizeSlider.ValueChanged += (s, e) => UpdateMinFontSizePreview();
        panel.Controls.Add(_minFontSizeSlider);
        
        _minFontSizeValue = new Label
        {
            Text = _settingsService.Settings.MinimumFontSize.ToString(),
            Location = new Point(DpiHelper.Scale(175), y + DpiHelper.Scale(5)),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale(9F))
        };
        panel.Controls.Add(_minFontSizeValue);
        
        _minFontSizePreview = new Label
        {
            Text = $"{_settingsService.Settings.MinimumFontSize}: Lorem ipsum dolor sit amet, consectetur",
            Location = new Point(DpiHelper.Scale(200), y),
            Size = DpiHelper.Scale(new Size(300, 30)),
            Font = new Font("Microsoft YaHei UI", DpiHelper.Scale((float)_settingsService.Settings.MinimumFontSize))
        };
        panel.Controls.Add(_minFontSizePreview);
        y += DpiHelper.Scale(60);
        
        // 完成按钮
        var doneBtn = new Button
        {
            Text = "完成",
            Location = new Point(DpiHelper.Scale(20), y),
            Size = DpiHelper.Scale(new Size(80, 30)),
            FlatStyle = FlatStyle.System
        };
        doneBtn.Click += OnDoneClick;
        panel.Controls.Add(doneBtn);
        
        Controls.Add(panel);
        
        // 绑定事件
        _standardFontCombo.SelectedIndexChanged += (s, e) => UpdateStandardPreview();
        _serifFontCombo.SelectedIndexChanged += (s, e) => UpdateSerifPreview();
        _sansSerifFontCombo.SelectedIndexChanged += (s, e) => UpdateSansSerifPreview();
        _fixedWidthFontCombo.SelectedIndexChanged += (s, e) => UpdateFixedWidthPreview();
    }
    
    private void LoadSystemFonts()
    {
        using var fonts = new InstalledFontCollection();
        foreach (var family in fonts.Families)
        {
            _fontNames.Add(family.Name);
        }
    }
    
    private ComboBox CreateFontComboBox(int x, int y, string selectedFont)
    {
        var combo = new ComboBox
        {
            Location = new Point(x, y),
            Size = DpiHelper.Scale(new Size(160, 25)),
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = DpiHelper.Scale(20)
        };
        
        combo.Items.AddRange(_fontNames.ToArray());
        
        // 选中当前字体
        var index = _fontNames.FindIndex(f => f.Equals(selectedFont, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            combo.SelectedIndex = index;
        else if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
        
        // 自定义绘制字体名称
        combo.DrawItem += (s, e) =>
        {
            if (e.Index < 0 || e.Index >= combo.Items.Count) return;
            
            e.DrawBackground();
            var item = combo.Items[e.Index];
            var fontName = item?.ToString() ?? "";
            
            try
            {
                using var font = new Font(fontName, DpiHelper.Scale(10F));
                using var brush = new SolidBrush(e.ForeColor);
                e.Graphics.DrawString(fontName, font, brush, e.Bounds.X + DpiHelper.Scale(2), e.Bounds.Y + DpiHelper.Scale(2));
            }
            catch
            {
                using var brush = new SolidBrush(e.ForeColor);
                var fallbackFont = e.Font ?? SystemFonts.DefaultFont;
                e.Graphics.DrawString(fontName, fallbackFont, brush, e.Bounds.X + DpiHelper.Scale(2), e.Bounds.Y + DpiHelper.Scale(2));
            }
            
            e.DrawFocusRectangle();
        };
        
        return combo;
    }

    
    private void UpdateStandardPreview()
    {
        try
        {
            var fontName = _standardFontCombo.SelectedItem?.ToString() ?? "Microsoft YaHei";
            var fontSize = _standardFontSizeSlider.Value;
            _standardPreview.Font = new Font(fontName, DpiHelper.Scale((float)fontSize));
            _standardPreview.Text = $"{fontSize}: Lorem ipsum dolor sit amet,\nconsectetur adipiscing elit.";
        }
        catch { }
    }
    
    private void UpdateSerifPreview()
    {
        try
        {
            var fontName = _serifFontCombo.SelectedItem?.ToString() ?? "Times New Roman";
            _serifPreview.Font = new Font(fontName, DpiHelper.Scale(16F));
        }
        catch { }
    }
    
    private void UpdateSansSerifPreview()
    {
        try
        {
            var fontName = _sansSerifFontCombo.SelectedItem?.ToString() ?? "Arial";
            _sansSerifPreview.Font = new Font(fontName, DpiHelper.Scale(16F));
        }
        catch { }
    }
    
    private void UpdateFixedWidthPreview()
    {
        try
        {
            var fontName = _fixedWidthFontCombo.SelectedItem?.ToString() ?? "Consolas";
            _fixedWidthPreview.Font = new Font(fontName, DpiHelper.Scale(14F));
        }
        catch { }
    }
    
    private void UpdateMinFontSizePreview()
    {
        try
        {
            var fontSize = _minFontSizeSlider.Value;
            _minFontSizeValue.Text = fontSize.ToString();
            _minFontSizePreview.Font = new Font("Microsoft YaHei UI", DpiHelper.Scale((float)fontSize));
            _minFontSizePreview.Text = $"{fontSize}: Lorem ipsum dolor sit amet, consectetur";
        }
        catch { }
    }
    
    private void OnDoneClick(object? sender, EventArgs e)
    {
        // 保存设置
        _settingsService.Settings.StandardFont = _standardFontCombo.SelectedItem?.ToString() ?? "Microsoft YaHei";
        _settingsService.Settings.StandardFontSize = _standardFontSizeSlider.Value;
        _settingsService.Settings.SerifFont = _serifFontCombo.SelectedItem?.ToString() ?? "Times New Roman";
        _settingsService.Settings.SansSerifFont = _sansSerifFontCombo.SelectedItem?.ToString() ?? "Arial";
        _settingsService.Settings.FixedWidthFont = _fixedWidthFontCombo.SelectedItem?.ToString() ?? "Consolas";
        _settingsService.Settings.MinimumFontSize = _minFontSizeSlider.Value;
        _settingsService.Save();
        
        DialogResult = DialogResult.OK;
        Close();
    }
}
