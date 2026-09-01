using System.ComponentModel;
using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;

namespace IPS_PROBACKOFFICE;

public enum MasterDataScreen
{
    Institutions,
    Dealers
}

public sealed class MasterDataForm : Form
{
    private readonly MasterDataRepository _repository;
    private readonly MasterDataScreen _screen;
    private readonly Action<long>? _openSelectedInstitutionDealers;
    private readonly BindingList<Institution> _institutions = [];
    private readonly BindingList<DealerInfo> _dealers = [];
    private readonly DataGridView _institutionGrid = CreateGrid();
    private readonly DataGridView _dealerGrid = CreateGrid();
    private readonly ComboBox _institutionFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
    private readonly Label _status = new() { AutoSize = true, Text = "준비" };
    private bool _busy;
    private long? _requestedInstitutionId;

    public MasterDataForm(
        MasterDataRepository repository,
        MasterDataScreen screen,
        Action<long>? openSelectedInstitutionDealers = null)
    {
        _repository = repository;
        _screen = screen;
        _openSelectedInstitutionDealers = openSelectedInstitutionDealers;
        Text = screen == MasterDataScreen.Institutions ? "기관 정보" : "딜러 정보";
        BuildUi();
        Shown += async (_, _) => await ReloadInstitutionsAsync();
    }

