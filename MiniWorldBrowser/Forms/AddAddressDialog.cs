using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 添加地址对话框
/// </summary>
public class AddAddressDialog : Form
{
    private TextBox _postalCodeBox = null!;
    private TextBox _provinceBox = null!;
    private TextBox _cityBox = null!;
    private TextBox _districtBox = null!;
    private TextBox _streetAddressBox = null!;
    private TextBox _organizationBox = null!;
    private TextBox _nameBox = null!;
    private ComboBox _countryCombo = null!;
    private TextBox _phoneBox = null!;
    private TextBox _emailBox = null!;
    private Button _okBtn = null!;
    private Button _cancelBtn = null!;

    public string AddressName => _nameBox.Text;
    public string FullAddress => $"{_provinceBox.Text} {_cityBox.Text} {_districtBox.Text} {_streetAddressBox.Text}";
    public string Phone => _phoneBox.Text;

    public AddAddressDialog()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        AppIconHelper.SetIcon(this);
        Text = "添加地址";
        Size = DpiHelper.Scale(new Size(450, 480));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", DpiHelper.ScaleFont(9F));

        var y = DpiHelper.Scale(20);

        // 邮编
        AddLabel("邮编", DpiHelper.Scale(20), y);
        y += DpiHelper.Scale(20);
        _postalCodeBox = AddTextBox(DpiHelper.Scale(20), y, DpiHelper.Scale(200));
        y += DpiHelper.Scale(35);

        // 省、城市、区
        AddLabel("省", DpiHelper.Scale(20), y);
        AddLabel("城市", DpiHelper.Scale(160), y);
        AddLabel("区", DpiHelper.Scale(300), y);
        y += DpiHelper.Scale(20);
        _provinceBox = AddTextBox(DpiHelper.Scale(20), y, DpiHelper.Scale(130));
        _cityBox = AddTextBox(DpiHelper.Scale(160), y, DpiHelper.Scale(130));
        _districtBox = AddTextBox(DpiHelper.Scale(300), y, DpiHelper.Scale(120));
        y += DpiHelper.Scale(35);

        // 街道地址
        AddLabel("街道地址", DpiHelper.Scale(20), y);
        y += DpiHelper.Scale(20);
        _streetAddressBox = new TextBox
        {
            Location = new Point(DpiHelper.Scale(20), y),
            Size = DpiHelper.Scale(new Size(200, 50)),
            Multiline = true
        };
        Controls.Add(_streetAddressBox);
        y += DpiHelper.Scale(60);

        // 组织
        AddLabel("组织", DpiHelper.Scale(20), y);
        y += DpiHelper.Scale(20);
        _organizationBox = AddTextBox(DpiHelper.Scale(20), y, DpiHelper.Scale(200));
        y += DpiHelper.Scale(35);

        // 名称
        AddLabel("名称", DpiHelper.Scale(20), y);
        y += DpiHelper.Scale(20);
        _nameBox = AddTextBox(DpiHelper.Scale(20), y, DpiHelper.Scale(200));
        y += DpiHelper.Scale(35);

        // 国家/地区
        AddLabel("国家/地区", DpiHelper.Scale(20), y);
        y += DpiHelper.Scale(20);
        _countryCombo = new ComboBox
        {
            Location = new Point(DpiHelper.Scale(20), y),
            Width = DpiHelper.Scale(200),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _countryCombo.Items.AddRange(new object[] { "中国", "美国", "日本", "韩国", "英国", "法国", "德国", "其他" });
        _countryCombo.SelectedIndex = 0;
        Controls.Add(_countryCombo);
        y += DpiHelper.Scale(35);

        // 电话和电子邮件
        AddLabel("电话", DpiHelper.Scale(20), y);
        AddLabel("电子邮件", DpiHelper.Scale(230), y);
        y += DpiHelper.Scale(20);
        _phoneBox = AddTextBox(DpiHelper.Scale(20), y, DpiHelper.Scale(200));
        _emailBox = AddTextBox(DpiHelper.Scale(230), y, DpiHelper.Scale(180));
        y += DpiHelper.Scale(45);

        // 按钮
        _okBtn = new Button
        {
            Text = "确定",
            Location = new Point(DpiHelper.Scale(260), y),
            Size = DpiHelper.Scale(new Size(75, 28)),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        _okBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        Controls.Add(_okBtn);

        _cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(DpiHelper.Scale(345), y),
            Size = DpiHelper.Scale(new Size(75, 28)),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };
        _cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        Controls.Add(_cancelBtn);

        AcceptButton = _okBtn;
        CancelButton = _cancelBtn;
    }

    private void AddLabel(string text, int x, int y)
    {
        var label = new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true
        };
        Controls.Add(label);
    }

    private TextBox AddTextBox(int x, int y, int width)
    {
        var textBox = new TextBox
        {
            Location = new Point(x, y),
            Width = width
        };
        Controls.Add(textBox);
        return textBox;
    }
}
