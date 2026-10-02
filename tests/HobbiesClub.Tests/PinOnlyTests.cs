using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

[Collection("StaticKioskLock")]
public class PinOnlyTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly MemberService _members;

    public PinOnlyTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        _members = new MemberService(_db, new Pbkdf2PasswordHasher(), _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task<MemberResult> Add(string phone, string pin) =>
        _members.AddMemberAsync(new NewMemberRequest("Ali Khan", phone, new[] { 1 }, pin, null, RegistrationSource.Admin));

    [Fact]
    public async Task Correct_pin_identifies_the_member()
    {
        var a = await Add("03001111111", "1111");
        await Add("03002222222", "2222");

        var r = await _members.VerifyPinOnlyAsync("1111");

        Assert.Equal(PinVerifyStatus.Success, r.Status);
        Assert.Equal(a.Member!.Id, r.Member!.Id);
    }

    [Fact]
    public async Task Wrong_pin_is_invalid()
    {
        await Add("03001111111", "1111");

        var r = await _members.VerifyPinOnlyAsync("9999");

        Assert.Equal(PinVerifyStatus.InvalidCredentials, r.Status);
    }

    [Fact]
    public async Task Same_pin_for_second_member_is_rejected()
    {
        await Add("03001111111", "1111");

        var second = await Add("03002222222", "1111");

        Assert.False(second.Succeeded);
    }

    [Fact]
    public async Task Five_wrong_pins_lock_the_kiosk()
    {
        await Add("03001111111", "1111");
        await _members.VerifyPinOnlyAsync("1111"); // resets the static counter

        for (var i = 0; i < MemberService.MaxKioskPinAttempts - 1; i++)
            Assert.Equal(PinVerifyStatus.InvalidCredentials, (await _members.VerifyPinOnlyAsync("9999")).Status);

        Assert.Equal(PinVerifyStatus.Locked, (await _members.VerifyPinOnlyAsync("9999")).Status);
        Assert.Equal(PinVerifyStatus.Locked, (await _members.VerifyPinOnlyAsync("1111")).Status);

        _clock.Now = _clock.Now.AddMinutes(10);
        Assert.Equal(PinVerifyStatus.Success, (await _members.VerifyPinOnlyAsync("1111")).Status);
    }
}

