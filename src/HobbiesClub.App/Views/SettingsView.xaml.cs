using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HobbiesClub.App.Helpers;

namespace HobbiesClub.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => Fill();
    }

    private void Fill()
    {
        var l = FieldLabelsSource.Instance;
        TxtName.Text = l.Name; TxtUnit.Text = l.Unit; TxtRank.Text = l.Rank; TxtRoll.Text = l.Roll; TxtPhone.Text = l.Phone;
    }

    private void Show(bool ok, string okText)
    {
        Msg.Text = ok ? okText : "Could not save the settings file.";
        Msg.Foreground = (Brush)FindResource(ok ? "SuccessBrush" : "DangerBrush");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var ok = FieldLabelsSource.Instance.Save(TxtName.Text, TxtUnit.Text, TxtRank.Text, TxtRoll.Text, TxtPhone.Text);
        Fill();
        Show(ok, "Saved. New labels are live.");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var ok = FieldLabelsSource.Instance.Save(null, null, null, null, null);
        Fill();
        Show(ok, "Labels reset to default.");
    }
}
