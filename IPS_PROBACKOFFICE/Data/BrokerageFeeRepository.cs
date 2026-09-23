using IPS_PROBACKOFFICE.Models;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class BrokerageFeeRepository
{
    private readonly string _connectionString;

    public BrokerageFeeRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<BrokerageFeeRow>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                inst_code,
                institution_name,
                df_1m_fee,
                ndf_1m_fee,
                nd_mar_fee,
                basis_mar_fee,
                currency_code,
                remarks
            FROM brokerage_fee_schedule
            WHERE is_active = 1
            ORDER BY
                CASE currency_code
                    WHEN 'KRW' THEN 1
                    WHEN 'USD' THEN 2
                    ELSE 3
                END,
                institution_name,
                inst_code;
            """;

        var result = new List<BrokerageFeeRow>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new BrokerageFeeRow(
                Convert.ToString(reader["inst_code"]) ?? string.Empty,
                reader.GetString("institution_name"),
                GetNullableDecimal(reader, "df_1m_fee"),
                GetNullableDecimal(reader, "ndf_1m_fee"),
                GetNullableDecimal(reader, "nd_mar_fee"),
                GetNullableDecimal(reader, "basis_mar_fee"),
                GetNullableString(reader, "currency_code"),
                GetNullableString(reader, "remarks")));
        }

        return result;
    }

    public async Task UpdateAllAsync(
        IReadOnlyCollection<BrokerageFeeRow> rows,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE brokerage_fee_schedule
            SET
                df_1m_fee = @df1mFee,
                ndf_1m_fee = @ndf1mFee,
                nd_mar_fee = @ndMarFee,
                basis_mar_fee = @basisMarFee,
                currency_code = @currencyCode,
                remarks = @remarks
            WHERE inst_code = @instCode
              AND is_active = 1;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var row in rows)
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@df1mFee", DbValue(row.Df1mFee));
                command.Parameters.AddWithValue("@ndf1mFee", DbValue(row.Ndf1mFee));
                command.Parameters.AddWithValue("@ndMarFee", DbValue(row.NdMarFee));
                command.Parameters.AddWithValue("@basisMarFee", DbValue(row.BasisMarFee));
                command.Parameters.AddWithValue("@currencyCode", DbValue(row.CurrencyCode?.Trim().ToUpperInvariant()));
                command.Parameters.AddWithValue("@remarks", DbValue(row.Remarks?.Trim()));
                command.Parameters.AddWithValue("@instCode", row.InstCode);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task AddAsync(
        BrokerageFeeRow row,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO brokerage_fee_schedule
            (
                inst_code,
                institution_name,
                df_1m_fee,
                ndf_1m_fee,
                nd_mar_fee,
                basis_mar_fee,
                currency_code,
                remarks,
                is_active
            )
            VALUES
            (
                @instCode,
                @institutionName,
                @df1mFee,
                @ndf1mFee,
                @ndMarFee,
                @basisMarFee,
                @currencyCode,
                @remarks,
                1
            );
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@instCode", row.InstCode);
        command.Parameters.AddWithValue("@institutionName", row.InstitutionName.Trim());
        command.Parameters.AddWithValue("@df1mFee", DbValue(row.Df1mFee));
        command.Parameters.AddWithValue("@ndf1mFee", DbValue(row.Ndf1mFee));
        command.Parameters.AddWithValue("@ndMarFee", DbValue(row.NdMarFee));
        command.Parameters.AddWithValue("@basisMarFee", DbValue(row.BasisMarFee));
        command.Parameters.AddWithValue("@currencyCode", DbValue(row.CurrencyCode?.Trim().ToUpperInvariant()));
        command.Parameters.AddWithValue("@remarks", DbValue(row.Remarks?.Trim()));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string instCode,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE brokerage_fee_schedule
            SET is_active = 0
            WHERE inst_code = @instCode
              AND is_active = 1;
            """;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@instCode", instCode);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static object DbValue(object? value) => value ?? DBNull.Value;

    private static decimal? GetNullableDecimal(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static string? GetNullableString(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
