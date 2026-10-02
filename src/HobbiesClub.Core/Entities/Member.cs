using HobbiesClub.Core.Enums;

namespace HobbiesClub.Core.Entities;

public class Member
{
    public int Id { get; set; }

    /// <summary>Short ID shown on the kiosk (1001, 1002...).</summary>
    public int MemberCode { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public string RollRegNo { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public byte[]? PhotoJpeg { get; set; }

    /// <summary>Performance badge, set by a Super Admin. New members start as Basic.</summary>
    public MemberBadge Badge { get; set; } = MemberBadge.Basic;

    public string PinHash { get; set; } = string.Empty;
    public string PinSalt { get; set; } = string.Empty;
    public int PinFailedAttempts { get; set; }
    public DateTime? PinLockedUntil { get; set; }

    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public DateTime? TerminatedAt { get; set; }
    public string? TerminationReason { get; set; }

    public RegistrationSource RegisteredVia { get; set; }
    public DateTime RegisteredAt { get; set; }
}
