using System.ComponentModel;
using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;

namespace IPS_PROBACKOFFICE;

public enum MasterDataScreen
{
    Combined,
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
    private readonly Label _dealerTitle = new() { AutoSize = true, Text = "딜러 정보", Margin = new Padding(0, 9, 12, 0) };
    private readonly Label _institutionTitle = new() { AutoSize = true, Text = "기관 목록", Margin = new Padding(0, 9, 12, 0) };
    private Institution? SelectedInstitution => _screen == MasterDataScreen.Combined
        ? _institutionGrid.CurrentRow?.DataBoundItem as Institution
        : _institutionFilter.SelectedItem as Institution;
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
        Font = new Font("맑은 고딕", 10F);
        BackColor = Color.FromArgb(245, 247, 250);
        Padding = new Padding(16);
        Text = screen == MasterDataScreen.Combined ? "기관·딜러 정보"
            : screen == MasterDataScreen.Institutions ? "기관 정보" : "딜러 정보";
        BuildUi();
        Shown += async (_, _) => await ReloadInstitutionsAsync();
    }

    private void BuildUi()
    {
        var content = new Panel { Dock = DockStyle.Fill };
        if (_screen == MasterDataScreen.Combined)
        {
            ConfigureInstitutionGrid();
            ConfigureDealerGrid();
            var split = new SplitContainer
            {
                Size = new Size(1000, 650), Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical, SplitterDistance = 450,
                Panel1MinSize = 150, Panel2MinSize = 150, SplitterWidth = 12,
                BackColor = BackColor
            };
            split.Panel1.Controls.Add(_institutionGrid);
            split.Panel1.Controls.Add(BuildInstitutionToolbar());
            split.Panel2.Controls.Add(_dealerGrid);
            split.Panel2.Controls.Add(BuildDealerToolbar());
            content.Controls.Add(split);
            Shown += (_, _) =>
            {
                int available = split.ClientSize.Width - split.SplitterWidth;
                if (available >= split.Panel1MinSize + split.Panel2MinSize)
                    split.SplitterDistance = Math.Clamp((int)(available * 0.45),
                        split.Panel1MinSize, available - split.Panel2MinSize);
            };
            long? currentInstitutionId = null;
            _institutionGrid.CurrentCellChanged += async (_, _) =>
            {
                if (_busy) return;
                var institutionId = SelectedInstitution?.ExternalInstitutionId;
                if (institutionId == currentInstitutionId) return;
                currentInstitutionId = institutionId;
                await ReloadDealersAsync();
            };
        }
        else if (_screen == MasterDataScreen.Institutions)
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

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(0, 8, 0, 0) };
        _status.Dock = DockStyle.Top;
        _status.ForeColor = Color.FromArgb(74, 88, 107);
        statusPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom, Height = 24,
            Text = "기관·딜러 기본정보는 Edgewater ETL 조회 전용입니다. 기관코드와 딜러 한글명만 수정할 수 있습니다.",
            ForeColor = Color.DimGray, AutoEllipsis = true
        });
        statusPanel.Controls.Add(_status);
        Controls.Add(content);
        Controls.Add(statusPanel);
    }

    private Control BuildInstitutionToolbar()
    {
        if (_screen == MasterDataScreen.Combined)
            return SectionToolbar(_institutionTitle, Color.FromArgb(34, 65, 96), Color.White,
                Button("기관코드 수정", async (_, _) => await EditInstitutionCodeAsync()),
                Button("새로고침", async (_, _) => await ReloadInstitutionsAsync()));
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
        if (_screen == MasterDataScreen.Combined)
            return SectionToolbar(_dealerTitle, Color.FromArgb(35, 66, 112), Color.FromArgb(232, 240, 252),
                Button("한글명 수정", async (_, _) => await EditDealerKoreanNameAsync()),
                Button("새로고침", async (_, _) => await ReloadDealersAsync()));
        var panel = Toolbar();
        if (_screen == MasterDataScreen.Combined)
            panel.Controls.Add(_dealerTitle);
        else
        {
            panel.Controls.Add(new Label { Text = "기관", AutoSize = true, Margin = new Padding(0, 9, 5, 0) });
            panel.Controls.Add(_institutionFilter);
        }
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
        AddColumn(_institutionGrid, nameof(Institution.Name), "기관명", 230);
        AddColumn(_institutionGrid, nameof(Institution.InstCode), "기관코드", 120);
        AddColumn(_institutionGrid, nameof(Institution.Emails), "대표 이메일", 320);
        AddColumn(_institutionGrid, nameof(Institution.RtnsOrgCode), "RTNS ORG CODE", 150);
        AddColumn(_institutionGrid, nameof(Institution.Slug), "SLUG", 210);
        _institutionGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditInstitutionCodeAsync(); };
        var contextMenu = AddCopyMenu(_institutionGrid);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("선택된 기관 딜러 정보", null, (_, _) => OpenSelectedInstitutionDealers());
    }

    private void ConfigureDealerGrid()
    {
        // 호스트가 나중에 테마를 적용해도 딜러 전용 색상을 덮어쓰지 않도록 먼저 적용한다.
        BackOfficeTheme.Apply(_dealerGrid);
        var header = _dealerGrid.ColumnHeadersDefaultCellStyle;
        header.BackColor = header.SelectionBackColor = Color.FromArgb(202, 219, 245);
        header.ForeColor = header.SelectionForeColor = Color.FromArgb(35, 66, 112);
        _dealerGrid.BackgroundColor = Color.FromArgb(239, 244, 252);
        _dealerGrid.DefaultCellStyle = new DataGridViewCellStyle(_dealerGrid.DefaultCellStyle)
        {
            BackColor = Color.FromArgb(234, 241, 252),
            ForeColor = Color.FromArgb(35, 56, 88),
            SelectionBackColor = Color.FromArgb(174, 201, 239),
            SelectionForeColor = Color.FromArgb(22, 47, 85)
        };
        _dealerGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 249);
        _dealerGrid.GridColor = Color.FromArgb(199, 214, 237);
        _dealerGrid.DataSource = _dealers;
        AddColumn(_dealerGrid, nameof(DealerInfo.Username), "USERNAME", 150);
        AddColumn(_dealerGrid, nameof(DealerInfo.NameEng), "영문명", 190);
        AddColumn(_dealerGrid, nameof(DealerInfo.NameKor), "한글명", 140);
        AddColumn(_dealerGrid, nameof(DealerInfo.Email), "이메일", 240);
        AddColumn(_dealerGrid, nameof(DealerInfo.MobilePhone), "휴대전화", 130);
        AddColumn(_dealerGrid, nameof(DealerInfo.WorkPhone), "회사전화", 130);
        AddColumn(_dealerGrid, nameof(DealerInfo.JobTitle), "직급", 130);
        AddColumn(_dealerGrid, nameof(DealerInfo.RtnsTraderCode), "RTNS TRADER CODE", 155);
        _dealerGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditDealerKoreanNameAsync(); };
        AddCopyMenu(_dealerGrid);
    }

    private static ContextMenuStrip AddCopyMenu(DataGridView grid)
    {
        var menu = new ContextMenuStrip();
        var copy = menu.Items.Add("복사 (Ctrl+C)", null, (_, _) =>
        {
            var content = grid.GetClipboardContent();
            if (content is null) return;
            try
            {
                Clipboard.SetDataObject(content, true);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                MessageBox.Show("클립보드를 사용할 수 없습니다. 잠시 후 다시 복사해 주세요.", "복사");
            }
        });
        menu.Opening += (_, _) => copy.Enabled = grid.SelectedCells.Count > 0;
        grid.ContextMenuStrip = menu;
        grid.Disposed += (_, _) => menu.Dispose();
        grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (cell.Selected) return;
            grid.ClearSelection();
            grid.CurrentCell = cell;
            cell.Selected = true;
        };
        return menu;
    }

    private async Task ReloadInstitutionsAsync(long? selectExternalId = null)
    {
        if (_busy) return;
        selectExternalId ??= _requestedInstitutionId;
        selectExternalId ??= SelectedInstitution?.ExternalInstitutionId;
        await RunAsync(async () =>
        {
            var rows = await _repository.GetInstitutionsAsync();
            Replace(_institutions, rows);
            _institutionTitle.Text = $"기관 목록 · {rows.Count:N0}개";

            _institutionFilter.DataSource = null;
            _institutionFilter.DataSource = rows.ToList();
            _institutionFilter.DisplayMember = nameof(Institution.Name);
            _institutionFilter.ValueMember = nameof(Institution.ExternalInstitutionId);
            if (selectExternalId.HasValue)
            {
                _institutionFilter.SelectedValue = selectExternalId.Value;
                foreach (DataGridViewRow row in _institutionGrid.Rows)
                    if (row.DataBoundItem is Institution institution && institution.ExternalInstitutionId == selectExternalId.Value)
                        _institutionGrid.CurrentCell = row.Cells[0];
            }

            _requestedInstitutionId = null;

            _status.Text = $"기관 {rows.Count:N0}개 (Edgewater ETL)";
        });

        if (_screen is MasterDataScreen.Dealers or MasterDataScreen.Combined)
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
        if (_screen == MasterDataScreen.Combined) _dealerGrid.Focus();
    }

    private async Task ReloadDealersAsync()
    {
        if (_busy) return;
        Replace(_dealers, Array.Empty<DealerInfo>());
        if (SelectedInstitution is not Institution institution)
        {
            _dealerTitle.Text = "기관을 선택하세요";
            return;
        }
        _dealerTitle.Text = $"{institution.Name} · 딜러 조회 중…";
        await RunAsync(async () =>
        {
            var rows = await _repository.GetDealersAsync(institution.ExternalInstitutionId);
            Replace(_dealers, rows);
            _dealerTitle.Text = rows.Count == 0
                ? $"{institution.Name} · 등록된 딜러가 없습니다"
                : $"{institution.Name} · 딜러 {rows.Count:N0}명";
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
            _status.Text = $"{selected.Name}: 기관코드가 저장되었습니다.";
        });
        await ReloadInstitutionsAsync(selected.ExternalInstitutionId);
    }

    private async Task EditDealerKoreanNameAsync()
    {
        if (SelectedInstitution is not Institution institution ||
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
            _institutionGrid.Enabled = _dealerGrid.Enabled = _institutionFilter.Enabled = false;
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
            _institutionGrid.Enabled = _dealerGrid.Enabled = _institutionFilter.Enabled = true;
        }
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        MultiSelect = true,
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
        RowHeadersVisible = false,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
        AllowUserToResizeRows = false,
        RowTemplate = { Height = 32 },
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        GridColor = Color.FromArgb(229, 234, 240),
        EnableHeadersVisualStyles = false,
        ColumnHeadersHeight = 38,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(225, 234, 244),
            ForeColor = Color.FromArgb(34, 65, 96),
            SelectionBackColor = Color.FromArgb(225, 234, 244),
            SelectionForeColor = Color.FromArgb(34, 65, 96),
            Padding = new Padding(8, 0, 8, 0)
        },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White, ForeColor = Color.FromArgb(38, 48, 62),
            SelectionBackColor = Color.FromArgb(210, 229, 249),
            SelectionForeColor = Color.FromArgb(20, 55, 90),
            Padding = new Padding(8, 0, 8, 0)
        },
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(247, 249, 252) }
    };

    private static Control SectionToolbar(Label title, Color accent, Color background, params Button[] buttons)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2,
            BackColor = background, Padding = new Padding(12, 6, 8, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        title.AutoSize = false;
        title.AutoEllipsis = true;
        title.Dock = DockStyle.Fill;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Margin = new Padding(0, 0, 12, 0);
        title.ForeColor = accent;
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty };
        foreach (var button in buttons)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(201, 213, 227);
            button.BackColor = Color.White;
            button.ForeColor = accent;
            button.Padding = new Padding(8, 3, 8, 3);
            button.Margin = new Padding(6, 0, 0, 0);
            actions.Controls.Add(button);
        }
        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(actions, 0, 1);
        return panel;
    }

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