    private void BuildUi()
    {
        var content = new Panel { Dock = DockStyle.Fill };
        if (_screen == MasterDataScreen.Institutions)
        {
            ConfigureInstitutionGrid();
            content.Controls.Add(_institutionGrid);
            content.Controls.Add(BuildInstitutionToolbar());
        }
        else
        {
            ConfigureDealerGrid();
            content.Controls.Add(_dealerGrid);
            content.Controls.Add(BuildDealerToolbar());
        }

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(10, 7, 0, 0) };
        statusPanel.Controls.Add(_status);
        Controls.Add(content);
        Controls.Add(statusPanel);
    }

    private Control BuildInstitutionToolbar()
    {
        var panel = Toolbar();
        panel.Controls.Add(Button("기관코드 수정", async (_, _) => await EditInstitutionCodeAsync()));
        panel.Controls.Add(Button("새로고침", async (_, _) => await ReloadInstitutionsAsync()));
        panel.Controls.Add(new Label
        {
            Text = "  ※ 기관 기본정보는 Edgewater ETL 조회 전용입니다.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(8, 9, 0, 0)
        });
        return panel;
    }

    private Control BuildDealerToolbar()
    {
        var panel = Toolbar();
        panel.Controls.Add(new Label { Text = "기관", AutoSize = true, Margin = new Padding(0, 9, 5, 0) });
        panel.Controls.Add(_institutionFilter);
        panel.Controls.Add(Button("한글명 수정", async (_, _) => await EditDealerKoreanNameAsync()));
        panel.Controls.Add(Button("새로고침", async (_, _) => await ReloadInstitutionsAsync()));
        panel.Controls.Add(new Label
        {
            Text = "  ※ 유저 기본정보는 Edgewater ETL 조회 전용입니다.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(8, 9, 0, 0)
        });
        _institutionFilter.SelectedIndexChanged += async (_, _) => await ReloadDealersAsync();
        return panel;
    }

    private void ConfigureInstitutionGrid()
    {
        _institutionGrid.DataSource = _institutions;
        AddColumn(_institutionGrid, nameof(Institution.ExternalInstitutionId), "Edgewater ID", 115);
        AddColumn(_institutionGrid, nameof(Institution.InstCode), "기관코드", 120);
        AddColumn(_institutionGrid, nameof(Institution.Name), "기관명", 230);
        AddColumn(_institutionGrid, nameof(Institution.Slug), "SLUG", 210);
        AddColumn(_institutionGrid, nameof(Institution.Emails), "대표 이메일", 320);
        AddColumn(_institutionGrid, nameof(Institution.RtnsOrgCode), "RTNS ORG CODE", 150);
        AddColumn(_institutionGrid, nameof(Institution.Enabled), "사용", 65);
        _institutionGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditInstitutionCodeAsync(); };
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("선택된 기관 딜러 정보", null, (_, _) => OpenSelectedInstitutionDealers());
        _institutionGrid.ContextMenuStrip = contextMenu;
        _institutionGrid.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                _institutionGrid.ClearSelection();
                _institutionGrid.Rows[e.RowIndex].Selected = true;
                _institutionGrid.CurrentCell = _institutionGrid.Rows[e.RowIndex].Cells[Math.Max(e.ColumnIndex, 0)];
            }
        };
        _institutionGrid.CellFormatting += (_, e) =>
        {
            if (_institutionGrid.Columns[e.ColumnIndex].DataPropertyName == nameof(Institution.Enabled) && Equals(e.Value, false))
            {
                e.CellStyle.BackColor = Color.MistyRose;
                e.CellStyle.ForeColor = Color.DarkRed;
            }
        };
    }

    private void ConfigureDealerGrid()
    {
        _dealerGrid.DataSource = _dealers;
        AddColumn(_dealerGrid, nameof(DealerInfo.ExternalUserId), "Edgewater ID", 115);
        AddColumn(_dealerGrid, nameof(DealerInfo.Username), "USERNAME", 150);
        AddColumn(_dealerGrid, nameof(DealerInfo.NameEng), "영문명", 190);
        AddColumn(_dealerGrid, nameof(DealerInfo.NameKor), "한글명", 140);
        AddColumn(_dealerGrid, nameof(DealerInfo.Email), "이메일", 240);
        AddColumn(_dealerGrid, nameof(DealerInfo.MobilePhone), "휴대전화", 130);
        AddColumn(_dealerGrid, nameof(DealerInfo.WorkPhone), "회사전화", 130);
        AddColumn(_dealerGrid, nameof(DealerInfo.JobTitle), "직급", 130);
        AddColumn(_dealerGrid, nameof(DealerInfo.RtnsTraderCode), "RTNS TRADER CODE", 155);
        AddColumn(_dealerGrid, nameof(DealerInfo.UseYn), "사용", 65);
        _dealerGrid.CellFormatting += (_, e) =>
        {
            if (_dealerGrid.Columns[e.ColumnIndex].DataPropertyName == nameof(DealerInfo.UseYn) && Equals(e.Value, "N"))
            {
                e.CellStyle.BackColor = Color.MistyRose;
                e.CellStyle.ForeColor = Color.DarkRed;
            }
        };
        _dealerGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditDealerKoreanNameAsync(); };
    }

    private async Task ReloadInstitutionsAsync(long? selectExternalId = null)
    {
        if (_busy) return;
        selectExternalId ??= _requestedInstitutionId;
        selectExternalId ??= (_institutionFilter.SelectedItem as Institution)?.ExternalInstitutionId;
        await RunAsync(async () =>
        {
            var rows = await _repository.GetInstitutionsAsync();
            Replace(_institutions, rows);

            _institutionFilter.DataSource = null;
            _institutionFilter.DataSource = rows.ToList();
            _institutionFilter.DisplayMember = nameof(Institution.Name);
            _institutionFilter.ValueMember = nameof(Institution.ExternalInstitutionId);
            if (selectExternalId.HasValue)
                _institutionFilter.SelectedValue = selectExternalId.Value;

            _requestedInstitutionId = null;

            _status.Text = $"기관 {rows.Count:N0}개 (Edgewater ETL)";
        });

        if (_screen == MasterDataScreen.Dealers)
            await ReloadDealersAsync();
    }

    public void SelectInstitution(long externalInstitutionId)
    {
        _requestedInstitutionId = externalInstitutionId;
        if (_institutionFilter.DataSource is not null)
        {
            _institutionFilter.SelectedValue = externalInstitutionId;
            _requestedInstitutionId = null;
        }
    }

    private void OpenSelectedInstitutionDealers()
    {
        if (_institutionGrid.CurrentRow?.DataBoundItem is not Institution selected)
        {
            MessageBox.Show("딜러 정보를 조회할 기관을 선택하세요.");
            return;
        }

        _openSelectedInstitutionDealers?.Invoke(selected.ExternalInstitutionId);
    }

    private async Task ReloadDealersAsync()
    {
        if (_busy || _institutionFilter.SelectedItem is not Institution institution) return;
        await RunAsync(async () =>
        {
            var rows = await _repository.GetDealersAsync(institution.ExternalInstitutionId);
            Replace(_dealers, rows);
            _status.Text = $"{institution.Name}: 딜러 {rows.Count:N0}명 (Edgewater ETL)";
        });
    }

    private async Task EditInstitutionCodeAsync()
    {
        if (_institutionGrid.CurrentRow?.DataBoundItem is not Institution selected)
        {
            MessageBox.Show("기관코드를 수정할 기관을 선택하세요.");
            return;
        }

        using var dialog = new LocalValueEditDialog(
            "기관코드 수정",
            $"{selected.Name} ({selected.Slug})",
            "기관코드",
            selected.InstCode,
            20);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await RunAsync(async () =>
        {
            await _repository.SaveInstitutionCodeAsync(selected.ExternalInstitutionId, dialog.Value);
            var rows = await _repository.GetInstitutionsAsync();
            Replace(_institutions, rows);
            _status.Text = $"{selected.Name}: 기관코드가 저장되었습니다.";
        });
    }

    private async Task EditDealerKoreanNameAsync()
    {
        if (_institutionFilter.SelectedItem is not Institution institution ||
            _dealerGrid.CurrentRow?.DataBoundItem is not DealerInfo selected)
        {
            MessageBox.Show("한글명을 수정할 딜러를 선택하세요.");
            return;
        }

        string description = string.IsNullOrWhiteSpace(selected.NameEng)
            ? selected.Username
            : $"{selected.Username} / {selected.NameEng}";
        using var dialog = new LocalValueEditDialog("딜러 한글명 수정", description, "한글명", selected.NameKor, 100);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await RunAsync(async () =>
        {
            await _repository.SaveUserKoreanNameAsync(selected.ExternalUserId, dialog.Value);
            Replace(_dealers, await _repository.GetDealersAsync(institution.ExternalInstitutionId));
            _status.Text = $"{selected.Username}: 한글명이 저장되었습니다.";
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        try
        {
            _busy = true;
            UseWaitCursor = true;
            _status.Text = "처리 중...";
            await action();
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            MessageBox.Show("이미 다른 기관에서 사용 중인 기관코드입니다.", "중복 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "기관·딜러 정보 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _busy = false;
        }
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells
    };

    private static FlowLayoutPanel Toolbar() => new()
    {
        Dock = DockStyle.Top,
        Height = 55,
        Padding = new Padding(10),
        WrapContents = false
    };

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30 };
        button.Click += click;
        return button;
    }

    private static void AddColumn(DataGridView grid, string property, string title, int width) =>
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = title, Width = width });

    private static void Replace<T>(BindingList<T> target, IEnumerable<T> source)
    {
        target.RaiseListChangedEvents = false;
        target.Clear();
        foreach (var row in source) target.Add(row);
        target.RaiseListChangedEvents = true;
        target.ResetBindings();
    }
}

