using HobbiesClub.Biometrics.Fake;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class RegistrationTransactionTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly MemberService _members;
    private readonly BiometricService _bio;

    public RegistrationTransactionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
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

    private static byte[] B(byte x) => new[] { x, x, x };

    private Task<MemberResult> Reg(string phone, string pin, byte[] face, byte[] f1, byte[] f2) =>
        _bio.RegisterMemberAsync(_members,
            new NewMemberRequest("Ali Khan", phone, new[] { 1 }, pin, null, RegistrationSource.SelfService, "Sgt", "RN-1", "Unit 1"),
            face, f1, f2);

    [Fact]
    public async Task Success_saves_member_face_and_two_fingers()
    {
        var r = await Reg("03001111111", "1111", B(1), B(2), B(3));

        Assert.True(r.Succeeded);
        Assert.Equal(1, _db.Members.Count());
        Assert.Equal(3, _db.BiometricTemplates.Count());
        Assert.Equal("Sgt", _db.Members.Single().Rank);
    }

    [Fact]
    public async Task Same_finger_twice_saves_nothing()
    {
        var r = await Reg("03001111111", "1111", B(1), B(2), B(2));

        Assert.False(r.Succeeded);
        Assert.Equal(0, _db.Members.Count());
        Assert.Equal(0, _db.BiometricTemplates.Count());
    }

    [Fact]
    public async Task Finger_of_another_member_rolls_everything_back()
    {
        await Reg("03001111111", "1111", B(1), B(2), B(3));

        var r = await Reg("03002222222", "2222", B(4), B(2), B(5));

        Assert.False(r.Succeeded);
        Assert.Equal(1, _db.Members.Count());
        Assert.Equal(3, _db.BiometricTemplates.Count());
    }

    [Fact]
    public async Task Face_of_another_member_rolls_everything_back()
    {
        await Reg("03001111111", "1111", B(1), B(2), B(3));

        var r = await Reg("03002222222", "2222", B(1), B(4), B(5));

        Assert.False(r.Succeeded);
        Assert.Equal(1, _db.Members.Count());
        Assert.Equal(3, _db.BiometricTemplates.Count());
    }

    [Fact]
    public async Task Taken_pin_saves_nothing()
    {
        await Reg("03001111111", "1111", B(1), B(2), B(3));

        var r = await Reg("03002222222", "1111", B(4), B(5), B(6));

        Assert.False(r.Succeeded);
        Assert.Equal(1, _db.Members.Count());
        Assert.Equal(3, _db.BiometricTemplates.Count());
    }
}
