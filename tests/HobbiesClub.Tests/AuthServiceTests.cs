using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class AuthServiceTests : IDisposable
{
    private class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 9, 0, 0);
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        DatabaseInitializer.Initialize(_db);
        _auth = new AuthService(_db, new Pbkdf2PasswordHasher(), _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task First_run_is_true_until_super_admin_is_created()
    {
        Assert.True(await _auth.IsFirstRunAsync());

        var result = await _auth.CreateFirstSuperAdminAsync("admin", "Owner", "Password#1");

        Assert.True(result.Succeeded);
        Assert.False(await _auth.IsFirstRunAsync());
    }

    [Fact]
    public async Task Second_setup_attempt_is_rejected()
    {
        await _auth.CreateFirstSuperAdminAsync("admin", "Owner", "Password#1");

        var again = await _auth.CreateFirstSuperAdminAsync("other", "Other", "Password#2");

        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task Short_password_is_rejected()
    {
        var result = await _auth.CreateFirstSuperAdminAsync("admin", "Owner", "short");

        Assert.False(result.Succeeded);
        Assert.True(await _auth.IsFirstRunAsync());
    }

    [Fact]
    public async Task Login_works_and_ignores_username_case()
    {
        await _auth.CreateFirstSuperAdminAsync("admin", "Owner", "Password#1");

        var login = await _auth.LoginAsync("ADMIN", "Password#1");

        Assert.Equal(LoginStatus.Success, login.Status);
        Assert.Equal("admin", login.User!.Username);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_then_it_unlocks()
    {
        await _auth.CreateFirstSuperAdminAsync("admin", "Owner", "Password#1");

        for (var i = 0; i < 4; i++)
            Assert.Equal(LoginStatus.InvalidCredentials, (await _auth.LoginAsync("admin", "wrong")).Status);

        Assert.Equal(LoginStatus.LockedOut, (await _auth.LoginAsync("admin", "wrong")).Status);
        Assert.Equal(LoginStatus.LockedOut, (await _auth.LoginAsync("admin", "Password#1")).Status);

        _clock.Now = _clock.Now.AddSeconds(31);

        Assert.Equal(LoginStatus.Success, (await _auth.LoginAsync("admin", "Password#1")).Status);
    }

    [Fact]
    public async Task Change_password_needs_current_password()
    {
        await _auth.CreateFirstSuperAdminAsync("admin", "Owner", "Password#1");
        var user = (await _auth.LoginAsync("admin", "Password#1")).User!;

        Assert.False((await _auth.ChangePasswordAsync(user.Id, "wrong", "NewPassword#2")).Succeeded);
        Assert.True((await _auth.ChangePasswordAsync(user.Id, "Password#1", "NewPassword#2")).Succeeded);

        Assert.Equal(LoginStatus.InvalidCredentials, (await _auth.LoginAsync("admin", "Password#1")).Status);
        Assert.Equal(LoginStatus.Success, (await _auth.LoginAsync("admin", "NewPassword#2")).Status);
    }
}
