using System.ComponentModel;
using System.Runtime.InteropServices;
using NvAPIWrapper;
using NvDisplay = NvAPIWrapper.Display.Display;

namespace DisplayLift;

internal sealed record RecoveryResult(bool ColorReset, bool GammaReset, bool NvidiaReset, string Message);

internal static class DisplayRecovery
{
    public static RecoveryResult ResetToSystemDefaults()
    {
        var notes = new List<string>();
        var colorReset = ResetColorEffect(out var colorMessage);
        notes.Add(colorMessage);
        var gammaReset = ResetGamma(out var gammaMessage);
        notes.Add(gammaMessage);
        var nvidiaReset = ResetNvidiaVibrance(out var nvidiaMessage);
        notes.Add(nvidiaMessage);

        return new RecoveryResult(
            colorReset,
            gammaReset,
            nvidiaReset,
            string.Join(" ", notes.Where(note => !string.IsNullOrWhiteSpace(note))));
    }

    internal static ushort[] BuildLinearChannel()
    {
        var channel = new ushort[256];
        for (var index = 0; index < channel.Length; index++)
        {
            channel[index] = (ushort)Math.Round(index / 255.0 * ushort.MaxValue);
        }
        return channel;
    }

    private static bool ResetColorEffect(out string message)
    {
        var initialized = false;
        try
        {
            initialized = MagInitialize();
            if (!initialized)
            {
                message = "Windows color reset was unavailable.";
                return false;
            }

            var identity = new MagColorEffect { Transform = ColorMatrixBuilder.Identity() };
            if (!MagSetFullscreenColorEffect(ref identity))
            {
                var code = Marshal.GetLastWin32Error();
                message = code == 0
                    ? "Windows rejected the identity color transform."
                    : $"Windows rejected the identity color transform: {new Win32Exception(code).Message}";
                return false;
            }

            message = "Windows color transform reset.";
            return true;
        }
        catch (Exception exception)
        {
            message = $"Windows color reset failed: {exception.Message}";
            return false;
        }
        finally
        {
            if (initialized)
            {
                try { _ = MagUninitialize(); }
                catch { }
            }
        }
    }

    private static bool ResetGamma(out string message)
    {
        var deviceContext = GetDC(IntPtr.Zero);
        if (deviceContext == IntPtr.Zero)
        {
            message = "Windows gamma reset could not open the desktop display context.";
            return false;
        }

        try
        {
            var channel = BuildLinearChannel();
            var ramp = new GammaRamp
            {
                Red = (ushort[])channel.Clone(),
                Green = (ushort[])channel.Clone(),
                Blue = (ushort[])channel.Clone()
            };

            if (!SetDeviceGammaRamp(deviceContext, ref ramp))
            {
                message = "The display driver rejected the normal gamma ramp.";
                return false;
            }

            message = "Gamma and shadow lift reset.";
            return true;
        }
        catch (Exception exception)
        {
            message = $"Gamma reset failed: {exception.Message}";
            return false;
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, deviceContext);
        }
    }

    private static bool ResetNvidiaVibrance(out string message)
    {
        var initialized = false;
        try
        {
            NVIDIA.Initialize();
            initialized = true;
            var displays = NvDisplay.GetDisplays();
            if (displays.Length == 0)
            {
                message = "No NVIDIA-driven display required a vibrance reset.";
                return true;
            }

            foreach (var display in displays)
            {
                var vibrance = display.DigitalVibranceControl;
                vibrance.CurrentLevel = vibrance.DefaultLevel;
            }

            message = $"NVIDIA Digital Vibrance reset to driver default on {displays.Length} display(s).";
            return true;
        }
        catch (Exception exception)
        {
            message = $"NVIDIA vibrance reset was unavailable: {exception.Message}";
            return false;
        }
        finally
        {
            if (initialized)
            {
                try { NVIDIA.Unload(); }
                catch { }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MagColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] Transform;
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
    }

    [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetFullscreenColorEffect(ref MagColorEffect effect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDeviceGammaRamp(IntPtr deviceContext, ref GammaRamp ramp);
}
