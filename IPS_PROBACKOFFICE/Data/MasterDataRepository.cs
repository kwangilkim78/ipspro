using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE.Models;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

/// <summary>
/// Edgewater ETL의 organizations/users는 조회 전용으로 사용하고,
/// 백오피스에서 관리하는 기관코드/한글명만 로컬 확장 테이블에 저장한다.
/// </summary>
public sealed class MasterDataRepository
{
    private readonly string _connectionString;

    public MasterDataRepository(string connectionString) => _connectionString = connectionString;

    public async Task<IReadOnlyList<Institution>> GetInstitutionsAsync()
    {
        const string sql = """
            SELECT o.id AS external_institution_id,
                   o.slug,
                   o.name,
                   o.emails,
                   o.rtns_org_code,
                   COALESCE(o.enabled, 0) AS enabled,
                   COALESCE(l.institution_code, '') AS institution_code
              FROM mextrader_admin_prod.organizations o
              LEFT JOIN ips_ew.institution_local_info l
                ON l.external_institution_id = o.id
             WHERE COALESCE(o.enabled, 0) = 1
              AND NULLIF(TRIM(l.institution_code), '') IS NOT NULL
            ORDER BY o.name, o.id
            """;

        var rows = new List<Institution>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new Institution(
                S(reader, "institution_code"),
                S(reader, "name"),
                N(reader, "emails"),
                N(reader, "rtns_org_code"),
                Convert.ToInt64(reader["external_institution_id"]),
                S(reader, "slug"),
                Convert.ToInt32(reader["enabled"]) == 1));
        }
        return rows;
    }

    public async Task SaveInstitutionCodeAsync(long externalInstitutionId, string? institutionCode)
    {
        const string sql = """
            INSERT INTO ips_ew.institution_local_info
                (external_institution_id, institution_code)
            VALUES
                (@externalInstitutionId, NULLIF(TRIM(@institutionCode), ''))
            ON DUPLICATE KEY UPDATE
                institution_code = VALUES(institution_code),
                updated_at = CURRENT_TIMESTAMP
            """;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@externalInstitutionId", externalInstitutionId);
        command.Parameters.AddWithValue("@institutionCode", institutionCode?.Trim() ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<DealerInfo>> GetDealersAsync(long externalInstitutionId)
    {
        const string sql = """
            SELECT u.id AS external_user_id,
                   u.organization_id AS external_institution_id,
                   u.username,
                   COALESCE(l.name_ko, '') AS name_ko,
                   COALESCE(u.full_name, '') AS name_eng,
                   COALESCE(u.mobile_number, '') AS mobile_phone,
                   COALESCE(u.work_number, '') AS work_phone,
                   COALESCE(u.email, '') AS email,
                   COALESCE(u.job_title, '') AS job_title,
                   COALESCE(u.rtns_id, '') AS rtns_trader_code,
                   COALESCE(u.rtns_email, '') AS rtns_trader_email,
                   COALESCE(u.permitted_ips, '') AS permitted_ip,
                   COALESCE(u.enabled, 0) AS enabled
              FROM mextrader_admin_prod.users u
              LEFT JOIN ips_ew.user_local_info l
                ON l.external_user_id = u.id
             WHERE u.organization_id = @externalInstitutionId
               AND COALESCE(u.enabled, 0) = 1
             ORDER BY CASE WHEN u.username REGEXP '[0-9]+$'
                           THEN CAST(REGEXP_SUBSTR(u.username, '[0-9]+$') AS UNSIGNED)
                           ELSE 999999 END,
                      u.username,
                      u.id
            """;

        var rows = new List<DealerInfo>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@externalInstitutionId", externalInstitutionId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new DealerInfo
            {
                ExternalUserId = Convert.ToInt64(reader["external_user_id"]),
                ExternalInstitutionId = Convert.ToInt64(reader["external_institution_id"]),
                Username = S(reader, "username"),
                NameKor = S(reader, "name_ko"),
                NameEng = S(reader, "name_eng"),
                MobilePhone = S(reader, "mobile_phone"),
                WorkPhone = S(reader, "work_phone"),
                Email = S(reader, "email"),
                JobTitle = S(reader, "job_title"),
                RtnsTraderCode = S(reader, "rtns_trader_code"),
                RtnsTraderEmail = S(reader, "rtns_trader_email"),
                PermittedIp = S(reader, "permitted_ip"),
                UseYn = Convert.ToInt32(reader["enabled"]) == 1 ? "Y" : "N"
            });
        }
        return rows;
    }

    public async Task SaveUserKoreanNameAsync(long externalUserId, string? nameKo)
    {
        const string sql = """
            INSERT INTO ips_ew.user_local_info
                (external_user_id, name_ko)
            VALUES
                (@externalUserId, NULLIF(TRIM(@nameKo), ''))
            ON DUPLICATE KEY UPDATE
                name_ko = VALUES(name_ko),
                updated_at = CURRENT_TIMESTAMP
            """;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@externalUserId", externalUserId);
        command.Parameters.AddWithValue("@nameKo", nameKo?.Trim() ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }

    private static string S(MySqlDataReader reader, string name) =>
        reader[name] == DBNull.Value ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static string? N(MySqlDataReader reader, string name) =>
        reader[name] == DBNull.Value ? null : Convert.ToString(reader[name]);
}
