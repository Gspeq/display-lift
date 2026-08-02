namespace DisplayLift;

internal sealed record EffectResult(bool Applied, string Message);

internal sealed class DisplayEffectEngine : IDisposable
{
    private readonly ColorEffectController _colorController = new();
    private readonly GammaController _gammaController = new();
    private readonly NvidiaVibranceController _nvidiaController = new();
    private bool _disposed;

    public bool NvidiaAvailable => _nvidiaController.Available;
    public string NvidiaStatus => _nvidiaController.Status;
    public bool ColorMatrixAvailable => _colorController.Available;

    public EffectResult Apply(DisplayProfile profile)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        profile.Validate();

        var applied = new List<string>();
        var warnings = new List<string>();
        var approximateVibrance = !profile.UseNvidiaVibrance || !_nvidiaController.Available;

        if (_colorController.TryApply(profile, approximateVibrance, out var colorError))
        {
            applied.Add("color/tone");
        }
        else if (!string.IsNullOrWhiteSpace(colorError))
        {
            warnings.Add(colorError);
        }

        if (_gammaController.TryApply(profile.GammaPercent, profile.ShadowLiftPercent, out var gammaError))
        {
            applied.Add("gamma/shadows");
        }
        else if (!string.IsNullOrWhiteSpace(gammaError))
        {
            warnings.Add(gammaError);
        }

        if (profile.UseNvidiaVibrance && _nvidiaController.Available)
        {
            if (_nvidiaController.TryApplyBoost(profile.VibrancePercent, out var vibranceError))
            {
                applied.Add("NVIDIA vibrance");
            }
            else if (!string.IsNullOrWhiteSpace(vibranceError))
            {
                warnings.Add(vibranceError);
            }
        }
        else if (profile.VibrancePercent > 0)
        {
            applied.Add("vibrance approximation");
            if (profile.UseNvidiaVibrance && !_nvidiaController.Available)
            {
                warnings.Add("NVIDIA vibrance was unavailable, so DisplayLift approximated vibrance through the Windows color matrix.");
            }
        }

        var message = applied.Count == 0
            ? "No display backend accepted the profile."
            : $"Applied {string.Join(", ", applied)}.";
        if (warnings.Count > 0)
        {
            message += " " + string.Join(" ", warnings);
        }

        return new EffectResult(applied.Count > 0, message);
    }

    public EffectResult Restore()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var restored = new List<string>();
        var warnings = new List<string>();

        if (_colorController.TryRestore(out var colorError)) restored.Add("color/tone");
        else if (!string.IsNullOrWhiteSpace(colorError)) warnings.Add(colorError);

        if (_gammaController.TryRestore(out var gammaError)) restored.Add("gamma");
        else if (!string.IsNullOrWhiteSpace(gammaError)) warnings.Add(gammaError);

        if (_nvidiaController.TryRestore(out var nvidiaError)) restored.Add("NVIDIA vibrance");
        else if (!string.IsNullOrWhiteSpace(nvidiaError)) warnings.Add(nvidiaError);

        var message = restored.Count > 0
            ? $"Restored original {string.Join(", ", restored)} settings."
            : "No display settings were restored.";
        if (warnings.Count > 0)
        {
            message += " " + string.Join(" ", warnings);
        }

        return new EffectResult(restored.Count > 0, message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _nvidiaController.Dispose();
        _gammaController.Dispose();
        _colorController.Dispose();
        _disposed = true;
    }
}
