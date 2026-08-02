using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DisplayLift;

internal sealed class MainForm : Form
{
    private const int WmHotkey = 0x0312;
    private const int AutoHotkeyId = 8101;
    private const int CycleHotkeyId = 8102;
    private const int RestoreHotkeyId = 8103;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;

    private static readonly Color BackgroundColor = Color.FromArgb(14, 16, 18);
    private static readonly Color HeaderColor = Color.FromArgb(20, 23, 26);
    private static readonly Color CardColor = Color.FromArgb(25, 29, 33);
    private static readonly Color CardRaisedColor = Color.FromArgb(31, 36, 41);
    private static readonly Color BorderColor = Color.FromArgb(49, 55, 61);
    private static readonly Color AccentColor = Color.FromArgb(239, 104, 47);
    private static readonly Color AccentHoverColor = Color.FromArgb(255, 127, 65);
    private static readonly Color TextColor = Color.FromArgb(241, 243, 245);
    private static readonly Color MutedColor = Color.FromArgb(164, 171, 178);
    private static readonly Color GoodColor = Color.FromArgb(71, 196, 125);
    private static readonly Color WarningColor = Color.FromArgb(245, 183, 66);

    private readonly SettingsStore _store = new();
    private readonly AppSettings _settings;
    private readonly DisplayEffectEngine _effects = new();
    private readonly ScreenSceneDetector _detector = new();
    private readonly SceneStabilizer _stabilizer = new();
    private readonly System.Windows.Forms.Timer _monitorTimer = new();
    private readonly System.Windows.Forms.Timer _reapplyTimer = new();
    private readonly NotifyIcon _trayIcon;

    private readonly Label _rustStateLabel = new();
    private readonly Label _sceneLabel = new();
    private readonly Label _confidenceLabel = new();
    private readonly Label _sceneDescriptionLabel = new();
    private readonly Label _detectorDetailsLabel = new();
    private readonly Label _effectDetailsLabel = new();
    private readonly Label _backendLabel = new();
    private readonly Label _pathLabel = new();
    private readonly Label _footerStatusLabel = new();
    private readonly ProgressBar _confidenceBar = new();
    private readonly CheckBox _autoToggle = new();
    private readonly CheckBox _nvidiaCheck = new();
    private readonly CheckBox _restoreCheck = new();
    private readonly CheckBox _startupCheck = new();
    private readonly CheckBox _trayCheck = new();
    private readonly TrackBar _colorTrack = CreateTrackBar(50, 150, 100, 10);
    private readonly TrackBar _brightnessTrack = CreateTrackBar(-15, 15, 0, 3);
    private readonly TrackBar _contrastTrack = CreateTrackBar(60, 150, 100, 10);
    private readonly TrackBar _shadowTrack = CreateTrackBar(50, 170, 100, 10);
    private readonly Label _colorValue = NewValueLabel();
    private readonly Label _brightnessValue = NewValueLabel();
    private readonly Label _contrastValue = NewValueLabel();
    private readonly Label _shadowValue = NewValueLabel();
    private readonly Dictionary<RustScene, Button> _sceneButtons = new();

    private RustScene? _activeScene;
    private bool _effectsApplied;
    private bool _syncingControls;
    private bool _allowClose;
    private bool _shutdown;
    private bool _suspended;

    public MainForm(bool startMinimized)
    {
        _settings = _store.Load();

        Text = "DisplayLift — Rust Auto Visuals";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1020, 700);
        Size = new Size(1120, 760);
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

        _trayIcon = BuildTrayIcon();
        Controls.Add(BuildLayout());
        LoadSettingsIntoControls();
        HookEvents();
        UpdateRustPathDisplay();
        UpdateBackendDisplay();
        UpdateSceneButtons();
        UpdateTuningLabels();

        _monitorTimer.Interval = _settings.DetectionIntervalMilliseconds;
        _monitorTimer.Tick += (_, _) => EvaluateRustState();
        _monitorTimer.Start();

