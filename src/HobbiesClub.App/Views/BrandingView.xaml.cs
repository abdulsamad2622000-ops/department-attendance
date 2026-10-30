using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HobbiesClub.App.Helpers;

namespace HobbiesClub.App.Views;

public partial class BrandingView : UserControl
{
    private const string PictureFilter = "Pictures (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
    private readonly BrandingState _draft = new();
    private bool _changing;
    private bool _recovered;

    public BrandingView()
    {
        InitializeComponent();
        DataContext = _draft;
        Loaded += (_, _) =>
        {
            _draft.CopyFrom(BrandingSource.Instance);
            Msg.Text = string.Empty;
            Lock();
        };
    }

    // ---------------- lock screen ----------------
    private void Lock()
    {
        _changing = false; _recovered = false;
        MainArea.Visibility = Visibility.Collapsed;
        LockPanel.Visibility = Visibility.Visible;
        SetMode(BrandingLock.IsSet ? "enter" : "first");
    }

    private void Unlock()
    {
        LockPanel.Visibility = Visibility.Collapsed;
        MainArea.Visibility = Visibility.Visible;
        ClearInputs();
    }

    private void ClearInputs()
    {
        PwdEnter.Clear(); PwdNew1.Clear(); PwdNew2.Clear(); PwdAdmin.Clear();
        TxtRecovery.Text = string.Empty; TxtAdminUser.Text = string.Empty;
    }

    private void SetMode(string mode)
    {
        LockMsg.Text = string.Empty;
        EnterPanel.Visibility = mode is "enter" or "change" ? Visibility.Visible : Visibility.Collapsed;
        SetPanel.Visibility = mode is "first" or "new" ? Visibility.Visible : Visibility.Collapsed;
        ForgotPanel.Visibility = mode == "forgot" ? Visibility.Visible : Visibility.Collapsed;
        SetCancel.Visibility = mode == "new" && _changing ? Visibility.Visible : Visibility.Collapsed;
        (LockTitle.Text, LockHint.Text) = mode switch
        {
            "first" => ("Set a Branding password", "This password will be asked every time the Branding page is opened. After saving it you will get a recovery code."),
            "new" => ("Set a new Branding password", "A new recovery code will be shown after saving."),
            "forgot" => ("Forgot the Branding password", "Use the recovery code, or sign in with a Super Admin account."),
            "change" => ("Change Branding password", "First enter the current Branding password."),
            _ => ("Branding is locked", "Enter the Branding password to continue.")
        };
        Dispatcher.BeginInvoke(() =>
        {
            if (EnterPanel.Visibility == Visibility.Visible) PwdEnter.Focus();
            else if (SetPanel.Visibility == Visibility.Visible) PwdNew1.Focus();
        });
    }

