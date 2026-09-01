using MySqlConnector;
using System.Net;
using System.Net.Sockets;

namespace IPS_PROBACKOFFICE.Security;

public sealed class AuditService
{
    private readonly string _connectionString;
    private readonly AppUser _user;

    public AuditService(string connectionString, AppUser user)
    {
        _connectionString = connectionString;
        _user = user;
    }

    public async Task WriteAsync(string actionType, string? targetName = null, string? detail = null)
    {
        try
        {
            const string sql = @"
INSERT INTO backoffice_audit_log
    (user_id, action_type, target_name, action_detail, client_ip)
VALUES
    (@userId, @actionType, @targetName, @detail, @clientIp);";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@userId", _user.UserId);
            command.Parameters.AddWithValue("@actionType", actionType);
            command.Parameters.AddWithValue("@targetName", (object?)targetName ?? DBNull.Value);
            command.Parameters.AddWithValue("@detail", (object?)detail ?? DBNull.Value);
            command.Parameters.AddWithValue("@clientIp", GetClientIp());
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // 감사로그 오류 때문에 실제 업무가 중단되지는 않도록 한다.
        }
    }

    private static string GetClientIp()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                       .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x))
                       ?.ToString()
                   ?? Environment.MachineName;
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
