namespace IPS_PROBACKOFFICE.Security;

public sealed record AppUser(
    long UserId,
    string LoginId,
    string UserName,
    string RoleCode,
    string? Email,
    bool MustChangePassword);
