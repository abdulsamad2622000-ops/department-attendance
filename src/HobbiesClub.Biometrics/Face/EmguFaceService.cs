using System.Drawing;
using Emgu.CV;
using HobbiesClub.Core.Interfaces;

namespace HobbiesClub.Biometrics.Face;

/// <summary>
/// Real face recognition with any USB/built-in camera (laptop camera, Logitech C920).
/// YuNet finds the face, SFace turns it into 128 numbers. Works fully offline.
/// </summary>
public sealed class EmguFaceService : IFaceService, IDisposable
{
    public const double MatchThreshold = 0.363;
    private const int FeatureLength = 128;
    private static readonly TimeSpan CaptureWindow = TimeSpan.FromSeconds(4);

    private readonly string _detectorModel;
    private readonly FaceRecognizerSF _recognizer;
    private readonly SemaphoreSlim _cameraLock = new(1, 1);

    public int CameraIndex { get; set; }

    public byte[]? LastCapturedPhotoJpeg { get; private set; }

    public EmguFaceService(int cameraIndex = 0)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "FaceModels");
        _detectorModel = Path.Combine(dir, "face_detection_yunet_2023mar.onnx");
        _recognizer = new FaceRecognizerSF(
            Path.Combine(dir, "face_recognition_sface_2021dec.onnx"), "",
            Emgu.CV.Dnn.Backend.Default, Emgu.CV.Dnn.Target.Cpu);
        CameraIndex = cameraIndex;
    }

    /// <summary>Returns the face template, or null if there is no camera or no single clear face.</summary>
    public async Task<byte[]?> CaptureTemplateAsync(CancellationToken ct = default)
    {
        await _cameraLock.WaitAsync(ct);
        try
        {
            return await Task.Run(() => Capture(ct), ct);
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    private byte[]? Capture(CancellationToken ct)
    {
        LastCapturedPhotoJpeg = null;
        using var cap = new VideoCapture(CameraIndex);
        if (!cap.IsOpened)
            return null;

        using var frame = new Mat();
        for (var i = 0; i < 10; i++) cap.Read(frame); // let the camera adjust light
        if (frame.IsEmpty)
            return null;

        using var detector = new FaceDetectorYN(_detectorModel, "", new Size(frame.Width, frame.Height), 0.9f, 0.3f, 5000);
        var deadline = DateTime.UtcNow + CaptureWindow;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (!cap.Read(frame) || frame.IsEmpty)
                continue;

            using var faces = new Mat();
            detector.Detect(frame, faces);
            if (faces.Rows != 1)
                continue; // nobody, or more than one person in front of the camera

            using var box = faces.Row(0);
            using var aligned = new Mat();
            _recognizer.AlignCrop(frame, box, aligned);
            using (var jpeg = new Emgu.CV.Util.VectorOfByte())
            {
                CvInvoke.Imencode(".jpg", aligned, jpeg);
                LastCapturedPhotoJpeg = jpeg.ToArray();
            }
            using var feature = new Mat();
            _recognizer.Feature(aligned, feature);

            var values = new float[FeatureLength];
            feature.CopyTo(values);
            Normalize(values);

            var bytes = new byte[FeatureLength * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        return null;
    }

    public bool IsMatch(byte[] captured, byte[] stored) => Similarity(captured, stored) >= MatchThreshold;

    /// <summary>Cosine similarity of two templates; -1 if either is not a real face template.</summary>
    public static double Similarity(byte[] a, byte[] b)
    {
        const int size = FeatureLength * sizeof(float);
        if (a is null || b is null || a.Length != size || b.Length != size)
            return -1;

        var x = new float[FeatureLength];
        var y = new float[FeatureLength];
        Buffer.BlockCopy(a, 0, x, 0, size);
        Buffer.BlockCopy(b, 0, y, 0, size);

        double dot = 0, nx = 0, ny = 0;
        for (var i = 0; i < FeatureLength; i++)
        {
            dot += x[i] * y[i];
            nx += x[i] * x[i];
            ny += y[i] * y[i];
        }
        return nx == 0 || ny == 0 ? -1 : dot / (Math.Sqrt(nx) * Math.Sqrt(ny));
    }

    private static void Normalize(float[] v)
    {
        double sum = 0;
        foreach (var f in v) sum += f * f;
        var len = Math.Sqrt(sum);
        if (len == 0) return;
        for (var i = 0; i < v.Length; i++) v[i] = (float)(v[i] / len);
    }

    public void Dispose()
    {
        _recognizer.Dispose();
        _cameraLock.Dispose();
    }
}



