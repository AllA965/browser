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
        Size = new Size(450, 480);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);

        var y = 20;

        // 邮编
        AddLabel("邮编", 20, y);
        y += 20;
        _postalCodeBox = AddTextBox(20, y, 200);
        y += 35;

        // 省、城市、区
        AddLabel("省", 20, y);
        AddLabel("城市", 160, y);
        AddLabel("区", 300, y);
        y += 20;
        _provinceBox = AddTextBox(20, y, 130);
        _cityBox = AddTextBox(160, y, 130);
        _districtBox = AddTextBox(300, y, 120);
        y += 35;

        // 街道地址
        AddLabel("街道地址", 20, y);
        y += 20;
        _streetAddressBox = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(200, 50),
            Multiline = true
        };
        Controls.Add(_streetAddressBox);
        y += 60;

        // 组织
        AddLabel("组织", 20, y);
        y += 20;
        _organizationBox = AddTextBox(20, y, 200);
        y += 35;

        // 名称
        AddLabel("名称", 20, y);
        y += 20;
        _nameBox = AddTextBox(20, y, 200);
        y += 35;

        // 国家/地区
        AddLabel("国家/地区", 20, y);
        y += 20;
        _countryCombo = new ComboBox
        {
            Location = new Point(20, y),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _countryCombo.Items.AddRange(new object[] { "中国", "美国", "日本", "韩国", "英国", "法国", "德国", "其他" });
        _countryCombo.SelectedIndex = 0;
        Controls.Add(_countryCombo);
        y += 35;

        // 电话和电子邮件
        AddLabel("电话", 20, y);
        AddLabel("电子邮件", 230, y);
        y += 20;
        _phoneBox = AddTextBox(20, y, 200);
        _emailBox = AddTextBox(230, y, 180);
        y += 45;

        // 按钮
        _okBtn = new Button
        {
            Text = "确定",
            Location = new Point(260, y),
            Size = new Size(75, 28),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        _okBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        Controls.Add(_okBtn);

        _cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(345, y),
            Size = new Size(75, 28),
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
