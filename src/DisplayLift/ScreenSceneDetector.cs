using System.Drawing.Imaging;

namespace DisplayLift;

internal sealed record SceneMetrics(
    double MeanLuminance,
    double MeanSaturation,
    double DarkRatio,
    double BrightRatio,
    double WhiteRatio,
    double WarmRatio,
    double GreenRatio,
    double BlueRatio,
    double LowerBlueRatio,
    double GrayRatio,
    double LuminanceVariance);

internal sealed record SceneAnalysis(
    RustScene Scene,
    double Confidence,
    SceneMetrics Metrics,
    IReadOnlyDictionary<RustScene, double> Scores,
    string Summary);

internal static class SceneClassifier
{
    public static SceneAnalysis Classify(SceneMetrics metrics)
    {
        var night = 0.05
            + metrics.DarkRatio * 1.35
            + Math.Max(0.0, 0.34 - metrics.MeanLuminance) * 2.2
            - metrics.BrightRatio * 0.45;

        var snow = 0.02
            + metrics.WhiteRatio * 1.55
            + metrics.BrightRatio * 0.65
            + metrics.BlueRatio * 0.12
            - metrics.WarmRatio * 0.45
            - metrics.GreenRatio * 0.42
            - metrics.DarkRatio * 0.35;

        var desert = 0.04
            + metrics.WarmRatio * 1.42
            + metrics.MeanSaturation * 0.18
            + Math.Max(0.0, metrics.MeanLuminance - 0.32) * 0.20
            - metrics.GreenRatio * 0.90
            - metrics.WhiteRatio * 0.30
            - metrics.DarkRatio * 0.22;

        var temperate = 0.05
            + metrics.GreenRatio * 1.62
            + metrics.MeanSaturation * 0.22
            - metrics.WhiteRatio * 0.34
            - metrics.DarkRatio * 0.20
            - metrics.WarmRatio * 0.08;

        var coast = 0.03
            + metrics.LowerBlueRatio * 1.85
            + metrics.BlueRatio * 0.22
            + metrics.WarmRatio * 0.12
            - metrics.GreenRatio * 0.22
            - metrics.DarkRatio * 0.28;

        var balanced = 0.30
            + Math.Max(0.0, 0.20 - Math.Abs(metrics.MeanSaturation - 0.35)) * 0.35
            + metrics.LuminanceVariance * 0.18;

        var scores = new Dictionary<RustScene, double>
        {
            [RustScene.NightInterior] = ClampScore(night),
            [RustScene.Snow] = ClampScore(snow),
            [RustScene.Desert] = ClampScore(desert),
            [RustScene.Temperate] = ClampScore(temperate),
            [RustScene.Coast] = ClampScore(coast),
            [RustScene.Balanced] = ClampScore(balanced)
        };

        var ranked = scores.OrderByDescending(pair => pair.Value).ToArray();
        var scene = ranked[0].Key;
        var top = ranked[0].Value;
        var second = ranked[1].Value;

        if (top < 0.42 || top - second < 0.055)
        {
            scene = RustScene.Balanced;
        }

        var confidence = scene == RustScene.Balanced && ranked[0].Key != RustScene.Balanced
            ? 0.42
            : Math.Clamp(0.36 + top * 0.20 + (top - second) * 0.62, 0.0, 0.98);

        var summary = $"Light {metrics.MeanLuminance:P0}  •  Color {metrics.MeanSaturation:P0}  •  Dark {metrics.DarkRatio:P0}  •  Warm {metrics.WarmRatio:P0}  •  Green {metrics.GreenRatio:P0}  •  Blue {metrics.LowerBlueRatio:P0}";
        return new SceneAnalysis(scene, confidence, metrics, scores, summary);
    }

    private static double ClampScore(double value) => Math.Clamp(value, 0.0, 2.5);
}

internal sealed record StabilizedScene(RustScene Scene, double Confidence, bool Changed, string Status);

internal sealed class SceneStabilizer
{
    private readonly Dictionary<RustScene, double> _smoothedScores = Enum.GetValues<RustScene>()
        .ToDictionary(scene => scene, _ => 0.0);
    private RustScene _current = RustScene.Balanced;
    private RustScene _pending = RustScene.Balanced;
    private int _pendingCount;
    private DateTime _lastSwitchUtc = DateTime.MinValue;
    private bool _initialized;

    public void Reset(RustScene initial = RustScene.Balanced)
    {
        foreach (var scene in _smoothedScores.Keys.ToArray()) _smoothedScores[scene] = 0.0;
        _current = initial;
        _pending = initial;
        _pendingCount = 0;
        _lastSwitchUtc = DateTime.MinValue;
        _initialized = false;
    }

    public StabilizedScene Update(SceneAnalysis analysis, int sensitivityPercent)
    {
        var alpha = 0.34;
        foreach (var scene in _smoothedScores.Keys.ToArray())
        {
            analysis.Scores.TryGetValue(scene, out var raw);
            _smoothedScores[scene] = _smoothedScores[scene] * (1.0 - alpha) + raw * alpha;
        }

        var ranked = _smoothedScores.OrderByDescending(pair => pair.Value).ToArray();
        var candidate = ranked[0].Key;
        var top = ranked[0].Value;
        var second = ranked[1].Value;
        var confidence = Math.Clamp(0.34 + top * 0.19 + (top - second) * 0.64, 0.0, 0.98);
        var threshold = 0.48 + Math.Clamp(sensitivityPercent, 35, 85) / 100.0 * 0.20;

        if (!_initialized)
        {
            _initialized = true;
            _current = candidate;
            _pending = candidate;
            _lastSwitchUtc = DateTime.UtcNow;
            return new StabilizedScene(_current, confidence, true, "Initial scene lock");
        }

        if (candidate == _current)
        {
            _pending = candidate;
            _pendingCount = 0;
            return new StabilizedScene(_current, confidence, false, "Scene stable");
        }

        if (candidate != _pending)
        {
            _pending = candidate;
            _pendingCount = 1;
        }
        else
        {
            _pendingCount++;
        }

        var requiredSamples = candidate == RustScene.NightInterior ? 2 : 3;
        var dwellElapsed = DateTime.UtcNow - _lastSwitchUtc >= TimeSpan.FromSeconds(3.5);
        if (_pendingCount >= requiredSamples && confidence >= threshold && dwellElapsed)
        {
            _current = candidate;
            _pendingCount = 0;
            _lastSwitchUtc = DateTime.UtcNow;
            return new StabilizedScene(_current, confidence, true, "Scene changed after stable confirmation");
        }

        return new StabilizedScene(_current, confidence, false, $"Checking {RustSceneCatalog.GetShortName(candidate)} ({_pendingCount}/{requiredSamples})");
    }
}

