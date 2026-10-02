using HobbiesClub.Core.Enums;

namespace HobbiesClub.Core.Entities;

public class AttendanceLog
{
    public int Id { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public AttendanceType Type { get; set; }
    public VerificationMethod Method { get; set; }

    /// <summary>Local PC time.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>True if a Super Admin added or corrected this entry manually.</summary>
    public bool IsManualEntry { get; set; }
    public string? Note { get; set; }
}
