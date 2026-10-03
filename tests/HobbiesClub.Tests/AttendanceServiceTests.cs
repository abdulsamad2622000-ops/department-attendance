using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class AttendanceServiceTests : IDisposable
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

    public AttendanceServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

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

    private async Task<Member> AddMember(string phone, params int[] clubs)
    {
        var r = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", phone, clubs.Length == 0 ? new[] { 1 } : clubs, "1234", null, RegistrationSource.Admin));
        return r.Member!;
    }

    [Fact]
    public async Task First_scan_is_checkin_then_checkout_then_checkin()
    {
        var m = await AddMember("03001111111");

        var a = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);
        _clock.Now = _clock.Now.AddMinutes(30);
        var b = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);
        _clock.Now = _clock.Now.AddMinutes(30);
        var c = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        Assert.Equal(AttendanceType.CheckIn, a.Type);
        Assert.Equal(AttendanceType.CheckOut, b.Type);
        Assert.Equal(AttendanceType.CheckIn, c.Type);
    }

    [Fact]
    public async Task Scan_within_one_minute_is_rejected()
    {
        var m = await AddMember("03001111111");
        await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        _clock.Now = _clock.Now.AddSeconds(30);
        var again = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.TooSoon, again.Status);
        Assert.Equal(1, _db.AttendanceLogs.Count());

        _clock.Now = _clock.Now.AddSeconds(31);
        var later = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.Success, later.Status);
        Assert.Equal(AttendanceType.CheckOut, later.Type);
    }

    [Fact]
    public async Task Other_club_within_one_minute_is_rejected()
    {
        var m = await AddMember("03001111111", 1, 2);
        await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        _clock.Now = _clock.Now.AddSeconds(20);
        var other = await _attendance.MarkAsync(m.Id, 2, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.TooSoon, other.Status);
        Assert.Equal(1, _db.AttendanceLogs.Count());
    }

    [Fact]
    public async Task Toggle_is_tracked_separately_per_club()
    {
        var m = await AddMember("03001111111", 1, 2);

        var ai = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);
        _clock.Now = _clock.Now.AddMinutes(2);
        var hobbies = await _attendance.MarkAsync(m.Id, 2, VerificationMethod.Pin);

        Assert.Equal(AttendanceType.CheckIn, ai.Type);
        Assert.Equal(AttendanceType.CheckIn, hobbies.Type);
    }

    [Fact]
    public async Task Member_cannot_mark_in_a_club_they_do_not_belong_to()
    {
        var m = await AddMember("03001111111", 1);

        var r = await _attendance.MarkAsync(m.Id, 2, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.NotInClub, r.Status);
    }

    [Fact]
    public async Task Terminated_member_cannot_mark_attendance()
    {
        var m = await AddMember("03001111111");
        await _members.TerminateAsync(m.Id, "Reason", "admin", 1);

        var r = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.MemberInactive, r.Status);
    }

    [Fact]
    public async Task Checkout_reports_todays_total_time_and_month_visits()
    {
        var m = await AddMember("03001111111");

        await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);
        _clock.Now = _clock.Now.AddMinutes(90);
        var r = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        Assert.Equal(TimeSpan.FromMinutes(90), r.TodayTotal);
        Assert.Equal(1, r.MonthVisits);
        Assert.Equal(2, r.Recent!.Count);
    }

    [Fact]
    public async Task Recent_list_keeps_only_last_five()
    {
        var m = await AddMember("03001111111");

        AttendanceResult? last = null;
        for (var i = 0; i < 7; i++)
        {
            last = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);
            _clock.Now = _clock.Now.AddMinutes(10);
        }

        Assert.Equal(5, last!.Recent!.Count);
    }

    [Fact]
    public async Task Month_log_shows_only_this_month()
    {
        var m = await AddMember("03001111111");
        await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        _clock.Now = new DateTime(2026, 11, 2, 9, 0, 0);
        await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin, new DateTime(2026, 10, 1, 10, 0, 0));

        var log = await _attendance.GetMonthLogAsync(m.Id);

        Assert.Single(log);
    }
}



