using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Services;

/// <summary>Super Admin actions on staff accounts: edit, reset password, unlock.</summary>
public class UserAdminService
{
    public const int MinPasswordLength = 8;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public UserAdminService(AppDbContext db, IPasswordHasher hasher, IClock clock)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<OperationResult> UpdateUserAsync(
        int userId, string fullName, UserRole role, bool isActive, string actorUsername, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return new OperationResult(false, "Full name is required.");

        var user = await _db.AppUsers.FindAsync(userId);
        if (user is null)
            return new OperationResult(false, "User not found.");

        if (userId == actorUserId && (!isActive || role != UserRole.SuperAdmin))
            return new OperationResult(false, "You cannot deactivate or demote your own account.");

        var wasActiveSuper = user.IsActive && user.Role == UserRole.SuperAdmin;
        var willBeActiveSuper = isActive && role == UserRole.SuperAdmin;
        if (wasActiveSuper && !willBeActiveSuper)
        {
            var others = await _db.AppUsers.CountAsync(u => u.Id != userId && u.IsActive && u.Role == UserRole.SuperAdmin);
            if (others == 0)
                return new OperationResult(false, "At least one active Super Admin must remain.");
        }

        user.FullName = fullName.Trim();
        user.Role = role;
        user.IsActive = isActive;

        AddAudit(actorUserId, actorUsername, "UserEdited", $"{user.Username}: {role}, {(isActive ? "active" : "inactive")}");
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    public async Task<OperationResult> ResetPasswordAsync(int userId, string newPassword, string actorUsername, int actorUserId)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinPasswordLength)
            return new OperationResult(false, $"Password must be at least {MinPasswordLength} characters.");

        var user = await _db.AppUsers.FindAsync(userId);
        if (user is null)
            return new OperationResult(false, "User not found.");

        var (hash, salt) = _hasher.Hash(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = true;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        AddAudit(actorUserId, actorUsername, "UserPasswordReset", user.Username);
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    public async Task<OperationResult> UnlockAsync(int userId, string actorUsername, int actorUserId)
    {
        var user = await _db.AppUsers.FindAsync(userId);
        if (user is null)
            return new OperationResult(false, "User not found.");

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        AddAudit(actorUserId, actorUsername, "UserUnlocked", user.Username);
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    private void AddAudit(int? userId, string username, string action, string? details) =>
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = _clock.Now,
            UserId = userId,
            Username = username,
            Action = action,
            Details = details
        });
}
