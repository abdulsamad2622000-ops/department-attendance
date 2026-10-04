namespace HobbiesClub.Core;

/// <summary>Finger positions 0-9: right thumb..little = 0..4, left thumb..little = 5..9.</summary>
public static class FingerNames
{
    private static readonly string[] Names =
    {
        "Right thumb", "Right index", "Right middle", "Right ring", "Right little",
        "Left thumb", "Left index", "Left middle", "Left ring", "Left little"
    };

    public static string Name(int index) => index is >= 0 and <= 9 ? Names[index] : "Finger";
}
