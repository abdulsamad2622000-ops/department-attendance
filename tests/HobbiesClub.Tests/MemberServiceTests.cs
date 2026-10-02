using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class MemberServiceTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly MemberService _members;

    public MemberServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        _members = new MemberService(_db, new Pbkdf2PasswordHasher(), new FakeClock());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static NewMemberRequest Request(string phone = "0300-1234567", string pin = "1234", params int[] clubs) =>
        new("Ali Khan", phone, clubs.Length == 0 ? new[] { 1 } : clubs, pin, null, RegistrationSource.Admin);

    [Fact]
    public async Task First_member_gets_code_1001_and_next_gets_1002()
    {
        var first = await _members.AddMemberAsync(Request("03001111111", "1111"));
        var second = await _members.AddMemberAsync(Request("03002222222", "2222"));

        Assert.True(first.Succeeded);
        Assert.Equal(1001, first.Member!.MemberCode);
        Assert.Equal(1002, second.Member!.MemberCode);
    }

    [Fact]
    public async Task Duplicate_phone_is_rejected_even_with_different_formatting()
    {
        await _members.AddMemberAsync(Request("0300-1234567"));

        var again = await _members.AddMemberAsync(Request("03001234567"));

        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task Member_can_join_both_clubs()
    {
        var result = await _members.AddMemberAsync(Request("03003333333", "1234", 1, 2));

        Assert.True(result.Succeeded);
        Assert.Equal(2, _db.MemberClubs.Count(mc => mc.MemberId == result.Member!.Id));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12ab")]
    [InlineData("")]
    public async Task Invalid_pin_is_rejected(string pin)
    {
        var result = await _members.AddMemberAsync(Request("03004444444", pin));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Unknown_club_is_rejected()
    {
        var result = await _members.AddMemberAsync(Request("03005555555", "1234", 99));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Pin_is_stored_hashed_not_plain()
    {
        var result = await _members.AddMemberAsync(Request("03006666666", "4321"));

        Assert.NotEqual("4321", result.Member!.PinHash);
        Assert.False(string.IsNullOrEmpty(result.Member.PinSalt));
    }
}

