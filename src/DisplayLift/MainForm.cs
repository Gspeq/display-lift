using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace DisplayLift;

internal sealed class MainForm : Form
{
    private const int WmHotkey = 0x0312;
    private const int CycleHotkeyId = 1001;
    private const int RestoreHotkeyId = 1002;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;

    private readonly ColorEffectController _colorController = new();
    private readonly GammaController _gammaController = new();
    private readonly Label _statusLabel = new();
    private readonly NotifyIcon _trayIcon;

    private readonly TrackBar _saturationTrack = CreateTrackBar(100, 350, 180, 25);
    private readonly TrackBar _contrastTrack = CreateTrackBar(80, 130, 106, 5);
    private readonly TrackBar _brightnessTrack = CreateTrackBar(-10, 20, 1, 5);
    private readonly TrackBar _shadowTrack = CreateTrackBar(0, 60, 5, 5);

    private readonly Label _saturationValue = new();
    private readonly Label _contrastValue = new();
    private readonly Label _brightnessValue = new();
    private readonly Label _shadowValue = new();

    private readonly DisplayPreset[] _presets =
    [
        new("Normal", 100, 100, 0, 0, "Restore the display exactly as it was."),
        new("Color Pop", 180, 106, 1, 5, "Strong digital-vibrance look without completely crushing colors."),
        new("Extreme", 260, 110, 2, 10, "Very saturated colors with extra separation and mild shadow lift."),
        new("Nuclear", 340, 115, 3, 16, "Maximum color punch. Deliberately unnatural and heavily clipped."),
        new("Neon Shadows", 290, 108, 4, 28, "Extreme color plus a large lift for dark interiors and nighttime scenes.")
    ];

    private int _activePresetIndex;
    private bool _customActive;
    private bool _hotkeysRegistered;