        _reapplyTimer.Interval = 180;
        _reapplyTimer.Tick += (_, _) =>
        {
            _reapplyTimer.Stop();
            if (ForegroundProcess.IsRust(ForegroundProcess.GetInfo().ProcessName))
                ApplyScene(_settings.AutoDetectScene ? (_activeScene ?? RustScene.Balanced) : _settings.ManualScene, "Tuning updated");
        };

        Shown += (_, _) =>
        {
            RegisterGlobalHotkeys();
            EvaluateRustState();
            if (startMinimized) HideToTray();
        };

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized && _settings.MinimizeToTray) HideToTray();
        };
        FormClosing += HandleFormClosing;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey)
        {
            switch (message.WParam.ToInt32())
            {
                case AutoHotkeyId:
                    EnableAutoMode();
                    return;
                case CycleHotkeyId:
                    CycleManualScene();
                    return;
                case RestoreHotkeyId:
                    RestoreOriginal("Restored with Ctrl+Alt+F10 — automatic changes paused", suspend: true);
                    return;
            }
        }
        base.WndProc(ref message);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackgroundColor,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildBody(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = HeaderColor, Padding = new Padding(26, 12, 26, 10) };
        panel.Controls.Add(new Label
        {
            Text = "DISPLAYLIFT",
            AutoSize = true,
            ForeColor = TextColor,
            Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold),
            Location = new Point(26, 11)
        });
        panel.Controls.Add(new Label
        {
            Text = "RUST AUTO REGION VISUALS  •  SCREEN-COLOR DETECTION  •  NO GAME INJECTION",
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(29, 49)
        });

        var restore = NewButton("RESTORE ORIGINAL", secondary: true);
        restore.Size = new Size(168, 36);
        restore.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        restore.Click += (_, _) => RestoreOriginal("Original display settings restored — automatic changes paused", suspend: true);
        panel.Controls.Add(restore);
        panel.Resize += (_, _) => restore.Location = new Point(panel.ClientSize.Width - restore.Width - 26, 20);
        return panel;
    }

    private Control BuildBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18),
            BackColor = BackgroundColor
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = new Padding(0, 0, 9, 0) };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 194));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 246));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(BuildAutoCard(), 0, 0);
        left.Controls.Add(BuildRegionsCard(), 0, 1);
        left.Controls.Add(BuildDetectorCard(), 0, 2);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = new Padding(9, 0, 0, 0) };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 278));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(BuildTuningCard(), 0, 0);
        right.Controls.Add(BuildRustSetupCard(), 0, 1);
        right.Controls.Add(BuildBehaviorCard(), 0, 2);

        body.Controls.Add(left, 0, 0);
        body.Controls.Add(right, 1, 0);
        return body;
    }

    private Control BuildAutoCard()
    {
        var card = NewCard("AUTO REGION");
        _rustStateLabel.Text = "WAITING FOR RUST";
        _rustStateLabel.ForeColor = MutedColor;
        _rustStateLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _rustStateLabel.AutoSize = true;
        _rustStateLabel.Location = new Point(20, 47);

        _sceneLabel.Text = "Balanced";
        _sceneLabel.ForeColor = TextColor;
        _sceneLabel.Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold);
        _sceneLabel.AutoSize = true;
        _sceneLabel.Location = new Point(18, 72);

        _confidenceLabel.Text = "Detector idle";
        _confidenceLabel.ForeColor = MutedColor;
        _confidenceLabel.AutoSize = true;
        _confidenceLabel.Location = new Point(21, 113);

        _confidenceBar.Location = new Point(21, 139);
        _confidenceBar.Size = new Size(375, 8);
        _confidenceBar.Style = ProgressBarStyle.Continuous;
        _confidenceBar.Maximum = 100;

        _sceneDescriptionLabel.Text = RustSceneCatalog.GetDescription(RustScene.Balanced);
        _sceneDescriptionLabel.ForeColor = MutedColor;
        _sceneDescriptionLabel.Location = new Point(21, 154);
        _sceneDescriptionLabel.AutoEllipsis = true;
        _sceneDescriptionLabel.Size = new Size(480, 30);
        _sceneDescriptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        _autoToggle.Appearance = Appearance.Button;
        _autoToggle.Text = "AUTO ON";
        _autoToggle.TextAlign = ContentAlignment.MiddleCenter;
        _autoToggle.FlatStyle = FlatStyle.Flat;
        _autoToggle.FlatAppearance.BorderSize = 0;
        _autoToggle.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        _autoToggle.Size = new Size(132, 42);
        _autoToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _autoToggle.Location = new Point(430, 58);
        card.Resize += (_, _) => _autoToggle.Left = card.ClientSize.Width - _autoToggle.Width - 20;

        card.Controls.Add(_rustStateLabel);
        card.Controls.Add(_sceneLabel);
        card.Controls.Add(_confidenceLabel);
        card.Controls.Add(_confidenceBar);
        card.Controls.Add(_sceneDescriptionLabel);
        card.Controls.Add(_autoToggle);
        return card;
    }

    private Control BuildRegionsCard()
    {
        var card = NewCard("MANUAL FALLBACK");
        var subtitle = new Label
        {
            Text = "Choose a scene instantly when weather or a monument confuses automatic detection.",
            ForeColor = MutedColor,
            AutoSize = true,
            Location = new Point(20, 43)
        };
        card.Controls.Add(subtitle);

        var grid = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 2,
            Location = new Point(18, 72),
            Size = new Size(560, 142),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        for (var column = 0; column < 3; column++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        for (var row = 0; row < 2; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var scenes = new[]
        {
            RustScene.Balanced, RustScene.Temperate, RustScene.Desert,
            RustScene.Snow, RustScene.Coast, RustScene.NightInterior
        };
        for (var index = 0; index < scenes.Length; index++)
        {
            var scene = scenes[index];
            var button = NewButton(RustSceneCatalog.GetShortName(scene), secondary: true);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4);
            button.Tag = scene;
            button.Click += (_, _) => SelectManualScene(scene);
            _sceneButtons[scene] = button;
            grid.Controls.Add(button, index % 3, index / 3);
        }
        card.Controls.Add(grid);
        card.Resize += (_, _) => grid.Width = card.ClientSize.Width - 36;
        return card;
    }

    private Control BuildDetectorCard()
    {
        var card = NewCard("DETECTOR DETAILS");
        _detectorDetailsLabel.Text = "DisplayLift samples six small terrain-focused areas from the Rust monitor. Samples are analyzed in memory and are never saved.";
        _detectorDetailsLabel.ForeColor = MutedColor;
        _detectorDetailsLabel.Location = new Point(20, 45);
        _detectorDetailsLabel.Size = new Size(540, 42);
        _detectorDetailsLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        _detectorDetailsLabel.AutoEllipsis = true;

        _effectDetailsLabel.Text = RustVisualPresets.Create(RustScene.Balanced, _settings).ToCompactString();
        _effectDetailsLabel.ForeColor = TextColor;
        _effectDetailsLabel.Location = new Point(20, 96);
        _effectDetailsLabel.Size = new Size(540, 24);
        _effectDetailsLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        _effectDetailsLabel.AutoEllipsis = true;

        _backendLabel.Text = "Checking display backends...";
        _backendLabel.ForeColor = MutedColor;
        _backendLabel.Location = new Point(20, 127);
        _backendLabel.Size = new Size(540, 40);
        _backendLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        _backendLabel.AutoEllipsis = true;

        card.Controls.Add(_detectorDetailsLabel);
        card.Controls.Add(_effectDetailsLabel);
        card.Controls.Add(_backendLabel);
        return card;
    }

    private Control BuildTuningCard()
    {
        var card = NewCard("GLOBAL TUNING");
        var table = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 4,
            Location = new Point(16, 42),
            Size = new Size(400, 215),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        for (var row = 0; row < 4; row++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        AddTuningRow(table, 0, "Color", _colorTrack, _colorValue);
        AddTuningRow(table, 1, "Brightness", _brightnessTrack, _brightnessValue);
        AddTuningRow(table, 2, "Contrast", _contrastTrack, _contrastValue);
        AddTuningRow(table, 3, "Shadows", _shadowTrack, _shadowValue);
        card.Controls.Add(table);
        card.Resize += (_, _) => table.Width = card.ClientSize.Width - 32;
        return card;
    }

    private Control BuildRustSetupCard()
    {
        var card = NewCard("RUST SETUP");
        _pathLabel.Text = "Searching Steam libraries...";
        _pathLabel.ForeColor = MutedColor;
        _pathLabel.Location = new Point(20, 45);
        _pathLabel.Size = new Size(370, 48);
        _pathLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        _pathLabel.AutoEllipsis = true;

        var find = NewButton("FIND RUST", secondary: false);
        find.Location = new Point(20, 106);
        find.Size = new Size(120, 38);
        find.Click += (_, _) => FindRust();
        var browse = NewButton("BROWSE", secondary: true);
        browse.Location = new Point(148, 106);
        browse.Size = new Size(112, 38);
        browse.Click += (_, _) => BrowseForRust();
        var launch = NewButton("LAUNCH RUST", secondary: true);
        launch.Location = new Point(268, 106);
        launch.Size = new Size(126, 38);
        launch.Click += (_, _) => LaunchRust();

        card.Controls.Add(_pathLabel);
        card.Controls.Add(find);
        card.Controls.Add(browse);
        card.Controls.Add(launch);
        return card;
    }

    private Control BuildBehaviorCard()
    {
        var card = NewCard("BEHAVIOR");
        ConfigureCheck(_nvidiaCheck, "Use NVIDIA driver vibrance when available", 20, 45);
        ConfigureCheck(_restoreCheck, "Restore desktop colors when Rust loses focus", 20, 76);
        ConfigureCheck(_startupCheck, "Start DisplayLift with Windows", 20, 107);
        ConfigureCheck(_trayCheck, "Minimize to the system tray", 20, 138);
        card.Controls.Add(_nvidiaCheck);
        card.Controls.Add(_restoreCheck);
        card.Controls.Add(_startupCheck);
        card.Controls.Add(_trayCheck);
        return card;
    }

    private Control BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Fill, BackColor = HeaderColor };
        _footerStatusLabel.Text = "Ctrl+Alt+F8 Auto  •  Ctrl+Alt+F9 Cycle region  •  Ctrl+Alt+F10 Restore";
        _footerStatusLabel.ForeColor = MutedColor;
        _footerStatusLabel.AutoSize = true;
        _footerStatusLabel.Location = new Point(20, 9);
        footer.Controls.Add(_footerStatusLabel);
        return footer;
    }

    private void HookEvents()
    {
        _autoToggle.CheckedChanged += (_, _) =>
        {
            if (_syncingControls) return;
            _settings.AutoDetectScene = _autoToggle.Checked;
            if (_settings.AutoDetectScene)
            {
                _suspended = false;
                _stabilizer.Reset(_activeScene ?? RustScene.Balanced);
            }
            SaveSettings();
            UpdateAutoToggleAppearance();
            UpdateSceneButtons();
            EvaluateRustState();
        };

        _colorTrack.ValueChanged += (_, _) => { if (!_syncingControls) { _settings.ColorStrengthPercent = _colorTrack.Value; TuningChanged(); } };
        _brightnessTrack.ValueChanged += (_, _) => { if (!_syncingControls) { _settings.BrightnessTrimPercent = _brightnessTrack.Value; TuningChanged(); } };
        _contrastTrack.ValueChanged += (_, _) => { if (!_syncingControls) { _settings.ContrastStrengthPercent = _contrastTrack.Value; TuningChanged(); } };
        _shadowTrack.ValueChanged += (_, _) => { if (!_syncingControls) { _settings.ShadowAssistPercent = _shadowTrack.Value; TuningChanged(); } };

        _nvidiaCheck.CheckedChanged += (_, _) =>
        {
            if (_syncingControls) return;
            _settings.UseNvidiaVibrance = _nvidiaCheck.Checked;
            SaveSettings();
            ScheduleReapply();
        };
        _restoreCheck.CheckedChanged += (_, _) => { if (!_syncingControls) { _settings.RestoreWhenRustInactive = _restoreCheck.Checked; SaveSettings(); } };
        _trayCheck.CheckedChanged += (_, _) => { if (!_syncingControls) { _settings.MinimizeToTray = _trayCheck.Checked; SaveSettings(); } };
        _startupCheck.CheckedChanged += (_, _) =>
        {
            if (_syncingControls) return;
            try
            {
                StartupManager.SetEnabled(_startupCheck.Checked);
                _settings.StartWithWindows = _startupCheck.Checked;
                SaveSettings();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Startup setting", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _syncingControls = true;
                _startupCheck.Checked = StartupManager.IsEnabled();
                _syncingControls = false;
            }
        };
    }

    private void LoadSettingsIntoControls()
    {
        _syncingControls = true;
        _autoToggle.Checked = _settings.AutoDetectScene;
        _colorTrack.Value = _settings.ColorStrengthPercent;
        _brightnessTrack.Value = _settings.BrightnessTrimPercent;
        _contrastTrack.Value = _settings.ContrastStrengthPercent;
        _shadowTrack.Value = _settings.ShadowAssistPercent;
        _nvidiaCheck.Checked = _settings.UseNvidiaVibrance;
        _restoreCheck.Checked = _settings.RestoreWhenRustInactive;
        _startupCheck.Checked = StartupManager.IsEnabled();
        _trayCheck.Checked = _settings.MinimizeToTray;
        _syncingControls = false;
        UpdateAutoToggleAppearance();
    }

    private void EvaluateRustState()
    {
        if (_shutdown) return;
        var foreground = ForegroundProcess.GetInfo();
        var rustForeground = ForegroundProcess.IsRust(foreground.ProcessName);
        var rustRunning = rustForeground || ForegroundProcess.IsRustRunning();

        if (!rustForeground)
        {
            _rustStateLabel.Text = rustRunning ? "RUST RUNNING — ALT-TABBED" : "WAITING FOR RUST";
            _rustStateLabel.ForeColor = rustRunning ? WarningColor : MutedColor;
            _confidenceLabel.Text = _settings.AutoDetectScene ? "Auto detection starts when Rust is foreground" : $"Manual: {RustSceneCatalog.GetName(_settings.ManualScene)}";
            _confidenceBar.Value = 0;
            if (_effectsApplied && _settings.RestoreWhenRustInactive) RestoreOriginal(null, suspend: false);
            return;
        }

        _rustStateLabel.Text = "RUST ACTIVE";
        _rustStateLabel.ForeColor = GoodColor;

        if (_suspended)
        {
            _sceneLabel.Text = "Paused";
            _sceneDescriptionLabel.Text = "Original desktop colors are active. Turn Auto on or choose a manual region to resume.";
            _confidenceLabel.Text = "Visual changes paused";
            _confidenceBar.Value = 0;
            return;
        }

        if (!_settings.AutoDetectScene)
        {
            _sceneLabel.Text = RustSceneCatalog.GetName(_settings.ManualScene);
            _sceneDescriptionLabel.Text = RustSceneCatalog.GetDescription(_settings.ManualScene);
            _confidenceLabel.Text = "Manual region selected";
            _confidenceBar.Value = 100;
            if (!_effectsApplied || _activeScene != _settings.ManualScene) ApplyScene(_settings.ManualScene, "Manual region");
            return;
        }

        try
        {
            var analysis = _detector.Analyze(foreground.ScreenBounds);
            var stabilized = _stabilizer.Update(analysis, _settings.DetectionSensitivityPercent);
            _sceneLabel.Text = RustSceneCatalog.GetName(stabilized.Scene);
            _sceneDescriptionLabel.Text = RustSceneCatalog.GetDescription(stabilized.Scene);
            _confidenceLabel.Text = $"{stabilized.Confidence:P0} confidence  •  {stabilized.Status}";
            _confidenceBar.Value = Math.Clamp((int)Math.Round(stabilized.Confidence * 100), 0, 100);
            _detectorDetailsLabel.Text = analysis.Summary;

            if (!_effectsApplied || _activeScene != stabilized.Scene || stabilized.Changed)
                ApplyScene(stabilized.Scene, "Auto region");
        }
        catch (Exception exception)
        {
            _confidenceLabel.Text = "Auto capture unavailable — choose a region below";
            _confidenceBar.Value = 0;
            _detectorDetailsLabel.Text = exception.Message;
            _rustStateLabel.ForeColor = WarningColor;
            if (!_effectsApplied) ApplyScene(RustScene.Balanced, "Balanced fallback");
        }
    }

    private void ApplyScene(RustScene scene, string source)
    {
        var visual = RustVisualPresets.Create(scene, _settings);
        var result = _effects.Apply(visual);
        _activeScene = scene;
        _effectsApplied = result.Applied;
        _effectDetailsLabel.Text = visual.ToCompactString();
        _footerStatusLabel.Text = result.Applied
            ? $"{source}: {RustSceneCatalog.GetName(scene)}  •  {result.Message}"
            : result.Message;
        _footerStatusLabel.ForeColor = result.Applied ? GoodColor : WarningColor;
        UpdateSceneButtons();
    }

    private void RestoreOriginal(string? message, bool suspend = false)
    {
        var result = _effects.Restore();
        _effectsApplied = false;
        _activeScene = null;
        if (suspend) _suspended = true;
        _footerStatusLabel.Text = message ?? result.Message;
        _footerStatusLabel.ForeColor = MutedColor;
        UpdateSceneButtons();
    }

    private void EnableAutoMode()
    {
        _suspended = false;
        _syncingControls = true;
        _autoToggle.Checked = true;
        _syncingControls = false;
        _settings.AutoDetectScene = true;
        _stabilizer.Reset(_activeScene ?? RustScene.Balanced);
        SaveSettings();
        UpdateAutoToggleAppearance();
        UpdateSceneButtons();
        EvaluateRustState();
    }

    private void SelectManualScene(RustScene scene)
    {
        _suspended = false;
        _syncingControls = true;
        _autoToggle.Checked = false;
        _syncingControls = false;
        _settings.AutoDetectScene = false;
        _settings.ManualScene = scene;
        SaveSettings();
        UpdateAutoToggleAppearance();
        UpdateSceneButtons();
        if (ForegroundProcess.IsRust(ForegroundProcess.GetInfo().ProcessName)) ApplyScene(scene, "Manual region");
        else
        {
            _sceneLabel.Text = RustSceneCatalog.GetName(scene);
            _sceneDescriptionLabel.Text = RustSceneCatalog.GetDescription(scene);
            _confidenceLabel.Text = "Manual region ready — launch or focus Rust";
            _effectDetailsLabel.Text = RustVisualPresets.Create(scene, _settings).ToCompactString();
        }
    }

    private void CycleManualScene()
    {
        var scenes = new[] { RustScene.Balanced, RustScene.Temperate, RustScene.Desert, RustScene.Snow, RustScene.Coast, RustScene.NightInterior };
        var current = _settings.AutoDetectScene ? RustScene.Balanced : _settings.ManualScene;
        var index = Array.IndexOf(scenes, current);
        SelectManualScene(scenes[(index + 1) % scenes.Length]);
    }

    private void TuningChanged()
    {
        UpdateTuningLabels();
        SaveSettings();
        var scene = _settings.AutoDetectScene ? (_activeScene ?? RustScene.Balanced) : _settings.ManualScene;
        _effectDetailsLabel.Text = RustVisualPresets.Create(scene, _settings).ToCompactString();
        ScheduleReapply();
    }

    private void ScheduleReapply()
    {
        _reapplyTimer.Stop();
        _reapplyTimer.Start();
    }

    private void UpdateTuningLabels()
    {
        _colorValue.Text = $"{_colorTrack.Value}%";
        _brightnessValue.Text = _brightnessTrack.Value == 0 ? "0" : $"{_brightnessTrack.Value:+#;-#}%";
        _contrastValue.Text = $"{_contrastTrack.Value}%";
        _shadowValue.Text = $"{_shadowTrack.Value}%";
    }

    private void UpdateAutoToggleAppearance()
    {
        _autoToggle.Text = _settings.AutoDetectScene ? "AUTO ON" : "AUTO OFF";
        _autoToggle.BackColor = _settings.AutoDetectScene ? AccentColor : CardRaisedColor;
        _autoToggle.ForeColor = TextColor;
    }

    private void UpdateSceneButtons()
    {
        foreach (var pair in _sceneButtons)
        {
            var selected = !_settings.AutoDetectScene && pair.Key == _settings.ManualScene;
            pair.Value.BackColor = selected ? AccentColor : CardRaisedColor;
            pair.Value.ForeColor = TextColor;
        }
    }

    private void UpdateBackendDisplay()
    {
        var colorStatus = _effects.ColorMatrixAvailable ? "Windows color matrix ready" : "Windows color matrix unavailable";
        _backendLabel.Text = $"{colorStatus}. {_effects.NvidiaStatus}";
        _backendLabel.ForeColor = _effects.ColorMatrixAvailable ? MutedColor : WarningColor;
    }

    private void FindRust()
    {
        var found = RustLocator.FindExecutable();
        if (string.IsNullOrWhiteSpace(found))
        {
            MessageBox.Show("Rust was not found in the Steam libraries Windows reported. DisplayLift can still detect RustClient.exe when the game runs, or you can browse to it manually.", "Rust not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _settings.RustExecutablePath = found;
        SaveSettings();
        UpdateRustPathDisplay();
    }

    private void BrowseForRust()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select RustClient.exe",
            Filter = "Rust client|RustClient.exe|Executable files|*.exe",
            CheckFileExists = true,
            FileName = "RustClient.exe"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!string.Equals(Path.GetFileName(dialog.FileName), "RustClient.exe", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Select RustClient.exe from the Rust installation folder.", "Wrong executable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _settings.RustExecutablePath = dialog.FileName;
        SaveSettings();
        UpdateRustPathDisplay();
    }

    private void LaunchRust()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_settings.RustExecutablePath) && File.Exists(_settings.RustExecutablePath))
            {
                Process.Start(new ProcessStartInfo(_settings.RustExecutablePath) { UseShellExecute = true });
                return;
            }
            Process.Start(new ProcessStartInfo("steam://rungameid/252490") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Could not launch Rust", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateRustPathDisplay()
    {
        _pathLabel.Text = string.IsNullOrWhiteSpace(_settings.RustExecutablePath)
            ? "RustClient.exe will be detected by process name. Use Find Rust only for the launch button."
            : $"RustClient.exe\n{_settings.RustExecutablePath}";
        _pathLabel.ForeColor = string.IsNullOrWhiteSpace(_settings.RustExecutablePath) ? WarningColor : MutedColor;
    }

    private void SaveSettings()
    {
        try { _store.Save(_settings); }
        catch (Exception exception) { _footerStatusLabel.Text = $"Could not save settings: {exception.Message}"; _footerStatusLabel.ForeColor = WarningColor; }
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open DisplayLift", null, (_, _) => ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Auto region", null, (_, _) => EnableAutoMode());
        foreach (var scene in new[] { RustScene.Balanced, RustScene.Temperate, RustScene.Desert, RustScene.Snow, RustScene.Coast, RustScene.NightInterior })
        {
            var captured = scene;
            menu.Items.Add(RustSceneCatalog.GetName(scene), null, (_, _) => SelectManualScene(captured));
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Restore original", null, (_, _) => RestoreOriginal("Original display settings restored — automatic changes paused", suspend: true));
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        var icon = new NotifyIcon
        {
            Text = "DisplayLift Rust Auto Visuals",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => ShowFromTray();
        return icon;
    }

    private void RegisterGlobalHotkeys()
    {
        _ = RegisterHotKey(Handle, AutoHotkeyId, ModControl | ModAlt, (uint)Keys.F8);
        _ = RegisterHotKey(Handle, CycleHotkeyId, ModControl | ModAlt, (uint)Keys.F9);
        _ = RegisterHotKey(Handle, RestoreHotkeyId, ModControl | ModAlt, (uint)Keys.F10);
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        WindowState = FormWindowState.Normal;
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (!_allowClose && eventArgs.CloseReason == CloseReason.UserClosing && _settings.MinimizeToTray)
        {
            eventArgs.Cancel = true;
            HideToTray();
            return;
        }
        Shutdown();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
    }

    private void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        _monitorTimer.Stop();
        _reapplyTimer.Stop();
        _ = UnregisterHotKey(Handle, AutoHotkeyId);
        _ = UnregisterHotKey(Handle, CycleHotkeyId);
        _ = UnregisterHotKey(Handle, RestoreHotkeyId);
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _detector.Dispose();
        _effects.Dispose();
    }

    private static Panel NewCard(string title)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CardColor,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(1)
        };
        card.Paint += (_, eventArgs) =>
        {
            using var pen = new Pen(BorderColor);
            eventArgs.Graphics.DrawRectangle(pen, 0, 0, card.ClientSize.Width - 1, card.ClientSize.Height - 1);
        };
        card.Controls.Add(new Label
        {
            Text = title,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 17)
        });
        return card;
    }

    private static Button NewButton(string text, bool secondary)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = secondary ? CardRaisedColor : AccentColor,
            ForeColor = TextColor,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = secondary ? 1 : 0;
        button.FlatAppearance.BorderColor = BorderColor;
        button.MouseEnter += (_, _) => { if (button.Enabled) button.BackColor = AccentHoverColor; };
        button.MouseLeave += (_, _) =>
        {
            if (button.Tag is RustScene scene && button.FindForm() is MainForm form && !form._settings.AutoDetectScene && form._settings.ManualScene == scene)
                button.BackColor = AccentColor;
            else
                button.BackColor = secondary ? CardRaisedColor : AccentColor;
        };
        return button;
    }

    private static TrackBar CreateTrackBar(int minimum, int maximum, int value, int tickFrequency) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Value = value,
        TickFrequency = tickFrequency,
        TickStyle = TickStyle.None,
        Dock = DockStyle.Fill,
        Margin = new Padding(5, 10, 5, 5)
    };

    private static Label NewValueLabel() => new()
    {
        ForeColor = TextColor,
        TextAlign = ContentAlignment.MiddleRight,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold)
    };

    private static void AddTuningRow(TableLayoutPanel table, int row, string label, TrackBar track, Label value)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            ForeColor = MutedColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(4)
        }, 0, row);
        table.Controls.Add(track, 1, row);
        table.Controls.Add(value, 2, row);
    }

    private static void ConfigureCheck(CheckBox checkBox, string text, int x, int y)
    {
        checkBox.Text = text;
        checkBox.ForeColor = TextColor;
        checkBox.BackColor = CardColor;
        checkBox.AutoSize = true;
        checkBox.Location = new Point(x, y);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
