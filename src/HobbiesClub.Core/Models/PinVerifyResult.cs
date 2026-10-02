using HobbiesClub.Core.Entities;

namespace HobbiesClub.Core.Models;

public enum PinVerifyStatus { Success, InvalidCredentials, Locked, Terminated, NotFound }

public record PinVerifyResult(PinVerifyStatus Status, Member? Member = null, DateTime? LockedUntil = null);
