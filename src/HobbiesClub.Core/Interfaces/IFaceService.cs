namespace HobbiesClub.Core.Interfaces;

public interface IFaceService
{
    /// <summary>Captures a face from the camera and returns a template. Null on failure.</summary>
    Task<byte[]?> CaptureTemplateAsync(CancellationToken ct = default);

    /// <summary>Whether two templates belong to the same person.</summary>
    bool IsMatch(byte[] captured, byte[] stored);

    /// <summary>JPEG of the last captured face (profile photo), or null.</summary>
    byte[]? LastCapturedPhotoJpeg { get; }
}

