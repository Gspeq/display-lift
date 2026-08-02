namespace DisplayLift;

internal sealed record DisplayPreset(
    string Name,
    int SaturationPercent,
    int ContrastPercent,
    int BrightnessPercent,
    int ShadowLiftPercent,
    string Description);
