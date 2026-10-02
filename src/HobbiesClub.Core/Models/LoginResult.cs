using HobbiesClub.Core.Entities;

namespace HobbiesClub.Core.Models;

public enum LoginStatus { Success, InvalidCredentials, LockedOut, Inactive }

public record LoginResult(LoginStatus Status, AppUser? User = null, DateTime? LockedUntil = null);
