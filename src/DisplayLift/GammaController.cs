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

    public bool TryApply(int gammaPercent, int shadowLiftPercent, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (gammaPercent is < 60 or > 180)
        {
            error = "Gamma must be between 60 and 180 percent.";
            return false;
        }

        if (shadowLiftPercent is < 0 or > 60)
        {
            error = "Shadow lift must be between 0 and 60 percent.";
            return false;
        }

        if (gammaPercent == 100 && shadowLiftPercent == 0)
        {
            return TryRestore(out error);
        }

        var gamma = gammaPercent / 100.0;
        var blackLift = shadowLiftPercent * 0.0008;
        var gain = 1.0 + (shadowLiftPercent * 0.00025);
        var ramp = BuildRamp(gamma, blackLift, gain);
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

    private static GammaRamp BuildRamp(double gamma, double blackLift, double gain)
    {
        var red = new ushort[256];
        var green = new ushort[256];
        var blue = new ushort[256];

        for (var i = 0; i < 256; i++)
        {
            var input = i / 255.0;
            var gammaAdjusted = Math.Pow(input, 1.0 / gamma);
            var lifted = blackLift + ((1.0 - blackLift) * gammaAdjusted);
            var output = Math.Clamp(lifted * gain, 0.0, 1.0);
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
                error = "The display driver rejected the gamma or shadow-lift change. Try borderless-windowed mode or update the GPU driver.";
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
