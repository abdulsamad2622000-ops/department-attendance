using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.App.Views;
using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public partial class PinViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private string _code = string.Empty;
    private string _pin = string.Empty;
    private int _stage = 1;
    private int _memberId;
    private DispatcherTimer? _backTimer;

    public PinViewModel(ShellViewModel shell) => _shell = shell;

    private VerificationMethod _method = VerificationMethod.Pin;

    /// <summary>Member already identified by face or fingerprint: the keypad is skipped.</summary>
    public PinViewModel(ShellViewModel shell, int memberId, VerificationMethod method) : this(shell)
    {
        _memberId = memberId;
        _method = method;
        ShowKeypad = false;
        Prompt = string.Empty;
    }

    /// <summary>Continues the normal flow (club choice, attendance, verify card) for an identified member.</summary>
    public async Task StartForMemberAsync()
    {
        using var scope = App.Services.CreateScope();
        var attendance = scope.ServiceProvider.GetRequiredService<AttendanceService>();

        var clubs = await attendance.GetMemberClubsAsync(_memberId);
        if (clubs.Count == 0)
        {
            Finish(true, "You are not in any active club.");
            return;
        }

        if (clubs.Count == 1)
        {
            await MarkAsync(clubs[0]);
            return;
        }

        Clubs.Clear();
        foreach (var c in clubs) Clubs.Add(c);
        ShowKeypad = false;
        ShowClubs = true;
        Prompt = "Select your club";
        Display = string.Empty;
        Message = string.Empty;
    }

    public ObservableCollection<Club> Clubs { get; } = new();

    [ObservableProperty] private string _prompt = "Enter your PIN";
    [ObservableProperty] private string _display = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private bool _showKeypad = true;
    [ObservableProperty] private bool _showClubs;

    [RelayCommand]
    private void Digit(string d)
    {
        Message = string.Empty;
        if (_stage == 0)
        {
            if (_code.Length < 9) _code += d;
            Display = _code;
        }
        else
        {
            if (_pin.Length < MemberService.PinLength) _pin += d;
            Display = new string('*', _pin.Length);
        }
    }

    [RelayCommand]
    private void Backspace()
    {
        Message = string.Empty;
        if (_stage == 0)
        {
            if (_code.Length > 0) _code = _code[..^1];
            Display = _code;
        }
        else
        {
            if (_pin.Length > 0) _pin = _pin[..^1];
            Display = new string('*', _pin.Length);
        }
    }

    private void Reset(string? error)
    {
        _stage = 1;
        _code = string.Empty;
        _pin = string.Empty;
        Prompt = "Enter your PIN";
        Display = string.Empty;
        IsError = error is not null;
        Message = error ?? string.Empty;
    }

    [RelayCommand]
    private async Task OkAsync()
    {
        if (_stage == 0)
        {
            if (_code.Length == 0) { IsError = true; Message = "Enter your PIN."; return; }
            _stage = 1;
            Prompt = "Enter your PIN";
            Display = string.Empty;
            Message = string.Empty;
            return;
        }

        if (_pin.Length != MemberService.PinLength)
        {
            IsError = true;
            Message = $"PIN must be {MemberService.PinLength} digits.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<MemberService>();
        var attendance = scope.ServiceProvider.GetRequiredService<AttendanceService>();

        var verify = await members.VerifyPinOnlyAsync(_pin);
        switch (verify.Status)
        {
            case PinVerifyStatus.Success:
                break;
            case PinVerifyStatus.Locked:
                var mins = Math.Max(1, (int)Math.Ceiling(((verify.LockedUntil ?? DateTime.Now) - DateTime.Now).TotalMinutes));
                Reset($"PIN locked. Try again in {mins} minute(s).");
                return;
            case PinVerifyStatus.Terminated:
                Reset("This membership is not active.");
                return;
            default:
                Reset("Incorrect PIN.");
                return;
        }

        _memberId = verify.Member!.Id;

        var clubs = await attendance.GetMemberClubsAsync(_memberId);
        if (clubs.Count == 0)
        {
            Reset("You are not in any active club.");
            return;
        }

        if (clubs.Count == 1)
        {
            await MarkAsync(clubs[0]);
            return;
        }

        Clubs.Clear();
        foreach (var c in clubs) Clubs.Add(c);
        ShowKeypad = false;
        ShowClubs = true;
        Prompt = "Select your club";
        Display = string.Empty;
        Message = string.Empty;
    }

    [RelayCommand]
    private async Task SelectClubAsync(Club club) => await MarkAsync(club);

    private async Task MarkAsync(Club club)
    {
        using var scope = App.Services.CreateScope();
        var attendance = scope.ServiceProvider.GetRequiredService<AttendanceService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();


        var result = await attendance.MarkAsync(_memberId, club.Id, _method);

        if (result.Status == AttendanceStatus.CheckOutRequired)
        {
            var dlg = new MissedCheckOutDialog(result.MissedCheckIn!.Value, result.MissedClubName) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true || dlg.Result is null)
            {
                Back();
                return;
            }

            result = await attendance.MarkAsync(_memberId, club.Id, _method, dlg.Result, dlg.NoteText);
        }

        switch (result.Status)
        {
            case AttendanceStatus.Success:
                // The entry just before the one recorded now (includes a late check-out if one was entered).
                var previous = await db.AttendanceLogs
                    .Where(a => a.MemberId == _memberId)
                    .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
                    .Skip(1)
                    .Select(a => new { a.Type, a.Timestamp, a.IsManualEntry, a.Method, a.Note, ClubName = a.Club.Name })
                    .FirstOrDefaultAsync();
                var currentLogId = await db.AttendanceLogs
                    .Where(a => a.MemberId == _memberId && a.ClubId == club.Id)
                    .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
                    .Select(a => a.Id).FirstOrDefaultAsync();
                var mem = await db.Members.AsNoTracking().FirstAsync(x => x.Id == _memberId);
                var clubNames = await db.MemberClubs.Where(mc => mc.MemberId == _memberId)
                    .Select(mc => mc.Club.Name).ToListAsync();

                Brush badgeBrush = mem.Badge switch
                {
                    MemberBadge.Pro => new SolidColorBrush(Color.FromRgb(0xC9, 0x97, 0x00)),
                    MemberBadge.Medium => new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
                    _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
                };
                var checkedIn = result.Type == AttendanceType.CheckIn;

                var info = new VerifyInfo
                {
                    Initial = mem.PhotoJpeg is { Length: > 0 } ? string.Empty : mem.FullName.Length > 0 ? mem.FullName.Substring(0, 1).ToUpperInvariant() : "?",
                    FullName = mem.FullName,
                    Photo = PhotoHelper.ToImage(mem.PhotoJpeg),
                    MemberCode = mem.MemberCode.ToString(),
                    BadgeText = mem.Badge.ToString().ToUpperInvariant(),
                    BadgeBrush = badgeBrush,
                    Rank = mem.Rank,
                    RollRegNo = mem.RollRegNo,
                    Unit = mem.Unit,
                    Phone = mem.Phone,
                    Clubs = string.Join(", ", clubNames),
                    PreviousText = previous is null
                        ? string.Empty
                        : $"{previous.Timestamp:dd MMM yyyy, dddd, hh:mm tt} ({previous.ClubName})" + (previous.IsManualEntry ? " (time entered by member" + (string.IsNullOrWhiteSpace(previous.Note) || previous.Note == "Late check-out entered by member" ? "" : ": " + previous.Note) + ")" : ""),
                    PreviousType = previous is null ? string.Empty
                        : (previous.Type == AttendanceType.CheckIn ? "CHECK-IN" : "CHECK-OUT"),
                    PreviousBrush = previous is not null && previous.Type == AttendanceType.CheckIn
                        ? Brushes.SeaGreen : Brushes.Firebrick,
                    CurrentText = checkedIn ? "CHECKED IN" : "CHECKED OUT",
                    PreviousMethodText = previous is null ? string.Empty : "Verified by: " + (previous.Method == VerificationMethod.Face ? "Face" : previous.Method == VerificationMethod.Fingerprint ? "Fingerprint" : "PIN"),
                    MethodText = "Verified by: " + (_method == VerificationMethod.Face ? "Face" : _method == VerificationMethod.Fingerprint ? "Fingerprint" : "PIN"),
                    AutoNote = result.AutoCheckedOutOf is null ? string.Empty : $"Checked out of {result.AutoCheckedOutOf} automatically.",
                    CurrentTime = (result.Timestamp ?? DateTime.Now).ToString("hh:mm tt") + " - " + (result.Timestamp ?? DateTime.Now).ToString("dddd, dd MMM yyyy"),
                    CurrentBrush = checkedIn ? Brushes.SeaGreen : Brushes.Firebrick,
                    ClubName = club.Name,
                    LogId = currentLogId
                };

                ShowKeypad = false;
                ShowClubs = false;
                
                try
                {
                    
                    new VerifyDialog(info) { Owner = Application.Current.MainWindow }.ShowDialog();
                }
                catch (Exception cardEx)
                {
                    
                    MessageBox.Show("Card error: " + cardEx.Message, "Verify card", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                _backTimer?.Stop();
                _shell.NavigateTo(new MemberClubsViewModel(_shell, _memberId));
                return;
            case AttendanceStatus.TooSoon:
                Finish(true, "Already marked a moment ago. Please wait 1 minute.");
                break;
            default:
                Finish(true, "Could not mark attendance for this club.");
                break;
        }
    }

    private void Finish(bool error, string text)
    {
        ShowKeypad = false;
        ShowClubs = false;
        Prompt = string.Empty;
        Display = string.Empty;
        IsError = error;
        Message = text;

        _backTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _backTimer.Tick += (_, _) => Back();
        _backTimer.Start();
    }

    [RelayCommand]
    private void Back()
    {
        _backTimer?.Stop();
        _shell.NavigateTo(new KioskViewModel(_shell, string.Empty));
    }
}





















