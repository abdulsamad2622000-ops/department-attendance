using HobbiesClub.Core.Interfaces;

namespace HobbiesClub.Biometrics.Fake;

/// <summary>For development without hardware. Set NextTemplate and it is returned as the "scan".</summary>
public class FakeFaceService : IFaceService
{
    public byte[]? NextTemplate { get; set; }

    public byte[]? LastCapturedPhotoJpeg => null;

    public Task<byte[]?> CaptureTemplateAsync(CancellationToken ct = default) =>
        Task.FromResult(NextTemplate);

    public bool IsMatch(byte[] captured, byte[] stored) =>
        captured.AsSpan().SequenceEqual(stored);
}

