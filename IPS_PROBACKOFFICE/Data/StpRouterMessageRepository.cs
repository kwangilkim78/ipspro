using IPS_PROBACKOFFICE.Models;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class StpRouterMessageRepository
{
    private readonly string _connectionString;

    public StpRouterMessageRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<StpRouterMessage>> SearchAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        string? keyword,
        int limit = 5000,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                m.message_id,
                m.report_id,
                m.ack,
                m.rule_id,
                m.channel_id,
                m.orig_report_id,
                m.trade_date,
                m.orig_order_id,
                m.status,
                1 AS is_transmitted,
                m.created,
                m.message
            FROM mex_prod.stp_router_messages m
            WHERE m.created >= @fromUtc
              AND m.created < @toUtc
              AND m.channel_id IN (2003, 2004)
              AND UPPER(m.status) IN ('NEW', 'SENT')
              AND (
                    @keyword IS NULL
                    OR m.report_id LIKE CONCAT('%', @keyword, '%')
                    OR m.orig_report_id LIKE CONCAT('%', @keyword, '%')
                    OR m.orig_order_id LIKE CONCAT('%', @keyword, '%')
                    OR m.message LIKE CONCAT('%', @keyword, '%')
                  )
            ORDER BY
                m.created DESC,
                m.message_id DESC
            LIMIT @limit;
            """;

        var result = new List<StpRouterMessage>();

        await using var connection =
            new MySqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@fromUtc", fromUtc);
        command.Parameters.AddWithValue("@toUtc", toUtcExclusive);
        command.Parameters.AddWithValue(
            "@keyword",
            string.IsNullOrWhiteSpace(keyword)
                ? DBNull.Value
                : keyword.Trim());

        command.Parameters.AddWithValue(
            "@limit",
            Math.Clamp(limit, 1, 5000));

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StpRouterMessage(
                MessageId: reader.GetInt32("message_id"),
                ReportId: reader.GetString("report_id"),
                Ack: reader.GetBoolean("ack"),
                RuleId: reader.GetInt32("rule_id"),
                ChannelId: reader.GetInt32("channel_id"),
                OrigReportId: reader.GetString("orig_report_id"),
                TradeDate: reader.GetString("trade_date"),
                OrigOrderId: reader.GetString("orig_order_id"),
                Status: reader.GetString("status"),
                IsTransmitted: true,
                Created: reader.GetDateTime("created"),
                Message: reader.GetString("message")));
        }

        return result;
    }
}