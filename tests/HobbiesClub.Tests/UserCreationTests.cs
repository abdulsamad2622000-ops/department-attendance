using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class UserCreationTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public UserCreationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        _auth = new AuthService(_db, new Pbkdf2PasswordHasher(), new FakeClock());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task New_super_admin_must_change_password()
    {
        var r = await _auth.CreateUserAsync("second", "Second Admin", "password123", UserRole.SuperAdmin, "root", 1);

        Assert.True(r.Succeeded);
        Assert.True(_db.AppUsers.Single(u => u.Username == "second").MustChangePassword);
    }

    [Fact]
    public async Task New_kiosk_user_does_not_need_password_change()
    {
        var r = await _auth.CreateUserAsync("kiosk1", "Front Desk", "password123", UserRole.Kiosk, "root", 1);

        Assert.True(r.Succeeded);
        Assert.False(_db.AppUsers.Single(u => u.Username == "kiosk1").MustChangePassword);
    }

    [Fact]
    public async Task Duplicate_username_is_rejected_ignoring_case()
    {
        await _auth.CreateUserAsync("kiosk1", "Front Desk", "password123", UserRole.Kiosk, "root", 1);

        var again = await _auth.CreateUserAsync("  KIOSK1 ", "Other", "password123", UserRole.Kiosk, "root", 1);

        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task Short_password_is_rejected()
    {
        var r = await _auth.CreateUserAsync("kiosk1", "Front Desk", "short", UserRole.Kiosk, "root", 1);

        Assert.False(r.Succeeded);
        Assert.Empty(_db.AppUsers);
    }

    [Fact]
    public async Task Changing_password_clears_the_flag_and_new_password_logs_in()
    {
        await _auth.CreateUserAsync("second", "Second Admin", "password123", UserRole.SuperAdmin, "root", 1);
        var login = await _auth.LoginAsync("second", "password123");
        Assert.True(login.User!.MustChangePassword);

        var change = await _auth.ChangePasswordAsync(login.User.Id, "password123", "newpassword456");
        var again = await _auth.LoginAsync("second", "newpassword456");

        Assert.True(change.Succeeded);
        Assert.Equal(LoginStatus.Success, again.Status);
        Assert.False(again.User!.MustChangePassword);
    }
}
