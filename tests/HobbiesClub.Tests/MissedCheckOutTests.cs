using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class MissedCheckOutTests : IDisposable
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

    public MissedCheckOutTests()
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

    private async Task<Member> MemberWithOpenCheckInYesterday()
    {
        var r = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", "03001111111", new[] { 1 }, "1234", null, RegistrationSource.Admin));
        var m = r.Member!;
        await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);
        _clock.Now = new DateTime(2026, 10, 2, 9, 5, 0);
        return m;
    }

    [Fact]
    public async Task Open_checkin_from_yesterday_requires_a_checkout_time_and_records_nothing()
    {
        var m = await MemberWithOpenCheckInYesterday();

        var r = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.CheckOutRequired, r.Status);
        Assert.Equal(new DateTime(2026, 10, 1, 9, 0, 0), r.MissedCheckIn);
        Assert.Equal(1, _db.AttendanceLogs.Count());
    }

    [Fact]
    public async Task Supplying_the_time_records_checkout_then_todays_checkin()
    {
        var m = await MemberWithOpenCheckInYesterday();
        var left = new DateTime(2026, 10, 1, 17, 30, 0);

        var r = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin, left);

        Assert.Equal(AttendanceStatus.Success, r.Status);
        Assert.Equal(AttendanceType.CheckIn, r.Type);

        var logs = _db.AttendanceLogs.OrderBy(a => a.Id).ToList();
        Assert.Equal(3, logs.Count);
        Assert.Equal(AttendanceType.CheckOut, logs[1].Type);
        Assert.Equal(left, logs[1].Timestamp);
        Assert.True(logs[1].IsManualEntry);
        Assert.Equal(AttendanceType.CheckIn, logs[2].Type);
        Assert.Equal(1, _db.AuditLogs.Count(a => a.Action == "LateCheckOutEntered"));
    }

    [Fact]
    public async Task Checkout_time_before_the_checkin_is_rejected()
    {
        var m = await MemberWithOpenCheckInYesterday();

        var r = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin, new DateTime(2026, 10, 1, 8, 0, 0));

        Assert.Equal(AttendanceStatus.InvalidCheckOutTime, r.Status);
        Assert.Equal(1, _db.AttendanceLogs.Count());
    }

    [Fact]
    public async Task Checkout_time_in_the_future_is_rejected()
    {
        var m = await MemberWithOpenCheckInYesterday();

        var r = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin, new DateTime(2026, 10, 2, 12, 0, 0));

        Assert.Equal(AttendanceStatus.InvalidCheckOutTime, r.Status);
        Assert.Equal(1, _db.AttendanceLogs.Count());
    }

    [Fact]
    public async Task Open_checkin_from_today_is_a_normal_checkout()
    {
        var r0 = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", "03002222222", new[] { 1 }, "1234", null, RegistrationSource.Admin));
        var m = r0.Member!;
        await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);
        _clock.Now = _clock.Now.AddHours(3);

        var r = await _attendance.MarkAsync(m.Id, 1, VerificationMethod.Pin);

        Assert.Equal(AttendanceStatus.Success, r.Status);
        Assert.Equal(AttendanceType.CheckOut, r.Type);
    }
}
