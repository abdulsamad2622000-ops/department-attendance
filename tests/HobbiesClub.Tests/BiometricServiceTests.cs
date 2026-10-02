using HobbiesClub.Biometrics.Fake;
using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class BiometricServiceTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly MemberService _members;
    private readonly BiometricService _bio;

    public BiometricServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);

        var clock = new FakeClock();
        _members = new MemberService(_db, new Pbkdf2PasswordHasher(), clock);
        _bio = new BiometricService(_db, new FakeFingerprintService(), new FakeFaceService(), clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Member> AddMember(string phone)
    {
        var r = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", phone, new[] { 1 }, phone.Substring(phone.Length - 4), null, RegistrationSource.Admin));
        return r.Member!;
    }

    [Fact]
    public async Task Enrolled_fingerprint_identifies_the_member()
    {
        var m = await AddMember("03001111111");
        await _bio.EnrollFingerprintAsync(m.Id, 1, new byte[] { 1, 2, 3 });

        var found = await _bio.IdentifyFingerprintAsync(new byte[] { 1, 2, 3 });

        Assert.Equal(m.Id, found!.Id);
    }

    [Fact]
    public async Task Unknown_fingerprint_finds_nobody()
    {
        var m = await AddMember("03001111111");
        await _bio.EnrollFingerprintAsync(m.Id, 1, new byte[] { 1, 2, 3 });

        Assert.Null(await _bio.IdentifyFingerprintAsync(new byte[] { 9, 9, 9 }));
    }

    [Fact]
    public async Task Same_finger_of_another_member_is_rejected_as_duplicate()
    {
        var a = await AddMember("03001111111");
        var b = await AddMember("03002222222");
        await _bio.EnrollFingerprintAsync(a.Id, 1, new byte[] { 1, 2, 3 });

        var r = await _bio.EnrollFingerprintAsync(b.Id, 1, new byte[] { 1, 2, 3 });

        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Only_two_fingers_allowed_per_member()
    {
        var m = await AddMember("03001111111");
        await _bio.EnrollFingerprintAsync(m.Id, 1, new byte[] { 1 });
        await _bio.EnrollFingerprintAsync(m.Id, 2, new byte[] { 2 });

        var third = await _bio.EnrollFingerprintAsync(m.Id, 3, new byte[] { 3 });

        Assert.False(third.Succeeded);
        Assert.Equal(2, await _bio.CountTemplatesAsync(m.Id, BiometricType.Fingerprint));
    }

    [Fact]
    public async Task Re_enrolling_same_finger_replaces_old_template()
    {
        var m = await AddMember("03001111111");
        await _bio.EnrollFingerprintAsync(m.Id, 1, new byte[] { 1 });

        var again = await _bio.EnrollFingerprintAsync(m.Id, 1, new byte[] { 5 });

        Assert.True(again.Succeeded);
        Assert.Equal(1, await _bio.CountTemplatesAsync(m.Id, BiometricType.Fingerprint));
        Assert.NotNull(await _bio.IdentifyFingerprintAsync(new byte[] { 5 }));
        Assert.Null(await _bio.IdentifyFingerprintAsync(new byte[] { 1 }));
    }

    [Fact]
    public async Task Face_enroll_identify_and_duplicate_check()
    {
        var a = await AddMember("03001111111");
        var b = await AddMember("03002222222");

        Assert.True((await _bio.EnrollFaceAsync(a.Id, new byte[] { 7, 7 })).Succeeded);
        Assert.Equal(a.Id, (await _bio.IdentifyFaceAsync(new byte[] { 7, 7 }))!.Id);
        Assert.False((await _bio.EnrollFaceAsync(b.Id, new byte[] { 7, 7 })).Succeeded);
    }

    [Fact]
    public async Task Terminated_member_is_not_identified()
    {
        var m = await AddMember("03001111111");
        await _bio.EnrollFaceAsync(m.Id, new byte[] { 4, 4 });
        await _members.TerminateAsync(m.Id, "Reason", "admin", 1);

        Assert.Null(await _bio.IdentifyFaceAsync(new byte[] { 4, 4 }));
    }
}

