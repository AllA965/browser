using MiniWorldBrowser.Helpers;

namespace MiniWorldBrowser.Forms;

/// <summary>
/// 自动填充设置对话框
/// </summary>
public class AutofillSettingsDialog : Form
{
    private ListView _addressList = null!;
    private ListView _creditCardList = null!;
    private Button _addAddressBtn = null!;
    private Button _addCreditCardBtn = null!;
    private Button _doneBtn = null!;

    public AutofillSettingsDialog()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        AppIconHelper.SetIcon(this);
        Text = "自动填充设置";
        Size = new Size(550, 550);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);

        var y = 20;

        // 地址标题
        var addressLabel = new Label
        {
            Text = "地址",
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
        };
        Controls.Add(addressLabel);
        y += 30;

        // 地址列表
        _addressList = new ListView
        {
            Location = new Point(20, y),
            Size = new Size(500, 150),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _addressList.Columns.Add("名称", 150);
        _addressList.Columns.Add("地址", 200);
        _addressList.Columns.Add("电话", 120);
        Controls.Add(_addressList);
        y += 160;

        // 添加新地址按钮
        _addAddressBtn = new Button
        {
            Text = "添加新地址...",
            Location = new Point(20, y),
            Size = new Size(100, 28),
            FlatStyle = FlatStyle.Flat
        };
        _addAddressBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _addAddressBtn.Click += OnAddAddress;
        Controls.Add(_addAddressBtn);
        y += 45;

        // 信用卡标题
        var creditCardLabel = new Label
        {
            Text = "信用卡",
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
        };
        Controls.Add(creditCardLabel);
        y += 30;

        // 信用卡列表
        _creditCardList = new ListView
        {
            Location = new Point(20, y),
            Size = new Size(500, 150),
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _creditCardList.Columns.Add("持卡人", 150);
        _creditCardList.Columns.Add("卡号", 200);
        _creditCardList.Columns.Add("有效期", 120);
        Controls.Add(_creditCardList);
        y += 160;

        // 添加新信用卡按钮
        _addCreditCardBtn = new Button
        {
            Text = "添加新信用卡...",
            Location = new Point(20, y),
            Size = new Size(110, 28),
            FlatStyle = FlatStyle.Flat
        };
        _addCreditCardBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        _addCreditCardBtn.Click += OnAddCreditCard;
        Controls.Add(_addCreditCardBtn);

        // 完成按钮
        _doneBtn = new Button
        {
            Text = "完成",
            Location = new Point(440, 475),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        _doneBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        Controls.Add(_doneBtn);

        AcceptButton = _doneBtn;
    }

    private void OnAddAddress(object? sender, EventArgs e)
    {
        using var dialog = new AddAddressDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            // 添加地址到列表
            var item = new ListViewItem(new[] { dialog.AddressName, dialog.FullAddress, dialog.Phone });
            _addressList.Items.Add(item);
        }
    }

    private void OnAddCreditCard(object? sender, EventArgs e)
    {
        using var dialog = new AddCreditCardDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            // 添加信用卡到列表（隐藏部分卡号）
            var maskedNumber = MaskCardNumber(dialog.CardNumber);
            var item = new ListViewItem(new[] { dialog.CardholderName, maskedNumber, dialog.ExpiryDate });
            _creditCardList.Items.Add(item);
        }
    }

    private static string MaskCardNumber(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
            return cardNumber;
        return "**** **** **** " + cardNumber[^4..];
    }
}
