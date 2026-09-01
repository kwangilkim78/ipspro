using IPS_PROBACKOFFICE.Models;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class ApiFeeExemptionRepository
{
    private readonly string _connectionString;

    public ApiFeeExemptionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<ApiFeeExemptionRow>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            SELECT
                afe.exemption_id AS id,
                CAST(afe.institution_code AS CHAR) AS institution_code,
                COALESCE(o.name, CAST(afe.institution_code AS CHAR)) AS institution_name,
                afe.trader_id,
                afe.start_datetime,
                afe.end_datetime,
                afe.is_active,
                afe.remarks
            FROM api_fee_exemption afe
            LEFT JOIN ips_ew.institution_local_info il
              ON CAST(il.institution_code AS CHAR) = CAST(afe.institution_code AS CHAR)
            LEFT JOIN mextrader_admin_prod.organizations o
              ON o.id = il.external_institution_id
            ORDER BY afe.is_active DESC, afe.start_datetime DESC, afe.exemption_id DESC;
            """;

        var rows = new List<ApiFeeExemptionRow>();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            DateTime endExclusive = reader.GetDateTime("end_datetime");
            rows.Add(new ApiFeeExemptionRow
            {
                Id = reader.GetInt64("id"),
                InstitutionCode = reader.GetString("institution_code"),
                InstitutionName = reader.GetString("institution_name"),
                TraderId = reader.GetString("trader_id"),
                StartDate = reader.GetDateTime("start_datetime").Date,
                EndDateInclusive = endExclusive.Date.AddDays(-1),
                IsActive = reader.GetBoolean("is_active"),
                Remarks = reader.IsDBNull(reader.GetOrdinal("remarks"))
                    ? null
                    : reader.GetString("remarks")
            });
        }

        return rows;
    }

    public async Task<IReadOnlyList<ApiFeeExemptionInstitution>> GetInstitutionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT
                CAST(il.institution_code AS CHAR) AS institution_code,
                o.name AS institution_name
            FROM ips_ew.institution_local_info il
            JOIN mextrader_admin_prod.organizations o
              ON o.id = il.external_institution_id
            WHERE NULLIF(TRIM(CAST(il.institution_code AS CHAR)), '') IS NOT NULL
              AND COALESCE(o.enabled, 0) = 1
            ORDER BY o.name;
            """;

        var rows = new List<ApiFeeExemptionInstitution>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ApiFeeExemptionInstitution(
                reader.GetString("institution_code"),
                reader.GetString("institution_name")));
        }
        return rows;
    }

    public async Task AddAsync(ApiFeeExemptionRow row, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO api_fee_exemption
                (institution_code, trader_id, start_datetime, end_datetime, is_active, remarks)
            VALUES
                (@institutionCode, @traderId, @startDate, @endExclusive, @isActive, @remarks);
            """;
        await ExecuteSaveAsync(connection, sql, row, includeId: false,
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(ApiFeeExemptionRow row, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            UPDATE api_fee_exemption
            SET institution_code = @institutionCode,
                trader_id = @traderId,
                start_datetime = @startDate,
                end_datetime = @endExclusive,
                is_active = @isActive,
                remarks = @remarks
            WHERE exemption_id = @id;
            """;
        int affected = await ExecuteSaveAsync(connection, sql, row, includeId: true,
            cancellationToken: cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException("수정할 API 수수료 면제 정책을 찾지 못했습니다.");
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM api_fee_exemption WHERE exemption_id = @id;";
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<int> ExecuteSaveAsync(
        MySqlConnection connection,
        string sql,
        ApiFeeExemptionRow row,
        bool includeId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@institutionCode", row.InstitutionCode);
        command.Parameters.AddWithValue("@traderId", row.TraderId.Trim());
        command.Parameters.AddWithValue("@startDate", row.StartDate.Date);
        command.Parameters.AddWithValue("@endExclusive", row.EndDateInclusive.Date.AddDays(1));
        command.Parameters.AddWithValue("@isActive", row.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@remarks",
            string.IsNullOrWhiteSpace(row.Remarks) ? DBNull.Value : row.Remarks.Trim());
        if (includeId)
            command.Parameters.AddWithValue("@id", row.Id);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

}
