# DisplayLift-Windows-Reset-Version: 9
$ErrorActionPreference = 'Continue'

$source = @'
using System;
using System.Runtime.InteropServices;
public static class DisplayLiftEmergencyReset
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MagColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] Transform;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GammaRamp
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Blue;
    }

    [DllImport("Magnification.dll", ExactSpelling = true)] public static extern bool MagInitialize();
    [DllImport("Magnification.dll", ExactSpelling = true)] public static extern bool MagUninitialize();
    [DllImport("Magnification.dll", ExactSpelling = true)] public static extern bool MagSetFullscreenColorEffect(ref MagColorEffect effect);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern bool SetDeviceGammaRamp(IntPtr hdc, ref GammaRamp ramp);
}
'@

try { Add-Type -TypeDefinition $source -ErrorAction Stop } catch { }

try {
    if ([DisplayLiftEmergencyReset]::MagInitialize()) {
        $identity = New-Object 'Single[]' 25
        $identity[0] = 1; $identity[6] = 1; $identity[12] = 1; $identity[18] = 1; $identity[24] = 1
        $effect = New-Object DisplayLiftEmergencyReset+MagColorEffect
        $effect.Transform = $identity
        [void][DisplayLiftEmergencyReset]::MagSetFullscreenColorEffect([ref]$effect)
        [void][DisplayLiftEmergencyReset]::MagUninitialize()
    }
} catch { }

try {
    $channel = New-Object 'UInt16[]' 256
    for ($i = 0; $i -lt 256; $i++) { $channel[$i] = [UInt16][Math]::Round(($i / 255.0) * 65535.0) }
    $ramp = New-Object DisplayLiftEmergencyReset+GammaRamp
    $ramp.Red = $channel.Clone(); $ramp.Green = $channel.Clone(); $ramp.Blue = $channel.Clone()
    $dc = [DisplayLiftEmergencyReset]::GetDC([IntPtr]::Zero)
    if ($dc -ne [IntPtr]::Zero) {
        [void][DisplayLiftEmergencyReset]::SetDeviceGammaRamp($dc, [ref]$ramp)
        [void][DisplayLiftEmergencyReset]::ReleaseDC([IntPtr]::Zero, $dc)
    }
} catch { }

Write-Host 'Emergency Windows color and gamma reset attempted.' -ForegroundColor DarkGray
