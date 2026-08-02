namespace DisplayLift;

internal enum RustScene
{
    Balanced,
    Temperate,
    Desert,
    Snow,
    Coast,
    NightInterior
}

internal static class RustSceneCatalog
{
    public static string GetName(RustScene scene) => scene switch
    {
        RustScene.Balanced => "Balanced",
        RustScene.Temperate => "Temperate / Forest",
        RustScene.Desert => "Desert",
        RustScene.Snow => "Snow",
        RustScene.Coast => "Coast / Water",
        RustScene.NightInterior => "Night / Interior",
        _ => scene.ToString()
    };

    public static string GetShortName(RustScene scene) => scene switch
    {
        RustScene.Balanced => "Balanced",
        RustScene.Temperate => "Temperate",
        RustScene.Desert => "Desert",
        RustScene.Snow => "Snow",
        RustScene.Coast => "Coast",
        RustScene.NightInterior => "Night",
        _ => scene.ToString()
    };

    public static string GetDescription(RustScene scene) => scene switch
    {
        RustScene.Balanced => "The clean all-purpose Rust look. This is also the fallback when the scene is visually mixed.",
        RustScene.Temperate => "Raises foliage separation and cool detail without turning grass into neon.",
        RustScene.Desert => "Cools the heavy yellow cast and adds firm edge contrast for sand, rocks and monuments.",
        RustScene.Snow => "Restrains bright snow while preserving silhouette contrast and colored clothing.",
        RustScene.Coast => "Keeps water and sky vivid while separating sand, rocks and shoreline detail.",
        RustScene.NightInterior => "Lifts midtones and shadows for night, caves and dark monument interiors.",
        _ => string.Empty
    };
}

internal sealed class VisualSettings
{
    public int VibrancePercent { get; init; }
    public int SaturationPercent { get; init; }
    public int ContrastPercent { get; init; }
    public int BrightnessPercent { get; init; }
    public int ExposureHundredths { get; init; }
    public int GammaPercent { get; init; }
    public int ShadowLiftPercent { get; init; }
    public int Temperature { get; init; }
    public int Tint { get; init; }
    public int RedGainPercent { get; init; }
    public int GreenGainPercent { get; init; }
    public int BlueGainPercent { get; init; }
    public bool UseNvidiaVibrance { get; init; }

    public string ToCompactString() =>
        $"Sat {SaturationPercent / 100.0:0.00}  •  Vib +{VibrancePercent / 100.0:0.00}  •  Bright {BrightnessPercent / 100.0:+0.00;-0.00;0.00}  •  Contrast {ContrastPercent / 100.0:0.00}  •  Gamma {GammaPercent / 100.0:0.00}";
}

internal static class RustVisualPresets
{
    public static VisualSettings Create(RustScene scene, AppSettings settings)
    {
        settings.Validate();
        var baseSettings = scene switch
        {
            RustScene.Temperate => New(84, 158, 108, 3, 2, 106, 10, -3, 1, 101, 101, 104),
            RustScene.Desert => New(78, 145, 111, 0, -3, 102, 8, -10, 2, 98, 100, 108),
            RustScene.Snow => New(70, 136, 114, -4, -7, 97, 5, 5, 1, 104, 100, 98),
            RustScene.Coast => New(82, 151, 109, 2, 1, 104, 8, -4, 1, 100, 100, 105),
            RustScene.NightInterior => New(64, 126, 97, 8, 18, 126, 34, -5, 0, 100, 101, 106),
            _ => New(80, 152, 105, 6, 0, 108, 8, -2, 1, 101, 100, 103)
        };

        var colorScale = settings.ColorStrengthPercent / 100.0;
        var contrastScale = settings.ContrastStrengthPercent / 100.0;
        var shadowScale = settings.ShadowAssistPercent / 100.0;

        return new VisualSettings
        {
            VibrancePercent = Math.Clamp((int)Math.Round(baseSettings.VibrancePercent * colorScale), 0, 100),
            SaturationPercent = Math.Clamp(100 + (int)Math.Round((baseSettings.SaturationPercent - 100) * colorScale), 80, 260),
            ContrastPercent = Math.Clamp(100 + (int)Math.Round((baseSettings.ContrastPercent - 100) * contrastScale), 80, 135),
            BrightnessPercent = Math.Clamp(baseSettings.BrightnessPercent + settings.BrightnessTrimPercent, -20, 20),
            ExposureHundredths = Math.Clamp(baseSettings.ExposureHundredths + settings.BrightnessTrimPercent / 2, -40, 45),
            GammaPercent = Math.Clamp(100 + (int)Math.Round((baseSettings.GammaPercent - 100) * shadowScale), 75, 155),
            ShadowLiftPercent = Math.Clamp((int)Math.Round(baseSettings.ShadowLiftPercent * shadowScale), 0, 60),
            Temperature = baseSettings.Temperature,
            Tint = baseSettings.Tint,
            RedGainPercent = baseSettings.RedGainPercent,
            GreenGainPercent = baseSettings.GreenGainPercent,
            BlueGainPercent = baseSettings.BlueGainPercent,
            UseNvidiaVibrance = settings.UseNvidiaVibrance
        };
    }

    private static VisualSettings New(
        int vibrance,
        int saturation,
        int contrast,
        int brightness,
        int exposure,
        int gamma,
        int shadows,
        int temperature,
        int tint,
        int red,
        int green,
        int blue) => new()
        {
            VibrancePercent = vibrance,
            SaturationPercent = saturation,
            ContrastPercent = contrast,
            BrightnessPercent = brightness,
            ExposureHundredths = exposure,
            GammaPercent = gamma,
            ShadowLiftPercent = shadows,
            Temperature = temperature,
            Tint = tint,
            RedGainPercent = red,
            GreenGainPercent = green,
            BlueGainPercent = blue,
            UseNvidiaVibrance = true
        };
}
