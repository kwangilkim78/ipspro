using IPS_PROBACKOFFICE.Models;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class SwitchFeeRuleRepository
{
    private readonly string _connectionString;

    public SwitchFeeRuleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<SwitchFeeRuleRow>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                r.inst_code,
                COALESCE(o.name, r.inst_code) AS institution_name,
                r.product_code,
                r.direction,
                r.leg_type,
                r.fee_per_million,
                r.effective_from,
                r.effective_to,
                r.is_active
            FROM switch_fee_rule r
            LEFT JOIN ips_ew.institution_local_info l
              ON CAST(l.institution_code AS CHAR) = CAST(r.inst_code AS CHAR)
            LEFT JOIN mextrader_admin_prod.organizations o
              ON o.id = l.external_institution_id
            ORDER BY
                r.inst_code,
                r.product_code,
                r.direction,
                COALESCE(r.leg_type, ''),
                r.effective_from DESC;
            """;

        var rows = new List<SwitchFeeRuleRow>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SwitchFeeRuleRow
            {
                InstCode = Convert.ToString(reader["inst_code"]) ?? string.Empty,
                InstitutionName = Convert.ToString(reader["institution_name"]) ?? string.Empty,
                ProductCode = Convert.ToString(reader["product_code"]) ?? string.Empty,
                Direction = Convert.ToString(reader["direction"]) ?? string.Empty,
                LegType = GetNullableString(reader, "leg_type"),
                FeePerMillion = reader.GetDecimal("fee_per_million"),
                EffectiveFrom = reader.GetDateTime("effective_from"),
                EffectiveTo = GetNullableDateTime(reader, "effective_to"),
                IsActive = Convert.ToBoolean(reader["is_active"])
            });
        }

        return rows;
    }

    public async Task UpdateAllAsync(
        IReadOnlyCollection<SwitchFeeRuleRow> rows,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE switch_fee_rule
            SET
                fee_per_million = @feePerMillion,
                effective_to = @effectiveTo,
                is_active = @isActive
            WHERE CAST(inst_code AS CHAR) = @instCode
              AND product_code = @productCode
              AND direction = @direction
              AND leg_type <=> @legType
              AND effective_from = @effectiveFrom;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (SwitchFeeRuleRow row in rows)
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@feePerMillion", row.FeePerMillion);
                command.Parameters.AddWithValue("@effectiveTo", DbValue(row.EffectiveTo));
                command.Parameters.AddWithValue("@isActive", row.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@instCode", row.InstCode);
                command.Parameters.AddWithValue("@productCode", row.ProductCode);
                command.Parameters.AddWithValue("@direction", row.Direction);
                command.Parameters.AddWithValue("@legType", DbValue(row.LegType));
                command.Parameters.AddWithValue("@effectiveFrom", row.EffectiveFrom);
                int affected = await command.ExecuteNonQueryAsync(cancellationToken);
                if (affected == 0)
                {
                    throw new InvalidOperationException(
                        $"저장할 스위치 수수료 규칙을 찾지 못했습니다: " +
                        $"{row.InstCode}/{row.ProductCode}/{row.Direction}/{row.LegType ?? "-"}");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? GetNullableString(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? GetNullableDateTime(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static object DbValue(object? value) => value ?? DBNull.Value;
}
