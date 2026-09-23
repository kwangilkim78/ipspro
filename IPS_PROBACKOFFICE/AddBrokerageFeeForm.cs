using System.Globalization;
using IPS_PROBACKOFFICE.Models;

namespace IPS_PROBACKOFFICE;

internal sealed class AddBrokerageFeeForm : Form
{
    private readonly TextBox _instCode = new() { Width = 220 };
    private readonly TextBox _institutionName = new() { Width = 220 };
    private readonly TextBox _df1mFee = new() { Width = 220, TextAlign = HorizontalAlignment.Right };
    private readonly TextBox _ndf1mFee = new() { Width = 220, TextAlign = HorizontalAlignment.Right };
    private readonly TextBox _ndMarFee = new() { Width = 220, TextAlign = HorizontalAlignment.Right };
    private readonly TextBox _basisMarFee = new() { Width = 220, TextAlign = HorizontalAlignment.Right };
    private readonly ComboBox _currencyCode = new()
    {
        Width = 220,
        DropDownStyle = ComboBoxStyle.DropDownList
    };
    private readonly TextBox _remarks = new() { Width = 220 };

    public BrokerageFeeRow? FeeRow { get; private set; }

    public AddBrokerageFeeForm()
    {
        Text = "신규 기관 중개 수수료 등록";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(410, 410);

        _currencyCode.Items.AddRange(["KRW", "USD"]);
        _currencyCode.SelectedIndex = 0;

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(20, 18, 20, 8)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddField(fields, "기관코드 *", _instCode);
        AddField(fields, "기관명 *", _institutionName);
        AddField(fields, "DF 1M", _df1mFee);
        AddField(fields, "NDF 1M", _ndf1mFee);
        AddField(fields, "ND/NDF MAR", _ndMarFee);
        AddField(fields, "BASIS MAR", _basisMarFee);
        AddField(fields, "통화 *", _currencyCode);
        AddField(fields, "비고", _remarks);

        var btnSave = new Button { Text = "등록", Width = 90, Height = 32 };
        var btnCancel = new Button { Text = "취소", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        btnSave.Click += (_, _) => SaveAndClose();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(0, 10, 18, 10),
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(btnCancel);
        buttons.Controls.Add(btnSave);

        Controls.Add(fields);
        Controls.Add(buttons);
        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control)
    {
        int row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold)
        }, 0, row);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        panel.Controls.Add(control, 1, row);
    }

    private void SaveAndClose()
    {
        string instCode = _instCode.Text.Trim();
        if (instCode.Length > 20 || !System.Text.RegularExpressions.Regex.IsMatch(instCode, @"\A[0-9]+(?:-[0-9]+)?\z")
            || !instCode.Any(c => c is >= '1' and <= '9'))
        {
            ShowInputError("기관코드는 20자 이내의 숫자 또는 숫자-숫자 형식으로 입력해 주세요. (예: 1123-1)", _instCode);
            return;
        }

        string institutionName = _institutionName.Text.Trim();
        if (string.IsNullOrWhiteSpace(institutionName))
        {
            ShowInputError("기관명을 입력해 주세요.", _institutionName);
            return;
        }

        if (!TryGetFee(_df1mFee, "DF 1M", out decimal? df1mFee)
            || !TryGetFee(_ndf1mFee, "NDF 1M", out decimal? ndf1mFee)
            || !TryGetFee(_ndMarFee, "ND/NDF MAR", out decimal? ndMarFee)
            || !TryGetFee(_basisMarFee, "BASIS MAR", out decimal? basisMarFee))
            return;

        FeeRow = new BrokerageFeeRow(
            instCode,
            institutionName,
            df1mFee,
            ndf1mFee,
            ndMarFee,
            basisMarFee,
            _currencyCode.Text,
            string.IsNullOrWhiteSpace(_remarks.Text) ? null : _remarks.Text.Trim());

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryGetFee(TextBox textBox, string fieldName, out decimal? value)
    {
        string text = textBox.Text.Trim().Replace(",", string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            || parsed < 0)
        {
            value = null;
            ShowInputError($"{fieldName} 수수료는 0 이상의 숫자로 입력해 주세요.", textBox);
            return false;
        }

        value = parsed;
        return true;
    }

    private void ShowInputError(string message, Control control)
    {
        MessageBox.Show(this, message, "입력값 확인",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        control.Focus();
    }
}