internal sealed class ScreenSceneDetector : IDisposable
{
    private const int PatchWidth = 56;
    private const int PatchHeight = 32;
    private const int Columns = 3;
    private const int Rows = 2;
    private readonly Bitmap _sampleBitmap = new(PatchWidth * Columns, PatchHeight * Rows, PixelFormat.Format24bppRgb);
    private bool _disposed;

    public SceneAnalysis Analyze(Rectangle screenBounds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (screenBounds.Width < 400 || screenBounds.Height < 300)
            throw new InvalidOperationException("The Rust display area is too small for automatic scene detection.");

        using (var graphics = Graphics.FromImage(_sampleBitmap))
        {
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;

            var xs = new[] { 0.22, 0.50, 0.78 };
            var ys = new[] { 0.38, 0.68 };
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var centerX = screenBounds.Left + (int)Math.Round(screenBounds.Width * xs[column]);
                    var centerY = screenBounds.Top + (int)Math.Round(screenBounds.Height * ys[row]);
                    var sourceX = Math.Clamp(centerX - PatchWidth / 2, screenBounds.Left, screenBounds.Right - PatchWidth);
                    var sourceY = Math.Clamp(centerY - PatchHeight / 2, screenBounds.Top, screenBounds.Bottom - PatchHeight);
                    graphics.CopyFromScreen(
                        sourceX,
                        sourceY,
                        column * PatchWidth,
                        row * PatchHeight,
                        new Size(PatchWidth, PatchHeight),
                        CopyPixelOperation.SourceCopy);
                }
            }
        }

        var metrics = Measure(_sampleBitmap);
        if (metrics.LuminanceVariance < 0.00015 && metrics.MeanLuminance < 0.02)
            throw new InvalidOperationException("Windows returned a blank screen sample. Use Rust borderless-windowed mode or choose a region manually.");
        return SceneClassifier.Classify(metrics);
    }

    private static SceneMetrics Measure(Bitmap bitmap)
    {
        var total = bitmap.Width * bitmap.Height;
        var lowerStart = bitmap.Height / 2;
        double luminanceSum = 0;
        double saturationSum = 0;
        double luminanceSquaredSum = 0;
        var dark = 0;
        var bright = 0;
        var white = 0;
        var warm = 0;
        var green = 0;
        var blue = 0;
        var lowerBlue = 0;
        var lowerPixels = bitmap.Width * (bitmap.Height - lowerStart);
        var gray = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                var red = color.R / 255.0;
                var greenChannel = color.G / 255.0;
                var blueChannel = color.B / 255.0;
                var max = Math.Max(red, Math.Max(greenChannel, blueChannel));
                var min = Math.Min(red, Math.Min(greenChannel, blueChannel));
                var delta = max - min;
                var saturation = max <= 0.0001 ? 0.0 : delta / max;
                var luminance = 0.2126 * red + 0.7152 * greenChannel + 0.0722 * blueChannel;
                var hue = GetHue(red, greenChannel, blueChannel, max, delta);

                luminanceSum += luminance;
                luminanceSquaredSum += luminance * luminance;
                saturationSum += saturation;
                if (luminance < 0.24) dark++;
                if (luminance > 0.72) bright++;
                if (luminance > 0.68 && saturation < 0.22) white++;
                if (saturation < 0.13) gray++;

                if (saturation > 0.16 && luminance > 0.10)
                {
                    if (hue >= 18 && hue <= 67) warm++;
                    if (hue >= 70 && hue <= 165) green++;
                    if (hue >= 175 && hue <= 250)
                    {
                        blue++;
                        if (y >= lowerStart) lowerBlue++;
                    }
                }
            }
        }

        var meanLuminance = luminanceSum / total;
        return new SceneMetrics(
            meanLuminance,
            saturationSum / total,
            dark / (double)total,
            bright / (double)total,
            white / (double)total,
            warm / (double)total,
            green / (double)total,
            blue / (double)total,
            lowerBlue / (double)Math.Max(1, lowerPixels),
            gray / (double)total,
            Math.Max(0.0, luminanceSquaredSum / total - meanLuminance * meanLuminance));
    }

    private static double GetHue(double red, double green, double blue, double max, double delta)
    {
        if (delta < 0.0001) return 0.0;
        double hue;
        if (Math.Abs(max - red) < 0.0001) hue = 60.0 * (((green - blue) / delta) % 6.0);
        else if (Math.Abs(max - green) < 0.0001) hue = 60.0 * (((blue - red) / delta) + 2.0);
        else hue = 60.0 * (((red - green) / delta) + 4.0);
        return hue < 0 ? hue + 360.0 : hue;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _sampleBitmap.Dispose();
        _disposed = true;
    }
}
