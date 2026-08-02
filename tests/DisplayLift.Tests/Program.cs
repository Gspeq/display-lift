using DisplayLift;

const float epsilon = 0.0001f;
var failures = new List<string>();

void AssertNear(float actual, float expected, string name)
{
    if (Math.Abs(actual - expected) > epsilon)
    {
        failures.Add($"{name}: expected {expected}, got {actual}");
    }
}

void AssertTrue(bool condition, string name)
{
    if (!condition)
    {
        failures.Add(name);
    }
}

var identity = ColorMatrixBuilder.Build(1.0, 1.0, 0.0);
AssertTrue(identity.Length == 25, "Identity matrix must contain 25 values.");
for (var row = 0; row < 5; row++)
{
    for (var column = 0; column < 5; column++)
    {
        AssertNear(identity[(row * 5) + column], row == column ? 1.0f : 0.0f, $"Identity[{row},{column}]");
    }
}

var grayscale = ColorMatrixBuilder.Build(0.0, 1.0, 0.0);
for (var inputRow = 0; inputRow < 3; inputRow++)
{
    AssertNear(grayscale[(inputRow * 5) + 0], grayscale[(inputRow * 5) + 1], $"Grayscale row {inputRow}, R/G equality");
    AssertNear(grayscale[(inputRow * 5) + 1], grayscale[(inputRow * 5) + 2], $"Grayscale row {inputRow}, G/B equality");
}

var extreme = ColorMatrixBuilder.Build(3.4, 1.15, 0.03);
AssertTrue(extreme[0] > 1.0f && extreme[6] > 1.0f && extreme[12] > 1.0f, "Extreme saturation must increase RGB diagonal values.");
AssertTrue(extreme[1] < 0.0f && extreme[5] < 0.0f && extreme[10] < 0.0f, "Extreme saturation must create negative cross-channel coefficients.");

var contrast = ColorMatrixBuilder.Build(1.0, 1.2, 0.0);
AssertNear(contrast[20], -0.1f, "Contrast translation red");
AssertNear(contrast[21], -0.1f, "Contrast translation green");
AssertNear(contrast[22], -0.1f, "Contrast translation blue");

try
{
    _ = ColorMatrixBuilder.Build(6.0, 1.0, 0.0);
    failures.Add("Out-of-range saturation should throw.");
}
catch (ArgumentOutOfRangeException)
{
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("DisplayLift color-matrix tests failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"  - {failure}");
    }
    return 1;
}

Console.WriteLine("DisplayLift color-matrix tests passed.");
return 0;
