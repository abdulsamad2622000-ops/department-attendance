namespace HobbiesClub.Core.Enums;

public enum UserRole { SuperAdmin = 1, Kiosk = 2 }
public enum MemberStatus { Active = 1, Terminated = 2 }
public enum RegistrationSource { Admin = 1, SelfService = 2 }
public enum BiometricType { Fingerprint = 1, Face = 2 }
public enum VerificationMethod { Face = 1, Fingerprint = 2, Pin = 3 }
public enum AttendanceType { CheckIn = 1, CheckOut = 2 }
public enum BackupKind { Automatic = 1, Manual = 2, PreRestore = 3 }
public enum MemberBadge { Basic = 0, Medium = 1, Pro = 2 }
