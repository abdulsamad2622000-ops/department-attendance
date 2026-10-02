using HobbiesClub.Core.Enums;

namespace HobbiesClub.Core.Models;

public record AttendanceLogInfo(AttendanceType Type, DateTime Timestamp, VerificationMethod Method, bool IsManualEntry);

public record ClubStats(
    int ClubId,
    string ClubName,
    bool IsInside,
    DateTime? LastEntry,
    int MonthVisits,
    TimeSpan MonthTime,
    int TotalVisits,
    TimeSpan TotalTime,
    IReadOnlyList<AttendanceLogInfo> Recent);
