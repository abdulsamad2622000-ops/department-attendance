using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HobbiesClub.App.Helpers;

public sealed class ParagraphItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private double _size = BrandingState.DefParagraphSize;
    public string Text { get => _text; set { _text = value ?? string.Empty; On(); } }
    public double Size { get => _size; set { _size = value; On(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void On([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>All login/kiosk branding values. Used for the applied settings and for the editable draft.</summary>
public class BrandingState : INotifyPropertyChanged
{
    public const string DefHeading = "PAKISTAN NAVY";
    public static string DefSubtitle => "Fleet UMS Hobby Club & AI Center  \u00B7  Attendance Management System";
    public static string[] DefFooter => new[] { "DISCIPLINE", "PROFESSIONALISM", "EXCELLENCE" };
    public const double DefHeadingSize = 36, DefSubtitleSize = 16, DefParagraphSize = 14, DefLogoSize = 110;
    public const int MaxParagraphs = 3;
    public static readonly string[] ImageKinds = { "login-bg", "kiosk-bg", "login-logo", "extra-logo", "left-logo" };

    private static readonly string[] Computed =
    {
        "HasHeading", "HasSubtitle", "HasCustomLoginBg", "IsDefaultLoginBg", "HasExtraLogo", "NoExtraLogo",
        "ExtraLeft", "ExtraRight", "ExtraTopLeft", "FooterText", "PosRight", "PosLeft", "PosTopLeft", "CanAddParagraph",
        "ShowLeft", "HeaderTitle", "HeaderSubtitle", "HasLeftLogo", "NoLeftLogo", "HasLeftHeading", "HasLeftSubtitle", "HasLeftText"
    };

    private string _heading = DefHeading, _subtitle = string.Empty;
    private double _headingSize = DefHeadingSize, _subtitleSize = DefSubtitleSize, _mainLogoSize = DefLogoSize, _extraLogoSize = DefLogoSize;
    private string _f1 = "", _f2 = "", _f3 = "", _f4 = "", _f5 = "";
    private string _extraPos = "Right";
    private ImageSource? _loginBg, _kioskBg, _loginLogo, _extraLogo, _leftLogo;
    private bool _leftOn;
    private string _leftHeading = "", _leftSubtitle = "", _leftText = "";
    private double _leftHeadingSize = DefHeadingSize, _leftSubtitleSize = DefSubtitleSize, _leftTextSize = DefParagraphSize, _leftLogoSize = DefLogoSize;

    public BrandingState()
    {
        Paragraphs.CollectionChanged += (_, _) => Raise(nameof(CanAddParagraph));
        ResetToDefaults();
        PendingImages.Clear();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        field = value;
        Raise(name);
        foreach (var c in Computed) Raise(c);
    }

    public string Heading { get => _heading; set => Set(ref _heading, value ?? string.Empty); }
    public double HeadingSize { get => _headingSize; set => Set(ref _headingSize, value); }
    public string Subtitle { get => _subtitle; set => Set(ref _subtitle, value ?? string.Empty); }
    public double SubtitleSize { get => _subtitleSize; set => Set(ref _subtitleSize, value); }
    public string Footer1 { get => _f1; set => Set(ref _f1, value ?? string.Empty); }
    public string Footer2 { get => _f2; set => Set(ref _f2, value ?? string.Empty); }
    public string Footer3 { get => _f3; set => Set(ref _f3, value ?? string.Empty); }
    public string Footer4 { get => _f4; set => Set(ref _f4, value ?? string.Empty); }
    public string Footer5 { get => _f5; set => Set(ref _f5, value ?? string.Empty); }
    public double MainLogoSize { get => _mainLogoSize; set => Set(ref _mainLogoSize, value); }
    public double ExtraLogoSize { get => _extraLogoSize; set => Set(ref _extraLogoSize, value); }
    public string ExtraLogoPosition { get => _extraPos; set => Set(ref _extraPos, value is "Left" or "TopLeft" ? value : "Right"); }
    public ImageSource? LoginBackground { get => _loginBg; set => Set(ref _loginBg, value); }
    public ImageSource? KioskBackground { get => _kioskBg; set => Set(ref _kioskBg, value); }
    public ImageSource? LoginLogo { get => _loginLogo; set => Set(ref _loginLogo, value); }
    public ImageSource? ExtraLogo { get => _extraLogo; set => Set(ref _extraLogo, value); }

    public bool LeftOn
    {
        get => _leftOn;
        set
        {
            if (value && string.IsNullOrWhiteSpace(_leftHeading) && string.IsNullOrWhiteSpace(_leftSubtitle))
            {
                _leftHeading = string.IsNullOrWhiteSpace(Heading) ? DefHeading : Heading;
                _leftSubtitle = string.IsNullOrWhiteSpace(Subtitle) ? DefSubtitle : Subtitle;
            }
            if (value && _leftLogo is null) _leftLogo = Default("navy-logo.png");
            Set(ref _leftOn, value);
            RaiseAll();
        }
    }
    private bool _leftLogoOn = true;
    public bool LeftLogoOn { get => _leftLogoOn; set => Set(ref _leftLogoOn, value); }
    /// <summary>The header on kiosk/admin/register/PIN screens uses the right side heading and paragraph.</summary>
    public string HeaderTitle => string.IsNullOrWhiteSpace(Heading) ? DefHeading : Heading;
    public string HeaderSubtitle => Subtitle;
    public ImageSource? LeftLogo { get => _leftLogo; set => Set(ref _leftLogo, value); }
    public double LeftLogoSize { get => _leftLogoSize; set => Set(ref _leftLogoSize, value); }
    public string LeftHeading { get => _leftHeading; set => Set(ref _leftHeading, value ?? string.Empty); }
    public double LeftHeadingSize { get => _leftHeadingSize; set => Set(ref _leftHeadingSize, value); }
    public string LeftSubtitle { get => _leftSubtitle; set => Set(ref _leftSubtitle, value ?? string.Empty); }
    public double LeftSubtitleSize { get => _leftSubtitleSize; set => Set(ref _leftSubtitleSize, value); }
    public string LeftText { get => _leftText; set => Set(ref _leftText, value ?? string.Empty); }
    public double LeftTextSize { get => _leftTextSize; set => Set(ref _leftTextSize, value); }

    public ObservableCollection<ParagraphItem> Paragraphs { get; } = new();
    public Dictionary<string, string> PendingImages { get; } = new();

    public bool HasHeading => !string.IsNullOrWhiteSpace(Heading);
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    public bool HasCustomLoginBg => LoginBackground is not null;
    public bool IsDefaultLoginBg => LoginBackground is null;
    public bool HasExtraLogo => ExtraLogo is not null;
    public bool NoExtraLogo => ExtraLogo is null;
    public bool ExtraLeft => HasExtraLogo && ExtraLogoPosition == "Left";
    public bool ExtraRight => HasExtraLogo && ExtraLogoPosition == "Right";
    public bool ExtraTopLeft => HasExtraLogo && ExtraLogoPosition == "TopLeft";
    public bool PosRight { get => ExtraLogoPosition == "Right"; set { if (value) ExtraLogoPosition = "Right"; } }
    public bool PosLeft { get => ExtraLogoPosition == "Left"; set { if (value) ExtraLogoPosition = "Left"; } }
    public bool PosTopLeft { get => ExtraLogoPosition == "TopLeft"; set { if (value) ExtraLogoPosition = "TopLeft"; } }
    public bool CanAddParagraph => Paragraphs.Count < MaxParagraphs;
    public string FooterText => string.Join("     |     ",
        new[] { Footer1, Footer2, Footer3, Footer4, Footer5 }.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()));

    public bool ShowLeft => LeftOn;
    public bool HasLeftLogo => LeftLogoOn && LeftLogo is not null;
    public bool NoLeftLogo => LeftLogo is null;
    public bool HasLeftHeading => !string.IsNullOrWhiteSpace(LeftHeading);
    public bool HasLeftSubtitle => !string.IsNullOrWhiteSpace(LeftSubtitle);
    public bool HasLeftText => !string.IsNullOrWhiteSpace(LeftText);

    public static ImageSource Default(string rel)
    {
        var b = new BitmapImage(new Uri("pack://application:,,,/Assets/" + rel));
        b.Freeze();
        return b;
    }

    public static ImageSource? LoadImage(string path, int width)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.DecodePixelWidth = width;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    public void ResetToDefaults()
    {
        _heading = DefHeading; _headingSize = DefHeadingSize;
        _subtitle = DefSubtitle; _subtitleSize = DefSubtitleSize;
        var f = DefFooter; _f1 = f[0]; _f2 = f[1]; _f3 = f[2]; _f4 = ""; _f5 = "";
        _mainLogoSize = DefLogoSize; _extraLogoSize = DefLogoSize; _extraPos = "Right";
        _loginBg = null; _kioskBg = Default("Backgrounds/kiosk-bg.jpg");
        _loginLogo = Default("navy-logo.png"); _extraLogo = null;
        _leftLogoOn = true; _leftOn = false; _leftLogo = Default("navy-logo.png"); _leftLogoSize = DefLogoSize;
        _leftHeading = DefHeading; _leftHeadingSize = DefHeadingSize;
        _leftSubtitle = DefSubtitle; _leftSubtitleSize = DefSubtitleSize;
        _leftText = ""; _leftTextSize = DefParagraphSize;
        Paragraphs.Clear();
        foreach (var k in ImageKinds) PendingImages[k] = string.Empty;
        RaiseAll();
    }

    public void CopyFrom(BrandingState s)
    {
        _heading = s.Heading; _headingSize = s.HeadingSize;
        _subtitle = s.Subtitle; _subtitleSize = s.SubtitleSize;
        _f1 = s.Footer1; _f2 = s.Footer2; _f3 = s.Footer3; _f4 = s.Footer4; _f5 = s.Footer5;
        _mainLogoSize = s.MainLogoSize; _extraLogoSize = s.ExtraLogoSize; _extraPos = s.ExtraLogoPosition;
        _loginBg = s.LoginBackground; _kioskBg = s.KioskBackground; _loginLogo = s.LoginLogo; _extraLogo = s.ExtraLogo;
        _leftLogoOn = s.LeftLogoOn; _leftOn = s.LeftOn; _leftLogo = s.LeftLogo; _leftLogoSize = s.LeftLogoSize;
        _leftHeading = s.LeftHeading; _leftHeadingSize = s.LeftHeadingSize;
        _leftSubtitle = s.LeftSubtitle; _leftSubtitleSize = s.LeftSubtitleSize;
        _leftText = s.LeftText; _leftTextSize = s.LeftTextSize;
        Paragraphs.Clear();
        foreach (var p in s.Paragraphs) Paragraphs.Add(new ParagraphItem { Text = p.Text, Size = p.Size });
        PendingImages.Clear();
        RaiseAll();
    }

    public string? SetPendingImage(string kind, string path)
    {
        var img = LoadImage(path, kind.EndsWith("logo") ? 500 : 1920);
        if (img is null) return "This file could not be read as a picture.";
        switch (kind)
        {
            case "login-bg": LoginBackground = img; break;
            case "kiosk-bg": KioskBackground = img; break;
            case "login-logo": LoginLogo = img; break;
            case "extra-logo": ExtraLogo = img; break;
            case "left-logo": LeftLogo = img; break;
        }
        PendingImages[kind] = path;
        return null;
    }

    public void ResetPendingImage(string kind)
    {
        switch (kind)
        {
            case "login-bg": LoginBackground = null; break;
            case "kiosk-bg": KioskBackground = Default("Backgrounds/kiosk-bg.jpg"); break;
            case "login-logo": LoginLogo = Default("navy-logo.png"); break;
            case "extra-logo": ExtraLogo = null; break;
            case "left-logo": LeftLogo = Default("navy-logo.png"); break;
        }
        PendingImages[kind] = string.Empty;
    }
}


