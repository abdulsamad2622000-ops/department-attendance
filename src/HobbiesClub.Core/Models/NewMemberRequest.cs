using HobbiesClub.Core.Enums;

namespace HobbiesClub.Core.Models;

public record NewMemberRequest(
    string FullName,
    string Phone,
    IReadOnlyList<int> ClubIds,
    string Pin,
    byte[]? PhotoJpeg,
    RegistrationSource Source,
    string Rank = "",
    string RollRegNo = "",
    string Unit = "");

