using System.Text.RegularExpressions;
using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Services;

public class MemberService
{
    public const int FirstMemberCode = 1001;
    public const int PinLength = 4;
    public const int MaxPinAttempts = 5;
    public static readonly TimeSpan PinLockDuration = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public MemberService(AppDbContext db, IPasswordHasher hasher, IClock clock)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<MemberResult> AddMemberAsync(NewMemberRequest request, string actorUsername = "kiosk", int? actorUserId = null)
    {
        var name = (request.FullName ?? string.Empty).Trim();
        var phone = NormalizePhone(request.Phone);
        var clubIds = (request.ClubIds ?? Array.Empty<int>()).Distinct().ToList();

        if (name.Length == 0)
            return Fail("Full name is required.");

        if (phone.Length < 10)
            return Fail("Phone number is not valid.");

        if (clubIds.Count == 0)
            return Fail("Select at least one club.");

        var validClubCount = await _db.Clubs.CountAsync(c => clubIds.Contains(c.Id) && c.IsActive);
        if (validClubCount != clubIds.Count)
            return Fail("The selected club is not valid.");

        if (!IsValidPin(request.Pin))
            return Fail($"PIN must be exactly {PinLength} digits.");

        if (await _db.Members.AnyAsync(m => m.Phone == phone))
            return Fail("This phone number is already registered.");

        foreach (var existing in await _db.Members.ToListAsync())
        {
            if (_hasher.Verify(request.Pin, existing.PinHash, existing.PinSalt))
                return Fail("This PIN is already taken, choose another.");
        }

        var maxCode = await _db.Members.MaxAsync(m => (int?)m.MemberCode) ?? (FirstMemberCode - 1);
        var (pinHash, pinSalt) = _hasher.Hash(request.Pin);
        var now = _clock.Now;

        var member = new Member
        {
            MemberCode = maxCode + 1,
            FullName = name,
            Phone = phone,
            Rank = (request.Rank ?? string.Empty).Trim(),
            RollRegNo = (request.RollRegNo ?? string.Empty).Trim(),
            Unit = (request.Unit ?? string.Empty).Trim(),
            PhotoJpeg = request.PhotoJpeg,
            PinHash = pinHash,
            PinSalt = pinSalt,
            Status = MemberStatus.Active,
            RegisteredVia = request.Source,
            RegisteredAt = now
        };

        foreach (var clubId in clubIds)
            _db.MemberClubs.Add(new MemberClub { Member = member, ClubId = clubId, JoinedAt = now });

        _db.Members.Add(member);
        AddAudit(actorUserId, actorUsername, "MemberAdded", $"Code {member.MemberCode}, {request.Source}");

        await _db.SaveChangesAsync();
        return new MemberResult(true, Member: member);
    }

