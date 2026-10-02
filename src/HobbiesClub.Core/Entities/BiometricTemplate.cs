using HobbiesClub.Core.Enums;

namespace HobbiesClub.Core.Entities;

public class BiometricTemplate
{
    public int Id { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public BiometricType Type { get; set; }

    /// <summary>Fingerprint only: which finger (0-9). Null for face.</summary>
    public int? FingerIndex { get; set; }

    public byte[] TemplateData { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; }
}
