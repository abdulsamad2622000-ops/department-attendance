using HobbiesClub.Core.Entities;

namespace HobbiesClub.Core.Models;

public record MemberResult(bool Succeeded, string? Error = null, Member? Member = null);
