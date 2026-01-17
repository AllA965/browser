using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 添加信用卡对话框
/// </summary>
public class AddCreditCardDialog : Form
{
    private TextBox _cardholderNameBox = null!;
    private TextBox _cardNumberBox = null!;
    private ComboBox _expiryMonthCombo = null!;
    private ComboBox _expiryYearCombo = null!;
    private Button _okBtn = null!;
    private Button _cancelBtn = null!;

    public string CardholderName => _cardholderNameBox.Text;
    public string CardNumber => _cardNumberBox.Text;
    public string ExpiryDate => $"{_expiryMonthCombo.SelectedItem}/{_expiryYearCombo.SelectedItem}";

    public AddCreditCardDialog()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        AppIconHelper.SetIcon(this);
        Text = "添加信用卡";
        Size = new Size(350, 290);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);

        var y = 20;

        // 持卡人姓名
        var nameLabel = new Label
        {
            Text = "持卡人姓名",
            Location = new Point(20, y),
            AutoSize = true
        };
        Controls.Add(nameLabel);
        y += 20;

        _cardholderNameBox = new TextBox
        {
            Location = new Point(20, y),
            Width = 200
        };
        Controls.Add(_cardholderNameBox);
        y += 35;

        // 信用卡号
        var cardNumberLabel = new Label
        {
            Text = "信用卡号",
            Location = new Point(20, y),
            AutoSize = true
        };
        Controls.Add(cardNumberLabel);
        y += 20;

        _cardNumberBox = new TextBox
        {
            Location = new Point(20, y),
            Width = 200
        };
        Controls.Add(_cardNumberBox);
        y += 35;

        // 截止日期
        var expiryLabel = new Label
        {
            Text = "截止日期",
            Location = new Point(20, y),
            AutoSize = true
        };
        Controls.Add(expiryLabel);
        y += 20;

        _expiryMonthCombo = new ComboBox
        {
            Location = new Point(20, y),
            Width = 60,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        for (int i = 1; i <= 12; i++)
        {
            _expiryMonthCombo.Items.Add(i.ToString("D2"));
        }
        _expiryMonthCombo.SelectedIndex = 0;
        Controls.Add(_expiryMonthCombo);

        _expiryYearCombo = new ComboBox
        {
            Location = new Point(90, y),
            Width = 80,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        var currentYear = DateTime.Now.Year;
        for (int i = currentYear; i <= currentYear + 20; i++)
        {
            _expiryYearCombo.Items.Add(i.ToString());
        }
        _expiryYearCombo.SelectedIndex = 0;
        Controls.Add(_expiryYearCombo);
        y += 45;

        // 按钮
        _okBtn = new Button
        {
            Text = "确定",
            Location = new Point(160, y),
            Size = new Size(75, 28),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        _okBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        Controls.Add(_okBtn);

        _cancelBtn = new Button
        {
            Text = "取消",
            Location = new Point(245, y),
            Size = new Size(75, 28),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };
        _cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        Controls.Add(_cancelBtn);

        AcceptButton = _okBtn;
        CancelButton = _cancelBtn;
    }
}