    private void PwdEnter_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Unlock_Click(sender, e);
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        var err = BrandingLock.Verify(PwdEnter.Password);
        PwdEnter.Clear();
        if (err is not null) { LockMsg.Text = err; return; }
        if (_changing) { SetMode("new"); return; }
        BrandingLock.Audit("BrandingOpened", null);
        Unlock();
    }

    private void SetPwd_Click(object sender, RoutedEventArgs e)
    {
        var p1 = PwdNew1.Password; var p2 = PwdNew2.Password;
        if (p1.Length < BrandingLock.MinLength) { LockMsg.Text = $"The password must be at least {BrandingLock.MinLength} characters."; return; }
        if (p1 != p2) { LockMsg.Text = "The two passwords do not match."; return; }

        var code = BrandingLock.SetPassword(p1);
        BrandingLock.Audit(_recovered ? "BrandingPasswordReset" : (_changing ? "BrandingPasswordChanged" : "BrandingPasswordSet"), null);
        try { Clipboard.SetText(code); } catch { /* clipboard busy */ }
        MessageBox.Show(
            $"Your Branding recovery code:\n\n{code}\n\nWrite it down and keep it safe. It is shown only once.\nIt has also been copied to the clipboard.",
            "Recovery code", MessageBoxButton.OK, MessageBoxImage.Information);
        _changing = false; _recovered = false;
        Unlock();
        Show(null, "Branding password saved.");
    }

    private void Forgot_Click(object sender, RoutedEventArgs e) => SetMode("forgot");

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_changing && SetPanel.Visibility == Visibility.Visible) { _changing = false; Unlock(); return; }
        SetMode(BrandingLock.IsSet ? "enter" : "first");
    }

    private async void Recover_Click(object sender, RoutedEventArgs e)
    {
        var code = TxtRecovery.Text.Trim();
        if (code.Length > 0)
        {
            if (BrandingLock.VerifyRecovery(code))
            {
                BrandingLock.Audit("BrandingRecovered", "recovery code");
                _recovered = true; ClearInputs(); SetMode("new"); return;
            }
            LockMsg.Text = "The recovery code is not correct."; return;
        }

        var user = TxtAdminUser.Text.Trim();
        if (user.Length == 0 || PwdAdmin.Password.Length == 0) { LockMsg.Text = "Enter the recovery code, or a Super Admin username and password."; return; }
        if (await BrandingLock.VerifySuperAdminAsync(user, PwdAdmin.Password))
        {
            BrandingLock.Audit("BrandingRecovered", "Super Admin login", user.ToLowerInvariant());
            _recovered = true; ClearInputs(); SetMode("new"); return;
        }
        PwdAdmin.Clear();
        BrandingLock.Audit("BrandingRecoveryFailed", user.ToLowerInvariant());
        LockMsg.Text = "Not a valid Super Admin login.";
    }

    private void ChangePwd_Click(object sender, RoutedEventArgs e)
    {
        _changing = true;
        MainArea.Visibility = Visibility.Collapsed;
        LockPanel.Visibility = Visibility.Visible;
        SetMode("change");
    }

    // ---------------- branding editor ----------------
    private void Show(string? error, string ok)
    {
        Msg.Text = error ?? ok;
        Msg.Foreground = (Brush)FindResource(error is null ? "SuccessBrush" : "DangerBrush");
    }

    private void Pick(string kind, string title)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = title, Filter = PictureFilter };
        if (dlg.ShowDialog() != true) return;
        Show(_draft.SetPendingImage(kind, dlg.FileName), "Picture added to the preview. Press Save & apply to use it.");
    }

    private void Undo(string kind, string ok)
    {
        _draft.ResetPendingImage(kind);
        Show(null, ok + " Press Save & apply to use it.");
    }

    private void LoginBg_Choose(object sender, RoutedEventArgs e) => Pick("login-bg", "Choose the login background (1920 x 1080)");
    private void LoginBg_Reset(object sender, RoutedEventArgs e) => Undo("login-bg", "Login background set back to the default ship photos.");
    private void KioskBg_Choose(object sender, RoutedEventArgs e) => Pick("kiosk-bg", "Choose the kiosk background (1920 x 1080)");
    private void KioskBg_Reset(object sender, RoutedEventArgs e) => Undo("kiosk-bg", "Kiosk background set back to default.");
    private void Logo_Choose(object sender, RoutedEventArgs e) => Pick("login-logo", "Choose the main logo (square PNG)");
    private void Logo_Reset(object sender, RoutedEventArgs e) => Undo("login-logo", "Main logo set back to default.");
    private void Extra_Choose(object sender, RoutedEventArgs e) => Pick("extra-logo", "Choose the second logo (square PNG)");
    private void Extra_Remove(object sender, RoutedEventArgs e) => Undo("extra-logo", "Second logo removed.");

    private void LeftLogo_Choose(object sender, RoutedEventArgs e) => Pick("left-logo", "Choose the left side logo (square PNG)");
    private void LeftLogo_Remove(object sender, RoutedEventArgs e) => Undo("left-logo", "Left side logo set back to default.");

    private void AddParagraph_Click(object sender, RoutedEventArgs e)
    {
        if (_draft.Paragraphs.Count < BrandingState.MaxParagraphs)
            _draft.Paragraphs.Add(new ParagraphItem { Size = BrandingState.DefParagraphSize });
    }

    private void RemoveParagraph_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ParagraphItem p) _draft.Paragraphs.Remove(p);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var err = BrandingSource.Instance.Apply(_draft);
        if (err is null) { _draft.CopyFrom(BrandingSource.Instance); BrandingLock.Audit("BrandingSaved", null); }
        Show(err, "Saved and applied. The login and kiosk screens are updated.");
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        _draft.CopyFrom(BrandingSource.Instance);
        Show(null, "Changes discarded.");
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        _draft.ResetToDefaults();
        Show(null, "Everything set back to default in the preview. Press Save & apply to use it.");
    }
}


