using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Services;

public class BiometricService
{
    public const int FingersPerMember = 2;

    private readonly AppDbContext _db;
    private readonly IFingerprintService _fingerprint;
    private readonly IFaceService _face;
    private readonly IClock _clock;

    public BiometricService(AppDbContext db, IFingerprintService fingerprint, IFaceService face, IClock clock)
    {
        _db = db;
        _fingerprint = fingerprint;
        _face = face;
        _clock = clock;
    }

    public async Task<OperationResult> EnrollFingerprintAsync(int memberId, int fingerIndex, byte[] template)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member is null || member.Status != MemberStatus.Active)
            return new OperationResult(false, "Member not found or not active.");

        if (template is null || template.Length == 0)
            return new OperationResult(false, "Scan failed.");

        var existing = await _db.BiometricTemplates
            .Where(t => t.Type == BiometricType.Fingerprint && t.Member.Status == MemberStatus.Active)
            .ToListAsync();

        if (existing.Any(t => t.MemberId != memberId && _fingerprint.IsMatch(template, t.TemplateData)))
            return new OperationResult(false, "This finger is already registered to another member.");

        var mine = existing.Where(t => t.MemberId == memberId).ToList();
        var sameFinger = mine.FirstOrDefault(t => t.FingerIndex == fingerIndex);

        if (sameFinger is null && mine.Count >= FingersPerMember)
            return new OperationResult(false, $"Only {FingersPerMember} fingers can be enrolled.");

        if (sameFinger is not null)
            _db.BiometricTemplates.Remove(sameFinger);

        _db.BiometricTemplates.Add(new BiometricTemplate
        {
            MemberId = memberId,
            Type = BiometricType.Fingerprint,
            FingerIndex = fingerIndex,
            TemplateData = template,
            CreatedAt = _clock.Now
        });

        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    public async Task<OperationResult> EnrollFaceAsync(int memberId, byte[] template)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member is null || member.Status != MemberStatus.Active)
            return new OperationResult(false, "Member not found or not active.");

        if (template is null || template.Length == 0)
            return new OperationResult(false, "Face capture failed.");

        var existing = await _db.BiometricTemplates
            .Where(t => t.Type == BiometricType.Face && t.Member.Status == MemberStatus.Active)
            .ToListAsync();

        if (existing.Any(t => t.MemberId != memberId && _face.IsMatch(template, t.TemplateData)))
            return new OperationResult(false, "This face is already registered to another member.");

        var old = existing.Where(t => t.MemberId == memberId);
        _db.BiometricTemplates.RemoveRange(old);

        _db.BiometricTemplates.Add(new BiometricTemplate
        {
            MemberId = memberId,
            Type = BiometricType.Face,
            TemplateData = template,
            CreatedAt = _clock.Now
        });

        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    /// <summary>Saves the member and all three biometrics in one transaction. Any failure rolls everything back.</summary>
    public async Task<MemberResult> RegisterMemberAsync(
        MemberService members, NewMemberRequest request,
        byte[] face, byte[] finger1, byte[] finger2,
        string actorUsername = "kiosk", int? actorUserId = null)
    {
        if (face is null || face.Length == 0)
            return new MemberResult(false, "Face capture failed.");
        if (finger1 is null || finger1.Length == 0 || finger2 is null || finger2.Length == 0)
            return new MemberResult(false, "Fingerprint scan failed.");
        if (_fingerprint.IsMatch(finger1, finger2))
            return new MemberResult(false, "Please use two different fingers.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        async Task<MemberResult> FailAsync(string? error)
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            return new MemberResult(false, error ?? "Registration failed.");
        }

        try
        {
            var added = await members.AddMemberAsync(request, actorUsername, actorUserId);
            if (!added.Succeeded)
                return await FailAsync(added.Error);

            var id = added.Member!.Id;

            var r = face is { Length: > 0 } ? await EnrollFaceAsync(id, face) : new OperationResult(true);
            if (!r.Succeeded) return await FailAsync(r.Error);

            r = await EnrollFingerprintAsync(id, 0, finger1);
            if (!r.Succeeded) return await FailAsync(r.Error);

            r = await EnrollFingerprintAsync(id, 1, finger2);
            if (!r.Succeeded) return await FailAsync(r.Error);

            await tx.CommitAsync();
            return added;
        }
        catch
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public const int MaxFingers = 10;

    /// <summary>Replaces all fingerprints of a member with the given fingers (position 0-9 to template).</summary>
    public async Task<OperationResult> SetFingerprintsAsync(int memberId, IReadOnlyDictionary<int, byte[]> fingers)
    {
        if (fingers.Count == 0) return new OperationResult(false, "Register at least one finger.");
        if (fingers.Count > MaxFingers || fingers.Keys.Any(k => k is < 0 or > 9)) return new OperationResult(false, "Invalid finger.");
        if (fingers.Values.Any(v => v is not { Length: > 0 })) return new OperationResult(false, "Fingerprint scan failed.");

        var list = fingers.ToList();
        for (var i = 0; i < list.Count; i++)
            for (var j = i + 1; j < list.Count; j++)
                if (_fingerprint.IsMatch(list[i].Value, list[j].Value))
                    return new OperationResult(false, $"{HobbiesClub.Core.FingerNames.Name(list[i].Key)} and {HobbiesClub.Core.FingerNames.Name(list[j].Key)} look like the same finger.");

        var member = await _db.Members.FindAsync(memberId);
        if (member is null || member.Status != MemberStatus.Active)
            return new OperationResult(false, "Member not found or not active.");

        var others = await _db.BiometricTemplates
            .Where(t => t.Type == BiometricType.Fingerprint && t.MemberId != memberId && t.Member.Status == MemberStatus.Active)
            .ToListAsync();
        foreach (var kv in list)
            if (others.Any(o => _fingerprint.IsMatch(kv.Value, o.TemplateData)))
                return new OperationResult(false, $"{HobbiesClub.Core.FingerNames.Name(kv.Key)} is already registered to another member.");

        var mine = await _db.BiometricTemplates
            .Where(t => t.MemberId == memberId && t.Type == BiometricType.Fingerprint)
            .ToListAsync();
        _db.BiometricTemplates.RemoveRange(mine);

        foreach (var kv in list)
            _db.BiometricTemplates.Add(new BiometricTemplate
            {
                MemberId = memberId,
                Type = BiometricType.Fingerprint,
                FingerIndex = kv.Key,
                TemplateData = kv.Value,
                CreatedAt = _clock.Now
            });

        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    /// <summary>Saves a new member with a face and any number of fingers in one transaction.</summary>
    public async Task<MemberResult> RegisterMemberAsync(
        MemberService members, NewMemberRequest request, byte[]? face, IReadOnlyDictionary<int, byte[]> fingers,
        string actorUsername = "kiosk", int? actorUserId = null)
    {
        
        

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var added = await members.AddMemberAsync(request, actorUsername, actorUserId);
            if (!added.Succeeded) { await tx.RollbackAsync(); _db.ChangeTracker.Clear(); return new MemberResult(false, added.Error ?? "Registration failed."); }

            var id = added.Member!.Id;
            var r = face is { Length: > 0 } ? await EnrollFaceAsync(id, face) : new OperationResult(true);
            if (r.Succeeded && fingers.Count > 0) r = await SetFingerprintsAsync(id, fingers);
            if (!r.Succeeded) { await tx.RollbackAsync(); _db.ChangeTracker.Clear(); return new MemberResult(false, r.Error ?? "Registration failed."); }

            await tx.CommitAsync();
            return added;
        }
        catch
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<Member?> IdentifyFingerprintAsync(byte[] captured)
    {
        var templates = await _db.BiometricTemplates
            .Include(t => t.Member)
            .Where(t => t.Type == BiometricType.Fingerprint && t.Member.Status == MemberStatus.Active)
            .ToListAsync();

        return templates.FirstOrDefault(t => _fingerprint.IsMatch(captured, t.TemplateData))?.Member;
    }

    public async Task<Member?> IdentifyFaceAsync(byte[] captured)
    {
        var templates = await _db.BiometricTemplates
            .Include(t => t.Member)
            .Where(t => t.Type == BiometricType.Face && t.Member.Status == MemberStatus.Active)
            .ToListAsync();

        return templates.FirstOrDefault(t => _face.IsMatch(captured, t.TemplateData))?.Member;
    }

    public bool IsSameFinger(byte[] a, byte[] b) => _fingerprint.IsMatch(a, b);

    public async Task<int> CountTemplatesAsync(int memberId, BiometricType type) =>
        await _db.BiometricTemplates.CountAsync(t => t.MemberId == memberId && t.Type == type);
}






