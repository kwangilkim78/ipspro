using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;
using IPS_PROBACKOFFICE.Security;

namespace IPS_PROBACKOFFICE;

public sealed class EmergencyContactForm : Form
{
    private readonly EmergencyContactRepository _repository;
    private readonly MasterDataRepository _master;
    private readonly AppUser _user;
    private readonly AuditService _audit;
    private readonly ContactEmailWorkflow _emailWorkflow;
    private readonly ComboBox _institution = new() { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _category = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _search = new() { Width = 220, PlaceholderText = "기관 / 담당자 / 전화 / 이메일 검색" };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
    private readonly FlowLayoutPanel _toolbar = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 10) };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
    };
    private IReadOnlyList<Institution> _institutions = [];
    private IReadOnlyList<EmergencyContact> _contacts = [];
    private bool _busy;

    public EmergencyContactForm(EmergencyContactRepository repository, MasterDataRepository master, AppUser user, AuditService audit, ContactEmailWorkflow emailWorkflow)
    {
        _repository = repository; _master = master; _user = user; _audit = audit;
        _emailWorkflow = emailWorkflow;
        Text = "기관별 비상연락망";
        Font = new Font("맑은 고딕", 10F);
        Padding = new Padding(16);
        _category.Items.AddRange(["전체 업무", "딜러", "IT", "백오피스"]);
        _category.SelectedIndex = 0;
        _toolbar.Controls.AddRange([_institution, _category, _search]);
        AddButton("신규입력", async () => await EditAsync(false));
        AddButton("수정", async () => await EditAsync(true));
        AddButton("본인 이메일 설정", () => _emailWorkflow.EditOwnEmailAsync(this));
        AddButton("이메일 보내기", SendEmailAsync);
        AddButton("새로고침", ReloadAsync);
        AddColumn(nameof(EmergencyContact.InstitutionName), "기관명", 200);
        AddColumn(nameof(EmergencyContact.Category), "업무 구분", 85);
        AddColumn(nameof(EmergencyContact.Priority), "연락 순서", 85);
        AddColumn(nameof(EmergencyContact.ContactName), "담당자", 100);
        AddColumn(nameof(EmergencyContact.WorkPhone), "회사전화", 145);
        AddColumn(nameof(EmergencyContact.MobilePhone), "휴대전화", 145);
        AddColumn(nameof(EmergencyContact.Email), "이메일", 200);
        AddColumn(nameof(EmergencyContact.Remarks), "비고", 220);
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        Controls.Add(_grid); Controls.Add(_toolbar); Controls.Add(_status);
        _institution.SelectedIndexChanged += (_, _) => ApplyFilter();
        _category.SelectedIndexChanged += (_, _) => ApplyFilter();
        _search.TextChanged += (_, _) => ApplyFilter();
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditAsync(true); };
        Shown += async (_, _) => await ReloadAsync();
    }

    private void AddColumn(string property, string title, int width) => _grid.Columns.Add(new DataGridViewTextBoxColumn
    { DataPropertyName = property, HeaderText = title, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable });

    private async Task SendEmailAsync()
    {
        if (_busy) return;
        if (_grid.CurrentRow?.DataBoundItem is not EmergencyContact selected)
        {
            MessageBox.Show(this, "메일을 보낼 담당자를 선택하세요.");
            return;
        }
        await _emailWorkflow.ComposeAsync(this, selected.Email, $"{selected.InstitutionName} / {selected.ContactName}");
    }

    private void AddButton(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += async (_, _) => await action();
        _toolbar.Controls.Add(button);
    }

    private async Task ReloadAsync() => await RunAsync(async () =>
    {
        long selected = (_institution.SelectedItem as Institution)?.ExternalInstitutionId ?? 0;
        var institutions = await _master.GetInstitutionsAsync();
        var contacts = await _repository.GetAllAsync();
        // 비활성화된 기관의 기존 비상연락처도 계속 조회·수정할 수 있습니다.
        _institutions = institutions.Concat(contacts.Where(c => institutions.All(i => i.ExternalInstitutionId != c.InstitutionId))
            .DistinctBy(c => c.InstitutionId).Select(c => new Institution("", c.InstitutionName, null, null, c.InstitutionId)))
            .OrderBy(i => i.Name).ToList();
        _contacts = contacts;
        _institution.DisplayMember = nameof(Institution.Name);
        _institution.ValueMember = nameof(Institution.ExternalInstitutionId);
        _institution.DataSource = new[] { new Institution("", "전체 기관", null, null) }.Concat(_institutions).ToList();
        _institution.SelectedValue = _institutions.Any(i => i.ExternalInstitutionId == selected) ? selected : 0L;
        ApplyFilter();
    });

    private void ApplyFilter()
    {
        long institutionId = (_institution.SelectedItem as Institution)?.ExternalInstitutionId ?? 0;
        string category = _category.SelectedItem?.ToString() ?? "전체 업무";
        string search = _search.Text.Trim();
        var rows = _contacts.Where(c => (institutionId == 0 || c.InstitutionId == institutionId)
            && (category == "전체 업무" || c.Category == category)
            && (search.Length == 0 || new[] { c.InstitutionName, c.ContactName, c.WorkPhone, c.MobilePhone, c.Email, c.Remarks }
                .Any(value => value.Contains(search, StringComparison.OrdinalIgnoreCase)))).ToList();
        _grid.DataSource = rows;
        _status.Text = rows.Count == 0 ? "등록된 연락처가 없습니다. 신규입력으로 추가하세요."
            : $"{rows.Count:N0}명 · 딜러 {rows.Count(c => c.Category == "딜러")} / IT {rows.Count(c => c.Category == "IT")} / 백오피스 {rows.Count(c => c.Category == "백오피스")} · 연락 순서가 작은 담당자부터 연락하세요.";
    }

    private async Task EditAsync(bool edit)
    {
        if (_busy) return;
        var selected = edit ? _grid.CurrentRow?.DataBoundItem as EmergencyContact : null;
        if (edit && selected is null) { MessageBox.Show(this, "수정할 연락처를 선택하세요."); return; }
        if (_institutions.Count == 0) { MessageBox.Show(this, "기관 목록을 먼저 새로고침하세요."); return; }
        using var dialog = new EmergencyContactEditForm(_institutions, selected ?? new EmergencyContact
        {
            InstitutionId = (_institution.SelectedItem as Institution)?.ExternalInstitutionId ?? 0,
            Category = _category.SelectedIndex > 0 ? _category.SelectedItem!.ToString()! : "딜러"
        }, async contact =>
        {
            await _repository.SaveAsync(contact, _user.LoginId);
            await _audit.WriteAsync(edit ? "EMERGENCY_CONTACT_UPDATE" : "EMERGENCY_CONTACT_CREATE",
                contact.InstitutionId.ToString(), $"category={contact.Category}; id={contact.Id}");
        }, async contact =>
        {
            await _repository.DeleteAsync(contact);
            await _audit.WriteAsync("EMERGENCY_CONTACT_DELETE", contact.InstitutionId.ToString(),
                $"category={contact.Category}; id={contact.Id}");
            _contacts = _contacts.Where(c => c.Id != contact.Id).ToList();
            ApplyFilter();
        });
        BackOfficeTheme.Apply(dialog);
        UiLanguage.Apply(dialog);
        if (dialog.ShowDialog(this) == DialogResult.OK) await ReloadAsync();
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true; _toolbar.Enabled = _grid.Enabled = false; UseWaitCursor = true;
        try { await action(); }
        catch (Exception ex)
        {
            _status.Text = "조회 실패 · 새로고침으로 다시 시도하세요.";
            MessageBox.Show(this, ex.Message, "비상연락망 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _busy = false; _toolbar.Enabled = _grid.Enabled = true; UseWaitCursor = false; }
    }
}

internal sealed class EmergencyContactEditForm : Form
{
    public EmergencyContactEditForm(IReadOnlyList<Institution> institutions, EmergencyContact original, Func<EmergencyContact, Task> save, Func<EmergencyContact, Task> delete)
    {
        Text = original.Id == 0 ? "비상연락망 신규입력" : "비상연락망 수정";
        ClientSize = new Size(580, 560);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowOnly;
        Padding = new Padding(18);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        Font = new Font("맑은 고딕", 10F);
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 10 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // 편집 창이 부모에 연결될 때 BindingContext가 바뀌어 첫 기관으로 초기화되지 않도록
        // 기관 객체를 직접 보관하고 저장된 ID에 해당하는 항목을 선택한다.
        var institution = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = nameof(Institution.Name) };
        institution.Items.AddRange(institutions.Cast<object>().ToArray());
        var originalInstitution = institutions.FirstOrDefault(i => i.ExternalInstitutionId == original.InstitutionId);
        if (originalInstitution is null && original.Id > 0 && original.InstitutionId > 0)
        {
            originalInstitution = new Institution("", string.IsNullOrWhiteSpace(original.InstitutionName)
                ? $"[기관 ID {original.InstitutionId}]" : original.InstitutionName, null, null, original.InstitutionId);
            institution.Items.Add(originalInstitution);
        }
        institution.SelectedItem = originalInstitution;
        var category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        category.Items.AddRange(["딜러", "IT", "백오피스"]); category.SelectedItem = original.Category;
        var name = new TextBox { MaxLength = 100, Text = original.ContactName };
        var work = new TextBox { MaxLength = 50, Text = original.WorkPhone };
        var mobile = new TextBox { MaxLength = 50, Text = original.MobilePhone };
        var email = new TextBox { MaxLength = 254, Text = original.Email };
        var priority = new NumericUpDown { Minimum = 1, Maximum = 99, Value = original.Priority };
        var remarks = new TextBox { MaxLength = 1000, Text = original.Remarks, Multiline = true, ScrollBars = ScrollBars.Vertical };
        Add("기관 *", institution, 0); Add("업무 구분 *", category, 1); Add("담당자 *", name, 2);
        Add("회사전화", work, 3); Add("휴대전화", mobile, 4); Add("이메일", email, 5);
        Add("연락 순서 *", priority, 6); Add("비고", remarks, 7, 85);
        var hint = new Label { Text = "* 필수 · 회사전화/휴대전화 중 하나 이상 입력 · 연락 순서 1 = 최우선", AutoSize = true, Dock = DockStyle.Fill };
        table.Controls.Add(hint, 0, 8); table.SetColumnSpan(hint, 2);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
        var saveButton = new Button { Text = "저장", AutoSize = true };
        var cancel = new Button { Text = "취소", AutoSize = true, DialogResult = DialogResult.Cancel };
        bool saving = false;
        FormClosing += (_, e) => { if (saving) e.Cancel = true; };
        saveButton.Click += async (_, _) =>
        {
            if (saving) return;
            try
            {
                var contact = original with
                {
                    InstitutionId = (institution.SelectedItem as Institution)?.ExternalInstitutionId ?? 0,
                    Category = category.SelectedItem?.ToString() ?? "", ContactName = name.Text.Trim(),
                    WorkPhone = work.Text.Trim(), MobilePhone = mobile.Text.Trim(), Email = email.Text.Trim(),
                    Priority = (int)priority.Value, Remarks = remarks.Text.Trim()
                };
                contact.Validate();
                saving = true; table.Enabled = false; UseWaitCursor = true;
                await save(contact);
                saving = false;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { saving = false; table.Enabled = true; UseWaitCursor = false; }
        };
        buttons.Controls.AddRange([saveButton, cancel]);
        if (original.Id > 0)
        {
            var deleteButton = new Button { Text = "삭제", AutoSize = true };
            deleteButton.Click += async (_, _) =>
            {
                if (saving) return;
                if (MessageBox.Show(this,
                    $"다음 연락처를 삭제하시겠습니까?\n\n기관: {original.InstitutionName}\n업무: {original.Category}\n담당자: {original.ContactName}\n\n삭제한 연락처는 복구할 수 없습니다.",
                    "비상연락처 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                saving = true; table.Enabled = false; UseWaitCursor = true;
                try
                {
                    await delete(original);
                    saving = false;
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "삭제 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { saving = false; table.Enabled = true; UseWaitCursor = false; }
            };
            buttons.Controls.Add(deleteButton);
        }
        table.Controls.Add(buttons, 0, 9); table.SetColumnSpan(buttons, 2);
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(table); AcceptButton = saveButton; CancelButton = cancel;

        void Add(string title, Control control, int row, int height = 40)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            control.Dock = DockStyle.Fill;
            table.Controls.Add(control, 1, row);
        }
    }
}
