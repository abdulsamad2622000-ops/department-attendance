using System.Windows.Media;

namespace HobbiesClub.App.ViewModels;

public class VerifyInfo
{
    public string Initial { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public ImageSource? Photo { get; init; }
    public bool HasPhoto => Photo is not null;
    public string MemberCode { get; init; } = string.Empty;
    public string BadgeText { get; init; } = string.Empty;
    public Brush BadgeBrush { get; init; } = Brushes.Gray;
    public string Rank { get; init; } = string.Empty;
    public string RollRegNo { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Clubs { get; init; } = string.Empty;
    public string PreviousText { get; init; } = string.Empty;
    public string PreviousType { get; init; } = string.Empty;
    public Brush PreviousBrush { get; init; } = Brushes.Gray;
    public bool HasPrevious => PreviousText.Length > 0;
    public string CurrentText { get; init; } = string.Empty;
    public string MethodText { get; init; } = string.Empty;
    public string PreviousMethodText { get; init; } = string.Empty;
    public string CurrentTime { get; init; } = string.Empty;
    public string AutoNote { get; init; } = string.Empty;
    public bool HasAutoNote => AutoNote.Length > 0;
    public Brush CurrentBrush { get; init; } = Brushes.SeaGreen;
    public string ClubName { get; init; } = string.Empty;
    public int LogId { get; init; }
}






