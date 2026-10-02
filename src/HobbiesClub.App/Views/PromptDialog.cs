using System.Windows;
using System.Windows.Controls;

namespace HobbiesClub.App.Views;

/// <summary>Small text prompt used by admin actions (new PIN, termination reason).</summary>
public class PromptDialog : Window
{
    private readonly TextBox _box = new() { Margin = new Thickness(0, 8, 0, 12), FontSize = 16 };

    public PromptDialog(string title, string label, int maxLength = 200)
    {
        Title = title;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _box.MaxLength = maxLength;

        var ok = new Button { Content = "OK", Width = 80, Padding = new Thickness(0, 6, 0, 6), IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", Width = 80, Padding = new Thickness(0, 6, 0, 6), IsCancel = true };
        ok.Click += (_, _) => DialogResult = true;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_box);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) => _box.Focus();
    }

    public string Value => _box.Text.Trim();

    public static string? Ask(string title, string label, int maxLength = 200)
    {
        var d = new PromptDialog(title, label, maxLength) { Owner = Application.Current.MainWindow };
        return d.ShowDialog() == true ? d.Value : null;
    }
}
