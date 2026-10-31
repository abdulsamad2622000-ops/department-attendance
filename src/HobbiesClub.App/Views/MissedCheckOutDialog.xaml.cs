using System.Windows;

namespace HobbiesClub.App.Views;

public partial class MissedCheckOutDialog : Window
{
    private readonly DateTime _checkIn;

    public DateTime? Result { get; private set; }
    public string? NoteText { get; private set; }

    public MissedCheckOutDialog(DateTime missedCheckIn, string? clubName = null)
    {
        InitializeComponent();
        _checkIn = missedCheckIn;
        Heading.Text = clubName is null ? $"You did not check out on {missedCheckIn:dd MMM}." : $"You did not check out of {clubName} on {missedCheckIn:dd MMM}.";

        for (var h = 1; h <= 12; h++) HourBox.Items.Add(h);
        for (var m = 0; m < 60; m += 5) MinuteBox.Items.Add(m.ToString("00"));
        AmPmBox.Items.Add("AM");
        AmPmBox.Items.Add("PM");

        var h12 = missedCheckIn.Hour % 12 == 0 ? 12 : missedCheckIn.Hour % 12;
        HourBox.SelectedItem = h12;
        MinuteBox.SelectedIndex = 0;
        AmPmBox.SelectedIndex = missedCheckIn.Hour >= 12 ? 1 : 0;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (HourBox.SelectedItem is not int hour || MinuteBox.SelectedItem is not string min || AmPmBox.SelectedItem is not string ampm)
        {
            ErrorText.Text = "Please choose the time.";
            return;
        }

        var h24 = hour % 12 + (ampm == "PM" ? 12 : 0);
        var value = _checkIn.Date.AddHours(h24).AddMinutes(int.Parse(min));

        if (value < _checkIn)
        {
            ErrorText.Text = $"The time cannot be before your check-in ({_checkIn:hh:mm tt}).";
            return;
        }

        NoteText = string.IsNullOrWhiteSpace(NoteBox.Text) ? null : NoteBox.Text.Trim();
        Result = value;
        DialogResult = true;
    }
}


