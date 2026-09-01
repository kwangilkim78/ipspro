namespace IPS_PROBACKOFFICE;

/// <summary>
/// DB에서 조회한 API 주문 일반 중개수수료 면제 규칙을 판정한다.
/// 종료 시각은 미포함으로 처리한다.
/// </summary>
public static class ApiFeeExemptionPolicy
{
    public sealed record Rule(
        string InstitutionCode,
        string TraderId,
        DateTime Start,
        DateTime End);

    public static bool IsBrokerageExempt(
        IEnumerable<Rule> rules,
        string institutionCode,
        string? traderId,
        DateTime? tradeTime)
    {
        if (string.IsNullOrWhiteSpace(traderId) || !tradeTime.HasValue)
            return false;

        return rules.Any(rule =>
            rule.InstitutionCode.Equals(
                institutionCode,
                StringComparison.OrdinalIgnoreCase)
            &&
            traderId.Trim().Equals(
                rule.TraderId,
                StringComparison.OrdinalIgnoreCase)
            && tradeTime.Value >= rule.Start
            && tradeTime.Value < rule.End);
    }
}
