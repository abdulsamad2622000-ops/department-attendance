using System.IO;
using System.Text.Json;
using System.Windows.Media;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.Helpers;

/// <summary>The applied branding, used by the login and kiosk screens. Stored in ProgramData\HobbiesClub\Branding.</summary>
public sealed class BrandingSource : BrandingState
{
    private static BrandingSource? _instance;
    public static BrandingSource Instance => _instance ??= new BrandingSource();

    public static string Folder => Path.Combine(AppPaths.Root, "Branding");
    private static string JsonPath => Path.Combine(Folder, "branding.json");

    private sealed class Para { public string? Text { get; set; } public double Size { get; set; } }
    private sealed class Data
    {
        public string? Heading { get; set; }
        public double? HeadingSize { get; set; }
        public string? Subtitle { get; set; }
        public double? SubtitleSize { get; set; }
        public string? Extra { get; set; }
        public List<Para>? Paragraphs { get; set; }
        public string[]? Footer { get; set; }
        public double? MainLogoSize { get; set; }
        public double? ExtraLogoSize { get; set; }
        public string? ExtraLogoPosition { get; set; }
        public bool? LeftOn { get; set; }
        public bool? LeftLogoOn { get; set; }
        public double? LeftLogoSize { get; set; }
        public string? LeftHeading { get; set; }
        public double? LeftHeadingSize { get; set; }
        public string? LeftSubtitle { get; set; }
        public double? LeftSubtitleSize { get; set; }
        public string? LeftText { get; set; }
        public double? LeftTextSize { get; set; }
    }

    private BrandingSource()
    {
        FixClubNames();
        Reload();
    }

    /// <summary>Older databases still have the old club names. Rename them once (safe to run every start).</summary>
    private static void FixClubNames()
    {
        try
        {
            using var scope = HobbiesClub.App.App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.ExecuteSqlRaw("UPDATE Clubs SET Name = 'AI Center' WHERE Name = 'AI Club'");
            db.Database.ExecuteSqlRaw("UPDATE Clubs SET Name = 'Fleet UMS Hobby Club' WHERE Name = 'Hobbies Club'");
        }
        catch { /* never block the login screen */ }
    }

    private static ImageSource? LoadKind(string kind, int width)
    {
        try
        {
            if (!Directory.Exists(Folder)) return null;
            var f = Directory.GetFiles(Folder, kind + ".*").FirstOrDefault();
            return f is null ? null : LoadImage(f, width);
        }
        catch { return null; }
    }

    private static double Clamp(double? v, double def, double min, double max) =>
        v is double x && x > 0 ? Math.Clamp(x, min, max) : def;

