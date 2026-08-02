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

    private readonly GammaController _gammaController = new();
    private readonly Label _statusLabel = new();
    private readonly NotifyIcon _trayIcon;

    private readonly DisplayPreset[] _presets =
    [
        new("Normal", 1.00, 0.00, 1.00, "Restores the display ramp captured when the app started."),
        new("Clear", 1.14, 0.015, 1.01, "A mild midtone lift for normal daytime play."),
        new("Shadow Lift", 1.32, 0.035, 1.02, "Raises dark interiors without flattening the whole image."),
        new("Strong", 1.52, 0.060, 1.03, "A stronger dark-scene preset. Expect washed-out blacks.")
    ];

    private int _activePresetIndex;
    private bool _hotkeysRegistered;

    public MainForm()
    {
        Text = "DisplayLift";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 430);
        Size = new Size(620, 430);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 20, FontStyle.Bold),
            Text = "DisplayLift"
        };

        var subtitle = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(550, 0),
            Text = "System-level display presets only. No game injection, overlays, process access, or configuration edits."
        };

        var presetPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 12)
        };

        for (var index = 0; index < _presets.Length; index++)
        {
            var capturedIndex = index;
            var preset = _presets[index];
            var button = new Button
            {
                AutoSize = false,
                Size = new Size(545, 48),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"  {index + 1}. {preset.Name} — {preset.Description}",
                Tag = index
            };
            button.Click += (_, _) => ApplyPreset(capturedIndex);
            presetPanel.Controls.Add(button);
        }

        _statusLabel.AutoSize = true;
        _statusLabel.MaximumSize = new Size(550, 0);
        _statusLabel.Text = "Active: Normal | Ctrl+Alt+F9 cycles presets; Ctrl+Alt+F10 restores.";

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(550, 0),
            Text = "The change affects the entire desktop. DisplayLift restores the original ramp when you restore or exit. Some GPU drivers and exclusive-fullscreen modes can override it."
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(28),
            ColumnCount = 1,
            RowCount = 5
        };
        root.Controls.Add(title);
        root.Controls.Add(subtitle);
        root.Controls.Add(presetPanel);
        root.Controls.Add(_statusLabel);
        root.Controls.Add(note);
        Controls.Add(root);

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show", null, (_, _) => ShowFromTray());
        trayMenu.Items.Add("Restore display", null, (_, _) => RestoreOriginal());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => Close());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "DisplayLift",
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
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey)
        {
            var id = message.WParam.ToInt32();
            if (id == CycleHotkeyId)
            {
                var next = (_activePresetIndex + 1) % _presets.Length;
                ApplyPreset(next);
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

    private void ApplyPreset(int index)
    {
        if (index == 0)
        {
            RestoreOriginal();
            return;
        }

        var preset = _presets[index];
        if (_gammaController.TryApply(preset, out var error))
        {
            _activePresetIndex = index;
            _statusLabel.Text = $"Active: {preset.Name} | Ctrl+Alt+F9 cycles presets; Ctrl+Alt+F10 restores.";
            _trayIcon.Text = $"DisplayLift — {preset.Name}";
            return;
        }

        MessageBox.Show(error, "DisplayLift", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void RestoreOriginal()
    {
        if (_gammaController.TryRestore(out var error))
        {
            _activePresetIndex = 0;
            _statusLabel.Text = "Active: Normal | Ctrl+Alt+F9 cycles presets; Ctrl+Alt+F10 restores.";
            _trayIcon.Text = "DisplayLift — Normal";
            return;
        }

        MessageBox.Show(error, "DisplayLift", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_activePresetIndex > 0)
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
