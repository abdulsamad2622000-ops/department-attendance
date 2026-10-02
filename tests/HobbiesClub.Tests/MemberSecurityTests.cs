using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class MemberSecurityTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly MemberService _members;

    public MemberSecurityTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        _members = new MemberService(_db, new Pbkdf2PasswordHasher(), _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Member> AddMember(string phone = "03001234567")
    {
        var result = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", phone, new[] { 1 }, "1234", null, RegistrationSource.Admin));
        return result.Member!;
    }

    [Fact]
    public async Task Correct_pin_succeeds()
    {
        var m = await AddMember();

        var r = await _members.VerifyPinAsync(m.MemberCode, "1234");

        Assert.Equal(PinVerifyStatus.Success, r.Status);
    }

    [Fact]
    public async Task Five_wrong_pins_lock_for_five_minutes_then_unlock()
    {
        var m = await AddMember();

        for (var i = 0; i < 4; i++)
            Assert.Equal(PinVerifyStatus.InvalidCredentials, (await _members.VerifyPinAsync(m.MemberCode, "0000")).Status);

        Assert.Equal(PinVerifyStatus.Locked, (await _members.VerifyPinAsync(m.MemberCode, "0000")).Status);
        Assert.Equal(PinVerifyStatus.Locked, (await _members.VerifyPinAsync(m.MemberCode, "1234")).Status);

        _clock.Now = _clock.Now.AddMinutes(5).AddSeconds(1);

        Assert.Equal(PinVerifyStatus.Success, (await _members.VerifyPinAsync(m.MemberCode, "1234")).Status);
    }

    [Fact]
    public async Task Unknown_member_code_is_not_found()
    {
        var r = await _members.VerifyPinAsync(9999, "1234");

        Assert.Equal(PinVerifyStatus.NotFound, r.Status);
    }

    [Fact]
    public async Task Terminate_needs_a_reason()
    {
        var m = await AddMember();

        var r = await _members.TerminateAsync(m.Id, "  ", "admin", 1);

        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Terminated_member_cannot_use_pin_and_biometrics_are_removed()
    {
        var m = await AddMember();
        _db.BiometricTemplates.Add(new BiometricTemplate
        {
            MemberId = m.Id,
            Type = BiometricType.Fingerprint,
            FingerIndex = 1,
            TemplateData = new byte[] { 1, 2, 3 },
            CreatedAt = _clock.Now
        });
        await _db.SaveChangesAsync();

        var t = await _members.TerminateAsync(m.Id, "Rule violation", "admin", 1);

        Assert.True(t.Succeeded);
        Assert.Equal(PinVerifyStatus.Terminated, (await _members.VerifyPinAsync(m.MemberCode, "1234")).Status);
        Assert.Equal(0, _db.BiometricTemplates.Count(x => x.MemberId == m.Id));
    }

    [Fact]
    public async Task Terminated_phone_stays_blocked_for_new_registration()
    {
        var m = await AddMember("03007777777");
        await _members.TerminateAsync(m.Id, "Reason", "admin", 1);

        var again = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", "0300-7777777", new[] { 1 }, "1234", null, RegistrationSource.SelfService));

        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task Reactivate_restores_pin_login()
    {
        var m = await AddMember();
        await _members.TerminateAsync(m.Id, "Reason", "admin", 1);

        var r = await _members.ReactivateAsync(m.Id, "admin", 1);

        Assert.True(r.Succeeded);
        Assert.Equal(PinVerifyStatus.Success, (await _members.VerifyPinAsync(m.MemberCode, "1234")).Status);
    }
}
