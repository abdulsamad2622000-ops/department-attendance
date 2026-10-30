using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.Helpers;

/// <summary>Separate password for the Branding page. Only hashes are stored (Branding\lock.json).</summary>
public static class BrandingLock
{
    public const int MinLength = 6;
    public const int MaxAttempts = 5;
    public static readonly TimeSpan LockTime = TimeSpan.FromMinutes(5);

    private sealed class Data
    {
        public string? Hash { get; set; }
        public string? Salt { get; set; }
        public string? RecoveryHash { get; set; }
        public string? RecoverySalt { get; set; }
        public int Failed { get; set; }
        public DateTime? LockedUntil { get; set; }
    }

    private static string FilePath => Path.Combine(BrandingSource.Folder, "lock.json");
    private static IPasswordHasher Hasher => HobbiesClub.App.App.Services.GetRequiredService<IPasswordHasher>();

    private static Data Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data(); }
        catch { /* treat as empty */ }
        return new Data();
    }

    private static void Save(Data d)
    {
        Directory.CreateDirectory(BrandingSource.Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(d));
    }

    public static bool IsSet => !string.IsNullOrEmpty(Load().Hash);

    /// <summary>Returns null when the password is right, otherwise a message.</summary>
    public static string? Verify(string password)
    {
        var d = Load();
        if (string.IsNullOrEmpty(d.Hash) || string.IsNullOrEmpty(d.Salt)) return "No Branding password is set yet.";
        if (d.LockedUntil is DateTime until && until > DateTime.Now)
            return $"Too many wrong attempts. Try again after {until:hh:mm tt}.";

        if (Hasher.Verify(password ?? string.Empty, d.Hash, d.Salt))
        {
            d.Failed = 0; d.LockedUntil = null; Save(d);
            return null;
        }

        d.Failed++;
        if (d.Failed >= MaxAttempts)
        {
            d.Failed = 0; d.LockedUntil = DateTime.Now + LockTime; Save(d);
            Audit("BrandingLocked", $"{MaxAttempts} wrong passwords");
            return $"Too many wrong attempts. Branding is locked for {LockTime.TotalMinutes:0} minutes.";
        }
        Save(d);
        Audit("BrandingPasswordWrong", null);
        return $"Wrong password. {MaxAttempts - d.Failed} attempt(s) left.";
    }

    /// <summary>Sets a new password and returns a new recovery code (shown once).</summary>
    public static string SetPassword(string password)
    {
        var (hash, salt) = Hasher.Hash(password);
        var code = NewRecoveryCode();
        var (rh, rs) = Hasher.Hash(NormalizeCode(code));
        Save(new Data { Hash = hash, Salt = salt, RecoveryHash = rh, RecoverySalt = rs });
        return code;
    }

    public static bool VerifyRecovery(string code)
    {
        var d = Load();
        return !string.IsNullOrEmpty(d.RecoveryHash) && !string.IsNullOrEmpty(d.RecoverySalt)
            && Hasher.Verify(NormalizeCode(code), d.RecoveryHash, d.RecoverySalt);
    }

    public static async Task<bool> VerifySuperAdminAsync(string username, string password)
    {
        using var scope = HobbiesClub.App.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var name = (username ?? string.Empty).Trim().ToLowerInvariant();
        var user = await db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == name && u.Role == UserRole.SuperAdmin && u.IsActive);
        return user is not null && Hasher.Verify(password ?? string.Empty, user.PasswordHash, user.PasswordSalt);
    }

    public static void Audit(string action, string? details, string username = "branding")
    {
        try
        {
            using var scope = HobbiesClub.App.App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogs.Add(new HobbiesClub.Core.Entities.AuditLog
            {
                Timestamp = DateTime.Now, Username = username, Action = action, Details = details
            });
            db.SaveChanges();
        }
        catch { /* never block the page */ }
    }

    private static string NormalizeCode(string code) =>
        new string((code ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string NewRecoveryCode()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        string Part() => new string(Enumerable.Range(0, 4).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
        return $"BRND-{Part()}-{Part()}";
    }
}
