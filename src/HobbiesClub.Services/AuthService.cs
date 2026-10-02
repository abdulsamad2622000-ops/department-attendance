using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Services;

public class AuthService
{
    public const int MaxFailedAttempts = 5;
    public const int MinPasswordLength = 8;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public AuthService(AppDbContext db, IPasswordHasher hasher, IClock clock)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<bool> IsFirstRunAsync() =>
        !await _db.AppUsers.AnyAsync(u => u.Role == UserRole.SuperAdmin);

    public async Task<OperationResult> CreateFirstSuperAdminAsync(string username, string fullName, string password)
    {
        if (!await IsFirstRunAsync())
            return new OperationResult(false, "Setup has already been completed.");

        var error = Validate(username, fullName, password);
        if (error is not null)
            return new OperationResult(false, error);

        var (hash, salt) = _hasher.Hash(password);
        var user = new AppUser
        {
            Username = Normalize(username),
            FullName = fullName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.SuperAdmin,
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = _clock.Now
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();

        AddAudit(user, "FirstSuperAdminCreated", null);
        await _db.SaveChangesAsync();

        return new OperationResult(true);
    }

    /// <summary>Creates a new user. Only a Super Admin should call this. Kiosk users do not need a forced password change.</summary>
    public async Task<OperationResult> CreateUserAsync(
        string username, string fullName, string password, UserRole role,
        string actorUsername, int? actorUserId)
    {
        var error = Validate(username, fullName, password);
        if (error is not null)
            return new OperationResult(false, error);

        var name = Normalize(username);
        if (await _db.AppUsers.AnyAsync(u => u.Username == name))
            return new OperationResult(false, "This username is already taken.");

        var (hash, salt) = _hasher.Hash(password);
        var now = _clock.Now;

        _db.AppUsers.Add(new AppUser
        {
            Username = name,
            FullName = fullName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            IsActive = true,
            MustChangePassword = role != UserRole.Kiosk,
            CreatedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = now,
            UserId = actorUserId,
            Username = actorUsername,
            Action = "UserCreated",
            Details = $"{name}, {role}"
        });

        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        var name = Normalize(username ?? string.Empty);
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Username == name);
        var now = _clock.Now;

        if (user is null)
            return new LoginResult(LoginStatus.InvalidCredentials);

        if (!user.IsActive)
            return new LoginResult(LoginStatus.Inactive);

        if (user.LockedUntil is DateTime until && until > now)
            return new LoginResult(LoginStatus.LockedOut, LockedUntil: until);

        if (!_hasher.Verify(password ?? string.Empty, user.PasswordHash, user.PasswordSalt))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = now + LockoutDuration;
                user.FailedLoginAttempts = 0;
                AddAudit(user, "AccountLocked", $"{MaxFailedAttempts} failed attempts");
                await _db.SaveChangesAsync();
                return new LoginResult(LoginStatus.LockedOut, LockedUntil: user.LockedUntil);
            }

            AddAudit(user, "LoginFailed", null);
            await _db.SaveChangesAsync();
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        AddAudit(user, "Login", null);
        await _db.SaveChangesAsync();

        return new LoginResult(LoginStatus.Success, user);
    }

    public async Task<OperationResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _db.AppUsers.FindAsync(userId);
        if (user is null)
            return new OperationResult(false, "User not found.");

        if (!_hasher.Verify(currentPassword ?? string.Empty, user.PasswordHash, user.PasswordSalt))
            return new OperationResult(false, "Current password is incorrect.");

        if (newPassword is null || newPassword.Length < MinPasswordLength)
            return new OperationResult(false, $"New password must be at least {MinPasswordLength} characters.");

        var (hash, salt) = _hasher.Hash(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = false;
        AddAudit(user, "PasswordChanged", null);
        await _db.SaveChangesAsync();

        return new OperationResult(true);
    }

    private static string Normalize(string username) => username.Trim().ToLowerInvariant();

    private static string? Validate(string? username, string? fullName, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
            return "Username must be at least 3 characters.";
        if (string.IsNullOrWhiteSpace(fullName))
            return "Full name is required.";
        if (password is null || password.Length < MinPasswordLength)
            return $"Password must be at least {MinPasswordLength} characters.";
        return null;
    }

    private void AddAudit(AppUser user, string action, string? details) =>
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = _clock.Now,
            UserId = user.Id,
            Username = user.Username,
            Action = action,
            Details = details
        });
}
