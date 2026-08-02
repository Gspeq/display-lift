using DisplayLift;

var failures = new List<string>();

void Assert(bool condition, string message)
{
    if (!condition) failures.Add(message);
}

void AssertNear(float actual, float expected, float tolerance, string message)
{
    if (Math.Abs(actual - expected) > tolerance)
    {
        failures.Add($"{message}: expected {expected}, actual {actual}");
    }
}

var identity = ColorMatrixBuilder.Identity();
Assert(identity.Length == 25, "Identity matrix must contain 25 elements");
AssertNear(identity[0], 1, 0.0001f, "Identity red diagonal");
AssertNear(identity[6], 1, 0.0001f, "Identity green diagonal");
AssertNear(identity[12], 1, 0.0001f, "Identity blue diagonal");
AssertNear(identity[18], 1, 0.0001f, "Identity alpha diagonal");
AssertNear(identity[24], 1, 0.0001f, "Identity homogeneous coordinate");

var grayscale = ColorMatrixBuilder.Build(0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0);
AssertNear(grayscale[0], 0.2126f, 0.0001f, "Grayscale red luma");
AssertNear(grayscale[5], 0.7152f, 0.0001f, "Grayscale green luma");
AssertNear(grayscale[10], 0.0722f, 0.0001f, "Grayscale blue luma");

var exposure = ColorMatrixBuilder.Build(1.0, 1.0, 0.0, 1.0, 0.0, 0.0, 1.0, 1.0, 1.0);
AssertNear(exposure[0], 2.0f, 0.0001f, "+1 EV must double red output");
AssertNear(exposure[6], 2.0f, 0.0001f, "+1 EV must double green output");
AssertNear(exposure[12], 2.0f, 0.0001f, "+1 EV must double blue output");

var extreme = ColorMatrixBuilder.Build(2.25, 1.12, 0.05, 0.08, -0.06, 0.03, 1.05, 1.0, 1.11);
Assert(extreme.All(float.IsFinite), "Maximum-color matrix must contain only finite values");
Assert(extreme[0] > identity[0], "Maximum color must increase primary red coefficient");
Assert(extreme[6] > identity[6], "Maximum color must increase primary green coefficient");
Assert(extreme[12] > identity[12], "Maximum color must increase primary blue coefficient");

var rust = DisplayProfile.CreateRust(@"C:\Steam\RustClient.exe");
Assert(rust.ProcessName == "RustClient", "Rust profile must target RustClient.exe");
Assert(rust.Enabled, "Rust profile must be enabled by default");
Assert(rust.Priority == 100, "Rust profile must have high priority");
Assert(rust.SaturationPercent == 152, "Clean Rust must use 1.52 saturation");
Assert(rust.VibrancePercent == 80, "Clean Rust must use +0.80 vibrance");
Assert(rust.BrightnessPercent == 6, "Clean Rust must use +0.06 brightness");
Assert(rust.ContrastPercent == 105, "Clean Rust must use 1.05 contrast");

var night = rust.Clone("Night test");
night.ApplyPreset(RustVisualPreset.Night);
Assert(night.ExposureHundredths > 0, "Night preset must raise exposure");
Assert(night.GammaPercent > 100, "Night preset must raise midtones");
Assert(night.ShadowLiftPercent >= 30, "Night preset must substantially lift shadows");

var winter = rust.Clone("Winter test");
winter.ApplyPreset(RustVisualPreset.Winter);
Assert(winter.ContrastPercent > rust.ContrastPercent, "Winter preset must add snow contrast");
Assert(winter.ExposureHundredths < 0, "Winter preset must restrain snow exposure");

var clone = rust.Clone();
Assert(clone.Id != rust.Id, "Cloned profiles must receive a new ID");
Assert(clone.ProcessName == rust.ProcessName, "Cloned profiles must retain process matching");

var neutral = rust.Clone("Neutral test");
neutral.ApplyPreset(RustVisualPreset.Neutral);
Assert(neutral.SaturationPercent == 100 && neutral.ContrastPercent == 100, "Neutral preset must restore neutral matrix values");
Assert(neutral.VibrancePercent == 0 && neutral.BrightnessPercent == 0, "Neutral preset must remove vibrance and brightness boosts");
Assert(neutral.GammaPercent == 100 && neutral.ShadowLiftPercent == 0, "Neutral preset must restore neutral gamma values");

try
{
    _ = ColorMatrixBuilder.Build(5.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0);
    failures.Add("Out-of-range saturation must throw");
}
catch (ArgumentOutOfRangeException)
{
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("DisplayLift tests failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"  - {failure}");
    }
    return 1;
}

Console.WriteLine("DisplayLift V7 profile, preset and color-matrix tests passed.");
return 0;
