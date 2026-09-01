using IPS_PROBACKOFFICE.Models;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class CreditRepository
{
    private readonly string _connectionString;

    public CreditRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<CreditInstitution>> GetInstitutionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.creditor_id,
                c.name,
                c.is_active
            FROM ips_credit.creditor c
            WHERE c.is_active = 1
            ORDER BY c.name, c.creditor_id;
            """;

        var result = new List<CreditInstitution>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CreditInstitution(
                CreditorId: Convert.ToInt64(reader.GetValue(reader.GetOrdinal("creditor_id"))),
                Name: reader.GetString("name"),
                IsActive: Convert.ToInt32(reader.GetValue(reader.GetOrdinal("is_active"))) == 1));
        }

        return result;
    }

    public async Task<IReadOnlyList<CreditLimitEntry>> GetLimitsAsync(
        long creditorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.debtor_id,
                COALESCE(
                    cp.name,
                    REPLACE(
                        SUBSTRING_INDEX(
                            SUBSTRING_INDEX(
                                cl.name,
                                CONCAT('-', a.debtor_id, '--CREDITOR-'),
                                1),
                            'DEBTOR-',
                            -1),
                        '_',
                        ' '),
                    CONCAT('ID ', a.debtor_id)
                ) AS counterparty_name,
                a.asset_class,
                a.tenor,
                cl.limit_amount,
                cl.is_active
            FROM ips_credit.credit_line_assignment a
            INNER JOIN ips_credit.credit_line cl
                ON cl.credit_line_id = a.credit_line_id
            INNER JOIN ips_credit.creditor cp
                ON cp.creditor_id = a.debtor_id
               AND cp.is_active = 1
            WHERE a.creditor_id = @creditorId
              AND a.is_active = 1
              AND cl.is_active = 1
            ORDER BY counterparty_name, a.debtor_id, a.asset_class, a.tenor;
            """;

        var result = new List<CreditLimitEntry>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@creditorId", creditorId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CreditLimitEntry(
                CounterpartyId: Convert.ToInt64(reader.GetValue(reader.GetOrdinal("debtor_id"))),
                CounterpartyName: reader.GetString("counterparty_name"),
                AssetClass: reader.GetString("asset_class"),
                Tenor: reader.GetString("tenor"),
                LimitAmount: reader.IsDBNull(reader.GetOrdinal("limit_amount"))
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("limit_amount"))),
                IsActive: Convert.ToInt32(reader.GetValue(reader.GetOrdinal("is_active"))) == 1));
        }

        return result;
    }

    public async Task<IReadOnlyList<CreditMatrixEntry>> GetAllLimitsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.creditor_id,
                cr.name AS institution_name,
                a.debtor_id,
                cp.name AS counterparty_name,
                a.asset_class,
                a.tenor,
                cl.limit_amount,
                cl.avail_amount,
                cl.is_active
            FROM ips_credit.credit_line_assignment a
            INNER JOIN ips_credit.credit_line cl
                ON cl.credit_line_id = a.credit_line_id
            INNER JOIN ips_credit.creditor cr
                ON cr.creditor_id = a.creditor_id
               AND cr.is_active = 1
            INNER JOIN ips_credit.creditor cp
                ON cp.creditor_id = a.debtor_id
               AND cp.is_active = 1
            WHERE a.is_active = 1
              AND cl.is_active = 1
            ORDER BY
                cr.name,
                a.creditor_id,
                cp.name,
                a.debtor_id,
                a.asset_class,
                a.tenor;
            """;

        var result = new List<CreditMatrixEntry>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CreditMatrixEntry(
                InstitutionId: Convert.ToInt64(reader.GetValue(reader.GetOrdinal("creditor_id"))),
                InstitutionName: reader.GetString("institution_name"),
                CounterpartyId: Convert.ToInt64(reader.GetValue(reader.GetOrdinal("debtor_id"))),
                CounterpartyName: reader.GetString("counterparty_name"),
                AssetClass: reader.GetString("asset_class"),
                Tenor: reader.GetString("tenor"),
                LimitAmount: reader.IsDBNull(reader.GetOrdinal("limit_amount"))
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("limit_amount"))),
                AvailableAmount: reader.IsDBNull(reader.GetOrdinal("avail_amount"))
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("avail_amount"))),
                IsActive: Convert.ToInt32(reader.GetValue(reader.GetOrdinal("is_active"))) == 1));
        }

        return result;
    }
}