internal sealed class LocalValueEditDialog : Form
{
    private readonly TextBox _value = new();
    private readonly int _maxLength;
    public string Value { get; private set; } = string.Empty;

    public LocalValueEditDialog(string title, string description, string fieldName, string? value, int maxLength)
    {
        Text = title;
        Width = 520;
        Height = 220;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        _maxLength = maxLength;
        _value.Text = value ?? string.Empty;
        _value.MaxLength = maxLength;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 2,
            RowCount = 3
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(new Label { Text = "대상", AutoSize = true, Margin = new Padding(0, 9, 5, 0) }, 0, 0);
        table.Controls.Add(new TextBox { Text = description, ReadOnly = true, Dock = DockStyle.Fill }, 1, 0);
        table.Controls.Add(new Label { Text = fieldName, AutoSize = true, Margin = new Padding(0, 9, 5, 0) }, 0, 1);
        _value.Dock = DockStyle.Fill;
        table.Controls.Add(_value, 1, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "저장", Width = 90 };
        var cancel = new Button { Text = "취소", Width = 90, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 0, 2);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void Save()
    {
        string value = _value.Text.Trim();
        if (value.Length > _maxLength)
        {
            MessageBox.Show($"최대 {_maxLength:N0}자까지 입력할 수 있습니다.");
            return;
        }
        Value = value;
        DialogResult = DialogResult.OK;
    }
}
