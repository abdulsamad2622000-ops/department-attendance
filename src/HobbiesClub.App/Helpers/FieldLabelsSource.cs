using System.ComponentModel;
using System.IO;
using System.Text.Json;
using HobbiesClub.Data;

namespace HobbiesClub.App.Helpers;

/// <summary>Editable labels for member detail fields, kept in field-labels.json next to the database folder.</summary>
public sealed class FieldLabelsSource : INotifyPropertyChanged
{
    public static FieldLabelsSource Instance { get; } = new();

    public const string DefName = "Full name";
    public const string DefUnit = "Unit";
    public const string DefRank = "Rank";
    public const string DefRoll = "R/N (Roll / Regiment No.)";
    public const string DefPhone = "Phone";

    private sealed class Data
    {
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public string? Rank { get; set; }
        public string? Roll { get; set; }
        public string? Phone { get; set; }
    }

    private static string FilePath => Path.Combine(AppPaths.Root, "field-labels.json");

    public string Name { get; private set; } = DefName;
    public string Unit { get; private set; } = DefUnit;
    public string Rank { get; private set; } = DefRank;
    public string Roll { get; private set; } = DefRoll;
    public string Phone { get; private set; } = DefPhone;

    public event PropertyChangedEventHandler? PropertyChanged;

    private FieldLabelsSource()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var d = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath));
                if (d != null) Apply(d.Name, d.Unit, d.Rank, d.Roll, d.Phone);
            }
        }
        catch { /* fall back to defaults */ }
    }

    private static string Pick(string? v, string def) => string.IsNullOrWhiteSpace(v) ? def : v.Trim();

    private void Apply(string? name, string? unit, string? rank, string? roll, string? phone)
    {
        Name = Pick(name, DefName);
        Unit = Pick(unit, DefUnit);
        Rank = Pick(rank, DefRank);
        Roll = Pick(roll, DefRoll);
        Phone = Pick(phone, DefPhone);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>Saves new labels (blank = default). Returns false if the file could not be written.</summary>
    public bool Save(string? name, string? unit, string? rank, string? roll, string? phone)
    {
        Apply(name, unit, rank, roll, phone);
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Data { Name = Name, Unit = Unit, Rank = Rank, Roll = Roll, Phone = Phone }));
            return true;
        }
        catch { return false; }
    }
}
