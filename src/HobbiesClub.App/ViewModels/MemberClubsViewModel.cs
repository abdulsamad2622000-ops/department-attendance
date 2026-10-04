using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Core.Enums;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public class ClubCard
{
    public int ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public Brush StatusBrush { get; init; } = Brushes.Gray;
    public string MonthText { get; init; } = string.Empty;
    public string LastText { get; init; } = string.Empty;
}

public class JoinClubCard
{
    public int ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>Shown after verification: the member's clubs as cards, plus clubs they can join. Returns to the kiosk after 60 seconds idle.</summary>
public partial class MemberClubsViewModel : ObservableObject
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    private readonly ShellViewModel _shell;
    private readonly int _memberId;
    private readonly DispatcherTimer _idle;

    public MemberClubsViewModel(ShellViewModel shell, int memberId)
    {
        _shell = shell;
        _memberId = memberId;
        _idle = new DispatcherTimer { Interval = IdleTimeout };
        _idle.Tick += (_, _) => Done();
        _idle.Start();
        _ = LoadAsync();
    }

    public ObservableCollection<ClubCard> Cards { get; } = new();
    public ObservableCollection<JoinClubCard> JoinCards { get; } = new();

    [ObservableProperty] private string _initial = string.Empty;
    [ObservableProperty] private ImageSource? _photo;
    [ObservableProperty] private bool _hasPhoto;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _memberCode = string.Empty;
    [ObservableProperty] private string _badgeText = string.Empty;
    [ObservableProperty] private Brush _badgeBrush = Brushes.Gray;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _hasJoinCards;
    [ObservableProperty] private string _rank = string.Empty;
    [ObservableProperty] private string _rollRegNo = string.Empty;
    [ObservableProperty] private string _unit = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _clubsText = string.Empty;
    [ObservableProperty] private bool _isDetailsOpen;
    [ObservableProperty] private IReadOnlyList<int> _fingerIndexes = Array.Empty<int>();
    [ObservableProperty] private string _fingersText = string.Empty;

    partial void OnIsDetailsOpenChanged(bool value) => ResetIdle();

    public static Brush BadgeBrushFor(MemberBadge badge) => badge switch
    {
        MemberBadge.Pro => new SolidColorBrush(Color.FromRgb(0xC9, 0x97, 0x00)),
        MemberBadge.Medium => new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
        _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
    };

    private void ResetIdle()
    {
        _idle.Stop();
        _idle.Start();
    }

    private async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attendance = scope.ServiceProvider.GetRequiredService<AttendanceService>();
        var members = scope.ServiceProvider.GetRequiredService<MemberService>();

        var mem = await db.Members.AsNoTracking().FirstAsync(x => x.Id == _memberId);
        FullName = mem.FullName;
        Photo = PhotoHelper.ToImage(mem.PhotoJpeg);
        HasPhoto = Photo is not null;
        Initial = mem.PhotoJpeg is { Length: > 0 } ? string.Empty : mem.FullName.Length > 0 ? mem.FullName.Substring(0, 1).ToUpperInvariant() : "?";
        MemberCode = $"Member ID {mem.MemberCode}";
        BadgeText = mem.Badge.ToString().ToUpperInvariant();
        BadgeBrush = BadgeBrushFor(mem.Badge);
        Rank = string.IsNullOrWhiteSpace(mem.Rank) ? "-" : mem.Rank;
        RollRegNo = string.IsNullOrWhiteSpace(mem.RollRegNo) ? "-" : mem.RollRegNo;
        Unit = string.IsNullOrWhiteSpace(mem.Unit) ? "-" : mem.Unit;
        Phone = string.IsNullOrWhiteSpace(mem.Phone) ? "-" : mem.Phone;

        Cards.Clear();
        var clubs = await attendance.GetMemberClubsAsync(_memberId);
        foreach (var c in clubs)
        {
            var s = await attendance.GetClubStatsAsync(_memberId, c.Id);
            if (s is null) continue;

            Cards.Add(new ClubCard
            {
                ClubId = s.ClubId,
                Name = s.ClubName,
                StatusText = s.IsInside ? "INSIDE NOW" : "NOT INSIDE",
                StatusBrush = s.IsInside ? Brushes.SeaGreen : Brushes.Gray,
                MonthText = $"{s.MonthVisits} visit(s) this month",
                LastText = s.LastEntry is DateTime d ? $"Last entry: {d:dd MMM, hh:mm tt}" : "No entries yet"
            });
        }

        ClubsText = string.Join(", ", Cards.Select(c => c.Name));
        var fingerIdx = await db.BiometricTemplates.AsNoTracking()
            .Where(t => t.MemberId == _memberId && t.Type == BiometricType.Fingerprint && t.FingerIndex != null)
            .Select(t => t.FingerIndex!.Value).ToListAsync();
        FingerIndexes = fingerIdx;
        FingersText = fingerIdx.Count == 0 ? "No fingerprints registered" : string.Join(", ", fingerIdx.Order().Select(HobbiesClub.Core.FingerNames.Name));
        JoinCards.Clear();
        foreach (var c in await members.GetJoinableClubsAsync(_memberId))
            JoinCards.Add(new JoinClubCard { ClubId = c.Id, Name = c.Name });
        HasJoinCards = JoinCards.Count > 0;
    }

    [RelayCommand]
    private void OpenClub(ClubCard card)
    {
        _idle.Stop();
        _shell.NavigateTo(new ClubDetailViewModel(_shell, _memberId, card.ClubId));
    }

    [RelayCommand]
    private async Task JoinClubAsync(JoinClubCard card)
    {
        ResetIdle();
        Message = string.Empty;

        var answer = MessageBox.Show(
            $"Join {card.Name}? You will be able to mark attendance there from now on.",
            "Join club", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        using (var scope = App.Services.CreateScope())
        {
            var members = scope.ServiceProvider.GetRequiredService<MemberService>();
            var r = await members.JoinClubAsync(_memberId, card.ClubId);
            if (!r.Succeeded)
            {
                Message = r.Error ?? "Could not join the club.";
                return;
            }
        }

        await LoadAsync();
        ResetIdle();
    }

    [RelayCommand]
    private void Done()
    {
        _idle.Stop();
        _shell.NavigateTo(new KioskViewModel(_shell, string.Empty));
    }
}





