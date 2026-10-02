using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class OneClubAtATimeTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly MemberService _members;
    private readonly AttendanceService _attendance;

    public OneClubAtATimeTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        _members = new MemberService(_db, new Pbkdf2PasswordHasher(), _clock);
        _attendance = new AttendanceService(_db, _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<int> AddMemberInBothClubs()
    {
        var r = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", "03001111111", new[] { 1, 2 }, "1234", null, RegistrationSource.Admin));
        return r.Member!.Id;
    }

    [Fact]
    public async Task Checking_in_to_another_club_today_checks_out_of_the_first()
    {
        var id = await AddMemberInBothClubs();
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);
        _clock.Now = new DateTime(2026, 10, 1, 10, 0, 0);

        var r = await _attendance.MarkAsync(id, 2, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.Success, r.Status);
        Assert.Equal(AttendanceType.CheckIn, r.Type);
        Assert.Equal("AI Club", r.AutoCheckedOutOf);
        var s1 = await _attendance.GetClubStatsAsync(id, 1);
        Assert.False(s1!.IsInside);
        Assert.Equal(TimeSpan.FromHours(1), s1.TotalTime);
        Assert.True((await _attendance.GetClubStatsAsync(id, 2))!.IsInside);
    }

    [Fact]
    public async Task Normal_check_out_does_not_touch_other_clubs()
    {
        var id = await AddMemberInBothClubs();
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);
        _clock.Now = new DateTime(2026, 10, 1, 10, 0, 0);

        var r = await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);

        Assert.Equal(AttendanceType.CheckOut, r.Type);
        Assert.Null(r.AutoCheckedOutOf);
        Assert.Equal(2, _db.AttendanceLogs.Count());
    }

    [Fact]
    public async Task Yesterdays_open_check_in_in_another_club_blocks_until_time_is_given()
    {
        var id = await AddMemberInBothClubs();
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);
        _clock.Now = new DateTime(2026, 10, 2, 9, 30, 0);

        var r = await _attendance.MarkAsync(id, 2, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.CheckOutRequired, r.Status);
        Assert.Equal("AI Club", r.MissedClubName);
        Assert.Equal(new DateTime(2026, 10, 1, 9, 0, 0), r.MissedCheckIn);
        Assert.Equal(1, _db.AttendanceLogs.Count());
    }

    [Fact]
    public async Task Giving_the_time_closes_the_other_club_then_checks_in()
    {
        var id = await AddMemberInBothClubs();
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);
        _clock.Now = new DateTime(2026, 10, 2, 9, 30, 0);

        var r = await _attendance.MarkAsync(id, 2, VerificationMethod.Pin, new DateTime(2026, 10, 1, 17, 0, 0));

        Assert.Equal(AttendanceStatus.Success, r.Status);
        Assert.Equal(AttendanceType.CheckIn, r.Type);
        var s1 = await _attendance.GetClubStatsAsync(id, 1);
        Assert.False(s1!.IsInside);
        Assert.Equal(TimeSpan.FromHours(8), s1.TotalTime);
        Assert.True((await _attendance.GetClubStatsAsync(id, 2))!.IsInside);
    }
}
