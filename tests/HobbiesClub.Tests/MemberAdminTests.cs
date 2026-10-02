using HobbiesClub.Biometrics.Fake;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class MemberAdminTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly MemberService _members;
    private readonly MemberAdminService _admin;

    public MemberAdminTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        var clock = new FakeClock();
        var hasher = new Pbkdf2PasswordHasher();
        _members = new MemberService(_db, hasher, clock);
        var bio = new BiometricService(_db, new FakeFingerprintService(), new FakeFaceService(), clock);
        _admin = new MemberAdminService(_db, hasher, clock, bio);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<int> Add(string phone, string pin, params int[] clubs)
    {
        var r = await _members.AddMemberAsync(new NewMemberRequest(
            "Ali Khan", phone, clubs.Length == 0 ? new[] { 1 } : clubs, pin, null, RegistrationSource.Admin, "Sgt", "RN-1", "Unit 1"));
        return r.Member!.Id;
    }

    [Fact]
    public async Task Reset_pin_works_and_new_pin_identifies_member()
    {
        var id = await Add("03001111111", "1111");

        var r = await _admin.ResetPinAsync(id, "5555", "root", 1);
        var v = await _members.VerifyPinOnlyAsync("5555");

        Assert.True(r.Succeeded);
        Assert.Equal(PinVerifyStatus.Success, v.Status);
        Assert.Contains(_db.AuditLogs, a => a.Action == "PinReset");
    }

    [Fact]
    public async Task Reset_pin_rejects_pin_of_another_member()
    {
        var id = await Add("03001111111", "1111");
        await Add("03002222222", "2222");

        var r = await _admin.ResetPinAsync(id, "2222", "root", 1);

        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Reset_pin_rejects_invalid_pin()
    {
        var id = await Add("03001111111", "1111");

        Assert.False((await _admin.ResetPinAsync(id, "12", "root", 1)).Succeeded);
    }

    [Fact]
    public async Task Reset_pin_clears_lockout()
    {
        var id = await Add("03001111111", "1111");
        var m = _db.Members.Single(x => x.Id == id);
        m.PinLockedUntil = new DateTime(2030, 1, 1);
        m.PinFailedAttempts = 3;
        await _db.SaveChangesAsync();

        await _admin.ResetPinAsync(id, "7777", "root", 1);

        var after = _db.Members.Single(x => x.Id == id);
        Assert.Null(after.PinLockedUntil);
        Assert.Equal(0, after.PinFailedAttempts);
    }

    [Fact]
    public async Task Leave_club_works_but_last_club_cannot_be_removed()
    {
        var id = await Add("03001111111", "1111", 1, 2);

        Assert.True((await _admin.LeaveClubAsync(id, 2, "root", 1)).Succeeded);
        Assert.False((await _admin.LeaveClubAsync(id, 1, "root", 1)).Succeeded);
        Assert.Equal(1, _db.MemberClubs.Count(mc => mc.MemberId == id));
    }

    [Fact]
    public async Task Reenroll_face_replaces_template_and_photo()
    {
        var id = await Add("03001111111", "1111");

        var r = await _admin.ReenrollFaceAsync(id, new byte[] { 9, 9, 9 }, new byte[] { 1, 2, 3 }, "root", 1);

        Assert.True(r.Succeeded);
        Assert.Equal(1, _db.BiometricTemplates.Count(t => t.MemberId == id && t.Type == BiometricType.Face));
        Assert.Equal(new byte[] { 1, 2, 3 }, _db.Members.Single(x => x.Id == id).PhotoJpeg);
    }

    [Fact]
    public async Task Reenroll_face_rejects_face_of_another_member()
    {
        var a = await Add("03001111111", "1111");
        var b = await Add("03002222222", "2222");
        await _admin.ReenrollFaceAsync(a, new byte[] { 4, 4 }, null, "root", 1);

        var r = await _admin.ReenrollFaceAsync(b, new byte[] { 4, 4 }, null, "root", 1);

        Assert.False(r.Succeeded);
    }
}
