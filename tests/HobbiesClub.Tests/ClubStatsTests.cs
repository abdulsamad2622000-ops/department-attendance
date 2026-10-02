using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class ClubStatsTests : IDisposable
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

    public ClubStatsTests()
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

    private async Task<int> AddMember()
    {
        var r = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", "03001111111", new[] { 1 }, "1234", null, RegistrationSource.Admin));
        return r.Member!.Id;
    }

    [Fact]
    public async Task Visits_and_time_are_added_up()
    {
        var id = await AddMember();
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);
        _clock.Now = new DateTime(2026, 10, 1, 11, 0, 0);
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);
        _clock.Now = new DateTime(2026, 10, 2, 10, 0, 0);
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);
        _clock.Now = new DateTime(2026, 10, 2, 10, 30, 0);
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);

        var s = await _attendance.GetClubStatsAsync(id, 1);

        Assert.NotNull(s);
        Assert.Equal(2, s!.MonthVisits);
        Assert.Equal(2, s.TotalVisits);
        Assert.Equal(TimeSpan.FromMinutes(150), s.MonthTime);
        Assert.Equal(TimeSpan.FromMinutes(150), s.TotalTime);
        Assert.False(s.IsInside);
        Assert.Equal(4, s.Recent.Count);
        Assert.Equal(AttendanceType.CheckOut, s.Recent[0].Type);
    }

    [Fact]
    public async Task Open_checkin_means_inside_and_adds_no_time()
    {
        var id = await AddMember();
        await _attendance.MarkAsync(id, 1, VerificationMethod.Pin);

        var s = await _attendance.GetClubStatsAsync(id, 1);

        Assert.True(s!.IsInside);
        Assert.Equal(1, s.MonthVisits);
        Assert.Equal(TimeSpan.Zero, s.MonthTime);
    }

    [Fact]
    public async Task No_entries_gives_empty_stats()
    {
        var id = await AddMember();

        var s = await _attendance.GetClubStatsAsync(id, 1);

        Assert.NotNull(s);
        Assert.Equal(0, s!.TotalVisits);
        Assert.Null(s.LastEntry);
        Assert.Empty(s.Recent);
    }
}
