using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.App.Views;
using HobbiesClub.Biometrics.Fake;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public partial class MemberRow : ObservableObject
{
    public int Id { get; init; }
    public int MemberCode { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Rank { get; init; } = string.Empty;
    public string RollRegNo { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string Clubs { get; init; } = string.Empty;
    public string Badge { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Registered { get; init; } = string.Empty;
    public string Initial { get; init; } = string.Empty;
    public ImageSource? Photo { get; init; }
    public bool FacePending { get; init; }
    public bool FingersPending { get; init; }
    [ObservableProperty] private bool _isChecked;
}

public record ClubItem(int Id, string Name);

/// <summary>Admin panel: list of all members with search, filters and per-member actions.</summary>
public partial class MembersViewModel : ObservableObject
{
    public const string AllClubs = "All departments";
    public const string AllStatuses = "All";

    private List<MemberRow> _all = new();

    public MembersViewModel(int actorId, string actorUsername)
    {
        ActorId = actorId;
        ActorUsername = actorUsername;
        _ = LoadAsync();
    }

    public int ActorId { get; }
    public string ActorUsername { get; }

    public ObservableCollection<MemberRow> Rows { get; } = new();
    public ObservableCollection<string> ClubOptions { get; } = new() { AllClubs };
    public string[] StatusOptions { get; } = { AllStatuses, "Active", "Terminated", "Face pending", "Fingers pending" };
    public MemberBadge[] BadgeOptions { get; } = { MemberBadge.Basic, MemberBadge.Medium, MemberBadge.Pro };
    public ObservableCollection<ClubItem> MyClubs { get; } = new();
    public ObservableCollection<ClubItem> JoinableClubs { get; } = new();

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private string _clubFilter = AllClubs;
    [ObservableProperty] private string _statusFilter = AllStatuses;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private MemberRow? _selected;

    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _isActiveMember;
    [ObservableProperty] private bool _isTerminated;
    [ObservableProperty] private MemberBadge _selectedBadge = MemberBadge.Basic;
    [ObservableProperty] private ClubItem? _selectedMyClub;
    [ObservableProperty] private ClubItem? _selectedJoinClub;
    [ObservableProperty] private string _actionMessage = string.Empty;
    [ObservableProperty] private string _actionError = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editPhone = string.Empty;
    [ObservableProperty] private string _editRank = string.Empty;
    [ObservableProperty] private string _editRoll = string.Empty;
    [ObservableProperty] private string _editUnit = string.Empty;
    [ObservableProperty] private string _faceButtonText = "Re-enroll face";
    [ObservableProperty] private string _fingerButtonText = "Re-enroll fingers";
    [ObservableProperty] private bool _allChecked;
    [ObservableProperty] private bool _hasChecked;
    [ObservableProperty] private string _deleteSelectedText = "Delete selected";

    private System.Windows.Threading.DispatcherTimer? _messageTimer;

    partial void OnActionMessageChanged(string value)
    {
        _messageTimer?.Stop();
        if (string.IsNullOrEmpty(value)) return;
        _messageTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer?.Stop();
            ActionMessage = string.Empty;
        };
        _messageTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer? _errorTimer;

    partial void OnActionErrorChanged(string value)
    {
        _errorTimer?.Stop();
        if (string.IsNullOrEmpty(value)) return;
        _errorTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _errorTimer.Tick += (_, _) =>
        {
            _errorTimer?.Stop();
            ActionError = string.Empty;
        };
        _errorTimer.Start();
    }

    partial void OnSearchChanged(string value) => ApplyFilter();
    partial void OnClubFilterChanged(string value) => ApplyFilter();
    partial void OnStatusFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedChanged(MemberRow? value)
    {
        ActionMessage = string.Empty;
        ActionError = string.Empty;
        _ = LoadDetailAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var keepId = Selected?.Id;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var clubs = await db.Clubs.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.Id).Select(c => c.Name).ToListAsync();
        var links = await db.MemberClubs.AsNoTracking()
            .Select(mc => new { mc.MemberId, ClubName = mc.Club.Name }).ToListAsync();
        var members = await db.Members.AsNoTracking().OrderBy(m => m.MemberCode).ToListAsync();
        var faceIds = (await db.BiometricTemplates.AsNoTracking()
            .Where(t => t.Type == BiometricType.Face).Select(t => t.MemberId).Distinct().ToListAsync()).ToHashSet();
        var fingerIds = (await db.BiometricTemplates.AsNoTracking()
            .Where(t => t.Type == BiometricType.Fingerprint).Select(t => t.MemberId).Distinct().ToListAsync()).ToHashSet();

        if (ClubOptions.Count == 1)
            foreach (var c in clubs) ClubOptions.Add(c);

        _all = members.Select(m => new MemberRow
        {
            Id = m.Id,
            MemberCode = m.MemberCode,
            FullName = m.FullName,
            Phone = m.Phone,
            Rank = m.Rank,
            RollRegNo = m.RollRegNo,
            Unit = m.Unit,
            FacePending = !faceIds.Contains(m.Id),
            FingersPending = !fingerIds.Contains(m.Id),
            Clubs = string.Join(", ", links.Where(l => l.MemberId == m.Id).Select(l => l.ClubName)),
            Badge = m.Badge.ToString(),
            Status = m.Status == MemberStatus.Active ? "Active" : "Terminated",
            Registered = m.RegisteredAt.ToString("dd MMM yyyy"),
            Photo = PhotoHelper.ToImage(m.PhotoJpeg),
            Initial = m.PhotoJpeg is { Length: > 0 } || m.FullName.Length == 0
                ? string.Empty : m.FullName.Substring(0, 1).ToUpperInvariant()
        }).ToList();

        ApplyFilter();

        if (keepId is int id)
            Selected = Rows.FirstOrDefault(r => r.Id == id);
    }

    private void ApplyFilter()
    {
        var q = (Search ?? string.Empty).Trim();
        IEnumerable<MemberRow> rows = _all;

        if (q.Length > 0)
            rows = rows.Where(x =>
                x.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Phone.Contains(q) ||
                x.MemberCode.ToString().Contains(q));

        if (ClubFilter != AllClubs)
            rows = rows.Where(x => x.Clubs.Split(", ").Contains(ClubFilter));

        if (StatusFilter == "Face pending")
            rows = rows.Where(x => x.FacePending && x.Status == "Active");
        else if (StatusFilter == "Fingers pending")
            rows = rows.Where(x => x.FingersPending && x.Status == "Active");
        else if (StatusFilter != AllStatuses)
            rows = rows.Where(x => x.Status == StatusFilter);

        Rows.Clear();
        foreach (var r in rows) Rows.Add(r);
        Summary = $"Showing {Rows.Count} of {_all.Count} members";
    }

    private async Task LoadDetailAsync()
    {
        MyClubs.Clear();
        JoinableClubs.Clear();
        var row = Selected;
        HasSelection = row is not null;
        if (row is null)
        {
            IsActiveMember = false;
            IsTerminated = false;
            return;
        }

        IsActiveMember = row.Status == "Active";
        IsTerminated = !IsActiveMember;
        FaceButtonText = row.FacePending ? "Add face" : "Re-enroll face";
        FingerButtonText = row.FingersPending ? "Add fingers" : "Re-enroll fingers";

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mine = await db.MemberClubs.AsNoTracking().Where(mc => mc.MemberId == row.Id)
            .Select(mc => new ClubItem(mc.ClubId, mc.Club.Name)).ToListAsync();
        var active = await db.Clubs.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Id)
            .Select(c => new ClubItem(c.Id, c.Name)).ToListAsync();

        foreach (var c in mine) MyClubs.Add(c);
        foreach (var c in active.Where(a => mine.All(m => m.Id != a.Id))) JoinableClubs.Add(c);

        SelectedBadge = Enum.TryParse<MemberBadge>(row.Badge, out var b) ? b : MemberBadge.Basic;
        EditName = row.FullName;
        EditPhone = row.Phone;
        EditRank = row.Rank;
        EditRoll = row.RollRegNo;
        EditUnit = row.Unit;
    }

    [RelayCommand]
    private async Task SaveDetailsAsync()
    {
        if (Selected is not { } m) return;
        var name = EditName; var phone = EditPhone; var rank = EditRank; var roll = EditRoll; var unit = EditUnit;
        await Run(sp => sp.GetRequiredService<MemberAdminService>().UpdateDetailsAsync(m.Id, name, phone, rank, roll, unit, ActorUsername, ActorId), "Details saved.");
    }

    private async Task Run(Func<IServiceProvider, Task<HobbiesClub.Core.Models.OperationResult>> action, string okText)
    {
        ActionMessage = string.Empty;
        ActionError = string.Empty;

        HobbiesClub.Core.Models.OperationResult result;
        using (var scope = App.Services.CreateScope())
            result = await action(scope.ServiceProvider);

        if (!result.Succeeded)
        {
            ActionError = result.Error ?? "Something went wrong.";
            return;
        }

        await LoadAsync();
        ActionMessage = okText;
    }

    [RelayCommand]
    private async Task ResetPinAsync()
    {
        if (Selected is not { } m) return;
        var pin = PromptDialog.Ask("Reset PIN", $"Enter a new 4-digit PIN for {m.FullName}:", MemberService.PinLength);
        if (pin is null) return;
        await Run(sp => sp.GetRequiredService<MemberAdminService>().ResetPinAsync(m.Id, pin, ActorUsername, ActorId), "PIN changed.");
    }

    [RelayCommand]
    private async Task SaveBadgeAsync()
    {
        if (Selected is not { } m) return;
        var badge = SelectedBadge;
        await Run(sp => sp.GetRequiredService<BadgeService>().SetBadgeAsync(m.Id, badge, ActorUsername, ActorId), "Badge saved.");
    }

    [RelayCommand]
    private async Task AddClubAsync()
    {
        if (Selected is not { } m || SelectedJoinClub is not { } c) return;
        await Run(sp => sp.GetRequiredService<MemberService>().JoinClubAsync(m.Id, c.Id, ActorUsername, ActorId), $"Added to {c.Name}.");
    }

    [RelayCommand]
    private async Task RemoveClubAsync()
    {
        if (Selected is not { } m || SelectedMyClub is not { } c) return;
        await Run(sp => sp.GetRequiredService<MemberAdminService>().LeaveClubAsync(m.Id, c.Id, ActorUsername, ActorId), $"Removed from {c.Name}.");
    }

    [RelayCommand]
    private async Task TerminateAsync()
    {
        if (Selected is not { } m) return;
        var reason = PromptDialog.Ask("Terminate member", $"Reason for terminating {m.FullName}:");
        if (string.IsNullOrWhiteSpace(reason)) return;
        await Run(sp => sp.GetRequiredService<MemberService>().TerminateAsync(m.Id, reason, ActorUsername, ActorId), "Member terminated.");
    }

    [RelayCommand]
    private async Task ReactivateAsync()
    {
        if (Selected is not { } m) return;
        await Run(sp => sp.GetRequiredService<MemberService>().ReactivateAsync(m.Id, ActorUsername, ActorId), "Member reactivated.");
    }

    [RelayCommand]
    private async Task ReenrollFaceAsync()
    {
        if (Selected is not { } m) return;
        await Run(async sp =>
        {
            var face = sp.GetRequiredService<IFaceService>();
            if (face is FakeFaceService ff) ff.NextTemplate = Guid.NewGuid().ToByteArray();
            var template = HobbiesClub.App.Views.FaceScanDialog.Scan(face, $"Re-enroll face · {m.FullName}", true);
            if (template is null || template.Length == 0)
                return new HobbiesClub.Core.Models.OperationResult(false, "Face capture failed. Try again.");
            return await sp.GetRequiredService<MemberAdminService>()
                .ReenrollFaceAsync(m.Id, template, face.LastCapturedPhotoJpeg, ActorUsername, ActorId);
        }, "Face re-enrolled.");
    }
            [RelayCommand]
    private async Task ReenrollFingersAsync()
    {
        if (Selected is not { } m) return;
        var fp = App.Services.GetRequiredService<IFingerprintService>();
        var fingers = HobbiesClub.App.Views.FingerEnrollDialog.Enroll(fp, $"Re-enroll fingerprints · {m.FullName}", "The fingers you register here replace the old ones.");
        if (fingers is not { Count: > 0 }) return;

        await Run(sp => sp.GetRequiredService<MemberAdminService>().ReenrollFingersAsync(m.Id, fingers, ActorUsername, ActorId), "Fingerprints re-enrolled.");

        if (string.IsNullOrEmpty(ActionError))
            System.Windows.MessageBox.Show($"{fingers.Count} fingerprint(s) of {m.FullName} were saved.", "Fingerprints saved",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        else
            System.Windows.MessageBox.Show(ActionError, "Fingerprints not saved",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    }
    [RelayCommand]
    private async Task UploadPhotoAsync()
    {
        if (Selected is not { } m) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Choose a photo for {m.FullName}",
            Filter = "Pictures (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
        };
        if (dlg.ShowDialog() != true) return;

        byte[] jpeg;
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(dlg.FileName);
            bmp.DecodePixelWidth = 600;
            bmp.EndInit();
            var enc = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 85 };
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var ms = new System.IO.MemoryStream();
            enc.Save(ms);
            jpeg = ms.ToArray();
        }
        catch
        {
            ActionError = "This file could not be read as a picture.";
            return;
        }

        await Run(sp => sp.GetRequiredService<MemberAdminService>().SetPhotoAsync(m.Id, jpeg, ActorUsername, ActorId), "Photo saved.");
    }
    partial void OnAllCheckedChanged(bool value)
    {
        foreach (var r in Rows) r.IsChecked = value;
        UpdateChecked();
    }

    [RelayCommand]
    private void UpdateChecked()
    {
        var n = _all.Count(r => r.IsChecked);
        HasChecked = n > 0;
        DeleteSelectedText = $"Delete selected ({n})";
    }

    [RelayCommand]
    private async Task DeleteMemberAsync()
    {
        if (Selected is not { } m) return;
        var ok = System.Windows.MessageBox.Show(
            $"Delete {m.FullName} permanently?\n\nThis also deletes all of their attendance history, face, fingerprints and department links.\nA backup is made first, so it can be undone from Backup.",
            "Delete member", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        await DeleteIdsAsync(new List<int> { m.Id }, $"{m.FullName} was");
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var ids = _all.Where(r => r.IsChecked).Select(r => r.Id).ToList();
        if (ids.Count == 0) return;
        var ok = System.Windows.MessageBox.Show(
            $"Delete {ids.Count} selected member(s) permanently?\n\nThis also deletes all of their attendance history, face, fingerprints and department links.\nA backup is made first, so it can be undone from Backup.",
            "Delete selected members", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (ok != System.Windows.MessageBoxResult.Yes) return;
        await DeleteIdsAsync(ids, $"{ids.Count} member(s) were");
    }

    [RelayCommand]
    private async Task DeleteAllAsync()
    {
        var ids = _all.Select(r => r.Id).ToList();
        if (ids.Count == 0) return;
        var typed = PromptDialog.Ask("Delete all members",
            $"This deletes ALL {ids.Count} members and all attendance history.\nA backup is made first.\n\nType DELETE to confirm:");
        if (!string.Equals(typed?.Trim(), "DELETE", StringComparison.Ordinal)) return;
        await DeleteIdsAsync(ids, $"All {ids.Count} members were");
    }

    private async Task DeleteIdsAsync(List<int> ids, string what)
    {
        using var scope = App.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var backup = await sp.GetRequiredService<BackupService>().CreateBackupAsync(BackupKind.Manual, ActorUsername, ActorId);
        if (!backup.Succeeded)
        {
            System.Windows.MessageBox.Show("A backup could not be made, so nothing was deleted.\n\n" + backup.Error,
                "Delete cancelled", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }
        var r = await sp.GetRequiredService<MemberAdminService>().DeleteMembersAsync(ids, ActorUsername, ActorId);
        if (!r.Succeeded)
        {
            System.Windows.MessageBox.Show(r.Error ?? "Delete failed.", "Delete failed",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }
        Selected = null;
        AllChecked = false;
        await LoadAsync();
        UpdateChecked();
        System.Windows.MessageBox.Show($"{what} deleted.\n\nA backup was made first, so this can be undone from Backup.",
            "Deleted", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }
}

