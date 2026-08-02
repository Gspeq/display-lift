using DisplayLift;

var failures = new List<string>();
void Assert(bool condition, string message)
{
    if (!condition) failures.Add(message);
}

SceneAnalysis Classify(SceneMetrics metrics) => SceneClassifier.Classify(metrics);

var snow = Classify(new SceneMetrics(0.78, 0.12, 0.04, 0.72, 0.62, 0.03, 0.02, 0.11, 0.06, 0.63, 0.05));
Assert(snow.Scene == RustScene.Snow, $"Snow metrics classified as {snow.Scene}");

var desert = Classify(new SceneMetrics(0.55, 0.50, 0.10, 0.28, 0.05, 0.48, 0.05, 0.05, 0.02, 0.08, 0.06));
Assert(desert.Scene == RustScene.Desert, $"Desert metrics classified as {desert.Scene}");

var temperate = Classify(new SceneMetrics(0.45, 0.48, 0.15, 0.15, 0.04, 0.10, 0.46, 0.04, 0.02, 0.08, 0.07));
Assert(temperate.Scene == RustScene.Temperate, $"Temperate metrics classified as {temperate.Scene}");

var coast = Classify(new SceneMetrics(0.50, 0.42, 0.12, 0.20, 0.05, 0.15, 0.06, 0.34, 0.42, 0.07, 0.08));
Assert(coast.Scene == RustScene.Coast, $"Coast metrics classified as {coast.Scene}");

var night = Classify(new SceneMetrics(0.14, 0.24, 0.78, 0.01, 0.01, 0.07, 0.04, 0.08, 0.04, 0.32, 0.03));
Assert(night.Scene == RustScene.NightInterior, $"Night metrics classified as {night.Scene}");

var settings = new AppSettings();
var balanced = RustVisualPresets.Create(RustScene.Balanced, settings);
Assert(balanced.SaturationPercent == 152, "Balanced must preserve the researched 1.52 saturation baseline");
Assert(balanced.VibrancePercent == 80, "Balanced must preserve +0.80 vibrance");
Assert(balanced.BrightnessPercent == 6, "Balanced must preserve +0.06 brightness");
Assert(balanced.ContrastPercent == 105, "Balanced must preserve 1.05 contrast");

var snowVisual = RustVisualPresets.Create(RustScene.Snow, settings);
Assert(snowVisual.BrightnessPercent < balanced.BrightnessPercent, "Snow must reduce brightness");
Assert(snowVisual.ContrastPercent > balanced.ContrastPercent, "Snow must raise contrast");

var nightVisual = RustVisualPresets.Create(RustScene.NightInterior, settings);
Assert(nightVisual.GammaPercent >= 120, "Night must lift midtones");
Assert(nightVisual.ShadowLiftPercent >= 30, "Night must substantially lift shadows");

settings.ColorStrengthPercent = 150;
settings.BrightnessTrimPercent = 10;
settings.ContrastStrengthPercent = 140;
settings.ShadowAssistPercent = 150;
var boosted = RustVisualPresets.Create(RustScene.Balanced, settings);
Assert(boosted.SaturationPercent > balanced.SaturationPercent, "Color trim must raise saturation");
Assert(boosted.BrightnessPercent > balanced.BrightnessPercent, "Brightness trim must raise brightness");
Assert(boosted.ContrastPercent > balanced.ContrastPercent, "Contrast trim must raise contrast");
Assert(boosted.ShadowLiftPercent > balanced.ShadowLiftPercent, "Shadow trim must raise shadow lift");

var stabilizer = new SceneStabilizer();
var first = stabilizer.Update(desert, 60);
Assert(first.Scene == RustScene.Desert, "Stabilizer must lock the first clear scene");
var noisyTemperate = new SceneAnalysis(RustScene.Temperate, 0.51, temperate.Metrics, temperate.Scores, temperate.Summary);
var second = stabilizer.Update(noisyTemperate, 60);
Assert(second.Scene == RustScene.Desert, "A single conflicting sample must not immediately switch scenes");


var linear = DisplayRecovery.BuildLinearChannel();
Assert(linear.Length == 256, "Recovery gamma channel must have 256 entries");
Assert(linear[0] == 0, "Recovery gamma channel must begin at zero");
Assert(linear[255] == ushort.MaxValue, "Recovery gamma channel must end at full scale");
Assert(linear.Zip(linear.Skip(1), (left, right) => right >= left).All(value => value), "Recovery gamma channel must be monotonic");

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
    Console.Error.WriteLine("DisplayLift V9 tests failed:");
    foreach (var failure in failures) Console.Error.WriteLine($"  - {failure}");
    return 1;
}

Console.WriteLine("DisplayLift V9 auto-scene, recovery, preset, stabilization and color-matrix tests passed.");
return 0;
