using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace HobbiesClub.App.Views;

public partial class PasswordField : UserControl
{
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
        nameof(Password), typeof(string), typeof(PasswordField),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

    private bool _updating;

    public PasswordField() => InitializeComponent();

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (PasswordField)d;
        if (field._updating) return;

        field._updating = true;
        var value = e.NewValue as string ?? string.Empty;
        if (field.HiddenBox.Password != value) field.HiddenBox.Password = value;
        if (field.VisibleBox.Text != value) field.VisibleBox.Text = value;
        field._updating = false;
    }

    private void OnHiddenChanged(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        VisibleBox.Text = HiddenBox.Password;
        Password = HiddenBox.Password;
        _updating = false;
    }

    private void OnVisibleChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        HiddenBox.Password = VisibleBox.Text;
        Password = VisibleBox.Text;
        _updating = false;
    }

    private void OnToggle(object sender, RoutedEventArgs e)
    {
        bool show = ShowCheck.IsChecked == true;
        VisibleBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        HiddenBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;

        if (show)
        {
            VisibleBox.Focus();
            VisibleBox.CaretIndex = VisibleBox.Text.Length;
        }
        else
        {
            HiddenBox.Focus();
        }
    }
}
