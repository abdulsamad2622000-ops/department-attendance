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

    public const double CenterYaw = 0.12;
    public const double TurnYaw = 0.22;
    private const int HoldFrames = 3;

    public async Task<byte[]?> CaptureGuidedAsync(bool liveness, IProgress<FaceScanUpdate>? progress, CancellationToken ct = default)
    {
        await _cameraLock.WaitAsync(ct);
        try
        {
            return await Task.Run(() => Guided(liveness, progress, ct), ct);
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    /// <summary>Head turn: about 0 when facing the camera; sign shows the direction.</summary>
    private static double Yaw(float[] d)
    {
        var eyeMid = (d[4] + d[6]) / 2f;
        var eyeDist = Math.Abs(d[6] - d[4]);
        return eyeDist < 1 ? 0 : (d[8] - eyeMid) / eyeDist;
    }

    private (byte[] Template, byte[] Photo) Extract(Mat frame, Mat box)
    {
        using var aligned = new Mat();
        _recognizer.AlignCrop(frame, box, aligned);
        byte[] photo;
        using (var jpeg = new Emgu.CV.Util.VectorOfByte())
        {
            CvInvoke.Imencode(".jpg", aligned, jpeg);
            photo = jpeg.ToArray();
        }
        using var feature = new Mat();
        _recognizer.Feature(aligned, feature);
        var values = new float[FeatureLength];
        feature.CopyTo(values);
        Normalize(values);
        var bytes = new byte[FeatureLength * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return (bytes, photo);
    }

    private byte[]? Guided(bool liveness, IProgress<FaceScanUpdate>? progress, CancellationToken ct)
    {
        LastCapturedPhotoJpeg = null;
        var steps = liveness ? 3 : 1;

        using var cap = new VideoCapture(CameraIndex);
        if (!cap.IsOpened)
        {
            progress?.Report(new FaceScanUpdate(null, 0, 0, 0, null, null, 0, steps, "Camera not found. Check the camera.", Failed: true));
            return null;
        }

        using var frame = new Mat();
        for (var i = 0; i < 5; i++) cap.Read(frame);
        if (frame.IsEmpty) return null;

        using var detector = new FaceDetectorYN(_detectorModel, "", new Size(frame.Width, frame.Height), 0.9f, 0.3f, 5000);
        var deadline = DateTime.UtcNow + (liveness ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(8));
        int step = 0, hold = 0;
        byte[]? template = null, photo = null;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (!cap.Read(frame) || frame.IsEmpty) continue;

            using var faces = new Mat();
            detector.Detect(frame, faces);
            var count = faces.IsEmpty ? 0 : faces.Rows;

            float[]? d = null;
            if (count == 1)
            {
                d = new float[faces.Cols];
                using var row = faces.Row(0);
                row.CopyTo(d);
            }

            var ok = false;
            string hint;
            if (count == 0) hint = "Look at the camera";
            else if (count > 1) hint = "Only one person in front of the camera, please";
            else if (d![2] < frame.Width * 0.18) hint = "Move a little closer";
            else
            {
                var yaw = Yaw(d);
                switch (step)
                {
                    case 0: ok = Math.Abs(yaw) < CenterYaw; hint = ok ? "Hold still…" : "Look straight at the camera"; break;
                    case 1: ok = yaw > TurnYaw; hint = "Slowly turn your head to the LEFT"; break;
                    default: ok = yaw < -TurnYaw; hint = "Now turn your head to the RIGHT"; break;
                }
            }

            hold = ok ? hold + 1 : 0;
            if (hold >= HoldFrames)
            {
                if (step == 0 && template is null)
                {
                    using var box = faces.Row(0);
                    (template, photo) = Extract(frame, box);
                }
                step++;
                hold = 0;
            }

            var done = step >= steps && template is not null;
            if (progress is not null)
            {
                using var jpg = new Emgu.CV.Util.VectorOfByte();
                CvInvoke.Imencode(".jpg", frame, jpg);
                progress.Report(new FaceScanUpdate(jpg.ToArray(), frame.Width, frame.Height, count,
                    d is null ? null : d[..4], d is null ? null : d[4..14],
                    Math.Min(step, steps), steps, done ? "Face captured" : hint, Done: done));
            }

            if (done)
            {
                LastCapturedPhotoJpeg = photo;
                return template;
            }
        }

        progress?.Report(new FaceScanUpdate(null, 0, 0, 0, null, null, Math.Min(step, steps), steps, "Time is up. Please try again.", Failed: true));
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




