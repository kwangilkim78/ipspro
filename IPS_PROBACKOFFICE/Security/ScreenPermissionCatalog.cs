namespace IPS_PROBACKOFFICE.Security;

public sealed record ScreenPermissionDefinition(string Key, string DisplayName);

public static class ScreenPermissionCatalog
{
    public const string Operations = "OPERATIONS";
    public const string MasterData = "MASTER_DATA";
    public const string CreditMatrix = "CREDIT_MATRIX";
    public const string BrokerageFee = "BROKERAGE_FEE";
    public const string ExecutiveReport = "EXECUTIVE_REPORT";
    public const string FeeRevenue = "FEE_REVENUE";
    public const string Invoice = "INVOICE";
    public const string FixMessage = "FIX_MESSAGE";
    public const string TradeImport = "TRADE_IMPORT";
    public const string FxValueDate = "FX_VALUE_DATE";
    public const string UserManagement = "USER_MANAGEMENT";

    public static readonly IReadOnlyList<ScreenPermissionDefinition> Screens =
    [
        new(Operations, "BOK · KB EOD · 뉴욕종가"),
        new(MasterData, "기관 · 딜러 관리"),
        new(CreditMatrix, "전체 기관 한도 매트릭스"),
        new(BrokerageFee, "기관별 중개 수수료"),
        new(ExecutiveReport, "대표이사 보고"),
        new(FeeRevenue, "매출 수수료 조회"),
        new(Invoice, "Invoice"),
        new(FixMessage, "FIX 전문 조회"),
        new(TradeImport, "거래 CSV 업로드"),
        new(FxValueDate, "Settle Date"),
        new(UserManagement, "백오피스 사용자 관리")
    ];

    public static IReadOnlySet<string> AllKeys { get; } = Screens
        .Select(x => x.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
