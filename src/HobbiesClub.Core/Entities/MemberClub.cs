namespace HobbiesClub.Core.Entities;

public class MemberClub
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public DateTime JoinedAt { get; set; }
}