    public MainForm()
    {
        Text = "DisplayLift Vibrance";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 690);
        Size = new Size(760, 690);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 20, FontStyle.Bold),
            Text = "DisplayLift Vibrance"
        };

        var subtitle = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            Text = "Extreme desktop saturation, contrast, brightness, and shadow lift. This changes the whole Windows display and does not inject into Rust or access the game process."
        };

        var presetPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            MaximumSize = new Size(680, 0),
            Margin = new Padding(0, 12, 0, 8)
        };

        for (var index = 0; index < _presets.Length; index++)
        {
            var capturedIndex = index;
            var preset = _presets[index];
            var button = new Button
            {
                AutoSize = false,
                Size = new Size(130, 46),
                Text = preset.Name,
                Tag = index
            };
            button.Click += (_, _) => ApplyPreset(capturedIndex);
            presetPanel.Controls.Add(button);
        }

        var sliderGrid = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 4,
            MaximumSize = new Size(680, 0),
            Margin = new Padding(0, 8, 0, 8)
        };
        sliderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        sliderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 470));
        sliderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        AddSliderRow(sliderGrid, 0, "Saturation", _saturationTrack, _saturationValue);
        AddSliderRow(sliderGrid, 1, "Contrast", _contrastTrack, _contrastValue);
        AddSliderRow(sliderGrid, 2, "Brightness", _brightnessTrack, _brightnessValue);
        AddSliderRow(sliderGrid, 3, "Shadow lift", _shadowTrack, _shadowValue);

        _saturationTrack.ValueChanged += (_, _) => UpdateValueLabels();
        _contrastTrack.ValueChanged += (_, _) => UpdateValueLabels();
        _brightnessTrack.ValueChanged += (_, _) => UpdateValueLabels();
        _shadowTrack.ValueChanged += (_, _) => UpdateValueLabels();
        UpdateValueLabels();

        var actionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 10)
        };

        var applyButton = new Button
        {
            Size = new Size(210, 44),
            Text = "Apply slider settings"
        };
        applyButton.Click += (_, _) => ApplyCurrentSettings("Custom", custom: true);

        var restoreButton = new Button
        {
            Size = new Size(180, 44),
            Text = "Restore normal"
        };
        restoreButton.Click += (_, _) => RestoreOriginal();

        actionPanel.Controls.Add(applyButton);
        actionPanel.Controls.Add(restoreButton);

        _statusLabel.AutoSize = true;
        _statusLabel.MaximumSize = new Size(680, 0);
        _statusLabel.Font = new Font(Font, FontStyle.Bold);
        _statusLabel.Text = "Active: Normal | Ctrl+Alt+F9 cycles presets; Ctrl+Alt+F10 restores.";

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            Text = "Nuclear is intentionally excessive. If Rust ignores the effect, use borderless-windowed mode; exclusive fullscreen or a GPU-driver color profile can override Windows display transforms. DisplayLift restores the color matrix and gamma ramp when you restore or exit normally."
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(28),
            ColumnCount = 1,
            RowCount = 7
        };
        root.Controls.Add(title);
        root.Controls.Add(subtitle);
        root.Controls.Add(presetPanel);
        root.Controls.Add(sliderGrid);
        root.Controls.Add(actionPanel);
        root.Controls.Add(_statusLabel);
        root.Controls.Add(note);
        Controls.Add(root);

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show", null, (_, _) => ShowFromTray());
        trayMenu.Items.Add("Cycle preset", null, (_, _) => CyclePreset());
        trayMenu.Items.Add("Restore display", null, (_, _) => RestoreOriginal());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => Close());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "DisplayLift Vibrance — Normal",
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RegisterGlobalHotkeys();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterGlobalHotkeys();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _gammaController.Dispose();
        _colorController.Dispose();
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey)
        {
            var id = message.WParam.ToInt32();
            if (id == CycleHotkeyId)
            {
                CyclePreset();
                return;
            }

            if (id == RestoreHotkeyId)
            {
                RestoreOriginal();
                return;
            }
        }

        base.WndProc(ref message);
    }

    private static TrackBar CreateTrackBar(int minimum, int maximum, int value, int largeChange) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Value = value,
        TickFrequency = largeChange,
        LargeChange = largeChange,
        SmallChange = 1,
        Width = 455,
        AutoSize = false,
        Height = 48
    };

    private static void AddSliderRow(TableLayoutPanel panel, int row, string name, TrackBar trackBar, Label valueLabel)
    {
        var nameLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Text = name
        };

        valueLabel.AutoSize = true;
        valueLabel.Anchor = AnchorStyles.Left;

        panel.Controls.Add(nameLabel, 0, row);
        panel.Controls.Add(trackBar, 1, row);
        panel.Controls.Add(valueLabel, 2, row);
    }

    private void UpdateValueLabels()
    {
        _saturationValue.Text = $"{_saturationTrack.Value}%";
        _contrastValue.Text = $"{_contrastTrack.Value}%";
        _brightnessValue.Text = $"{_brightnessTrack.Value:+0;-0;0}%";
        _shadowValue.Text = $"{_shadowTrack.Value}%";
    }

    private void ApplyPreset(int index)
    {
        if (index == 0)
        {
            RestoreOriginal();
            return;
        }

        var preset = _presets[index];
        _saturationTrack.Value = preset.SaturationPercent;
        _contrastTrack.Value = preset.ContrastPercent;
        _brightnessTrack.Value = preset.BrightnessPercent;
        _shadowTrack.Value = preset.ShadowLiftPercent;
        _activePresetIndex = index;
        _customActive = false;
        ApplyCurrentSettings(preset.Name, custom: false);
    }

    private void ApplyCurrentSettings(string displayName, bool custom)
    {
        var saturation = _saturationTrack.Value / 100.0;
        var contrast = _contrastTrack.Value / 100.0;
        var brightness = _brightnessTrack.Value / 100.0;

        if (!_colorController.TryApply(saturation, contrast, brightness, out var colorError))
        {
            MessageBox.Show(colorError, "DisplayLift Vibrance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? gammaError;
        bool gammaSucceeded;
        if (_shadowTrack.Value == 0)
        {
            gammaSucceeded = _gammaController.TryRestore(out gammaError);
        }
        else
        {
            gammaSucceeded = _gammaController.TryApply(_shadowTrack.Value, out gammaError);
        }

        if (!gammaSucceeded)
        {
            _ = _colorController.TryRestore(out _);
            MessageBox.Show(gammaError, "DisplayLift Vibrance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _customActive = custom;
        if (custom)
        {
            _activePresetIndex = 0;
        }

        _statusLabel.Text = $"Active: {displayName} — Sat {_saturationTrack.Value}%, Contrast {_contrastTrack.Value}%, Brightness {_brightnessTrack.Value:+0;-0;0}%, Shadows {_shadowTrack.Value}% | Ctrl+Alt+F10 restores.";
        _trayIcon.Text = $"DisplayLift Vibrance — {displayName}";
    }

    private void RestoreOriginal()
    {
        var errors = new List<string>();
        if (!_colorController.TryRestore(out var colorError) && colorError is not null)
        {
            errors.Add(colorError);
        }

        if (!_gammaController.TryRestore(out var gammaError) && gammaError is not null)
        {
            errors.Add(gammaError);
        }

        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), "DisplayLift Vibrance", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _activePresetIndex = 0;
        _customActive = false;
        _statusLabel.Text = "Active: Normal | Ctrl+Alt+F9 cycles presets; Ctrl+Alt+F10 restores.";
        _trayIcon.Text = "DisplayLift Vibrance — Normal";
    }

    private void CyclePreset()
    {
        var next = _customActive ? 1 : (_activePresetIndex + 1) % _presets.Length;
        ApplyPreset(next);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_customActive)
        {
            ApplyCurrentSettings("Custom", custom: true);
        }
        else if (_activePresetIndex > 0)
        {
            ApplyPreset(_activePresetIndex);
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void RegisterGlobalHotkeys()
    {
        if (_hotkeysRegistered)
        {
            return;
        }

        var cycleRegistered = RegisterHotKey(Handle, CycleHotkeyId, ModControl | ModAlt, (uint)Keys.F9);
        var restoreRegistered = RegisterHotKey(Handle, RestoreHotkeyId, ModControl | ModAlt, (uint)Keys.F10);
        _hotkeysRegistered = cycleRegistered && restoreRegistered;

        if (!_hotkeysRegistered)
        {
            UnregisterGlobalHotkeys();
            _statusLabel.Text += " Global hotkeys were unavailable; buttons still work.";
        }
    }

    private void UnregisterGlobalHotkeys()
    {
        _ = UnregisterHotKey(Handle, CycleHotkeyId);
        _ = UnregisterHotKey(Handle, RestoreHotkeyId);
        _hotkeysRegistered = false;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
