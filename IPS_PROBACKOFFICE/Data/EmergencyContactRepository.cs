using IPS_PROBACKOFFICE.Models;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class EmergencyContactRepository(string connectionString)
{
    private bool _initialized;

    public async Task DeleteAsync(EmergencyContact contact)
    {
        if (contact.Id <= 0) throw new ArgumentException("삭제할 연락처를 선택하세요.");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new MySqlCommand("DELETE FROM ips_ew.institution_emergency_contact WHERE id=@id AND updated_at=@updatedAt", connection);
        command.Parameters.AddWithValue("@id", contact.Id);
        command.Parameters.AddWithValue("@updatedAt", contact.UpdatedAt);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new InvalidOperationException("다른 사용자가 수정하거나 삭제한 연락처입니다. 새로고침 후 다시 확인하세요.");
    }

    public async Task<IReadOnlyList<EmergencyContact>> GetAllAsync()
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        if (!_initialized)
        {
            // 기존 테이블이 있으면 CREATE 권한 없이 조회할 수 있습니다.
            using var exists = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='ips_ew' AND table_name='institution_emergency_contact'", connection);
            if (Convert.ToInt32(await exists.ExecuteScalarAsync()) == 0)
            {
                using var stream = typeof(EmergencyContactRepository).Assembly.GetManifestResourceStream("IPS_PROBACKOFFICE.Database.v63_emergency_contacts.sql")!;
                using var reader = new StreamReader(stream);
                using var install = new MySqlCommand(await reader.ReadToEndAsync(), connection);
                await install.ExecuteNonQueryAsync();
            }
            _initialized = true;
        }
        const string sql = """
            SELECT c.*, COALESCE(o.name, CONCAT('[기관 ID ', c.external_institution_id, ']')) AS institution_name
            FROM ips_ew.institution_emergency_contact c
            LEFT JOIN mextrader_admin_prod.organizations o ON o.id = c.external_institution_id
            ORDER BY institution_name, FIELD(c.category, '딜러', 'IT', '백오피스'), c.contact_priority, c.contact_name, c.id
            """;
        using var command = new MySqlCommand(sql, connection);
        await using var rows = await command.ExecuteReaderAsync();
        var result = new List<EmergencyContact>();
        while (await rows.ReadAsync())
            result.Add(new EmergencyContact
            {
                Id = rows.GetInt64("id"), InstitutionId = rows.GetInt64("external_institution_id"),
                InstitutionName = rows.GetString("institution_name"), Category = rows.GetString("category"),
                ContactName = rows.GetString("contact_name"), WorkPhone = rows.GetString("work_phone"),
                MobilePhone = rows.GetString("mobile_phone"), Email = rows.GetString("email"),
                Priority = rows.GetInt32("contact_priority"), Remarks = rows.GetString("remarks"),
                UpdatedBy = rows.GetString("updated_by"), UpdatedAt = rows.GetDateTime("updated_at")
            });
        return result;
    }

    public async Task SaveAsync(EmergencyContact contact, string loginId)
    {
        contact.Validate();
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        string sql = contact.Id == 0 ? """
            INSERT INTO ips_ew.institution_emergency_contact
            (external_institution_id, category, contact_name, work_phone, mobile_phone, email, contact_priority, remarks, created_by, updated_by)
            VALUES (@institution, @category, @name, @work, @mobile, @email, @priority, @remarks, @user, @user)
            """ : """
            UPDATE ips_ew.institution_emergency_contact SET
                external_institution_id=@institution, category=@category, contact_name=@name,
                work_phone=@work, mobile_phone=@mobile, email=@email, contact_priority=@priority,
                remarks=@remarks, updated_by=@user, updated_at=CURRENT_TIMESTAMP(6)
            WHERE id=@id AND updated_at=@originalUpdatedAt
            """;
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@institution", contact.InstitutionId);
        command.Parameters.AddWithValue("@category", contact.Category);
        command.Parameters.AddWithValue("@name", contact.ContactName.Trim());
        command.Parameters.AddWithValue("@work", contact.WorkPhone.Trim());
        command.Parameters.AddWithValue("@mobile", contact.MobilePhone.Trim());
        command.Parameters.AddWithValue("@email", contact.Email.Trim());
        command.Parameters.AddWithValue("@priority", contact.Priority);
        command.Parameters.AddWithValue("@remarks", contact.Remarks.Trim());
        command.Parameters.AddWithValue("@user", loginId);
        command.Parameters.AddWithValue("@id", contact.Id);
        command.Parameters.AddWithValue("@originalUpdatedAt", contact.UpdatedAt);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new InvalidOperationException("다른 사용자가 변경한 연락처입니다. 새로고침 후 다시 수정하세요.");
    }
}
