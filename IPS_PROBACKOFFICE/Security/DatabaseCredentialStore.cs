using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IPS_PROBACKOFFICE.Security;

public sealed record SavedDatabaseCredential(string UserName, string Password);

public static class DatabaseCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("IPS_PROBACKOFFICE_DATABASE_V1");
    private static readonly string StoreDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IPS_PROBACKOFFICE");
    private static readonly string StorePath = Path.Combine(StoreDirectory, "database.dat");

    public static SavedDatabaseCredential? Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return null;
            byte[] encrypted = File.ReadAllBytes(StorePath);
            byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<SavedDatabaseCredential>(plain);
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public static bool Save(SavedDatabaseCredential credential)
    {
        try
        {
            Directory.CreateDirectory(StoreDirectory);
            byte[] plain = JsonSerializer.SerializeToUtf8Bytes(credential);
            byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(StorePath, encrypted);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(StorePath)) File.Delete(StorePath);
        }
        catch
        {
            // 저장정보 삭제 실패가 프로그램 실행을 막지 않도록 한다.
        }
    }
}
