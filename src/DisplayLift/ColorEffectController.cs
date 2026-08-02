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

    public bool TryApply(double saturation, double contrast, double brightness, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            error = "Windows could not initialize the full-screen color-effect API.";
            return false;
        }

        var effect = new MagColorEffect
        {
            Transform = ColorMatrixBuilder.Build(saturation, contrast, brightness)
        };

        if (!MagSetFullscreenColorEffect(ref effect))
        {
            error = GetLastError("Windows rejected the saturation/contrast color transform");
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
            error = "Windows could not initialize the full-screen color-effect API.";
            return false;
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
            ? $"{prefix}. Try borderless-windowed mode and make sure Windows Magnifier is not actively changing color filters."
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
