using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Services;

public class AttendanceService
{
    public static readonly TimeSpan MinGap = TimeSpan.FromMinutes(1);

    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public AttendanceService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>The clubs a member belongs to (for club selection on the kiosk).</summary>
    public async Task<List<Club>> GetMemberClubsAsync(int memberId) =>
        await _db.MemberClubs
            .Where(mc => mc.MemberId == memberId && mc.Club.IsActive)
            .OrderBy(mc => mc.ClubId)
            .Select(mc => mc.Club)
            .ToListAsync();

    private Task<AttendanceLog?> LastLogAsync(int memberId, int clubId) =>
        _db.AttendanceLogs
            .Include(a => a.Club)
            .Where(a => a.MemberId == memberId && a.ClubId == clubId)
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Marks attendance. Rules:
    /// 1. A check-in left open on an earlier day, in ANY of the member's clubs, blocks everything until the
    ///    member supplies the time they left (missedCheckOutTime).
    /// 2. A member can be inside only one club at a time: checking in to a club checks the member out of
    ///    any other club they are inside today.
    /// </summary>
    public async Task<AttendanceResult> MarkAsync(
        int memberId, int clubId, VerificationMethod method, DateTime? missedCheckOutTime = null, string? missedNote = null)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member is null)
            return new AttendanceResult(AttendanceStatus.MemberNotFound);

        if (member.Status != MemberStatus.Active)
            return new AttendanceResult(AttendanceStatus.MemberInactive, member);

        var club = await _db.Clubs.FindAsync(clubId);
        if (club is null || !club.IsActive)
            return new AttendanceResult(AttendanceStatus.ClubNotFound, member);

        var memberClubIds = await _db.MemberClubs
            .Where(mc => mc.MemberId == memberId)
            .Select(mc => mc.ClubId)
            .ToListAsync();
        if (!memberClubIds.Contains(clubId))
            return new AttendanceResult(AttendanceStatus.NotInClub, member, club.Name);

        var now = _clock.Now;

        // Rule 1: an open check-in from an earlier day, in any club.
        AttendanceLog? stale = null;
        foreach (var id in memberClubIds)
        {
            var l = await LastLogAsync(memberId, id);
            if (l is not null && l.Type == AttendanceType.CheckIn && l.Timestamp.Date < now.Date)
            {
                stale = l;
                break;
            }
        }

        if (stale is not null)
        {
            if (missedCheckOutTime is not DateTime leftAt)
                return new AttendanceResult(AttendanceStatus.CheckOutRequired, member, club.Name,
                    MissedCheckIn: stale.Timestamp, MissedClubName: stale.Club.Name);

            if (leftAt < stale.Timestamp || leftAt > now)
                return new AttendanceResult(AttendanceStatus.InvalidCheckOutTime, member, club.Name,
                    MissedCheckIn: stale.Timestamp, MissedClubName: stale.Club.Name);

            _db.AttendanceLogs.Add(new AttendanceLog
            {
                MemberId = memberId,
                ClubId = stale.ClubId,
                Type = AttendanceType.CheckOut,
                Method = method,
                Timestamp = leftAt,
                IsManualEntry = true,
                Note = string.IsNullOrWhiteSpace(missedNote) ? "Late check-out entered by member" : missedNote.Trim()
            });
            _db.AuditLogs.Add(new AuditLog
            {
                Timestamp = now,
                UserId = null,
                Username = "kiosk",
                Action = "LateCheckOutEntered",
                Details = $"Code {member.MemberCode}, {stale.Club.Name}: check-in {stale.Timestamp:yyyy-MM-dd HH:mm}, left {leftAt:yyyy-MM-dd HH:mm}" + (string.IsNullOrWhiteSpace(missedNote) ? "" : $", note: {missedNote.Trim()}")
            });
            await _db.SaveChangesAsync();
        }

        // 1-minute guard applies to the member across all clubs, not only this one.
        var lastAny = await _db.AttendanceLogs.Where(a => a.MemberId == memberId)
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id).FirstOrDefaultAsync();
        if (lastAny is not null && now - lastAny.Timestamp < MinGap)
            return new AttendanceResult(AttendanceStatus.TooSoon, member, club.Name, RetryAfter: lastAny.Timestamp + MinGap);

        var last = await LastLogAsync(memberId, clubId);
        if (last is not null && now - last.Timestamp < MinGap)
            return new AttendanceResult(AttendanceStatus.TooSoon, member, club.Name, RetryAfter: last.Timestamp + MinGap);

        var type = last is null || last.Type == AttendanceType.CheckOut
            ? AttendanceType.CheckIn
            : AttendanceType.CheckOut;

        // Rule 2: only one club at a time.
        string? autoCheckedOutOf = null;
        if (type == AttendanceType.CheckIn)
        {
            var closed = new List<string>();
            foreach (var otherId in memberClubIds.Where(x => x != clubId))
            {
                var o = await LastLogAsync(memberId, otherId);
                if (o is null || o.Type != AttendanceType.CheckIn || o.Timestamp.Date != now.Date)
                    continue;

                _db.AttendanceLogs.Add(new AttendanceLog
                {
                    MemberId = memberId,
                    ClubId = otherId,
                    Type = AttendanceType.CheckOut,
                    Method = method,
                    Timestamp = now,
                    IsManualEntry = false,
                    Note = $"Auto check-out: checked in to {club.Name}"
                });
                _db.AuditLogs.Add(new AuditLog
                {
                    Timestamp = now,
                    UserId = null,
                    Username = "kiosk",
                    Action = "AutoCheckOut",
                    Details = $"Code {member.MemberCode}: {o.Club.Name} closed, checked in to {club.Name}"
                });
                closed.Add(o.Club.Name);
            }

            if (closed.Count > 0)
            {
                await _db.SaveChangesAsync();
                autoCheckedOutOf = string.Join(", ", closed);
            }
        }

        _db.AttendanceLogs.Add(new AttendanceLog
        {
            MemberId = memberId,
            ClubId = clubId,
            Type = type,
            Method = method,
            Timestamp = now,
            IsManualEntry = false
        });
        await _db.SaveChangesAsync();

        var summary = await BuildSummaryAsync(memberId, clubId, now);
        return new AttendanceResult(
            AttendanceStatus.Success, member, club.Name, type, now,
            summary.TodayTotal, summary.MonthVisits, summary.Recent, AutoCheckedOutOf: autoCheckedOutOf);
    }

    /// <summary>A member's full performance in one club, for the member's club detail page.</summary>
    public async Task<ClubStats?> GetClubStatsAsync(int memberId, int clubId)
    {
        var club = await _db.Clubs.FindAsync(clubId);
        if (club is null)
            return null;

        var logs = await _db.AttendanceLogs
            .Where(a => a.MemberId == memberId && a.ClubId == clubId)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
            .ToListAsync();

        var now = _clock.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var monthTime = TimeSpan.Zero;
        var totalTime = TimeSpan.Zero;
        DateTime? openIn = null;

        foreach (var log in logs)
        {
            if (log.Type == AttendanceType.CheckIn)
            {
                openIn = log.Timestamp;
            }
            else if (openIn is DateTime start)
            {
                var length = log.Timestamp - start;
                totalTime += length;
                if (start >= monthStart)
                    monthTime += length;
                openIn = null;
            }
        }

        var last = logs.LastOrDefault();
        var recent = logs
            .AsEnumerable()
            .Reverse()
            .Take(10)
            .Select(a => new AttendanceLogInfo(a.Type, a.Timestamp, a.Method, a.IsManualEntry))
            .ToList();

        return new ClubStats(
            club.Id,
            club.Name,
            last is not null && last.Type == AttendanceType.CheckIn,
            last?.Timestamp,
            logs.Count(a => a.Type == AttendanceType.CheckIn && a.Timestamp >= monthStart),
            monthTime,
            logs.Count(a => a.Type == AttendanceType.CheckIn),
            totalTime,
            recent);
    }

    /// <summary>"View my record only": does not mark attendance.</summary>
    public async Task<List<AttendanceLog>> GetMonthLogAsync(int memberId, int? clubId = null)
    {
        var now = _clock.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var query = _db.AttendanceLogs
            .Include(a => a.Club)
            .Where(a => a.MemberId == memberId && a.Timestamp >= monthStart);

        if (clubId is int id)
            query = query.Where(a => a.ClubId == id);

        return await query.OrderByDescending(a => a.Timestamp).ToListAsync();
    }

    private async Task<(TimeSpan TodayTotal, int MonthVisits, List<AttendanceEntryInfo> Recent)> BuildSummaryAsync(
        int memberId, int clubId, DateTime now)
    {
        var today = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var todayLogs = await _db.AttendanceLogs
            .Where(a => a.MemberId == memberId && a.ClubId == clubId && a.Timestamp >= today && a.Timestamp < today.AddDays(1))
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
            .ToListAsync();

        var total = TimeSpan.Zero;
        DateTime? openIn = null;
        foreach (var log in todayLogs)
        {
            if (log.Type == AttendanceType.CheckIn)
                openIn = log.Timestamp;
            else if (openIn is DateTime start)
            {
                total += log.Timestamp - start;
                openIn = null;
            }
        }

        var monthVisits = await _db.AttendanceLogs.CountAsync(a =>
            a.MemberId == memberId && a.ClubId == clubId &&
            a.Type == AttendanceType.CheckIn && a.Timestamp >= monthStart);

        var recent = await _db.AttendanceLogs
            .Where(a => a.MemberId == memberId && a.ClubId == clubId)
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            .Take(5)
            .Select(a => new AttendanceEntryInfo(a.Type, a.Timestamp, a.Method))
            .ToListAsync();

        return (total, monthVisits, recent);
    }
}



