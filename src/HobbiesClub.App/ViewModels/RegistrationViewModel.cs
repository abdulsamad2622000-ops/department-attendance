using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Biometrics.Fake;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public partial class RegistrationViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private string _buf = string.Empty;
    private string _pin = string.Empty;
    private byte[]? _face;
    private byte[]? _photo;
    private byte[]? _f1;
    private byte[]? _f2;

    public RegistrationViewModel(ShellViewModel shell) => _shell = shell;

    public string[] Keys { get; } = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "Del", "0", "OK" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetails), nameof(IsClubs), nameof(IsPin), nameof(IsBiometric), nameof(IsSaved), nameof(ShowNext), nameof(Prompt), nameof(ScanLabel))]
    private int _step = 1;

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _rank = string.Empty;
    [ObservableProperty] private string _rollRegNo = string.Empty;
    [ObservableProperty] private string _unit = string.Empty;
    [ObservableProperty] private bool _joinAi;
    [ObservableProperty] private bool _joinHobbies;
    [ObservableProperty] private string _display = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _savedText = string.Empty;

    public bool IsDetails => Step == 1;
    public bool IsClubs => Step == 2;
    public bool IsPin => Step == 3 || Step == 4;
    public bool IsBiometric => Step >= 5 && Step <= 7;
    public bool IsSaved => Step == 8;
    public bool ShowNext => Step == 1 || Step == 2;

    public string Prompt => Step switch
    {
        1 => "Your details",
        2 => "Choose your club(s)",
        3 => "Choose a 4-digit PIN",
        4 => "Confirm your PIN",
        5 => "Face scan",
        6 => "Fingerprint 1 of 2",
        7 => "Fingerprint 2 of 2 (use another finger)",
        _ => "Registered"
    };

    public string ScanLabel => Step switch
    {
        5 => "Scan face",
        6 => "Scan finger 1",
        7 => "Scan finger 2",
        _ => string.Empty
    };

    [RelayCommand]
    private async Task KeyAsync(string k)
    {
        Message = string.Empty;
        if (k == "OK") { await NextAsync(); return; }
        if (k == "Del") { if (_buf.Length > 0) _buf = _buf[..^1]; }
        else if (_buf.Length < MemberService.PinLength) _buf += k;
        Display = new string('*', _buf.Length);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        Message = string.Empty;
        using var scope = App.Services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<MemberService>();

        switch (Step)
        {
            case 1:
                if (string.IsNullOrWhiteSpace(FullName)) { Message = "Name is required."; return; }
                if (MemberService.NormalizePhone(Phone).Length < 10) { Message = "Phone number is not valid."; return; }
                if (string.IsNullOrWhiteSpace(Rank)) { Message = "Rank is required."; return; }
                if (string.IsNullOrWhiteSpace(RollRegNo)) { Message = "R/N is required."; return; }
                if (string.IsNullOrWhiteSpace(Unit)) { Message = "Unit is required."; return; }
                if (await members.IsPhoneTakenAsync(Phone)) { Message = "This phone number is already registered."; return; }
                Step = 2;
                break;
            case 2:
                if (!JoinAi && !JoinHobbies) { Message = "Select at least one club."; return; }
                Step = 3;
                break;
            case 3:
                if (!MemberService.IsValidPin(_buf)) { Message = $"PIN must be {MemberService.PinLength} digits."; return; }
                if (await members.IsPinTakenAsync(_buf)) { Message = "This PIN is already taken, choose another."; _buf = string.Empty; Display = string.Empty; return; }
                _pin = _buf; _buf = string.Empty; Display = string.Empty;
                Step = 4;
                break;
            case 4:
                if (_buf != _pin) { Message = "The two PINs do not match. Start again."; _buf = string.Empty; _pin = string.Empty; Display = string.Empty; Step = 3; return; }
                _buf = string.Empty; Display = string.Empty;
                Step = 5;
                break;
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        Message = string.Empty;
        using var scope = App.Services.CreateScope();
        var sp = scope.ServiceProvider;

        if (Step == 5)
        {
            var faceSvc = sp.GetRequiredService<IFaceService>();
            if (faceSvc is FakeFaceService ff) ff.NextTemplate = Guid.NewGuid().ToByteArray();
            var t = await faceSvc.CaptureTemplateAsync();
            if (t is null || t.Length == 0) { Message = "Face capture failed. Try again."; return; }
            _face = t;
            _photo = faceSvc.LastCapturedPhotoJpeg;
            Step = 6;
            return;
        }

        var fpSvc = sp.GetRequiredService<IFingerprintService>();
        if (fpSvc is FakeFingerprintService fp) fp.NextTemplate = Guid.NewGuid().ToByteArray();
        var scan = await fpSvc.CaptureTemplateAsync();
        if (scan is null || scan.Length == 0) { Message = "Fingerprint scan failed. Try again."; return; }

        if (Step == 6)
        {
            _f1 = scan;
            Step = 7;
            return;
        }

        if (_f1 is not null && fpSvc.IsMatch(scan, _f1)) { Message = "Use a different finger."; return; }
        _f2 = scan;
        await SaveAsync(sp);
    }

    private async Task SaveAsync(IServiceProvider sp)
    {
        var members = sp.GetRequiredService<MemberService>();
        var bio = sp.GetRequiredService<BiometricService>();

        var clubs = new List<int>();
        if (JoinAi) clubs.Add(1);
        if (JoinHobbies) clubs.Add(2);

        var request = new NewMemberRequest(
            FullName.Trim(), Phone, clubs, _pin, _photo, RegistrationSource.SelfService,
            Rank.Trim(), RollRegNo.Trim(), Unit.Trim());

        var result = await bio.RegisterMemberAsync(members, request, _face!, _f1!, _f2!);

        if (!result.Succeeded)
        {
            _face = null; _f1 = null; _f2 = null;
            Step = 5;
            Message = (result.Error ?? "Registration failed.") + " Nothing was saved. Scan again.";
            return;
        }

        SavedText = $"Registered: {FullName.Trim()}. Member ID {result.Member!.MemberCode}. You can now mark attendance with your PIN, face or fingerprint.";
        Step = 8;
    }

    [RelayCommand]
    private void Back() => _shell.NavigateTo(new KioskViewModel(_shell, string.Empty));
}



