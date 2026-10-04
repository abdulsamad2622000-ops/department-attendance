namespace HobbiesClub.Core.Interfaces;

/// <summary>One live camera update during a guided face scan.</summary>
public sealed record FaceScanUpdate(
    byte[]? Jpeg, int Width, int Height, int FaceCount,
    float[]? Box, float[]? Landmarks, int Step, int Steps, string Hint,
    bool Done = false, bool Failed = false);

public interface IFaceService
{
    /// <summary>Captures a face from the camera and returns a template. Null on failure.</summary>
    Task<byte[]?> CaptureTemplateAsync(CancellationToken ct = default);

    /// <summary>Whether two templates belong to the same person.</summary>
    bool IsMatch(byte[] captured, byte[] stored);

    /// <summary>JPEG of the last captured face (profile photo), or null.</summary>
    byte[]? LastCapturedPhotoJpeg { get; }

    /// <summary>Guided scan: look straight, then (with liveness) turn left and right. Reports live frames.</summary>
    async Task<byte[]?> CaptureGuidedAsync(bool liveness, IProgress<FaceScanUpdate>? progress, CancellationToken ct = default)
    {
        progress?.Report(new FaceScanUpdate(null, 0, 0, 0, null, null, 0, 1, "Scanning…"));
        var t = await CaptureTemplateAsync(ct);
        progress?.Report(new FaceScanUpdate(null, 0, 0, 0, null, null, 1, 1, t is null ? "Face not found" : "Face captured", Done: t is not null, Failed: t is null));
        return t;
    }
}
