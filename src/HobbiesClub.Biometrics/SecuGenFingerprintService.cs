using HobbiesClub.Core.Interfaces;
using SecuGen.FDxSDKPro.Windows;

namespace HobbiesClub.Biometrics;

/// <summary>SecuGen Hamster Pro 20 (U20) through the FDx SDK Pro. One device, used from one thread at a time.</summary>
public sealed class SecuGenFingerprintService : IFingerprintService, IDisposable
{
    public const int CaptureTimeoutMs = 10000;
    public const int MinQuality = 40;
    private const int TimeoutCode = 54;

    private readonly object _lock = new();
    private SGFingerPrintManager? _fpm;
    private int _width;
    private int _height;
    private int _maxTemplate;

    public string LastError { get; private set; } = string.Empty;

    private bool EnsureOpen()
    {
        if (_fpm is not null) return true;

        var fpm = new SGFingerPrintManager();
        var err = fpm.Init(SGFPMDeviceName.DEV_AUTO);
        if (err != (int)SGFPMError.ERROR_NONE) { LastError = "Fingerprint scanner not ready. Check the USB cable."; return false; }

        err = fpm.OpenDevice((int)SGFPMPortAddr.USB_AUTO_DETECT);
        if (err != (int)SGFPMError.ERROR_NONE) { LastError = "Fingerprint scanner not connected. Check the USB cable."; return false; }

        var info = new SGFPMDeviceInfoParam();
        fpm.GetDeviceInfo(info);
        _width = info.ImageWidth;
        _height = info.ImageHeight;

        var max = 0;
        fpm.GetMaxTemplateSize(ref max);
        _maxTemplate = max;

        _fpm = fpm;
        LastError = string.Empty;
        return true;
    }

    private void Reset()
    {
        try { _fpm?.CloseDevice(); } catch { /* device already gone */ }
        _fpm = null;
    }

    public async Task<byte[]?> CaptureTemplateAsync(CancellationToken ct = default) => (await CaptureAsync(ct))?.Template;

    public Task<FingerCapture?> CaptureAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        lock (_lock)
        {
            if (!EnsureOpen()) return null;

            var image = new byte[_width * _height];
            var err = _fpm!.GetImageEx(image, CaptureTimeoutMs, 0, MinQuality);
            if (err != (int)SGFPMError.ERROR_NONE)
            {
                if (err == TimeoutCode) LastError = "No finger detected. Place your finger flat on the scanner.";
                else { LastError = "Scanner error. Please try again."; Reset(); }
                return null;
            }

            var quality = 0;
            _fpm.GetImageQuality(_width, _height, image, ref quality);
            if (quality < MinQuality) { LastError = $"Scan too light (quality {quality}%). Press a little firmer."; return null; }

            var finger = new SGFPMFingerInfo
            {
                FingerNumber = SGFPMFingerPosition.FINGPOS_UK,
                ImageQuality = (short)quality,
                ImpressionType = (short)SGFPMImpressionType.IMPTYPE_LP,
                ViewNumber = 1
            };

            var template = new byte[_maxTemplate];
            err = _fpm.CreateTemplate(finger, image, template);
            if (err != (int)SGFPMError.ERROR_NONE) { LastError = "Could not read the fingerprint. Try again."; return null; }

            LastError = string.Empty;
            return (FingerCapture?)new FingerCapture(template, image, _width, _height, quality);
        }
    }, ct);

    public bool IsMatch(byte[] captured, byte[] stored)
    {
        lock (_lock)
        {
            if (!EnsureOpen()) return false;
            var matched = false;
            var err = _fpm!.MatchTemplate(captured, stored, SGFPMSecurityLevel.NORMAL, ref matched);
            return err == (int)SGFPMError.ERROR_NONE && matched;
        }
    }

    public void Dispose()
    {
        lock (_lock) Reset();
    }
}
