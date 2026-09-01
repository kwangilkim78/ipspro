using InvoiceReportPrinter.WinForms.Configuration;
using InvoiceReportPrinter.WinForms.Data;
using InvoiceReportPrinter.WinForms.Services;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Security;
using Microsoft.Extensions.Configuration;

namespace IPS_PROBACKOFFICE;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            string appSettingsPath =
    Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            // ClickOnce에서 데이터 파일로 설치된 경우
            if (!File.Exists(appSettingsPath))
            {
                string? clickOnceDataDirectory =
                    Environment.GetEnvironmentVariable("ClickOnce_DataDirectory");

                if (!string.IsNullOrWhiteSpace(clickOnceDataDirectory))
                {
                    string clickOncePath =
                        Path.Combine(clickOnceDataDirectory, "appsettings.json");

                    if (File.Exists(clickOncePath))
                        appSettingsPath = clickOncePath;
                }
            }

            if (!File.Exists(appSettingsPath))
            {
                throw new FileNotFoundException(
                    "appsettings.json을 찾을 수 없습니다.\n" +
                    $"실행 폴더: {AppContext.BaseDirectory}\n" +
                    $"ClickOnce 데이터 폴더: " +
                    $"{Environment.GetEnvironmentVariable("ClickOnce_DataDirectory")}");
            }

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(appSettingsPath)!)
                .AddJsonFile(
                    Path.GetFileName(appSettingsPath),
                    optional: false,
                    reloadOnChange: false)
                .Build();

            var settings = configuration.GetSection("Invoice").Get<InvoiceSettings>()
                ?? throw new InvalidOperationException("appsettings.json의 Invoice 설정을 읽을 수 없습니다.");
            string configuredConnectionString = configuration.GetConnectionString("MariaDb")
                ?? throw new InvalidOperationException("MariaDb 연결 문자열이 없습니다.");
            string? connectionString = DatabaseConnectionManager.Resolve(configuredConnectionString);
            if (connectionString is null)
                return;

            var authRepository = new AuthRepository(connectionString);
            if (authRepository.GetActiveUserCountAsync().GetAwaiter().GetResult() == 0)
            {
                authRepository.CreateInitialUserAsync(
                    "kikim",
                    "Kwangil Kim",
                    "1234",
                    null).GetAwaiter().GetResult();
            }

            bool showLoginAgain;
            bool suppressAutoLogin = false;
            do
            {
                showLoginAgain = false;
                using var loginForm = new LoginForm(authRepository, suppressAutoLogin);
                UiLanguage.Apply(loginForm);
                suppressAutoLogin = false;
                if (loginForm.ShowDialog() != DialogResult.OK || loginForm.AuthenticatedUser is null)
                    return;

                AppUser user = loginForm.AuthenticatedUser;
                var audit = new AuditService(connectionString, user);
                audit.WriteAsync("LOGIN", "IPS_PROBACKOFFICE").GetAwaiter().GetResult();

                if (user.MustChangePassword)
                {
                    using var passwordChange = new PasswordChangeForm(authRepository, user, required: true);
                    UiLanguage.Apply(passwordChange);
                    if (passwordChange.ShowDialog() != DialogResult.OK)
                    {
                        audit.WriteAsync("PASSWORD_CHANGE_CANCEL", user.LoginId).GetAwaiter().GetResult();
                        showLoginAgain = true;
                        suppressAutoLogin = true;
                        continue;
                    }
                    if (passwordChange.ChangedPassword is not null)
                        LoginCredentialStore.UpdatePassword(user.LoginId, passwordChange.ChangedPassword);
                    audit.WriteAsync("PASSWORD_CHANGE", user.LoginId, "required=true").GetAwaiter().GetResult();
                    user = user with { MustChangePassword = false };
                }

                var repository = new TradeRepository(connectionString);
                var templateService = new TemplateService(settings);
                var invoiceService = new InvoiceService(settings, templateService);
                var creditRepository = new CreditRepository(connectionString);
                var tradeImportRepository = new TradeImportRepository(connectionString);
                var fxValueDateRepository = new FxValueDateRepository(connectionString);
                var masterDataRepository = new MasterDataRepository(connectionString);
                var brokerageFeeRepository = new BrokerageFeeRepository(connectionString);
                var switchFeeRuleRepository = new SwitchFeeRuleRepository(connectionString);
                var apiFeeExemptionRepository = new ApiFeeExemptionRepository(connectionString);
                var stpRouterMessageRepository = new StpRouterMessageRepository(connectionString);
                IReadOnlySet<string> screenPermissions = authRepository
                    .GetScreenPermissionsAsync(user.LoginId)
                    .GetAwaiter().GetResult();

                using var mainForm = new BackOfficeForm(
                    repository,
                    invoiceService,
                    creditRepository,
                    tradeImportRepository,
                    fxValueDateRepository,
                    masterDataRepository,
                    brokerageFeeRepository,
                    switchFeeRuleRepository,
                    apiFeeExemptionRepository,
                    stpRouterMessageRepository,
                    authRepository,
                    user,
                    screenPermissions,
                    audit);
                Application.Run(mainForm);
                audit.WriteAsync(mainForm.LogoutRequested ? "LOGOUT" : "APP_CLOSE", "IPS_PROBACKOFFICE")
                    .GetAwaiter().GetResult();
                showLoginAgain = mainForm.LogoutRequested;
                suppressAutoLogin = mainForm.LogoutRequested;
            }
            while (showLoginAgain);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "프로그램 시작 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
