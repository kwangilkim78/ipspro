using InvoiceReportPrinter.WinForms.Helpers;
using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE;
using MySqlConnector;

namespace InvoiceReportPrinter.WinForms.Data;

public sealed class TradeRepository
{
    private readonly string _connectionString;

    public TradeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string ConnectionString => _connectionString;

    private static async Task<IReadOnlyList<ApiFeeExemptionPolicy.Rule>>
        GetApiFeeExemptionRulesAsync(
            MySqlConnection connection,
            string institutionCode,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                institution_code,
                trader_id,
                start_datetime,
                end_datetime
            FROM api_fee_exemption
            WHERE is_active = 1
              AND institution_code = @institutionCode
              AND start_datetime < @toDate
              AND end_datetime > @fromDate;
            """;

        var rules = new List<ApiFeeExemptionPolicy.Rule>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@institutionCode", institutionCode);
        command.Parameters.AddWithValue("@fromDate", from);
        command.Parameters.AddWithValue("@toDate", to);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(new ApiFeeExemptionPolicy.Rule(
                reader.GetString("institution_code"),
                reader.GetString("trader_id"),
                reader.GetDateTime("start_datetime"),
                reader.GetDateTime("end_datetime")));
        }

        return rules;
    }

    public async Task<IReadOnlyList<Institution>> GetInstitutionsAsync(
        CancellationToken cancellationToken = default,
        bool includeInactive = false)
    {
        const string sql = """
            SELECT
                COALESCE(l.institution_code, '') AS inst_code,
                o.name,
                o.emails,
                o.rtns_org_code,
                o.id AS external_institution_id,
                COALESCE(o.slug, '') AS slug,
                COALESCE(o.enabled, 0) AS enabled
            FROM mextrader_admin_prod.organizations o
            LEFT JOIN ips_ew.institution_local_info l
              ON l.external_institution_id = o.id
            WHERE (@includeInactive = 1 OR COALESCE(o.enabled, 0) = 1)
            AND NULLIF(TRIM(l.institution_code), '') IS NOT NULL
            ORDER BY o.name, o.id;
            """;

        var result = new List<Institution>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@includeInactive", includeInactive);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(
                new Institution(
                    InstCode: reader.GetString("inst_code"),
                    Name: reader.GetString("name"),
                    Emails: GetNullableString(reader, "emails"),
                    RtnsOrgCode: GetNullableString(reader, "rtns_org_code"),
                    ExternalInstitutionId: reader.GetInt64("external_institution_id"),
                    Slug: GetString(reader, "slug"),
                    Enabled: reader.GetBoolean("enabled")));
        }

        return result;
    }

    public async Task<IReadOnlyList<UserInfoRow>> GetUsersByInstitutionAsync(
        string instCode,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                u.username,
                COALESCE(u.full_name, '') AS name_eng,
                COALESCE(ul.name_ko, '') AS name_kor,
                COALESCE(u.email, '') AS email,
                COALESCE(u.mobile_number, '') AS mobile_phone,
                COALESCE(u.rtns_id, '') AS rtns_trader_code
            FROM mextrader_admin_prod.users u
            JOIN ips_ew.institution_local_info il
              ON il.external_institution_id = u.organization_id
            LEFT JOIN ips_ew.user_local_info ul
              ON ul.external_user_id = u.id
            WHERE il.institution_code = @instCode
              AND COALESCE(u.enabled, 0) = 1
            ORDER BY
                   CASE
                       WHEN username REGEXP '[0-9]+$'
                       THEN CAST(REGEXP_SUBSTR(username, '[0-9]+$') AS UNSIGNED)
                       ELSE 999999
                   END,
                   username;
            """;

        var result = new List<UserInfoRow>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@instCode", instCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new UserInfoRow
            {
                Username = GetString(reader, "username"),
                NameEng = GetString(reader, "name_eng"),
                NameKor = GetString(reader, "name_kor"),
                Email = GetString(reader, "email"),
                MobilePhone = GetString(reader, "mobile_phone"),
                RtnsTraderCode = GetString(reader, "rtns_trader_code")
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<TradeRow>> GetTradesAsync(
        Institution institution,
        DateTime month,
        bool switchOnly,
        CancellationToken cancellationToken = default)
    {
        DateTime from = new(month.Year, month.Month, 1);
        DateTime to = from.AddMonths(1);

        return await GetTradesAsync(
            institution,
            from,
            to,
            switchOnly,
            cancellationToken);
    }

    public async Task<IReadOnlyList<TradeRow>> GetTradesAsync(
        Institution institution,
        DateTime from,
        DateTime to,
        bool switchOnly,
        CancellationToken cancellationToken = default)
    {

        const string sql = """
            SELECT
                t.`Trade Date`,
                t.`ID`,
                t.`B/S`,
                t.`Counterparty`,
                t.`Tenor`,
                t.`Rate`,

                t.`Requested Amount` AS Amount,

                t.`Value Date`,
                t.`Username`,
                t.`Brokerage`,
                t.`Type`,
                t.`Switch Client`,
                t.`Switch Bank`,
                t.`Organization`,

                il.`institution_code` AS UserInstCode,
                u.`email` AS DealerEmail,
                NULLIF(TRIM(u.`full_name`), '') AS DealerNameEng,

                o.`name` AS UserInstitutionName

            FROM trades t

            LEFT JOIN mextrader_admin_prod.users u
                ON CONVERT(TRIM(u.`username`) USING utf8mb4)
                       COLLATE utf8mb4_unicode_ci
                 =
                   CONVERT(TRIM(t.`Username`) USING utf8mb4)
                       COLLATE utf8mb4_unicode_ci

            LEFT JOIN mextrader_admin_prod.organizations o
                ON o.id = u.organization_id
            LEFT JOIN ips_ew.institution_local_info il
                ON il.external_institution_id = o.id

            WHERE
                CONVERT(TRIM(t.`Organization`) USING utf8mb4)
                    COLLATE utf8mb4_unicode_ci
                =
                CONVERT(TRIM(@organization) USING utf8mb4)
                    COLLATE utf8mb4_unicode_ci

                AND t.`Trade Date` >= @fromDate
                AND t.`Trade Date` < @toDate

            ORDER BY
                COALESCE(NULLIF(TRIM(u.`full_name`), ''), t.`Username`),
                t.`Trade Date`,
                t.`ID`;
            """;

        var rows = new List<TradeRow>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        IReadOnlyList<SwitchFeeRule> switchFeeRules =
            await GetSwitchFeeRulesAsync(
                connection,
                institution.InstCode,
                from,
                to,
                cancellationToken);

        IReadOnlyList<ApiFeeExemptionPolicy.Rule> apiFeeExemptionRules =
            await GetApiFeeExemptionRulesAsync(
                connection,
                institution.InstCode,
                from,
                to,
                cancellationToken);

        await using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@organization",
            institution.Name);

        command.Parameters.AddWithValue(
            "@fromDate",
            from);

        command.Parameters.AddWithValue(
            "@toDate",
            to);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            string type = GetString(reader, "Type");
            string tenor = GetString(reader, "Tenor");

            string traderId = GetString(reader, "Username");
            string dealerName =
                GetNullableString(reader, "DealerNameEng")
                ?? traderId;

            string product =
                ProductClassifier.Classify(type, tenor);

            string? switchClient = GetNullableString(
                reader,
                "Switch Client");
            string? switchBank = GetNullableString(
                reader,
                "Switch Bank");
            string direction = GetString(reader, "B/S");
            decimal? amount = GetNullableDecimal(reader, "Amount");
            DateTime? tradeDate = GetNullableDateTime(reader, "Trade Date");
            bool isSwitch =
                !string.IsNullOrWhiteSpace(switchClient)
                || !string.IsNullOrWhiteSpace(switchBank);

            string brokerage = GetString(reader, "Brokerage");
            if (!isSwitch
                && ApiFeeExemptionPolicy.IsBrokerageExempt(
                    apiFeeExemptionRules,
                    institution.InstCode,
                    traderId,
                    tradeDate))
            {
                brokerage = "0";
            }

            if (isSwitch && IsSwitchFeeInstitution(institution.InstCode))
            {
                decimal? switchFee = CalculateSwitchFee(
                    switchFeeRules,
                    product,
                    tenor,
                    direction,
                    amount,
                    tradeDate);

                // 확인되지 않은 상품/Leg는 임의 계산하지 않고 0원 처리한다.
                brokerage = (switchFee ?? 0m).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            rows.Add(
                new TradeRow
                {
                    TradeDate = tradeDate,

                    RefNumber = GetString(
                        reader,
                        "ID"),

                    Direction = direction,

                    Counterparty = GetString(
                        reader,
                        "Counterparty"),

                    Tenor = tenor,

                    ExchangeRate = GetNullableDecimal(
                        reader,
                        "Rate"),

                    Amount = amount,

                    ValueDate = GetNullableDateTime(
                        reader,
                        "Value Date"),

                    Dealer = dealerName,

                    Brokerage = brokerage,

                    Product = product,

                    SwitchClient = switchClient,

                    SwitchBank = switchBank,

                    InstitutionCode = institution.InstCode,

                    InstitutionName = institution.Name,

                    DealerEmail = GetNullableString(
                        reader,
                        "DealerEmail")
                });
        }

        return rows
            // 1. Dealer
            .OrderBy(
                x => x.Dealer,
                StringComparer.OrdinalIgnoreCase)

            // 2. Product: DF 1M -> NDF 1M -> NDF MAR ->
            //    NDF TOMMAR -> BASIS MAR -> BASIS TOMMAR
            .ThenBy(x => ProductClassifier.SortIndex(x.Product))

            // 3. Trade Date
            .ThenBy(x => x.TradeDate ?? DateTime.MinValue)

            // 동일한 날짜의 결과도 항상 같은 순서를 유지한다.
            .ThenBy(
                x => x.RefNumber,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<TradeRow>> GetTradesFromViewAsync(
        Institution institution,
        DateTime month,
        CancellationToken cancellationToken = default)
    {
        DateTime from = new(month.Year, month.Month, 1);
        DateTime to = from.AddMonths(1);

        return await GetTradesFromViewCoreAsync(
            institution,
            from,
            to,
            useExecutionTime: false,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<TradeRow>> GetTradesFromViewAsync(
        Institution institution,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        return await GetTradesFromViewCoreAsync(
            institution,
            from,
            to,
            useExecutionTime: true,
            cancellationToken: cancellationToken);
    }

    private async Task<IReadOnlyList<TradeRow>> GetTradesFromViewCoreAsync(
        Institution institution,
        DateTime from,
        DateTime to,
        bool useExecutionTime,
        CancellationToken cancellationToken)
    {

        const string sql = """
            SELECT
                tr.`trade_id`,
                CASE
                    WHEN @useExecutionTime = 1 THEN DATE_ADD(
                        GREATEST(
                            COALESCE(tr.`Buy.created`, '1970-01-01'),
                            COALESCE(tr.`Sell.created`, '1970-01-01')
                        ), INTERVAL 9 HOUR
                    )
                    ELSE tr.`Trade Date`
                END AS `Trade Date`,
                tr.`PRODUCT`,
                tr.`TYPE`,
                tr.`LEG_TYPE`,
                tr.`is_switch_trade`,
                tr.`Switch Client`,
                tr.`Switch Bank`,

                CASE
                    WHEN LOWER(TRIM(tr.`Buy.Executing_Organisation`))
                         = LOWER(TRIM(@organization))
                        THEN tr.`Buy.client_order_id`
                    ELSE tr.`Sell.client_order_id`
                END AS RefNumber,

                CASE
                    WHEN LOWER(TRIM(tr.`Buy.Executing_Organisation`))
                         = LOWER(TRIM(@organization))
                        THEN 'BUY'
                    ELSE 'SELL'
                END AS Direction,

                CASE
                    WHEN LOWER(TRIM(tr.`Buy.Executing_Organisation`))
                         = LOWER(TRIM(@organization))
                        THEN tr.`Buy.Counterparty_Organisation`
                    ELSE tr.`Sell.Counterparty_Organisation`
                END AS Counterparty,

                CASE
                    WHEN LOWER(TRIM(tr.`Buy.Executing_Organisation`))
                         = LOWER(TRIM(@organization))
                        THEN tr.`Buy.Executing_Trader`
                    ELSE tr.`Sell.Executing_Trader`
                END AS Username,

                CASE
                    WHEN LOWER(TRIM(tr.`Buy.Executing_Organisation`))
                         = LOWER(TRIM(@organization))
                        THEN tr.`Buy.brokerage_commission`
                    ELSE tr.`Sell.brokerage_commission`
                END AS Brokerage,

                tr.`Filled Quantity` AS Amount,

                CASE
                    WHEN NULLIF(TRIM(tr.`LEG_TYPE`), '') IS NULL
                        THEN tr.`price`
                    ELSE COALESCE(tr.`leg_price`, tr.`price`)
                END AS Rate,

                CASE
                    WHEN NULLIF(TRIM(tr.`LEG_TYPE`), '') IS NULL
                        THEN tr.`Settle Date`
                    ELSE COALESCE(tr.`leg_settle_date`, tr.`Settle Date`)
                END AS ValueDate,

                CASE
                    WHEN NULLIF(TRIM(tr.`LEG_TYPE`), '') IS NULL
                        THEN CONCAT(TRIM(tr.`PRODUCT`), ' ', TRIM(tr.`TYPE`))
                    ELSE CONCAT(TRIM(tr.`PRODUCT`), ' ', TRIM(tr.`TYPE`))
                END AS Tenor,

                CASE
                    WHEN NULLIF(TRIM(tr.`LEG_TYPE`), '') IS NULL
                        THEN CONCAT(TRIM(tr.`PRODUCT`), ' ', TRIM(tr.`TYPE`))
                    WHEN EXISTS (
                        SELECT 1
                        FROM v_trading_report_backoffice grouped_leg
                        WHERE grouped_leg.`trade_id` = tr.`trade_id`
                          AND UPPER(TRIM(grouped_leg.`PRODUCT`)) = 'DF'
                    )
                        THEN 'BASIS TOMMAR'
                    ELSE 'NDF TOMMAR'
                END AS InvoiceProduct,

                u.`email` AS DealerEmail,
                NULLIF(TRIM(u.`full_name`), '') AS DealerNameEng

            FROM v_trading_report_backoffice tr

            LEFT JOIN mextrader_admin_prod.users u
                ON CONVERT(TRIM(u.`username`) USING utf8mb4)
                       COLLATE utf8mb4_unicode_ci
                 =
                   CONVERT(
                       TRIM(
                           CASE
                               WHEN LOWER(TRIM(tr.`Buy.Executing_Organisation`))
                                    = LOWER(TRIM(@organization))
                                   THEN tr.`Buy.Executing_Trader`
                               ELSE tr.`Sell.Executing_Trader`
                           END
                       ) USING utf8mb4
                   ) COLLATE utf8mb4_unicode_ci

            WHERE
                (
                    CONVERT(TRIM(tr.`Buy.Executing_Organisation`) USING utf8mb4)
                        COLLATE utf8mb4_unicode_ci
                    =
                    CONVERT(TRIM(@organization) USING utf8mb4)
                        COLLATE utf8mb4_unicode_ci
                    OR
                    CONVERT(TRIM(tr.`Sell.Executing_Organisation`) USING utf8mb4)
                        COLLATE utf8mb4_unicode_ci
                    =
                    CONVERT(TRIM(@organization) USING utf8mb4)
                        COLLATE utf8mb4_unicode_ci
                )
                AND CASE
                    WHEN @useExecutionTime = 1 THEN DATE_ADD(
                        GREATEST(
                            COALESCE(tr.`Buy.created`, '1970-01-01'),
                            COALESCE(tr.`Sell.created`, '1970-01-01')
                        ), INTERVAL 9 HOUR
                    )
                    ELSE tr.`Trade Date`
                END >= @fromDate
                AND CASE
                    WHEN @useExecutionTime = 1 THEN DATE_ADD(
                        GREATEST(
                            COALESCE(tr.`Buy.created`, '1970-01-01'),
                            COALESCE(tr.`Sell.created`, '1970-01-01')
                        ), INTERVAL 9 HOUR
                    )
                    ELSE tr.`Trade Date`
                END < @toDate
                AND UPPER(TRIM(tr.`status`)) = 'FILLED'

            ORDER BY
                COALESCE(
                    NULLIF(TRIM(u.`full_name`), ''),
                    CASE
                        WHEN LOWER(TRIM(tr.`Buy.Executing_Organisation`))
                             = LOWER(TRIM(@organization))
                            THEN tr.`Buy.Executing_Trader`
                        ELSE tr.`Sell.Executing_Trader`
                    END
                ),
                tr.`Trade Date`,
                tr.`trade_id`,
                FIELD(UPPER(TRIM(tr.`LEG_TYPE`)), 'NEAR', 'FAR');
            """;

        var rows = new List<TradeRow>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        IReadOnlyList<SwitchFeeRule> switchFeeRules =
            await GetSwitchFeeRulesAsync(
                connection,
                institution.InstCode,
                from,
                to,
                cancellationToken);

        IReadOnlyList<ApiFeeExemptionPolicy.Rule> apiFeeExemptionRules =
            await GetApiFeeExemptionRulesAsync(
                connection,
                institution.InstCode,
                from,
                to,
                cancellationToken);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@organization", institution.Name);
        command.Parameters.AddWithValue("@useExecutionTime", useExecutionTime ? 1 : 0);
        command.Parameters.AddWithValue("@fromDate", from);
        command.Parameters.AddWithValue("@toDate", to);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            string tenor = GetString(reader, "Tenor");
            string product = GetString(reader, "InvoiceProduct");
            string direction = GetString(reader, "Direction");
            decimal? amount = GetNullableDecimal(reader, "Amount");
            DateTime? tradeDate = GetNullableDateTime(reader, "Trade Date");
            string? legType = GetNullableString(reader, "LEG_TYPE");
            string? switchClient = GetNullableString(reader, "Switch Client");
            string? switchBank = GetNullableString(reader, "Switch Bank");
            bool isSwitch =
                !string.IsNullOrWhiteSpace(switchClient)
                || !string.IsNullOrWhiteSpace(switchBank);

            string traderId = GetString(reader, "Username");
            string dealerName =
                GetNullableString(reader, "DealerNameEng")
                ?? traderId;

            string brokerage = GetString(reader, "Brokerage");
            if (!isSwitch
                && ApiFeeExemptionPolicy.IsBrokerageExempt(
                    apiFeeExemptionRules,
                    institution.InstCode,
                    traderId,
                    tradeDate))
            {
                brokerage = "0";
            }

            if (isSwitch && IsSwitchFeeInstitution(institution.InstCode))
            {
                decimal? switchFee = CalculateSwitchFee(
                    switchFeeRules,
                    product,
                    tenor,
                    direction,
                    amount,
                    tradeDate);

                brokerage = (switchFee ?? 0m).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            rows.Add(new TradeRow
            {
                TradeDate = tradeDate,
                RefNumber = GetString(reader, "RefNumber"),
                Direction = direction,
                Counterparty = GetString(reader, "Counterparty"),
                Tenor = tenor,
                ExchangeRate = GetNullableDecimal(reader, "Rate"),
                Amount = amount,
                ValueDate = GetNullableDateTime(reader, "ValueDate"),
                Dealer = dealerName,
                Brokerage = brokerage,
                Product = product,
                SwitchClient = switchClient,
                SwitchBank = switchBank,
                InstitutionCode = institution.InstCode,
                InstitutionName = institution.Name,
                DealerEmail = GetNullableString(reader, "DealerEmail")
            });
        }

        return SortTrades(rows);
    }

    private static IReadOnlyList<TradeRow> SortTrades(
        IEnumerable<TradeRow> rows)
    {
        return rows
            .OrderBy(
                x => x.Dealer,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => ProductClassifier.SortIndex(x.Product))
            .ThenBy(x => x.TradeDate ?? DateTime.MinValue)
            .ThenBy(
                x => x.RefNumber,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<IReadOnlyList<SwitchFeeRule>> GetSwitchFeeRulesAsync(
        MySqlConnection connection,
        string instCode,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        if (!IsSwitchFeeInstitution(instCode))
            return Array.Empty<SwitchFeeRule>();

        const string sql = """
            SELECT
                product_code,
                direction,
                leg_type,
                fee_per_million,
                effective_from,
                effective_to
            FROM switch_fee_rule
            WHERE inst_code = @instCode
              AND is_active = 1
              AND effective_from < @toDate
              AND (effective_to IS NULL OR effective_to >= @fromDate)
            ORDER BY effective_from DESC;
            """;

        var rules = new List<SwitchFeeRule>();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@instCode", instCode);
        command.Parameters.AddWithValue("@fromDate", from);
        command.Parameters.AddWithValue("@toDate", to);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(new SwitchFeeRule(
                Product: GetString(reader, "product_code"),
                Direction: GetString(reader, "direction"),
                LegType: GetNullableString(reader, "leg_type"),
                FeePerMillion: GetNullableDecimal(reader, "fee_per_million") ?? 0m,
                EffectiveFrom: GetNullableDateTime(reader, "effective_from") ?? DateTime.MinValue,
                EffectiveTo: GetNullableDateTime(reader, "effective_to")));
        }

        return rules;
    }

    private static decimal? CalculateSwitchFee(
        IReadOnlyList<SwitchFeeRule> rules,
        string product,
        string tenor,
        string direction,
        decimal? amount,
        DateTime? tradeDate)
    {
        if (!amount.HasValue || !tradeDate.HasValue)
            return null;

        string? legType = GetLegType(product, tenor);

        // MAR 상품은 Leg 규칙이 확정되기 전까지 계산하지 않는다.
        if ((product.Equals("NDF MAR", StringComparison.OrdinalIgnoreCase)
             || product.Equals("BASIS MAR", StringComparison.OrdinalIgnoreCase))
            && legType is null)
            return null;

        string normalizedDirection = NormalizeDirection(direction);

        SwitchFeeRule? rule = rules.FirstOrDefault(x =>
            x.Product.Equals(product, StringComparison.OrdinalIgnoreCase)
            && x.Direction.Equals(normalizedDirection, StringComparison.OrdinalIgnoreCase)
            && LegMatches(x.LegType, legType)
            && x.EffectiveFrom.Date <= tradeDate.Value.Date
            && (!x.EffectiveTo.HasValue
                || x.EffectiveTo.Value.Date >= tradeDate.Value.Date));

        if (rule is null)
            return null;

        decimal fee =
            Math.Abs(amount.Value)
            / 1_000_000m
            * rule.FeePerMillion;

        return -decimal.Round(
            fee,
            0,
            MidpointRounding.AwayFromZero);
    }

    private static string? GetLegType(string product, string tenor)
    {
        string p = NormalizeText(product);
        string t = NormalizeText(tenor);

        return (p, t) switch
        {
            ("NDF TOMMAR", "NDF SN") => "NEAR",
            ("NDF TOMMAR", "NDF SN1M") => "FAR",
            ("BASIS TOMMAR", "NDF SN") => "NEAR",
            ("BASIS TOMMAR", "DF SN") => "FAR",
            _ => null
        };
    }

    private static string NormalizeDirection(string direction) =>
        direction.Trim().ToUpperInvariant() switch
        {
            "B" => "BUY",
            "S" => "SELL",
            var value => value
        };

    private static string NormalizeText(string value) =>
        string.Join(
            ' ',
            value.Trim()
                .ToUpperInvariant()
                .Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static bool LegMatches(string? ruleLeg, string? tradeLeg) =>
        string.IsNullOrWhiteSpace(ruleLeg)
            ? string.IsNullOrWhiteSpace(tradeLeg)
            : ruleLeg.Equals(tradeLeg, StringComparison.OrdinalIgnoreCase);

    private static bool IsSwitchFeeInstitution(string instCode) =>
        instCode is "1104" or "1181";

    private sealed record SwitchFeeRule(
        string Product,
        string Direction,
        string? LegType,
        decimal FeePerMillion,
        DateTime EffectiveFrom,
        DateTime? EffectiveTo);

    private static string GetString(
        MySqlDataReader reader,
        string columnName)
    {
        return GetNullableString(reader, columnName)
               ?? string.Empty;
    }

    private static string? GetNullableString(
        MySqlDataReader reader,
        string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal));
    }

    private static decimal? GetNullableDecimal(
        MySqlDataReader reader,
        string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? GetNullableDateTime(
        MySqlDataReader reader,
        string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}
