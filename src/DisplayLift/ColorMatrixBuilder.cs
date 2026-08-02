namespace DisplayLift;

internal static class ColorMatrixBuilder
{
    private const double RedLuma = 0.2126;
    private const double GreenLuma = 0.7152;
    private const double BlueLuma = 0.0722;

    public static float[] Build(double saturation, double contrast, double brightness)
    {
        if (!double.IsFinite(saturation) || saturation < 0.0 || saturation > 5.0)
        {
            throw new ArgumentOutOfRangeException(nameof(saturation));
        }

        if (!double.IsFinite(contrast) || contrast < 0.1 || contrast > 3.0)
        {
            throw new ArgumentOutOfRangeException(nameof(contrast));
        }

        if (!double.IsFinite(brightness) || brightness < -1.0 || brightness > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(brightness));
        }

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

        return matrix;
    }

    public static float[] Identity() => Build(1.0, 1.0, 0.0);

    private static float ToFloat(double value) => checked((float)value);
}