    public void Reload()
    {
        ResetToDefaults();
        PendingImages.Clear();
        try
        {
            if (File.Exists(JsonPath))
            {
                var d = JsonSerializer.Deserialize<Data>(File.ReadAllText(JsonPath));
                if (d is not null)
                {
                    Heading = d.Heading ?? DefHeading;
                    HeadingSize = Clamp(d.HeadingSize, DefHeadingSize, 24, 56);
                    Subtitle = d.Subtitle ?? DefSubtitle;
                    SubtitleSize = Clamp(d.SubtitleSize, DefSubtitleSize, 12, 26);
                    if (d.Footer is { Length: > 0 })
                    {
                        var f = d.Footer.Concat(Enumerable.Repeat(string.Empty, 5)).Take(5).ToArray();
                        Footer1 = f[0] ?? ""; Footer2 = f[1] ?? ""; Footer3 = f[2] ?? ""; Footer4 = f[3] ?? ""; Footer5 = f[4] ?? "";
                    }
                    MainLogoSize = Clamp(d.MainLogoSize, DefLogoSize, 70, 160);
                    ExtraLogoSize = Clamp(d.ExtraLogoSize, DefLogoSize, 50, 160);
                    ExtraLogoPosition = d.ExtraLogoPosition ?? "Right";
                    if (d.Paragraphs is { Count: > 0 })
                        foreach (var p in d.Paragraphs.Take(MaxParagraphs))
                            Paragraphs.Add(new ParagraphItem { Text = p.Text ?? "", Size = Clamp(p.Size, DefParagraphSize, 11, 24) });
                    else if (!string.IsNullOrWhiteSpace(d.Extra))
                        Paragraphs.Add(new ParagraphItem { Text = d.Extra.Trim(), Size = DefParagraphSize });
                    LeftOn = d.LeftOn ?? false;
                    LeftLogoOn = d.LeftLogoOn ?? true;
                    LeftLogoSize = Clamp(d.LeftLogoSize, DefLogoSize, 50, 160);
                    LeftHeading = string.IsNullOrWhiteSpace(d.LeftHeading) ? DefHeading : d.LeftHeading;
                    LeftHeadingSize = Clamp(d.LeftHeadingSize, DefHeadingSize, 20, 60);
                    LeftSubtitle = string.IsNullOrWhiteSpace(d.LeftSubtitle) ? DefSubtitle : d.LeftSubtitle;
                    LeftSubtitleSize = Clamp(d.LeftSubtitleSize, DefSubtitleSize, 12, 28);
                    LeftText = d.LeftText ?? "";
                    LeftTextSize = Clamp(d.LeftTextSize, DefParagraphSize, 11, 26);
                }
            }
        }
        catch { /* keep defaults */ }

        LoginBackground = LoadKind("login-bg", 1920);
        KioskBackground = LoadKind("kiosk-bg", 1920) ?? Default("Backgrounds/kiosk-bg.jpg");
        LoginLogo = LoadKind("login-logo", 500) ?? Default("navy-logo.png");
        ExtraLogo = LoadKind("extra-logo", 500);
        LeftLogo = LoadKind("left-logo", 500) ?? Default("navy-logo.png");
        RaiseAll();
    }

    public string? Apply(BrandingState draft)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (var kv in draft.PendingImages)
            {
                string? temp = null;
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    temp = Path.Combine(Path.GetTempPath(), "hc-brand-" + Guid.NewGuid().ToString("N") + Path.GetExtension(kv.Value));
                    File.Copy(kv.Value, temp, true);
                }
                foreach (var old in Directory.GetFiles(Folder, kv.Key + ".*")) File.Delete(old);
                if (temp is not null)
                {
                    File.Copy(temp, Path.Combine(Folder, kv.Key + Path.GetExtension(kv.Value).ToLowerInvariant()), true);
                    File.Delete(temp);
                }
            }

            var data = new Data
            {
                Heading = draft.Heading, HeadingSize = draft.HeadingSize,
                Subtitle = draft.Subtitle, SubtitleSize = draft.SubtitleSize,
                Paragraphs = draft.Paragraphs.Select(p => new Para { Text = p.Text, Size = p.Size }).ToList(),
                Footer = new[] { draft.Footer1, draft.Footer2, draft.Footer3, draft.Footer4, draft.Footer5 },
                MainLogoSize = draft.MainLogoSize, ExtraLogoSize = draft.ExtraLogoSize, ExtraLogoPosition = draft.ExtraLogoPosition,
                LeftOn = draft.LeftOn, LeftLogoSize = draft.LeftLogoSize, LeftLogoOn = draft.LeftLogoOn,
                LeftHeading = draft.LeftHeading, LeftHeadingSize = draft.LeftHeadingSize,
                LeftSubtitle = draft.LeftSubtitle, LeftSubtitleSize = draft.LeftSubtitleSize,
                LeftText = draft.LeftText, LeftTextSize = draft.LeftTextSize
            };
            File.WriteAllText(JsonPath, JsonSerializer.Serialize(data));
            Reload();
            return null;
        }
        catch (Exception ex) { return "Could not save: " + ex.Message; }
    }
}


