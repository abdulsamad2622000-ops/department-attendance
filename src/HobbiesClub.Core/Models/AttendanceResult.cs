using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;

namespace HobbiesClub.Core.Models;

public enum AttendanceStatus
{
    Success, TooSoon, MemberInactive, NotInClub, MemberNotFound, ClubNotFound,
    CheckOutRequired, InvalidCheckOutTime
}

public record AttendanceEntryInfo(AttendanceType Type, DateTime Timestamp, VerificationMethod Method);

public record AttendanceResult(
    AttendanceStatus Status,
    Member? Member = null,
    string? ClubName = null,
    AttendanceType? Type = null,
    DateTime? Timestamp = null,
    TimeSpan? TodayTotal = null,
    int MonthVisits = 0,
    IReadOnlyList<AttendanceEntryInfo>? Recent = null,
    DateTime? RetryAfter = null,
    DateTime? MissedCheckIn = null,
    string? MissedClubName = null,
    string? AutoCheckedOutOf = null);
