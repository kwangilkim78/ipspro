using System.ComponentModel;
using InvoiceReportPrinter.WinForms.Data;
using InvoiceReportPrinter.WinForms.Models;

namespace IPS_PROBACKOFFICE;

public sealed class UserInfoForm : Form
{
    private readonly TradeRepository _repository;
    private readonly ComboBox _cmbInstitution = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 300
    };
    private readonly Button _btnRefresh = new() { Text = "새로고침", Width = 100 };
    private readonly Label _status = new() { AutoSize = true, Text = "기관을 선택하세요." };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };
    private readonly BindingList<UserInfoRow> _rows = [];

    public UserInfoForm(TradeRepository repository)
    {
        _repository = repository;
        Text = "기관별 유저정보";
        BuildUi();

        Shown += async (_, _) => await LoadInstitutionsAsync();
        _cmbInstitution.SelectedIndexChanged += async (_, _) => await LoadUsersAsync();
        _btnRefresh.Click += async (_, _) => await LoadUsersAsync();
    }

    private void BuildUi()
    {
        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(10),
            WrapContents = false
        };
        filterPanel.Controls.Add(new Label
        {
            Text = "기관",
            AutoSize = true,
            Margin = new Padding(0, 8, 7, 0)
        });
        filterPanel.Controls.Add(_cmbInstitution);
        filterPanel.Controls.Add(_btnRefresh);
        filterPanel.Controls.Add(_status);

        AddColumn(nameof(UserInfoRow.Username), "USERNAME", 150);
        AddColumn(nameof(UserInfoRow.NameEng), "영문명", 180);
        AddColumn(nameof(UserInfoRow.NameKor), "한글명", 150);
        AddColumn(nameof(UserInfoRow.Email), "이메일", 250);
        AddColumn(nameof(UserInfoRow.MobilePhone), "휴대전화", 150);
        AddColumn(nameof(UserInfoRow.RtnsTraderCode), "RTNS TRADER CODE", 170);
        _grid.DataSource = _rows;

        Controls.Add(_grid);
        Controls.Add(filterPanel);
    }

    private void AddColumn(string propertyName, string headerText, int width)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = headerText,
            Width = width
        });
    }

    private async Task LoadInstitutionsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var institutions = await _repository.GetInstitutionsAsync();
            _cmbInstitution.DataSource = institutions.ToList();
            _cmbInstitution.DisplayMember = nameof(Institution.Name);
            _cmbInstitution.ValueMember = nameof(Institution.InstCode);
            _status.Text = institutions.Count == 0
                ? "등록된 기관이 없습니다."
                : $"기관 {institutions.Count:N0}개";
        });
    }

    private async Task LoadUsersAsync()
    {
        if (_cmbInstitution.SelectedItem is not Institution institution)
            return;

        await RunBusyAsync(async () =>
        {
            var users = await _repository.GetUsersByInstitutionAsync(institution.InstCode);
            _rows.RaiseListChangedEvents = false;
            _rows.Clear();
            foreach (var user in users)
                _rows.Add(user);
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
            _status.Text = $"{institution.Name}: {_rows.Count:N0}명";
        });
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            UseWaitCursor = true;
            _cmbInstitution.Enabled = false;
            _btnRefresh.Enabled = false;
            _status.Text = "조회 중...";
            await action();
        }
        catch (Exception ex)
        {
            _status.Text = "조회 오류";
            MessageBox.Show(ex.Message, "유저정보 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _cmbInstitution.Enabled = true;
            _btnRefresh.Enabled = true;
        }
    }
}
