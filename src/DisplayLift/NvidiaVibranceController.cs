using NvAPIWrapper;
using NvDisplay = NvAPIWrapper.Display.Display;

namespace DisplayLift;

internal sealed class NvidiaVibranceController : IDisposable
{
    private sealed record DisplayState(int OriginalLevel, int MinimumLevel, int MaximumLevel);

    private readonly Dictionary<string, DisplayState> _originalStates = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;
    private bool _disposed;

    public NvidiaVibranceController()
    {
        try
        {
            NVIDIA.Initialize();
            _initialized = true;
            foreach (var display in NvDisplay.GetDisplays())
            {
                var dvc = display.DigitalVibranceControl;
                _originalStates[display.Name] = new DisplayState(
                    dvc.CurrentLevel,
                    dvc.MinimumLevel,
                    dvc.MaximumLevel);
            }

            Available = _originalStates.Count > 0;
            Status = Available
                ? $"NVIDIA driver vibrance ready on {_originalStates.Count} display(s)."
                : "NVIDIA NVAPI loaded, but no NVIDIA-driven display was found.";
        }
        catch (Exception exception)
        {
            Available = false;
            Status = $"NVIDIA driver vibrance unavailable: {exception.Message}";
        }
    }

    public bool Available { get; }
    public string Status { get; }

    public bool TryApplyBoost(int boostPercent, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        boostPercent = Math.Clamp(boostPercent, 0, 100);

        if (!Available)
        {
            error = Status;
            return false;
        }

        try
        {
            var displays = NvDisplay.GetDisplays();
            if (displays.Length == 0)
            {
                error = "NVIDIA NVAPI no longer reports an active display.";
                return false;
            }

            foreach (var display in displays)
            {
                var dvc = display.DigitalVibranceControl;
                if (!_originalStates.TryGetValue(display.Name, out var original))
                {
                    original = new DisplayState(dvc.CurrentLevel, dvc.MinimumLevel, dvc.MaximumLevel);
                    _originalStates[display.Name] = original;
                }

                var maximum = Math.Max(original.MaximumLevel, original.OriginalLevel);
                var target = original.OriginalLevel + (int)Math.Round((maximum - original.OriginalLevel) * (boostPercent / 100.0));
                dvc.CurrentLevel = Math.Clamp(target, original.MinimumLevel, maximum);
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = $"NVIDIA Digital Vibrance could not be changed: {exception.Message}";
            return false;
        }
    }

    public bool TryRestore(out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Available)
        {
            error = null;
            return true;
        }

        try
        {
            foreach (var display in NvDisplay.GetDisplays())
            {
                if (_originalStates.TryGetValue(display.Name, out var original))
                {
                    display.DigitalVibranceControl.CurrentLevel = original.OriginalLevel;
                }
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = $"NVIDIA Digital Vibrance could not be restored: {exception.Message}";
            return false;
        }
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
            try
            {
                NVIDIA.Unload();
            }
            catch
            {
                // Best effort during shutdown.
            }
        }

        _disposed = true;
    }
}
