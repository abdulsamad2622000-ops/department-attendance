using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;

namespace HobbiesClub.Services;

public class BadgeService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public BadgeService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Changes a member's badge. Only a Super Admin should call this.</summary>
    public async Task<OperationResult> SetBadgeAsync(int memberId, MemberBadge badge, string actorUsername, int? actorUserId)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member is null)
            return new OperationResult(false, "Member not found.");

        var old = member.Badge;
        member.Badge = badge;

        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = _clock.Now,
            UserId = actorUserId,
            Username = actorUsername,
            Action = "BadgeChanged",
            Details = $"Member {member.MemberCode}: {old} -> {badge}"
        });

        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }
}
