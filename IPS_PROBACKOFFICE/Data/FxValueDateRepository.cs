using IPS_PROBACKOFFICE.Models;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class FxValueDateRepository
{
    private readonly string _connectionString;

    public FxValueDateRepository(string connectionString) => _connectionString = connectionString;

    public async Task<IReadOnlyList<FxValueDateRow>> SearchAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT trade_date, tenor, start_date, end_date, day_count,
                   day_adjustment, COALESCE(raw_text, '') AS raw_text
            FROM fx_value_date
            WHERE trade_date BETWEEN @fromDate AND @toDate
            ORDER BY trade_date, FIELD(tenor, 'S/N', '1M'), tenor;
            """;

        var rows = new List<FxValueDateRow>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@fromDate", fromDate.Date);
        command.Parameters.AddWithValue("@toDate", toDate.Date);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new FxValueDateRow(
                reader.GetDateTime("trade_date"),
                reader.GetString("tenor"),
                reader.GetDateTime("start_date"),
                reader.GetDateTime("end_date"),
                reader.GetInt32("day_count"),
                reader.GetInt32("day_adjustment"),
                reader.GetString("raw_text")));
        }
        return rows;
    }

    public async Task<FxValueDateImportResult> ImportAsync(
        IReadOnlyList<FxValueDateRow> rows,
        CancellationToken cancellationToken = default)
    {
        const string existsSql = """
            SELECT 1 FROM fx_value_date
            WHERE trade_date = @tradeDate AND tenor = @tenor
            LIMIT 1;
            """;
        const string upsertSql = """
            INSERT INTO fx_value_date
                (trade_date, tenor, start_date, end_date, day_count, day_adjustment, raw_text)
            VALUES
                (@tradeDate, @tenor, @startDate, @endDate, @dayCount, @dayAdjustment, @rawText)
            ON DUPLICATE KEY UPDATE
                start_date = VALUES(start_date),
                end_date = VALUES(end_date),
                day_count = VALUES(day_count),
                day_adjustment = VALUES(day_adjustment),
                raw_text = VALUES(raw_text);
            """;

        int inserted = 0, updated = 0;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (FxValueDateRow row in rows)
        {
            bool exists;
            await using (var existsCommand = new MySqlCommand(existsSql, connection, transaction))
            {
                existsCommand.Parameters.AddWithValue("@tradeDate", row.TradeDate.Date);
                existsCommand.Parameters.AddWithValue("@tenor", row.Tenor);
                exists = await existsCommand.ExecuteScalarAsync(cancellationToken) is not null;
            }

            await using var command = new MySqlCommand(upsertSql, connection, transaction);
            command.Parameters.AddWithValue("@tradeDate", row.TradeDate.Date);
            command.Parameters.AddWithValue("@tenor", row.Tenor);
            command.Parameters.AddWithValue("@startDate", row.StartDate.Date);
            command.Parameters.AddWithValue("@endDate", row.EndDate.Date);
            command.Parameters.AddWithValue("@dayCount", row.DayCount);
            command.Parameters.AddWithValue("@dayAdjustment", row.DayAdjustment);
            command.Parameters.AddWithValue("@rawText", row.RawText);
            await command.ExecuteNonQueryAsync(cancellationToken);
            if (exists) updated++; else inserted++;
        }

        await transaction.CommitAsync(cancellationToken);
        return new FxValueDateImportResult(inserted, updated);
    }

    public async Task<IReadOnlyList<HolidayOverrideRow>> GetHolidayOverridesAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT holiday_date, country_code, holiday_name, is_holiday, COALESCE(memo, '') AS memo
            FROM holiday_calendar
            WHERE holiday_date BETWEEN @fromDate AND @toDate
            ORDER BY holiday_date, country_code;
            """;
        var rows = new List<HolidayOverrideRow>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@fromDate", fromDate.Date);
        command.Parameters.AddWithValue("@toDate", toDate.Date);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HolidayOverrideRow(
                reader.GetDateTime("holiday_date").Date,
                reader.GetString("country_code"),
                reader.GetString("holiday_name"),
                reader.GetBoolean("is_holiday"),
                reader.GetString("memo")));
        }
        return rows;
    }

    public async Task UpsertHolidayOverrideAsync(
        HolidayOverrideRow row,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO holiday_calendar
                (holiday_date, country_code, holiday_name, is_holiday, memo, created_by)
            VALUES
                (@date, @country, @name, @isHoliday, @memo, @createdBy)
            ON DUPLICATE KEY UPDATE
                holiday_name = VALUES(holiday_name),
                is_holiday = VALUES(is_holiday),
                memo = VALUES(memo),
                created_by = VALUES(created_by);
            """;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@date", row.HolidayDate.Date);
        command.Parameters.AddWithValue("@country", row.CountryCode);
        command.Parameters.AddWithValue("@name", row.HolidayName);
        command.Parameters.AddWithValue("@isHoliday", row.IsHoliday);
        command.Parameters.AddWithValue("@memo", string.IsNullOrWhiteSpace(row.Memo) ? DBNull.Value : row.Memo);
        command.Parameters.AddWithValue("@createdBy", string.IsNullOrWhiteSpace(createdBy) ? DBNull.Value : createdBy);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteHolidayOverrideAsync(
        DateTime holidayDate,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM holiday_calendar
            WHERE holiday_date = @date AND country_code = @country;
            """;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@date", holidayDate.Date);
        command.Parameters.AddWithValue("@country", countryCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record FxValueDateImportResult(int Inserted, int Updated);
public sealed record HolidayOverrideRow(
    DateTime HolidayDate,
    string CountryCode,
    string HolidayName,
    bool IsHoliday,
    string Memo);
