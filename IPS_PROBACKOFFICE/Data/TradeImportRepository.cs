using System.Data;
using System.Globalization;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class TradeImportRepository
{
    private readonly string _connectionString;

    public TradeImportRepository(string connectionString) => _connectionString = connectionString;

    public async Task<ImportResult> ImportAsync(DataTable csv, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var schema = await LoadSchemaAsync(connection, cancellationToken);
        var csvColumns = csv.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        var insertColumns = csvColumns.Where(schema.ContainsKey).ToList();

        string[] required = ["ID", "Organization", "B/S", "Tenor"];
        string[] missing = required.Where(c => !insertColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("trades 테이블 또는 CSV에 필수 컬럼이 없습니다: " + string.Join(", ", missing));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int inserted = 0, duplicates = 0, errors = 0;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (DataRow row in csv.Rows)
        {
            string key = MakeKey(row);
            try
            {
                if (!seen.Add(key) || await ExistsAsync(connection, transaction, row, cancellationToken))
                {
                    row["처리결과"] = "중복 제외";
                    duplicates++;
                    continue;
                }

                string names = string.Join(", ", insertColumns.Select(c => $"`{c.Replace("`", "``")}`"));
                string values = string.Join(", ", insertColumns.Select((_, i) => $"@p{i}"));
                await using var command = new MySqlCommand($"INSERT INTO trades ({names}) VALUES ({values});", connection, transaction);
                for (int i = 0; i < insertColumns.Count; i++)
                {
                    string column = insertColumns[i];
                    command.Parameters.AddWithValue($"@p{i}", ConvertValue(row[column]?.ToString(), schema[column]));
                }
                await command.ExecuteNonQueryAsync(cancellationToken);
                row["처리결과"] = "입력 완료";
                inserted++;
            }
            catch (Exception ex)
            {
                row["처리결과"] = "오류: " + ex.Message;
                errors++;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new ImportResult(inserted, duplicates, errors);
    }

    private static async Task<Dictionary<string, string>> LoadSchemaAsync(MySqlConnection connection, CancellationToken token)
    {
        const string sql = "SELECT COLUMN_NAME, DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'trades';";
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result[reader.GetString(0)] = reader.GetString(1);
        if (result.Count == 0) throw new InvalidOperationException("현재 데이터베이스에서 trades 테이블을 찾을 수 없습니다.");
        return result;
    }

    private static async Task<bool> ExistsAsync(MySqlConnection connection, MySqlTransaction transaction, DataRow row, CancellationToken token)
    {
        const string sql = """
            SELECT 1 FROM trades
            WHERE TRIM(`ID`) = @id AND TRIM(`Organization`) = @organization
              AND TRIM(`B/S`) = @direction AND TRIM(`Tenor`) = @tenor
            LIMIT 1;
            """;
        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id", Clean(row["ID"]));
        command.Parameters.AddWithValue("@organization", Clean(row["Organization"]));
        command.Parameters.AddWithValue("@direction", Clean(row["B/S"]));
        command.Parameters.AddWithValue("@tenor", Clean(row["Tenor"]));
        return await command.ExecuteScalarAsync(token) is not null;
    }

    private static string MakeKey(DataRow row) => string.Join("\u001f", Clean(row["ID"]), Clean(row["Organization"]), Clean(row["B/S"]), Clean(row["Tenor"]));
    private static string Clean(object? value) => value?.ToString()?.Trim() ?? string.Empty;

    private static object ConvertValue(string? raw, string dataType)
    {
        string value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0 || value == "-") return DBNull.Value;
        string type = dataType.ToLowerInvariant();
        if (type is "date" or "datetime" or "timestamp")
        {
            string normalized = value.Replace('/', '-');
            // Creation Date/Expiry는 밀리초 앞 구분자가 공백인 형태도 내려온다.
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"(\d{2}:\d{2}:\d{2})\s+(\d{3})$",
                "$1.$2");
            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)) return date;
            throw new FormatException($"날짜 형식이 올바르지 않습니다: {value}");
        }
        if (type is "decimal" or "numeric" or "double" or "float" or "real")
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal number)) return number;
            throw new FormatException($"숫자 형식이 올바르지 않습니다: {value}");
        }
        if (type.Contains("int"))
        {
            if (long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out long number)) return number;
            throw new FormatException($"정수 형식이 올바르지 않습니다: {value}");
        }
        return value;
    }
}

public sealed record ImportResult(int Inserted, int Duplicates, int Errors);
