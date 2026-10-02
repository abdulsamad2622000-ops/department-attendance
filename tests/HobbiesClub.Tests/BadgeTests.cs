using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class BadgeTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly MemberService _members;
    private readonly BadgeService _badges;

    public BadgeTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        var clock = new FakeClock();
        _members = new MemberService(_db, new Pbkdf2PasswordHasher(), clock);
        _badges = new BadgeService(_db, clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<int> AddMember()
    {
        var r = await _members.AddMemberAsync(
            new NewMemberRequest("Ali Khan", "03001111111", new[] { 1 }, "1234", null, RegistrationSource.Admin, "Sgt", "RN-1", "Unit 1"));
        return r.Member!.Id;
    }

    [Fact]
    public async Task New_member_starts_as_basic()
    {
        var id = await AddMember();

        Assert.Equal(MemberBadge.Basic, _db.Members.Single(m => m.Id == id).Badge);
    }

    [Fact]
    public async Task Super_admin_can_change_badge_and_it_is_audited()
    {
        var id = await AddMember();

        var r = await _badges.SetBadgeAsync(id, MemberBadge.Pro, "root", 1);

        Assert.True(r.Succeeded);
        Assert.Equal(MemberBadge.Pro, _db.Members.Single(m => m.Id == id).Badge);
        Assert.Contains(_db.AuditLogs, a => a.Action == "BadgeChanged");
    }

    [Fact]
    public async Task Unknown_member_is_rejected()
    {
        var r = await _badges.SetBadgeAsync(999, MemberBadge.Medium, "root", 1);

        Assert.False(r.Succeeded);
    }
}
