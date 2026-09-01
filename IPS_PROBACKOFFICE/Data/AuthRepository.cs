using IPS_PROBACKOFFICE.Security;
using MySqlConnector;

namespace IPS_PROBACKOFFICE.Data;

public sealed class AuthRepository
{
    private readonly string _connectionString;

    public AuthRepository(string connectionString) => _connectionString = connectionString;

    public async Task<int> GetActiveUserCountAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM backoffice_user WHERE is_active = 1", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task CreateInitialUserAsync(string loginId, string userName, string password, string? email)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var countCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM backoffice_user WHERE is_active = 1",
            connection,
            transaction))
        {
            if (Convert.ToInt32(await countCommand.ExecuteScalarAsync()) > 0)
                throw new InvalidOperationException("이미 등록된 사용자가 있습니다. 로그인 화면을 다시 열어주세요.");
        }

        const string sql = @"
INSERT INTO backoffice_user
    (login_id, user_name, password_hash, must_change_password, role_code, email, is_active)
VALUES
    (@loginId, @userName, @passwordHash, 0, 'USER', @email, 1);";
        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@loginId", loginId.Trim());
        command.Parameters.AddWithValue("@userName", userName.Trim());
        command.Parameters.AddWithValue("@passwordHash", PasswordHasher.Hash(password));
        command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email.Trim());
        await command.ExecuteNonQueryAsync();

        const string permissionSql = @"
INSERT INTO backoffice_user_screen_permission
    (login_id, screen_key, can_view)