    public async Task<PinVerifyResult> VerifyPinAsync(int memberCode, string pin)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.MemberCode == memberCode);
        if (member is null)
            return new PinVerifyResult(PinVerifyStatus.NotFound);

        if (member.Status == MemberStatus.Terminated)
            return new PinVerifyResult(PinVerifyStatus.Terminated);

        var now = _clock.Now;

        if (member.PinLockedUntil is DateTime until)
        {
            if (until > now)
                return new PinVerifyResult(PinVerifyStatus.Locked, LockedUntil: until);

            member.PinLockedUntil = null;
            member.PinFailedAttempts = 0;
        }

        if (!_hasher.Verify(pin ?? string.Empty, member.PinHash, member.PinSalt))
        {
            member.PinFailedAttempts++;

            if (member.PinFailedAttempts >= MaxPinAttempts)
            {
                member.PinLockedUntil = now + PinLockDuration;
                member.PinFailedAttempts = 0;
                AddAudit(null, "kiosk", "PinLocked", $"Code {member.MemberCode}");
                await _db.SaveChangesAsync();
                return new PinVerifyResult(PinVerifyStatus.Locked, LockedUntil: member.PinLockedUntil);
            }

            await _db.SaveChangesAsync();
            return new PinVerifyResult(PinVerifyStatus.InvalidCredentials);
        }

        member.PinFailedAttempts = 0;
        member.PinLockedUntil = null;
        await _db.SaveChangesAsync();
        return new PinVerifyResult(PinVerifyStatus.Success, member);
    }

    public const int MaxKioskPinAttempts = 5;
    private static int _kioskFailed;
    private static DateTime? _kioskLockedUntil;

    /// <summary>PIN-only login on the kiosk. PINs are unique, so the PIN identifies the member.</summary>
    public async Task<PinVerifyResult> VerifyPinOnlyAsync(string pin)
    {
        var now = _clock.Now;

        if (_kioskLockedUntil is DateTime until)
        {
            if (until > now)
                return new PinVerifyResult(PinVerifyStatus.Locked, LockedUntil: until);

            _kioskLockedUntil = null;
            _kioskFailed = 0;
        }

        if (IsValidPin(pin))
        {
            var members = await _db.Members.ToListAsync();
            foreach (var m in members)
            {
                if (!_hasher.Verify(pin, m.PinHash, m.PinSalt))
                    continue;

                if (m.Status == MemberStatus.Terminated)
                    return new PinVerifyResult(PinVerifyStatus.Terminated);

                _kioskFailed = 0;
                return new PinVerifyResult(PinVerifyStatus.Success, m);
            }
        }

        _kioskFailed++;
        if (_kioskFailed >= MaxKioskPinAttempts)
        {
            _kioskLockedUntil = now + PinLockDuration;
            _kioskFailed = 0;
            AddAudit(null, "kiosk", "KioskPinLocked", null);
            await _db.SaveChangesAsync();
            return new PinVerifyResult(PinVerifyStatus.Locked, LockedUntil: _kioskLockedUntil);
        }

        return new PinVerifyResult(PinVerifyStatus.InvalidCredentials);
    }

    public async Task<OperationResult> TerminateAsync(int memberId, string reason, string actorUsername, int? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return new OperationResult(false, "A reason is required to terminate a member.");

        var member = await _db.Members.FindAsync(memberId);
        if (member is null)
            return new OperationResult(false, "Member not found.");

        if (member.Status == MemberStatus.Terminated)
            return new OperationResult(false, "Member is already terminated.");

        member.Status = MemberStatus.Terminated;
        member.TerminatedAt = _clock.Now;
        member.TerminationReason = reason.Trim();

        var templates = _db.BiometricTemplates.Where(t => t.MemberId == memberId);
        _db.BiometricTemplates.RemoveRange(templates);

        AddAudit(actorUserId, actorUsername, "MemberTerminated", $"Code {member.MemberCode}: {member.TerminationReason}");
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    public async Task<OperationResult> ReactivateAsync(int memberId, string actorUsername, int? actorUserId)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member is null)
            return new OperationResult(false, "Member not found.");

        if (member.Status == MemberStatus.Active)
            return new OperationResult(false, "Member is already active.");

        member.Status = MemberStatus.Active;
        member.TerminatedAt = null;
        member.TerminationReason = null;
        member.PinFailedAttempts = 0;
        member.PinLockedUntil = null;

        AddAudit(actorUserId, actorUsername, "MemberReactivated", $"Code {member.MemberCode}");
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }

    public async Task<bool> IsPhoneTakenAsync(string? phone)
    {
        var p = NormalizePhone(phone);
        return await _db.Members.AnyAsync(m => m.Phone == p);
    }

    public async Task<bool> IsPinTakenAsync(string pin)
    {
        foreach (var m in await _db.Members.ToListAsync())
            if (_hasher.Verify(pin, m.PinHash, m.PinSalt))
                return true;
        return false;
    }

    /// <summary>Active clubs the member has not joined yet (for the "Join this club" card).</summary>
    public async Task<List<Club>> GetJoinableClubsAsync(int memberId) =>
        await _db.Clubs
            .Where(c => c.IsActive && !_db.MemberClubs.Any(mc => mc.MemberId == memberId && mc.ClubId == c.Id))
            .OrderBy(c => c.Id)
            .ToListAsync();

    /// <summary>Adds an active member to another club. Self-service from the kiosk or by a Super Admin.</summary>
    public async Task<OperationResult> JoinClubAsync(int memberId, int clubId, string actorUsername = "kiosk", int? actorUserId = null)
    {
        var member = await _db.Members.FindAsync(memberId);
        if (member is null)
            return new OperationResult(false, "Member not found.");
        if (member.Status != MemberStatus.Active)
            return new OperationResult(false, "This membership is not active.");

        var club = await _db.Clubs.FindAsync(clubId);
        if (club is null || !club.IsActive)
            return new OperationResult(false, "Club not found.");

        if (await _db.MemberClubs.AnyAsync(mc => mc.MemberId == memberId && mc.ClubId == clubId))
            return new OperationResult(false, "You are already a member of this club.");

        var now = _clock.Now;
        _db.MemberClubs.Add(new MemberClub { MemberId = memberId, ClubId = clubId, JoinedAt = now });
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = now,
            UserId = actorUserId,
            Username = actorUsername,
            Action = "ClubJoined",
            Details = $"Member {member.MemberCode} joined {club.Name}"
        });
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }
    public static bool IsValidPin(string? pin) =>
        pin is not null && Regex.IsMatch(pin, $"^[0-9]{{{PinLength}}}$");

    public static string NormalizePhone(string? phone) =>
        new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

    private static MemberResult Fail(string error) => new(false, error);

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




