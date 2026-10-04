namespace HobbiesClub.Core.Interfaces;

/// <summary>One fingerprint scan: the template plus the raw 8-bit grey image for display.</summary>
public sealed record FingerCapture(byte[] Template, byte[]? Image, int Width, int Height, int Quality);

public interface IFingerprintService
{
    /// <summary>Captures one scan from the scanner and returns a template. Null on failure.</summary>
    Task<byte[]?> CaptureTemplateAsync(CancellationToken ct = default);

    /// <summary>Whether two templates belong to the same finger.</summary>
    bool IsMatch(byte[] captured, byte[] stored);

    /// <summary>Captures one scan with its image and quality. Scanners without images return the template only.</summary>
    async Task<FingerCapture?> CaptureAsync(CancellationToken ct = default)
    {
        var t = await CaptureTemplateAsync(ct);
        return t is { Length: > 0 } ? new FingerCapture(t, null, 0, 0, 0) : null;
    }

    /// <summary>Why the last capture failed, for the user.</summary>
    string LastError => string.Empty;
}
