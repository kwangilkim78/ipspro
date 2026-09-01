using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IPS_PROBACKOFFICE.Security;

public sealed record SavedLoginCredential(
    string LoginId,
    string Password,
    bool RememberPassword,
    bool AutoLogin);

public static class LoginCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("IPS_PROBACKOFFICE_LOGIN_V1");
    private static readonly string StoreDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IPS_PROBACKOFFICE");
    private static readonly string StorePath = Path.Combine(StoreDirectory, "login.dat");

    public static SavedLoginCredential? Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return null;
            byte[] encrypted = File.ReadAllBytes(StorePath);
            byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<SavedLoginCredential>(plain);
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public static bool Save(SavedLoginCredential credential)
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

    public static void DisableAutoLogin()
    {
        SavedLoginCredential? saved = Load();
        if (saved is not null)
            Save(saved with { AutoLogin = false });
    }

    public static void UpdatePassword(string loginId, string password)
    {
        SavedLoginCredential? saved = Load();
        if (saved is not null && saved.LoginId.Equals(loginId, StringComparison.OrdinalIgnoreCase))
            Save(saved with { Password = password });
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(StorePath)) File.Delete(StorePath);
        }
        catch
        {
            // 로그인 정보 삭제 실패가 프로그램 실행을 막지 않도록 한다.
        }
    }
}
