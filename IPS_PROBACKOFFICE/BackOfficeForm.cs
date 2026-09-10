using InvoiceReportPrinter.WinForms;
using InvoiceReportPrinter.WinForms.Data;
using InvoiceReportPrinter.WinForms.Services;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Security;

namespace IPS_PROBACKOFFICE;

public sealed class BackOfficeForm : Form
{
    private static readonly Color UnifiedBackground = Color.FromArgb(245, 246, 248);
    private static readonly Color MenuBackground = Color.FromArgb(35, 43, 55);
    private static readonly Color MenuHover = Color.FromArgb(52, 64, 81);
    private static readonly Color MenuSelected = Color.FromArgb(38, 116, 181);

    public bool LogoutRequested { get; private set; }

    public BackOfficeForm(
        TradeRepository repository,
        InvoiceService invoiceService,
        CreditRepository creditRepository,
        TradeImportRepository tradeImportRepository,
        FxValueDateRepository fxValueDateRepository,
        MasterDataRepository masterDataRepository,
        BrokerageFeeRepository brokerageFeeRepository,
        SwitchFeeRuleRepository switchFeeRuleRepository,
        ApiFeeExemptionRepository apiFeeExemptionRepository,
        StpRouterMessageRepository stpRouterMessageRepository,
        AuthRepository authRepository,
        AppUser currentUser,
        IReadOnlySet<string> screenPermissions,
        AuditService audit)
    {
        Text = $"IPS_PROBACKOFFICE - {currentUser.UserName}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1200, 720);
        Width = 1420;
        Height = 850;
        BackColor = UnifiedBackground;

        var businessDaySettings = new BusinessDaySettings();
        var cutoffPicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH시",
            ShowUpDown = true,
            Width = 65,
            Value = DateTime.Today.Add(businessDaySettings.EndTime)
        };
        cutoffPicker.ValueChanged += (_, _) =>
            businessDaySettings.EndTime = TimeSpan.FromHours(cutoffPicker.Value.Hour);

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10, 7, 10, 7),
            BackColor = UnifiedBackground
        };
        header.Controls.Add(new Label { Text = "영업일 종료시간", AutoSize = true, Margin = new Padding(3, 7, 6, 0) });
        header.Controls.Add(cutoffPicker);
        header.Controls.Add(new Label { Text = "(당일 08시~익일 선택시간 · BOK/대표이사 보고/Invoice)", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(6, 7, 3, 0) });
        header.Controls.Add(new Label
        {
            Text = $"로그인: {currentUser.UserName}",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(24, 7, 4, 0)
        });

        var passwordButton = new Button { Text = "비밀번호 변경", AutoSize = true, Height = 28, Margin = new Padding(8, 1, 0, 0) };
        passwordButton.Click += async (_, _) =>
        {
            using var dialog = new PasswordChangeForm(authRepository, currentUser, required: false);
            UiLanguage.Apply(dialog);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                if (dialog.ChangedPassword is not null)
                    LoginCredentialStore.UpdatePassword(currentUser.LoginId, dialog.ChangedPassword);
                await audit.WriteAsync("PASSWORD_CHANGE", currentUser.LoginId, "required=false");
                MessageBox.Show("비밀번호가 변경되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        header.Controls.Add(passwordButton);

        var logoutButton = new Button { Text = "로그아웃", AutoSize = true, Height = 28, Margin = new Padding(8, 1, 0, 0) };
        logoutButton.Click += (_, _) => { LogoutRequested = true; Close(); };
        header.Controls.Add(logoutButton);

        var englishCheckBox = new CheckBox
        {
            Text = "English",
            AutoSize = true,
            Checked = UiLanguage.English,
            Margin = new Padding(18, 6, 0, 0)
        };
        header.Controls.Add(englishCheckBox);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = UnifiedBackground
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var menuPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 12, 0, 12),
            Margin = Padding.Empty,
            BackColor = MenuBackground
        };
        var brandLabel = new Label
        {
            Text = "IPS PRO BACKOFFICE",
            Width = 220,
            Height = 42,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            Margin = Padding.Empty
        };
        menuPanel.Controls.Add(brandLabel);

        var contentArea = new Panel { Dock = DockStyle.Fill, BackColor = UnifiedBackground, Margin = Padding.Empty };
        var screenTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(16, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            BackColor = Color.White
        };
        var contentHost = new Panel { Dock = DockStyle.Fill, BackColor = UnifiedBackground };
        contentArea.Controls.Add(contentHost);
        contentArea.Controls.Add(screenTitle);
        body.Controls.Add(menuPanel, 0, 0);
        body.Controls.Add(contentArea, 1, 0);
        Controls.Add(body);
        Controls.Add(header);

        var screens = new Dictionary<Button, (string Name, Form Form)>();
        Button? selectedButton = null;
        string? currentScreenName = null;
        int allowedScreenCount = 0;

        AddSection("리포트");
        AddScreen(ScreenPermissionCatalog.Operations, "BOK", () => new IPSEBO.Form1(repository.ConnectionString, businessDaySettings, IPSEBO.OperationsScreen.Bok));
        AddScreen(ScreenPermissionCatalog.Operations, "KB EOD", () => new IPSEBO.Form1(repository.ConnectionString, businessDaySettings, IPSEBO.OperationsScreen.KbEod));
        AddScreen(ScreenPermissionCatalog.Operations, "뉴욕종가", () => new IPSEBO.Form1(repository.ConnectionString, businessDaySettings, IPSEBO.OperationsScreen.NewYorkClose));
        AddScreen(ScreenPermissionCatalog.ExecutiveReport, "대표이사 보고", () => new FeeSummaryForm(repository, brokerageFeeRepository, businessDaySettings));

        AddSection("수수료");
        AddScreen(ScreenPermissionCatalog.Invoice, "Invoice", () => new MainForm(repository, invoiceService, businessDaySettings));
        AddScreen(ScreenPermissionCatalog.BrokerageFee, "중개 수수료 관리", () => new BrokerageFeeManagementForm(brokerageFeeRepository, apiFeeExemptionRepository));
        AddScreen(ScreenPermissionCatalog.BrokerageFee, "스위치 수수료", () => new SwitchFeeRuleForm(switchFeeRuleRepository));
        AddScreen(ScreenPermissionCatalog.DailyFeeTotal, "월/일별 수수료", () => new DailyFeeTotalForm(repository, brokerageFeeRepository, businessDaySettings));
        AddScreen(ScreenPermissionCatalog.FeeRevenue, "매출 수수료 조회", () => new FeeRevenueByBankForm(repository, brokerageFeeRepository, businessDaySettings));
        AddScreen(ScreenPermissionCatalog.MonthlyFeeStatement, "수수료 계산서 내역", () => new MonthlyFeeStatementForm(repository, brokerageFeeRepository, businessDaySettings));

        AddSection("기준정보");
        AddScreen(
            ScreenPermissionCatalog.MasterData,
            "기관·딜러 정보",
            () => new MasterDataForm(masterDataRepository, MasterDataScreen.Combined));
        AddScreen(ScreenPermissionCatalog.CreditMatrix, "전체 기관 한도 매트릭스", () => new CreditLimitMatrixForm(creditRepository));
        AddScreen(ScreenPermissionCatalog.FxValueDate, "Settle Date", () => new FxValueDateForm(fxValueDateRepository));

        AddSection("시스템");
        AddScreen(ScreenPermissionCatalog.FixMessage, "FIX 전문 조회", () => new FixMessageViewerForm(stpRouterMessageRepository));
        AddScreen(ScreenPermissionCatalog.TradeImport, "거래 CSV 업로드", () => new TradeImportForm(tradeImportRepository));
        AddScreen(ScreenPermissionCatalog.UserManagement, "백오피스 사용자 관리", () => new UserManagementForm(authRepository, currentUser, audit));

        foreach (var emptySection in menuPanel.Controls.OfType<Label>()
                     .Where(x => Equals(x.Tag, "SECTION") && !HasMenuAfter(x)).ToList())
            menuPanel.Controls.Remove(emptySection);

        if (allowedScreenCount == 0)
        {
            screenTitle.Text = "화면 권한 없음";
            contentHost.Controls.Add(new Label
            {
                Text = "현재 아이디에 허용된 화면이 없습니다. 관리자에게 화면 권한을 요청하세요.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 13F, FontStyle.Bold)
            });
        }
        else
        {
            ShowScreen(screens.Keys.First());
        }

        englishCheckBox.CheckedChanged += (_, _) =>
        {
            UiLanguage.SetEnglish(englishCheckBox.Checked);
            UiLanguage.Apply(this);
            if (selectedButton is not null)
                screenTitle.Text = selectedButton.Text;
        };
        UiLanguage.Apply(this);

        void AddSection(string title)
        {
            menuPanel.Controls.Add(new Label
            {
                Text = title,
                Tag = "SECTION",
                Width = 220,
                Height = 34,
                Padding = new Padding(18, 12, 0, 0),
                ForeColor = Color.FromArgb(164, 174, 187),
                Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
                Margin = Padding.Empty
            });
        }

        Button? AddScreen(string key, string name, Func<Form> formFactory)
        {
            if (!screenPermissions.Contains(key))
                return null;

            var button = new Button
            {
                Text = name,
                Width = 220,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = MenuHover },
                BackColor = MenuBackground,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(22, 0, 8, 0),
                Margin = Padding.Empty,
                Cursor = Cursors.Hand
            };
            var form = formFactory();
            PrepareEmbeddedForm(form);
            RegisterActionAudit(form, audit, () => currentScreenName);
            screens.Add(button, (name, form));
            button.Click += async (_, _) =>
            {
                ShowScreen(button);
                await audit.WriteAsync("MENU_VIEW", currentScreenName);
            };
            menuPanel.Controls.Add(button);
            allowedScreenCount++;
            return button;
        }

        void ShowScreen(Button button)
        {
            if (selectedButton == button)
                return;

            if (selectedButton is not null)
                selectedButton.BackColor = MenuBackground;

            selectedButton = button;
            selectedButton.BackColor = MenuSelected;
            var screen = screens[button];
            currentScreenName = screen.Name;
            screenTitle.Text = button.Text;

            contentHost.SuspendLayout();
            contentHost.Controls.Clear();
            contentHost.Controls.Add(screen.Form);
            screen.Form.Show();
            contentHost.ResumeLayout();
        }

        bool HasMenuAfter(Label section)
        {
            int index = menuPanel.Controls.GetChildIndex(section);
            for (int i = index + 1; i < menuPanel.Controls.Count; i++)
            {
                if (menuPanel.Controls[i] is Label label && Equals(label.Tag, "SECTION"))
                    return false;
                if (menuPanel.Controls[i] is Button)
                    return true;
            }
            return false;
        }
    }

    private static void RegisterActionAudit(Control control, AuditService audit, Func<string?> screenName)
    {
        foreach (Control child in control.Controls)
        {
            if (child is Button button)
            {
                string caption = button.Text.Trim();
                bool queryOnly = caption is "조회" or "새로고침" or "전체 새로고침" or "로그아웃";
                if (!queryOnly)
                    button.Click += async (_, _) => await audit.WriteAsync("ACTION_CLICK", screenName(), caption);
            }
            RegisterActionAudit(child, audit, screenName);
        }
    }

    private static void PrepareEmbeddedForm(Form child)
    {
        child.TopLevel = false;
        child.FormBorderStyle = FormBorderStyle.None;
        child.Dock = DockStyle.Fill;
        ApplyUnifiedTheme(child);
    }

    private static void ApplyUnifiedTheme(Control control)
    {
        BackOfficeTheme.Apply(control);
    }
}
