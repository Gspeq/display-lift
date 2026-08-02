using System.Text.Json.Serialization;

namespace DisplayLift;

internal enum ProfileTrigger
{
    Foreground,
    WhileRunning
}

internal enum RustVisualPreset
{
    CleanRust,
    Summer,
    Winter,
    Desert,
    Night,
    Competitive,
    MaximumColor,
    Neutral
}

internal static class RustPresetCatalog
{
    public static string GetName(RustVisualPreset preset) => preset switch
    {
        RustVisualPreset.CleanRust => "Clean Rust",
        RustVisualPreset.Summer => "Summer",
        RustVisualPreset.Winter => "Winter",
        RustVisualPreset.Desert => "Desert",
        RustVisualPreset.Night => "Night",
        RustVisualPreset.Competitive => "Competitive",
        RustVisualPreset.MaximumColor => "Maximum Color",
        RustVisualPreset.Neutral => "Neutral",
        _ => preset.ToString()
    };

    public static string GetDescription(RustVisualPreset preset) => preset switch
    {
        RustVisualPreset.CleanRust => "Balanced color and tone using the same public demo values advertised by the closest Rust visual-panel example: 1.52 saturation, +0.80 vibrance, +0.06 brightness and 1.05 contrast.",
        RustVisualPreset.Summer => "Strong foliage separation with controlled highlights and a mild shadow lift.",
        RustVisualPreset.Winter => "Extra contrast and vibrance while restraining snow brightness so silhouettes remain readable.",
        RustVisualPreset.Desert => "Reduces the yellow cast, adds edge contrast and keeps dark rocks and monuments readable.",
        RustVisualPreset.Night => "Raises midtones, exposure and shadows for dark interiors and nighttime roaming without crushing highlights.",
        RustVisualPreset.Competitive => "The strongest balanced Rust preset: vivid colors, cool separation, firm contrast and lifted dark detail.",
        RustVisualPreset.MaximumColor => "Very aggressive saturation and driver vibrance for the most colorful possible image.",
        RustVisualPreset.Neutral => "Restores neutral color, brightness, exposure, contrast and gamma values.",
        _ => string.Empty
    };
}