VALUES
    (@loginId, @screenKey, 1);";

        foreach (ScreenPermissionDefinition screen in ScreenPermissionCatalog.Screens)
        {
            await using var permission = new MySqlCommand(permissionSql, connection, transaction);
            permission.Parameters.AddWithValue("@loginId", loginId.Trim());
            permission.Parameters.AddWithValue("@screenKey", screen.Key);
            await permission.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    public async Task<AppUser?> AuthenticateAsync(string loginId, string password)
    {
        const string sql = @"
SELECT user_id, login_id, user_name, password_hash, role_code, email, must_change_password
FROM backoffice_user
WHERE login_id = @loginId AND is_active = 1
LIMIT 1;";
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@loginId", loginId.Trim());
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        string storedHash = reader.GetString("password_hash");
        if (!PasswordHasher.Verify(password, storedHash))
            return null;

        var user = new AppUser(
            reader.GetInt64("user_id"),
            reader.GetString("login_id"),
            reader.GetString("user_name"),
            reader.GetString("role_code").ToUpperInvariant(),
            reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString("email"),
            reader.GetBoolean("must_change_password"));
        await reader.CloseAsync();

        await using var update = new MySqlCommand(
            "UPDATE backoffice_user SET last_login_at = NOW() WHERE user_id = @userId",
            connection);
        update.Parameters.AddWithValue("@userId", user.UserId);
        await update.ExecuteNonQueryAsync();
        return user;
    }

    public async Task<IReadOnlyList<BackOfficeUserRow>> GetUsersAsync()
    {
        const string sql = @"
SELECT user_id, login_id, user_name, role_code, email, is_active,
       must_change_password, last_login_at, password_changed_at, created_at
FROM backoffice_user
ORDER BY is_active DESC, user_name, login_id;";
        var rows = new List<BackOfficeUserRow>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new BackOfficeUserRow(
                reader.GetInt64("user_id"),
                reader.GetString("login_id"),
                reader.GetString("user_name"),
                reader.GetString("role_code"),
                reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString("email"),
                reader.GetBoolean("is_active"),
                reader.GetBoolean("must_change_password"),
                reader.IsDBNull(reader.GetOrdinal("last_login_at")) ? null : reader.GetDateTime("last_login_at"),
                reader.IsDBNull(reader.GetOrdinal("password_changed_at")) ? null : reader.GetDateTime("password_changed_at"),
                reader.GetDateTime("created_at")));
        }
        return rows;
    }

    public async Task AddUserAsync(string loginId, string userName, string password, string roleCode, string? email)
    {
        const string sql = @"
INSERT INTO backoffice_user
    (login_id, user_name, password_hash, role_code, email, is_active)
VALUES
    (@loginId, @userName, @passwordHash, @roleCode, @email, 1);";
        await ExecuteUserCommandAsync(sql, loginId, userName, roleCode, email, PasswordHasher.Hash(password));
    }

    public async Task UpdateUserAsync(long userId, string userName, string roleCode, string? email)
    {
        const string sql = @"
UPDATE backoffice_user
SET user_name = @userName, role_code = @roleCode, email = @email
WHERE user_id = @userId;";
        await ExecuteUserCommandAsync(sql, null, userName, roleCode, email, null, userId);
    }

    public async Task ResetPasswordAsync(long userId, string password)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(
            @"UPDATE backoffice_user
              SET password_hash = @hash,
                  must_change_password = 1,
                  password_changed_at = NULL
              WHERE user_id = @userId", connection);
        command.Parameters.AddWithValue("@hash", PasswordHasher.Hash(password));
        command.Parameters.AddWithValue("@userId", userId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> ChangeOwnPasswordAsync(long userId, string currentPassword, string newPassword)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        string? storedHash;
        await using (var select = new MySqlCommand(
            "SELECT password_hash FROM backoffice_user WHERE user_id = @userId AND is_active = 1",
            connection,
            transaction))
        {
            select.Parameters.AddWithValue("@userId", userId);
            storedHash = Convert.ToString(await select.ExecuteScalarAsync());
        }

        if (string.IsNullOrWhiteSpace(storedHash) || !PasswordHasher.Verify(currentPassword, storedHash))
        {
            await transaction.RollbackAsync();
            return false;
        }

        await using var update = new MySqlCommand(
            @"UPDATE backoffice_user
              SET password_hash = @passwordHash,
                  must_change_password = 0,
                  password_changed_at = NOW()
              WHERE user_id = @userId",
            connection,
            transaction);
        update.Parameters.AddWithValue("@passwordHash", PasswordHasher.Hash(newPassword));
        update.Parameters.AddWithValue("@userId", userId);
        await update.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task SetActiveAsync(long userId, bool isActive)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(
            "UPDATE backoffice_user SET is_active = @isActive WHERE user_id = @userId", connection);
        command.Parameters.AddWithValue("@isActive", isActive);
        command.Parameters.AddWithValue("@userId", userId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlySet<string>> GetScreenPermissionsAsync(
        string loginId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT screen_key
FROM backoffice_user_screen_permission
WHERE login_id = @loginId
  AND can_view = 1;";

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@loginId", loginId.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            permissions.Add(reader.GetString("screen_key"));

        return permissions;
    }

    public async Task<bool> HasScreenPermissionConfigurationAsync(
        string loginId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COUNT(*)
FROM backoffice_user_screen_permission
WHERE login_id = @loginId;";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@loginId", loginId.Trim());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public async Task SaveScreenPermissionsAsync(
        string loginId,
        IEnumerable<string> screenKeys,
        CancellationToken cancellationToken = default)
    {
        string normalizedLoginId = loginId.Trim();
        HashSet<string> keys = screenKeys
            .Where(key => ScreenPermissionCatalog.AllKeys.Contains(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var delete = new MySqlCommand(
            "DELETE FROM backoffice_user_screen_permission WHERE login_id = @loginId",
            connection,
            transaction))
        {
            delete.Parameters.AddWithValue("@loginId", normalizedLoginId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = @"
INSERT INTO backoffice_user_screen_permission
    (login_id, screen_key, can_view)
VALUES
    (@loginId, @screenKey, @canView);";

        foreach (ScreenPermissionDefinition screen in ScreenPermissionCatalog.Screens)
        {
            await using var insert = new MySqlCommand(insertSql, connection, transaction);
            insert.Parameters.AddWithValue("@loginId", normalizedLoginId);
            insert.Parameters.AddWithValue("@screenKey", screen.Key);
            insert.Parameters.AddWithValue("@canView", keys.Contains(screen.Key));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ExecuteUserCommandAsync(
        string sql,
        string? loginId,
        string userName,
        string roleCode,
        string? email,
        string? passwordHash = null,
        long? userId = null)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        if (loginId is not null) command.Parameters.AddWithValue("@loginId", loginId.Trim());
        command.Parameters.AddWithValue("@userName", userName.Trim());
        command.Parameters.AddWithValue("@roleCode", roleCode.ToUpperInvariant());
        command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email.Trim());
        if (passwordHash is not null) command.Parameters.AddWithValue("@passwordHash", passwordHash);
        if (userId.HasValue) command.Parameters.AddWithValue("@userId", userId.Value);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed record BackOfficeUserRow(
    long UserId,
    string LoginId,
    string UserName,
    string RoleCode,
    string? Email,
    bool IsActive,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    DateTime? PasswordChangedAt,
    DateTime CreatedAt);
