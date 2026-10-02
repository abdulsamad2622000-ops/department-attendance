namespace HobbiesClub.Core.Entities;

public class AuditLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; }

    /// <summary>May be null for kiosk or self-registration actions.</summary>
    public int? UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>Examples: Login, MemberTerminated, BackupRestored, PasswordReset.</summary>
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
}
