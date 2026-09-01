using IPS_PROBACKOFFICE.Security;
using MySqlConnector;

namespace IPS_PROBACKOFFICE;

public static class DatabaseConnectionManager
{
    public static string? Resolve(string configuredConnectionString)
    {
        string baseConnectionString = RemoveCredentials(configuredConnectionString);
        SavedDatabaseCredential? saved = DatabaseCredentialStore.Load();
        string? error = null;

        if (saved is not null)
        {
            string savedConnectionString = BuildConnectionString(
                baseConnectionString,
                saved.UserName,
                saved.Password);
            try
            {
                using var connection = new MySqlConnection(savedConnectionString);
                connection.Open();
                return savedConnectionString;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        using var form = new DatabaseConnectionForm(baseConnectionString, saved, error);
        UiLanguage.Apply(form);
        return form.ShowDialog() == DialogResult.OK ? form.ConnectionString : null;
    }

    public static string BuildConnectionString(string baseConnectionString, string userName, string password)
    {
        var builder = new MySqlConnectionStringBuilder(baseConnectionString)
        {
            UserID = userName,
            Password = password
        };
        return builder.ConnectionString;
    }

    private static string RemoveCredentials(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            UserID = string.Empty,
            Password = string.Empty
        };
        return builder.ConnectionString;
    }
}
