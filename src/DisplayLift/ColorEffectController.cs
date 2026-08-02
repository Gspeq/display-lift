using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DisplayLift;

internal sealed class ColorEffectController : IDisposable
{
    private readonly MagColorEffect? _originalEffect;
    private readonly bool _initialized;
    private bool _disposed;

    public ColorEffectController()
    {
        _initialized = MagInitialize();
        if (!_initialized)
        {
            return;
        }

        var current = MagColorEffect.CreateEmpty();
        if (MagGetFullscreenColorEffect(ref current))
        {
            _originalEffect = current.Clone();
        }
    }

    public bool Available => _initialized;

    public bool TryApply(DisplayProfile profile, bool approximateVibrance, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            error = "Windows could not initialize the full-screen color-effect API.";
            return false;
        }

        var saturation = profile.SaturationPercent / 100.0;
        if (approximateVibrance)
        {
            saturation += (profile.VibrancePercent / 100.0) * 0.35;
        }
        saturation = Math.Clamp(saturation, 0.0, 3.0);

        var effect = new MagColorEffect
        {
            Transform = ColorMatrixBuilder.Build(
                saturation,
                profile.ContrastPercent / 100.0,
                profile.BrightnessPercent / 100.0,
                profile.ExposureHundredths / 100.0,
                profile.Temperature / 100.0,
                profile.Tint / 100.0,
                profile.RedGainPercent / 100.0,
                profile.GreenGainPercent / 100.0,
                profile.BlueGainPercent / 100.0)
        };

        if (!MagSetFullscreenColorEffect(ref effect))
        {
            error = GetLastError("Windows rejected the profile color transform");
            return false;
        }

        error = null;
        return true;
    }

    public bool TryRestore(out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            error = null;
            return true;
        }

        var effect = _originalEffect?.Clone() ?? new MagColorEffect
        {
            Transform = ColorMatrixBuilder.Identity()
        };

        if (!MagSetFullscreenColorEffect(ref effect))
        {
            error = GetLastError("Windows could not restore the original color transform");
            return false;
        }

        error = null;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_initialized)
        {
            _ = TryRestore(out _);
            _ = MagUninitialize();
        }

        _disposed = true;
    }

    private static string GetLastError(string prefix)
    {
        var code = Marshal.GetLastWin32Error();
        return code == 0
            ? $"{prefix}. Try Rust in borderless-windowed mode and make sure Windows Magnifier is not using another color filter."
            : $"{prefix}: {new Win32Exception(code).Message}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MagColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] Transform;

        public static MagColorEffect CreateEmpty() => new()
        {
            Transform = new float[25]
        };

        public readonly MagColorEffect Clone() => new()
        {
            Transform = (float[])Transform.Clone()
        };
    }

    [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagGetFullscreenColorEffect(ref MagColorEffect effect);

    [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetFullscreenColorEffect(ref MagColorEffect effect);
}
