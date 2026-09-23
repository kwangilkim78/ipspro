using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Helpers;
using IPS_PROBACKOFFICE.Models;
using System.ComponentModel;

namespace IPS_PROBACKOFFICE;

public sealed class FixMessageViewerForm : Form
{
    private readonly StpRouterMessageRepository _repository;
    private readonly InvoiceReportPrinter.WinForms.Data.TradeRepository _institutionRepository;
    private readonly ComboBox _institution = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 230,
        Enabled = false
    };

    private readonly DateTimePicker _from = CreateDatePicker();
    private readonly DateTimePicker _to = CreateDatePicker();

    private readonly TextBox _keyword = new()
    {
        Width = 220,
        PlaceholderText = "Orig Order ID, Report ID, FIX 값"
    };

    private readonly Button _search = new()
    {
        Text = "조회",
        AutoSize = true
    };

    private readonly Label _result = new()
    {
        Text = "조회 대기",
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(10, 8, 3, 0)
    };

    private readonly DataGridView _messages = new();
    private readonly DataGridView _fields = new();
    private readonly TextBox _raw = new();

    private readonly SortableBindingList<MessageGridRow> _rows = new();
    private readonly List<MessageGridRow> _allRows = new();
    private readonly RadioButton _allDestinations = new() { Text = "전체", AutoSize = true, Checked = true };
    private readonly RadioButton _rtnsDestination = new() { Text = "RTNS", AutoSize = true };
    private readonly RadioButton _trianaDestination = new() { Text = "TRAIANA", AutoSize = true };
    private readonly FlowLayoutPanel _destination = new()
    {
        AutoSize = true,
        WrapContents = false,
        Margin = new Padding(3, 5, 3, 0)
    };

    public FixMessageViewerForm(
        StpRouterMessageRepository repository,
        InvoiceReportPrinter.WinForms.Data.TradeRepository institutionRepository)
    {
        _repository = repository;
        _institutionRepository = institutionRepository;

        Text = "FIX 전문 조회";
        Font = new Font("맑은 고딕", 10F);
        BackColor = Color.White;
        Padding = new Padding(14);

        _from.Value = DateTime.Today;
        _to.Value = DateTime.Today;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };

        toolbar.Controls.Add(LabelFor("메시지 전송일"));
        toolbar.Controls.Add(_from);
        toolbar.Controls.Add(LabelFor("~", 2));
        toolbar.Controls.Add(_to);

        toolbar.Controls.Add(LabelFor("검색", 12));
        toolbar.Controls.Add(_keyword);
        toolbar.Controls.Add(LabelFor("기관", 12));
        _institution.Items.Add(new InstitutionOption("전체 기관", null));
        _institution.SelectedIndex = 0;
        toolbar.Controls.Add(_institution);
        toolbar.Controls.Add(LabelFor("구분", 12));
        _destination.Controls.AddRange(new Control[] { _allDestinations, _rtnsDestination, _trianaDestination });
        toolbar.Controls.Add(_destination);
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(_result);

        ConfigureMessageGrid();
        ConfigureFieldGrid();
        ConfigureRawText();

        var lower = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };

        lower.Panel1.Controls.Add(_fields);
        lower.Panel1.Controls.Add(
            SectionLabel("파싱된 FIX 태그"));

        lower.Panel2.Controls.Add(_raw);
        lower.Panel2.Controls.Add(
            SectionLabel("FIX 원문 (SOH는 |로 표시)"));

        var main = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };

        main.Panel1.Controls.Add(_messages);
        main.Panel2.Controls.Add(lower);

        Controls.Add(main);
        Controls.Add(toolbar);

        _search.Click += async (_, _) =>
            await SearchAsync();

        _keyword.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            await SearchAsync();
        };

        _messages.SelectionChanged += (_, _) =>
            ShowSelectedMessage();
        _institution.SelectedIndexChanged += (_, _) => ApplyDestinationFilter();
        foreach (var option in new[] { _allDestinations, _rtnsDestination, _trianaDestination })
            option.CheckedChanged += (_, _) =>
            {
                if (option.Checked)
                    ApplyDestinationFilter();
            };
        _messages.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                _messages.Columns[e.ColumnIndex].DataPropertyName != nameof(MessageGridRow.Destination))
                return;
            bool triana = Equals(e.Value, "TRAIANA");
            e.CellStyle.BackColor = triana ? Color.Lavender : Color.FromArgb(225, 243, 250);
            e.CellStyle.ForeColor = triana ? Color.Indigo : Color.FromArgb(0, 75, 110);
            e.CellStyle.SelectionBackColor = triana ? Color.Indigo : Color.FromArgb(0, 75, 110);
            e.CellStyle.SelectionForeColor = Color.White;
        };

        Shown += async (_, _) =>
        {
            SetSafeSplitterDistance(main, 0.56);
            SetSafeSplitterDistance(lower, 0.55);
            try
            {
                var institutions = await _institutionRepository.GetInstitutionsAsync();
                foreach (var institution in institutions.Where(item => !string.IsNullOrWhiteSpace(item.RtnsOrgCode)))
                    _institution.Items.Add(new InstitutionOption(
                        $"{institution.Name} ({institution.RtnsOrgCode!.Trim()})", institution.RtnsOrgCode.Trim()));
                _institution.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "기관 목록을 불러오지 못했습니다: " + ex.Message,
                    "FIX 전문 조회", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
    }

    private async Task SearchAsync()
    {
        DateTime selectedFrom = _from.Value.Date;
        DateTime selectedTo = _to.Value.Date;

        if (selectedFrom > selectedTo)
        {
            MessageBox.Show(
                "조회 시작일은 종료일보다 늦을 수 없습니다.",
                "FIX 전문 조회",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        _search.Enabled = false;
        _destination.Enabled = false;
        _result.Text = "조회 중...";

        _allRows.Clear();
        _rows.Clear();
        _fields.DataSource = null;
        _raw.Clear();

        try
        {
            // 화면에서 선택한 날짜는 KST 기준이다.
            // DB의 created 값은 UTC이므로 조회 시작·종료를 UTC로 변환한다.
            DateTime fromUtc =
                selectedFrom.AddHours(-9);

            DateTime toUtcExclusive =
                selectedTo.AddDays(1).AddHours(-9);

            IReadOnlyList<StpRouterMessage> records =
                await _repository.SearchAsync(
                    fromUtc,
                    toUtcExclusive,
                    _keyword.Text);

            foreach (StpRouterMessage item in records)
            {
                _allRows.Add(
                    new MessageGridRow(item));
            }

            ApplyDestinationFilter();
        }
        catch (Exception ex)
        {
            _result.Text = "조회 실패";

            MessageBox.Show(
                ex.Message,
                "FIX 전문 조회 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _search.Enabled = true;
            _destination.Enabled = true;
        }
    }

    private void ApplyDestinationFilter()
    {
        string selected = _rtnsDestination.Checked ? "RTNS"
            : _trianaDestination.Checked ? "TRAIANA" : "전체";
        string? institutionCode = (_institution.SelectedItem as InstitutionOption)?.Code;
        var institutionRows = _allRows.Where(row => institutionCode is null || row.PartyCodes.Contains(institutionCode)).ToList();
        _rows.RaiseListChangedEvents = false;
        try
        {
            _rows.Clear();
            foreach (var row in institutionRows)
                if (selected == "전체" || row.Destination == selected)
                    _rows.Add(row);
            _rows.ReapplySort();
        }
        finally
        {
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
        }

        int rtns = institutionRows.Count(row => row.Destination == "RTNS");
        int triana = institutionRows.Count(row => row.Destination == "TRAIANA");
        _result.Text = $"표시 {_rows.Count:N0}건 · RTNS {rtns:N0} / TRAIANA {triana:N0}";
        if (_allRows.Count >= 5000)
            _result.Text += " · 조회 상한 5,000건 내 필터 · 조건을 좁혀주세요.";

        if (_messages.Rows.Count > 0)
        {
            _messages.ClearSelection();
            _messages.CurrentCell = _messages.Rows[0].Cells[0];
            _messages.Rows[0].Selected = true;
        }
        ShowSelectedMessage();
    }

    private void ShowSelectedMessage()
    {
        if (_messages.CurrentRow?.DataBoundItem
            is not MessageGridRow row)
        {
            _fields.DataSource = null;
            _raw.Clear();
            return;
        }

        _fields.DataSource =
            FixMessageParser.Parse(row.Source.Message)
                .ToList();

        _raw.Text =
            FixMessageParser.ToDisplayText(
                row.Source.Message);

        UiLanguage.Apply(_fields);
    }

    private void ConfigureMessageGrid()
    {
        ConfigureGrid(_messages);

        _messages.AutoGenerateColumns = false;

        AddColumn(
            _messages,
            "CreatedKst",
            "전송시간(KST)",
            170);

        AddColumn(
            _messages,
            "MessageId",
            "ID",
            80);

        AddColumn(
            _messages,
            "TradeDate",
            "거래일",
            100);

        AddColumn(
            _messages,
            "Status",
            "상태",
            90);

        AddColumn(
            _messages,
            nameof(MessageGridRow.Destination),
            "구분",
            100);

        AddColumn(
            _messages,
            "OrigOrderId",
            "Orig Order ID",
            250);

        AddColumn(
            _messages,
            "Preview",
            "Message",
            500,
            DataGridViewAutoSizeColumnMode.Fill);

        _messages.DataSource = _rows;
        _messages.Columns[0].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";

        foreach (DataGridViewColumn column in _messages.Columns)
            column.SortMode = DataGridViewColumnSortMode.Automatic;
    }

    private void ConfigureFieldGrid()
    {
        ConfigureGrid(_fields);

        _fields.AutoGenerateColumns = false;

        AddColumn(
            _fields,
            "Sequence",
            "순서",
            55);

        AddColumn(
            _fields,
            "Tag",
            "Tag",
            65);

        AddColumn(
            _fields,
            "Name",
            "필드명",
            155);

        AddColumn(
            _fields,
            "Value",
            "값",
            330,
            DataGridViewAutoSizeColumnMode.Fill);
    }

    private void ConfigureRawText()
    {
        _raw.Dock = DockStyle.Fill;
        _raw.Multiline = true;
        _raw.ReadOnly = true;
        _raw.ScrollBars = ScrollBars.Both;
        _raw.WordWrap = true;
        _raw.Font = new Font("Consolas", 10F);
        _raw.BackColor = Color.White;
    }

    private static void ConfigureGrid(
        DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;

        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;

        grid.RowHeadersVisible = false;

        grid.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;

        grid.MultiSelect = false;

        grid.AutoSizeRowsMode =
            DataGridViewAutoSizeRowsMode.None;

        grid.RowTemplate.Height = 27;
        grid.BackgroundColor = Color.White;

        grid.DefaultCellStyle.SelectionBackColor =
            Color.FromArgb(220, 235, 252);

        grid.DefaultCellStyle.SelectionForeColor =
            Color.Black;
    }

    private static void AddColumn(
        DataGridView grid,
        string property,
        string header,
        int width,
        DataGridViewAutoSizeColumnMode autoSizeMode =
            DataGridViewAutoSizeColumnMode.None)
    {
        grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                HeaderText = header,
                Width = width,
                AutoSizeMode = autoSizeMode,
                SortMode =
                    DataGridViewColumnSortMode.NotSortable
            });
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd",
            Width = 120
        };
    }

    private static Label LabelFor(
        string text,
        int left = 3)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(left, 8, 3, 0)
        };
    }

    private static Label SectionLabel(
        string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font(
                "맑은 고딕",
                10F,
                FontStyle.Bold),
            Padding = new Padding(4, 4, 0, 0)
        };
    }

    private static void SetSafeSplitterDistance(
        SplitContainer split,
        double ratio)
    {
        int available =
            split.Orientation == Orientation.Vertical
                ? split.ClientSize.Width -
                  split.SplitterWidth
                : split.ClientSize.Height -
                  split.SplitterWidth;

        if (available <= 2)
            return;

        split.SplitterDistance =
            Math.Clamp(
                (int)Math.Round(available * ratio),
                1,
                available - 1);
    }

    private sealed record InstitutionOption(string Label, string? Code)
    {
        public override string ToString() => Label;
    }

    private sealed class MessageGridRow
    {
        public MessageGridRow(
            StpRouterMessage source)
        {
            Source = source;
            var fields = FixMessageParser.Parse(source.Message);
            PartyCodes = fields.Where(field => field.Tag == "448")
                .Select(field => field.Value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Destination = fields
                .Any(field => field.Tag == "56" && string.Equals(
                    field.Value, "traianaprod", StringComparison.OrdinalIgnoreCase))
                ? "TRAIANA"
                : "RTNS";
        }

        [Browsable(false)]
        public StpRouterMessage Source { get; }

        [Browsable(false)]
        public HashSet<string> PartyCodes { get; }

        public string Destination { get; }

        public DateTime CreatedKst =>
            Source.Created
                .AddHours(9);

        public int MessageId =>
            Source.MessageId;

        public string TradeDate =>
            Source.TradeDate;

        public string Status =>
            Source.Status;

        public string OrigOrderId =>
            Source.OrigOrderId;

        public string Preview =>
            FixMessageParser.ToDisplayText(
                Source.Message);
    }

    private sealed class SortableBindingList<T> : BindingList<T>
    {
        private PropertyDescriptor? _sortProperty;
        private ListSortDirection _sortDirection;

        protected override bool SupportsSortingCore => true;
        protected override bool IsSortedCore => _sortProperty is not null;
        protected override PropertyDescriptor? SortPropertyCore => _sortProperty;
        protected override ListSortDirection SortDirectionCore => _sortDirection;

        protected override void ApplySortCore(
            PropertyDescriptor property,
            ListSortDirection direction)
        {
            // OrderBy is stable and compares numeric/date properties by their actual type.
            var sorted = direction == ListSortDirection.Ascending
                ? Items.OrderBy(item => property.GetValue(item)).ToList()
                : Items.OrderByDescending(item => property.GetValue(item)).ToList();

            for (int index = 0; index < sorted.Count; index++)
                Items[index] = sorted[index];

            _sortProperty = property;
            _sortDirection = direction;
            ResetBindings();
        }

        public void ReapplySort()
        {
            if (_sortProperty is not null)
                ApplySortCore(_sortProperty, _sortDirection);
        }

        protected override void RemoveSortCore()
        {
            _sortProperty = null;
        }
    }
}
