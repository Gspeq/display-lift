namespace DisplayLift;

internal sealed class AppSettings
{
    public int Version { get; set; } = 9;
    public string RustExecutablePath { get; set; } = string.Empty;
    public bool AutoDetectScene { get; set; } = true;
    public RustScene ManualScene { get; set; } = RustScene.Balanced;
    public int ColorStrengthPercent { get; set; } = 100;
    public int BrightnessTrimPercent { get; set; } = 0;
    public int ContrastStrengthPercent { get; set; } = 100;
    public int ShadowAssistPercent { get; set; } = 100;
    public bool UseNvidiaVibrance { get; set; } = true;
    public bool RestoreWhenRustInactive { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int DetectionIntervalMilliseconds { get; set; } = 900;
    public int DetectionSensitivityPercent { get; set; } = 60;

    public void Validate()
    {
        Version = 9;
        RustExecutablePath ??= string.Empty;
        ColorStrengthPercent = Math.Clamp(ColorStrengthPercent, 50, 150);
        BrightnessTrimPercent = Math.Clamp(BrightnessTrimPercent, -15, 15);
        ContrastStrengthPercent = Math.Clamp(ContrastStrengthPercent, 60, 150);
        ShadowAssistPercent = Math.Clamp(ShadowAssistPercent, 50, 170);
        DetectionIntervalMilliseconds = Math.Clamp(DetectionIntervalMilliseconds, 500, 2500);
        DetectionSensitivityPercent = Math.Clamp(DetectionSensitivityPercent, 35, 85);
        if (!Enum.IsDefined(ManualScene)) ManualScene = RustScene.Balanced;
    }
}
