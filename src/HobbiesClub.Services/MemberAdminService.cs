using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Services;

public class MemberAdminService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly BiometricService _bio;

    public MemberAdminService(AppDbContext db, IPasswordHasher hasher, IClock clock, BiometricService bio)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
        _bio = bio;
    }

    /// <summary>Sets a new PIN for a member and clears any PIN lockout.</summary>
    public async Task<OperationResult> ResetPinAsync(int memberId, string newPin, string actorUsername, int? actorUserId)
    {
        if (!MemberService.IsValidPin(newPin))
            return new OperationResult(false, $"PIN must be exactly {MemberService.PinLength} digits.");

        var member = await _db.Members.FindAsync(memberId);
        if (member is null)
            return new OperationResult(false, "Member not found.");

        var others = await _db.Members.Where(m => m.Id != memberId).ToListAsync();
        if (others.Any(m => _hasher.Verify(newPin, m.PinHash, m.PinSalt)))
            return new OperationResult(false, "This PIN is already taken, choose another.");

        var (hash, salt) = _hasher.Hash(newPin);
        member.PinHash = hash;
        member.PinSalt = salt;
        member.PinFailedAttempts = 0;
        member.PinLockedUntil = null;

        AddAudit(actorUserId, actorUsername, "PinReset", $"Member {member.MemberCode}");
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    /// <summary>Removes a member from a club. A member must stay in at least one club.</summary>
    public async Task<OperationResult> LeaveClubAsync(int memberId, int clubId, string actorUsername, int? actorUserId)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member is null)
            return new OperationResult(false, "Member not found.");

        var links = await _db.MemberClubs.Where(mc => mc.MemberId == memberId).ToListAsync();
        var link = links.FirstOrDefault(mc => mc.ClubId == clubId);
        if (link is null)
            return new OperationResult(false, "Member is not in this club.");

        if (links.Count <= 1)
            return new OperationResult(false, "A member must stay in at least one club.");

        _db.MemberClubs.Remove(link);
        AddAudit(actorUserId, actorUsername, "ClubLeft", $"Member {member.MemberCode}, club {clubId}");
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    /// <summary>Replaces the member's face template and photo.</summary>
    public async Task<OperationResult> ReenrollFaceAsync(
        int memberId, byte[] template, byte[]? photoJpeg, string actorUsername, int? actorUserId)
    {
        var r = await _bio.EnrollFaceAsync(memberId, template);
        if (!r.Succeeded)
            return r;

        var member = await _db.Members.FindAsync(memberId);
        if (member is not null && photoJpeg is { Length: > 0 })
            member.PhotoJpeg = photoJpeg;

        AddAudit(actorUserId, actorUsername, "FaceReenrolled", $"Member {member?.MemberCode}");
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    /// <summary>Replaces both fingerprints in one transaction. On any problem nothing changes.</summary>
    public async Task<OperationResult> ReenrollFingersAsync(
        int memberId, byte[] finger1, byte[] finger2, string actorUsername, int? actorUserId)
    {
        if (finger1 is not { Length: > 0 } || finger2 is not { Length: > 0 })
            return new OperationResult(false, "Fingerprint scan failed.");
        if (_bio.IsSameFinger(finger1, finger2))
            return new OperationResult(false, "Please use two different fingers.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var r = await _bio.EnrollFingerprintAsync(memberId, 0, finger1);
        if (r.Succeeded)
            r = await _bio.EnrollFingerprintAsync(memberId, 1, finger2);

        if (!r.Succeeded)
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            return r;
        }

        var member = await _db.Members.FindAsync(memberId);
        AddAudit(actorUserId, actorUsername, "FingersReenrolled", $"Member {member?.MemberCode}");
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return new OperationResult(true);
    }

    /// <summary>Replaces all fingerprints of a member with the fingers given (position 0-9).</summary>
    public async Task<OperationResult> ReenrollFingersAsync(int memberId, IReadOnlyDictionary<int, byte[]> fingers, string actorUsername, int? actorUserId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var r = await _bio.SetFingerprintsAsync(memberId, fingers);
        if (!r.Succeeded)
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            return r;
        }

        var member = await _db.Members.FindAsync(memberId);
        var names = string.Join(", ", fingers.Keys.Order().Select(HobbiesClub.Core.FingerNames.Name));
        AddAudit(actorUserId, actorUsername, "FingersReenrolled", $"Member {member?.MemberCode}: {names}");
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return new OperationResult(true);
    }

    /// <summary>Edits the basic details of a member.</summary>
    public async Task<OperationResult> UpdateDetailsAsync(int memberId, string fullName, string phone, string rank, string rollRegNo, string unit, string actorUsername, int? actorUserId)
    {
        await MemberService.EnsureDetailFieldsNotUniqueAsync(_db);
        if (string.IsNullOrWhiteSpace(fullName))
            return new OperationResult(false, "Name is required.");
        if (string.IsNullOrWhiteSpace(phone))
            { }

        var member = await _db.Members.FindAsync(memberId);
        if (member is null)
            return new OperationResult(false, "Member not found.");

        member.FullName = fullName.Trim();
        member.Phone = phone.Trim();
        member.Rank = (rank ?? string.Empty).Trim();
        member.RollRegNo = (rollRegNo ?? string.Empty).Trim();
        member.Unit = (unit ?? string.Empty).Trim();

        AddAudit(actorUserId, actorUsername, "DetailsEdited", $"Member {member.MemberCode}");
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    /// <summary>Sets the member profile photo (JPEG) uploaded by an admin.</summary>
    public async Task<OperationResult> SetPhotoAsync(int memberId, byte[] jpeg, string actorUsername, int? actorUserId)
    {
        if (jpeg is not { Length: > 0 }) return new OperationResult(false, "No picture selected.");
        var member = await _db.Members.FindAsync(memberId);
        if (member is null) return new OperationResult(false, "Member not found.");
        member.PhotoJpeg = jpeg;
        AddAudit(actorUserId, actorUsername, "PhotoUploaded", $"Member {member.MemberCode}");
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    /// <summary>Permanently deletes members with their attendance, biometrics and club links, in one transaction.</summary>
    public async Task<OperationResult> DeleteMembersAsync(IReadOnlyList<int> ids, string actorUsername, int? actorUserId)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new OperationResult(false, "No member selected.");
        var members = await _db.Members.Where(m => list.Contains(m.Id)).Select(m => new { m.Id, m.MemberCode, m.FullName }).ToListAsync();
        if (members.Count == 0) return new OperationResult(false, "Member not found.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.AttendanceLogs.Where(a => list.Contains(a.MemberId)).ExecuteDeleteAsync();
            await _db.BiometricTemplates.Where(b => list.Contains(b.MemberId)).ExecuteDeleteAsync();
            await _db.MemberClubs.Where(mc => list.Contains(mc.MemberId)).ExecuteDeleteAsync();
            await _db.Members.Where(m => list.Contains(m.Id)).ExecuteDeleteAsync();

            var names = string.Join(", ", members.Take(20).Select(m => $"{m.FullName} ({m.MemberCode})"));
            AddAudit(actorUserId, actorUsername, "MembersDeleted", $"{members.Count} deleted: {names}" + (members.Count > 20 ? " ..." : string.Empty));
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            return new OperationResult(false, "Delete failed: " + (ex.InnerException?.Message ?? ex.Message));
        }
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






