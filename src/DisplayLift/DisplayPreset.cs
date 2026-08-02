namespace DisplayLift;

internal sealed record DisplayPreset(
    string Name,
    double Gamma,
    double BlackLift,
    double Gain,
    string Description);
