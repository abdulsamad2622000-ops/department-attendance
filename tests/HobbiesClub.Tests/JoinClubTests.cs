using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class JoinClubTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly MemberService _members;

    public JoinClubTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        _members = new MemberService(_db, new Pbkdf2PasswordHasher(), new FakeClock());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<int> AddMemberInClub1()
    {
        var r = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", "03001111111", new[] { 1 }, "1234", null, RegistrationSource.Admin));
        return r.Member!.Id;
    }

    [Fact]
    public async Task Joinable_clubs_are_the_ones_not_joined()
    {
        var id = await AddMemberInClub1();
        var joinable = await _members.GetJoinableClubsAsync(id);
        Assert.Single(joinable);
        Assert.Equal(2, joinable[0].Id);
    }

    [Fact]
    public async Task Member_can_join_second_club_and_it_is_audited()
    {
        var id = await AddMemberInClub1();
        var r = await _members.JoinClubAsync(id, 2);
        Assert.True(r.Succeeded);
        Assert.Equal(2, _db.MemberClubs.Count(mc => mc.MemberId == id));
        Assert.Contains(_db.AuditLogs, a => a.Action == "ClubJoined");
        Assert.Empty(await _members.GetJoinableClubsAsync(id));
    }

    [Fact]
    public async Task Joining_a_club_twice_is_rejected()
    {
        var id = await AddMemberInClub1();
        var r = await _members.JoinClubAsync(id, 1);
        Assert.False(r.Succeeded);
        Assert.Equal(1, _db.MemberClubs.Count(mc => mc.MemberId == id));
    }

    [Fact]
    public async Task Unknown_club_is_rejected()
    {
        var id = await AddMemberInClub1();
        var r = await _members.JoinClubAsync(id, 99);
        Assert.False(r.Succeeded);
    }
}
