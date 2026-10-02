namespace HobbiesClub.Core.Interfaces;

public interface IFingerprintService
{
    /// <summary>Captures one scan from the scanner and returns a template. Null on failure.</summary>
    Task<byte[]?> CaptureTemplateAsync(CancellationToken ct = default);

    /// <summary>Whether two templates belong to the same finger.</summary>
    bool IsMatch(byte[] captured, byte[] stored);
}
