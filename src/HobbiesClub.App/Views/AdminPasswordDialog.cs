using System.Windows;
using System.Windows.Controls;

namespace HobbiesClub.App.Views;

public class AdminPasswordDialog : Window
{
    private readonly PasswordBox _box = new() { Margin = new Thickness(0, 8, 0, 12), FontSize = 16 };

    public AdminPasswordDialog()
    {
        Title = "Super Admin password";
        Width = 340;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new Button { Content = "OK", Width = 80, Padding = new Thickness(0, 6, 0, 6), IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", Width = 80, Padding = new Thickness(0, 6, 0, 6), IsCancel = true };
        ok.Click += (_, _) => DialogResult = true;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Enter the Super Admin password to log out:", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_box);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) => _box.Focus();
    }

    public string Password => _box.Password;
}