internal sealed class DisplayProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New profile";
    public bool Enabled { get; set; } = true;
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public ProfileTrigger Trigger { get; set; } = ProfileTrigger.Foreground;
    public int Priority { get; set; } = 50;
    public bool RestoreOnDeactivate { get; set; } = true;

    public bool UseNvidiaVibrance { get; set; } = true;
    public int VibrancePercent { get; set; } = 80;
    public int SaturationPercent { get; set; } = 152;
    public int ContrastPercent { get; set; } = 105;
    public int BrightnessPercent { get; set; } = 6;
    public int ExposureHundredths { get; set; } = 0;
    public int GammaPercent { get; set; } = 108;
    public int ShadowLiftPercent { get; set; } = 8;
    public int Temperature { get; set; } = -2;
    public int Tint { get; set; } = 1;
    public int RedGainPercent { get; set; } = 101;
    public int GreenGainPercent { get; set; } = 100;
    public int BlueGainPercent { get; set; } = 103;
    public RustVisualPreset LastPreset { get; set; } = RustVisualPreset.CleanRust;

    // Migration bridge for V6 profiles. It is omitted when writing V7 JSON.
    [JsonPropertyName("NvidiaVibrancePercent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LegacyNvidiaVibrancePercent
    {
        get => 0;
        set
        {
            if (value > 0)
            {
                VibrancePercent = Math.Clamp((value - 50) * 2, 0, 100);
            }
        }
    }

    public override string ToString() => Name;

    public DisplayProfile Clone(string? name = null)
    {
        return new DisplayProfile
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"{Name} copy",
            Enabled = Enabled,
            ProcessName = ProcessName,
            ExecutablePath = ExecutablePath,
            Trigger = Trigger,
            Priority = Priority,
            RestoreOnDeactivate = RestoreOnDeactivate,
            UseNvidiaVibrance = UseNvidiaVibrance,
            VibrancePercent = VibrancePercent,
            SaturationPercent = SaturationPercent,
            ContrastPercent = ContrastPercent,
            BrightnessPercent = BrightnessPercent,
            ExposureHundredths = ExposureHundredths,
            GammaPercent = GammaPercent,
            ShadowLiftPercent = ShadowLiftPercent,
            Temperature = Temperature,
            Tint = Tint,
            RedGainPercent = RedGainPercent,
            GreenGainPercent = GreenGainPercent,
            BlueGainPercent = BlueGainPercent,
            LastPreset = LastPreset
        };
    }

    public static DisplayProfile CreateRust(string executablePath = "")
    {
        var profile = new DisplayProfile
        {
            Name = "Rust - Clean Visibility",
            Enabled = true,
            ProcessName = "RustClient",
            ExecutablePath = executablePath,
            Trigger = ProfileTrigger.Foreground,
            Priority = 100,
            RestoreOnDeactivate = true,
            UseNvidiaVibrance = true
        };
        profile.ApplyPreset(RustVisualPreset.CleanRust);
        return profile;
    }

    public void ApplyPreset(RustVisualPreset preset)
    {
        LastPreset = preset;
        switch (preset)
        {
            case RustVisualPreset.CleanRust:
                VibrancePercent = 80;
                SaturationPercent = 152;
                BrightnessPercent = 6;
                ContrastPercent = 105;
                ExposureHundredths = 0;
                GammaPercent = 108;
                ShadowLiftPercent = 8;
                Temperature = -2;
                Tint = 1;
                RedGainPercent = 101;
                GreenGainPercent = 100;
                BlueGainPercent = 103;
                break;
            case RustVisualPreset.Summer:
                VibrancePercent = 82;
                SaturationPercent = 160;
                BrightnessPercent = 4;
                ContrastPercent = 106;
                ExposureHundredths = 3;
                GammaPercent = 106;
                ShadowLiftPercent = 10;
                Temperature = -3;
                Tint = 1;
                RedGainPercent = 101;
                GreenGainPercent = 101;
                BlueGainPercent = 104;
                break;
            case RustVisualPreset.Winter:
                VibrancePercent = 88;
                SaturationPercent = 148;
                BrightnessPercent = -2;
                ContrastPercent = 112;
                ExposureHundredths = -6;
                GammaPercent = 98;
                ShadowLiftPercent = 6;
                Temperature = 5;
                Tint = 1;
                RedGainPercent = 104;
                GreenGainPercent = 100;
                BlueGainPercent = 98;
                break;
            case RustVisualPreset.Desert:
                VibrancePercent = 76;
                SaturationPercent = 146;
                BrightnessPercent = 1;
                ContrastPercent = 110;
                ExposureHundredths = -2;
                GammaPercent = 102;
                ShadowLiftPercent = 9;
                Temperature = -10;
                Tint = 2;
                RedGainPercent = 99;
                GreenGainPercent = 100;
                BlueGainPercent = 107;
                break;
            case RustVisualPreset.Night:
                VibrancePercent = 68;
                SaturationPercent = 128;
                BrightnessPercent = 8;
                ContrastPercent = 98;
                ExposureHundredths = 22;
                GammaPercent = 128;
                ShadowLiftPercent = 36;
                Temperature = -5;
                Tint = 0;
                RedGainPercent = 100;
                GreenGainPercent = 101;
                BlueGainPercent = 106;
                break;
            case RustVisualPreset.Competitive:
                VibrancePercent = 92;
                SaturationPercent = 168;
                BrightnessPercent = 4;
                ContrastPercent = 109;
                ExposureHundredths = 6;
                GammaPercent = 112;
                ShadowLiftPercent = 16;
                Temperature = -5;
                Tint = 2;
                RedGainPercent = 102;
                GreenGainPercent = 100;
                BlueGainPercent = 107;
                break;
            case RustVisualPreset.MaximumColor:
                VibrancePercent = 100;
                SaturationPercent = 225;
                BrightnessPercent = 5;
                ContrastPercent = 112;
                ExposureHundredths = 8;
                GammaPercent = 115;
                ShadowLiftPercent = 20;
                Temperature = -6;
                Tint = 3;
                RedGainPercent = 105;
                GreenGainPercent = 100;
                BlueGainPercent = 111;
                break;
            case RustVisualPreset.Neutral:
                VibrancePercent = 0;
                SaturationPercent = 100;
                BrightnessPercent = 0;
                ContrastPercent = 100;
                ExposureHundredths = 0;
                GammaPercent = 100;
                ShadowLiftPercent = 0;
                Temperature = 0;
                Tint = 0;
                RedGainPercent = 100;
                GreenGainPercent = 100;
                BlueGainPercent = 100;
                break;
        }
    }

    public void Validate()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "Unnamed profile" : Name.Trim();
        ProcessName = NormalizeProcessName(ProcessName);
        Priority = Math.Clamp(Priority, 0, 999);
        VibrancePercent = Math.Clamp(VibrancePercent, 0, 100);
        SaturationPercent = Math.Clamp(SaturationPercent, 0, 300);
        ContrastPercent = Math.Clamp(ContrastPercent, 50, 150);
        BrightnessPercent = Math.Clamp(BrightnessPercent, -20, 20);
        ExposureHundredths = Math.Clamp(ExposureHundredths, -100, 100);
        GammaPercent = Math.Clamp(GammaPercent, 60, 180);
        ShadowLiftPercent = Math.Clamp(ShadowLiftPercent, 0, 60);
        Temperature = Math.Clamp(Temperature, -100, 100);
        Tint = Math.Clamp(Tint, -100, 100);
        RedGainPercent = Math.Clamp(RedGainPercent, 70, 130);
        GreenGainPercent = Math.Clamp(GreenGainPercent, 70, 130);
        BlueGainPercent = Math.Clamp(BlueGainPercent, 70, 130);
    }

    public static string NormalizeProcessName(string value)
    {
        value = value?.Trim() ?? string.Empty;
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;
    }
}

internal sealed class AppConfiguration
{
    public int Version { get; set; } = 7;
    public bool RestoreWhenNoProfile { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public int PollIntervalMilliseconds { get; set; } = 350;
    public Guid? LastSelectedProfileId { get; set; }
    public List<DisplayProfile> Profiles { get; set; } = [];

    public static AppConfiguration CreateDefault(string rustPath)
    {
        var rust = DisplayProfile.CreateRust(rustPath);
        return new AppConfiguration
        {
            Profiles = [rust],
            LastSelectedProfileId = rust.Id
        };
    }
}
