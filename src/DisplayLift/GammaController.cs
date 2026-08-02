using System.Runtime.InteropServices;

namespace DisplayLift;

internal sealed class GammaController : IDisposable
{
    private GammaRamp? _originalRamp;
    private bool _disposed;

    public GammaController()
    {
        _originalRamp = ReadCurrentRamp();
    }

    public bool TryApply(DisplayPreset preset, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var ramp = BuildRamp(preset);
        return TrySetRamp(ramp, out error);
    }

    public bool TryRestore(out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_originalRamp is null)
        {
            error = "The original display gamma ramp could not be captured.";
            return false;
        }

        return TrySetRamp(_originalRamp.Value, out error);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_originalRamp is not null)
        {
            _ = TrySetRamp(_originalRamp.Value, out _);
        }

        _disposed = true;
    }

    private static GammaRamp BuildRamp(DisplayPreset preset)
    {
        var red = new ushort[256];
        var green = new ushort[256];
        var blue = new ushort[256];

        for (var i = 0; i < 256; i++)
        {
            var input = i / 255.0;
            var gammaAdjusted = Math.Pow(input, 1.0 / preset.Gamma);
            var lifted = preset.BlackLift + ((1.0 - preset.BlackLift) * gammaAdjusted);
            var output = Math.Clamp(lifted * preset.Gain, 0.0, 1.0);
            var value = (ushort)Math.Round(output * ushort.MaxValue);

            red[i] = value;
            green[i] = value;
            blue[i] = value;
        }

        return new GammaRamp
        {
            Red = red,
            Green = green,
            Blue = blue
        };
    }

    private static GammaRamp? ReadCurrentRamp()
    {
        var deviceContext = GetDC(IntPtr.Zero);
        if (deviceContext == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var ramp = GammaRamp.CreateEmpty();
            return GetDeviceGammaRamp(deviceContext, ref ramp) ? ramp : null;
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, deviceContext);
        }
    }

    private static bool TrySetRamp(GammaRamp ramp, out string? error)
    {
        var deviceContext = GetDC(IntPtr.Zero);
        if (deviceContext == IntPtr.Zero)
        {
            error = "Windows could not open the desktop display device context.";
            return false;
        }

        try
        {
            if (!SetDeviceGammaRamp(deviceContext, ref ramp))
            {
                error = "The display driver rejected the gamma change. Try borderless mode or update the GPU driver.";
                return false;
            }

            error = null;
            return true;
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, deviceContext);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GammaRamp
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;

        public static GammaRamp CreateEmpty() => new()
        {
            Red = new ushort[256],
            Green = new ushort[256],
            Blue = new ushort[256]
        };
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDeviceGammaRamp(IntPtr deviceContext, ref GammaRamp ramp);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDeviceGammaRamp(IntPtr deviceContext, ref GammaRamp ramp);
}
