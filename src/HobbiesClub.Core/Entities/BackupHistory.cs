using HobbiesClub.Core.Enums;

namespace HobbiesClub.Core.Entities;

public class BackupHistory
{
    public int Id { get; set; }

    public BackupKind Kind { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Null for automatic backups; the Super Admin ID for manual ones.</summary>
    public int? CreatedByUserId { get; set; }
}
