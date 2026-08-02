namespace DisplayLift;

internal static class ColorMatrixBuilder
{
    private const double RedLuma = 0.2126;
    private const double GreenLuma = 0.7152;
    private const double BlueLuma = 0.0722;

    public static float[] Build(
        double saturation,
        double contrast,
        double brightness,
        double exposureEv,
        double temperature,
        double tint,
        double redGain,
        double greenGain,
        double blueGain)
    {
        ValidateFiniteRange(saturation, 0.0, 4.0, nameof(saturation));
        ValidateFiniteRange(contrast, 0.1, 3.0, nameof(contrast));
        ValidateFiniteRange(brightness, -1.0, 1.0, nameof(brightness));
        ValidateFiniteRange(exposureEv, -2.0, 2.0, nameof(exposureEv));
        ValidateFiniteRange(temperature, -1.0, 1.0, nameof(temperature));
        ValidateFiniteRange(tint, -1.0, 1.0, nameof(tint));
        ValidateFiniteRange(redGain, 0.1, 3.0, nameof(redGain));
        ValidateFiniteRange(greenGain, 0.1, 3.0, nameof(greenGain));
        ValidateFiniteRange(blueGain, 0.1, 3.0, nameof(blueGain));

        var inverseSaturation = 1.0 - saturation;
        var translation = (0.5 * (1.0 - contrast)) + brightness;
        var matrix = new float[25];

        matrix[0] = ToFloat((RedLuma * inverseSaturation + saturation) * contrast);
        matrix[1] = ToFloat((RedLuma * inverseSaturation) * contrast);
        matrix[2] = ToFloat((RedLuma * inverseSaturation) * contrast);

        matrix[5] = ToFloat((GreenLuma * inverseSaturation) * contrast);
        matrix[6] = ToFloat((GreenLuma * inverseSaturation + saturation) * contrast);
        matrix[7] = ToFloat((GreenLuma * inverseSaturation) * contrast);

        matrix[10] = ToFloat((BlueLuma * inverseSaturation) * contrast);
        matrix[11] = ToFloat((BlueLuma * inverseSaturation) * contrast);
        matrix[12] = ToFloat((BlueLuma * inverseSaturation + saturation) * contrast);

        matrix[18] = 1.0f;
        matrix[20] = ToFloat(translation);
        matrix[21] = ToFloat(translation);
        matrix[22] = ToFloat(translation);
        matrix[24] = 1.0f;

        var warmRed = 1.0 + Math.Max(temperature, 0.0) * 0.16 - Math.Max(-temperature, 0.0) * 0.05;
        var warmGreen = 1.0 - Math.Abs(temperature) * 0.035;
        var warmBlue = 1.0 + Math.Max(-temperature, 0.0) * 0.16 - Math.Max(temperature, 0.0) * 0.09;

        var tintRed = 1.0 + Math.Max(tint, 0.0) * 0.035 - Math.Max(-tint, 0.0) * 0.015;
        var tintGreen = 1.0 - Math.Max(tint, 0.0) * 0.13 + Math.Max(-tint, 0.0) * 0.09;
        var tintBlue = 1.0 + Math.Max(tint, 0.0) * 0.035 - Math.Max(-tint, 0.0) * 0.015;
        var exposureGain = Math.Pow(2.0, exposureEv);

        ApplyOutputGain(matrix, 0, redGain * warmRed * tintRed * exposureGain);
        ApplyOutputGain(matrix, 1, greenGain * warmGreen * tintGreen * exposureGain);
        ApplyOutputGain(matrix, 2, blueGain * warmBlue * tintBlue * exposureGain);

        return matrix;
    }

    public static float[] Identity() => Build(1.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0);

    private static void ApplyOutputGain(float[] matrix, int column, double gain)
    {
        for (var row = 0; row < 5; row++)
        {
            var index = row * 5 + column;
            matrix[index] = ToFloat(matrix[index] * gain);
        }
    }

    private static void ValidateFiniteRange(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static float ToFloat(double value) => checked((float)value);
}
