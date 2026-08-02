using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DisplayLift;

internal sealed class MainForm : Form
{
    private const int WmHotkey = 0x0312;
    private const int CycleHotkeyId = 7001;
    private const int RestoreHotkeyId = 7002;
    private const int AutoHotkeyId = 7003;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;

    private static readonly Color WindowColor = Color.FromArgb(16, 18, 20);
    private static readonly Color PanelColor = Color.FromArgb(25, 28, 31);
    private static readonly Color CardColor = Color.FromArgb(31, 35, 39);
    private static readonly Color InputColor = Color.FromArgb(38, 42, 46);
    private static readonly Color AccentColor = Color.FromArgb(235, 103, 45);
    private static readonly Color AccentHoverColor = Color.FromArgb(255, 124, 62);
    private static readonly Color MutedColor = Color.FromArgb(171, 178, 185);
    private static readonly Color SuccessColor = Color.FromArgb(66, 201, 127);

    private readonly ProfileStore _profileStore = new();
    private readonly AppConfiguration _configuration;
    private readonly DisplayEffectEngine _effects = new();
    private readonly System.Windows.Forms.Timer _profileTimer = new();
    private readonly System.Windows.Forms.Timer _previewTimer = new();
    private readonly NotifyIcon _trayIcon;

    private readonly TabControl _tabs = new();
    private readonly ListBox _profileList = new();
    private readonly ComboBox _profileSelector = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _processBox = new();
    private readonly TextBox _pathBox = new();
    private readonly ComboBox _triggerBox = new();
    private readonly NumericUpDown _priorityBox = new();
    private readonly CheckBox _enabledBox = new();
    private readonly CheckBox _restoreOnDeactivateBox = new();
    private readonly CheckBox _useNvidiaBox = new();
    private readonly CheckBox _livePreviewBox = new();
    private readonly CheckBox _autoStartBox = new();
    private readonly CheckBox _restoreInactiveBox = new();
    private readonly CheckBox _minimizeToTrayBox = new();
    private readonly NumericUpDown _pollIntervalBox = new();

    private readonly TrackBar _saturationTrack = CreateTrackBar(0, 300, 152, 10);
    private readonly TrackBar _vibranceTrack = CreateTrackBar(0, 100, 80, 10);
    private readonly TrackBar _brightnessTrack = CreateTrackBar(-20, 20, 6, 2);
    private readonly TrackBar _contrastTrack = CreateTrackBar(50, 150, 105, 5);
    private readonly TrackBar _exposureTrack = CreateTrackBar(-100, 100, 0, 10);
    private readonly TrackBar _gammaTrack = CreateTrackBar(60, 180, 108, 10);
    private readonly TrackBar _shadowTrack = CreateTrackBar(0, 60, 8, 5);
    private readonly TrackBar _temperatureTrack = CreateTrackBar(-100, 100, -2, 10);
    private readonly TrackBar _tintTrack = CreateTrackBar(-100, 100, 1, 10);
    private readonly TrackBar _redGainTrack = CreateTrackBar(70, 130, 101, 5);
    private readonly TrackBar _greenGainTrack = CreateTrackBar(70, 130, 100, 5);
    private readonly TrackBar _blueGainTrack = CreateTrackBar(70, 130, 103, 5);

    private readonly Label _saturationValue = CreateValueLabel();
    private readonly Label _vibranceValue = CreateValueLabel();
    private readonly Label _brightnessValue = CreateValueLabel();
    private readonly Label _contrastValue = CreateValueLabel();
    private readonly Label _exposureValue = CreateValueLabel();
    private readonly Label _gammaValue = CreateValueLabel();
    private readonly Label _shadowValue = CreateValueLabel();
    private readonly Label _temperatureValue = CreateValueLabel();
    private readonly Label _tintValue = CreateValueLabel();
    private readonly Label _redGainValue = CreateValueLabel();
    private readonly Label _greenGainValue = CreateValueLabel();
    private readonly Label _blueGainValue = CreateValueLabel();

    private readonly Label _engineStatusLabel = new();
    private readonly Label _activeModeLabel = new();
    private readonly Label _foregroundLabel = new();
    private readonly Label _driverLabel = new();
    private readonly Label _presetDescriptionLabel = new();
    private readonly Label _selectedProfileLabel = new();
    private readonly Label _rustPathLabel = new();

    private DisplayProfile? _editingProfile;
    private Guid? _activeProfileId;
    private bool _loadingEditor;
    private bool _syncingSelection;
    private bool _manualMode;
    private bool _allowClose;
    private bool _shutdown;

    public MainForm(bool startMinimized)
    {
        _configuration = _profileStore.Load();

        Text = "DisplayLift Visual Panel";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 720);
        Size = new Size(1240, 830);
        BackColor = WindowColor;
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

        _trayIcon = CreateTrayIcon();
        Controls.Add(BuildRoot());
        HookEvents();
        ApplyTheme(this);

        RefreshProfileControls(_configuration.LastSelectedProfileId);
        LoadSettingsControls();
        UpdateValueLabels();
        UpdateStatus("Engine ready. Select a preset or let Rust auto-activation take over.", SuccessColor);

        _previewTimer.Interval = 120;
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            PreviewCurrentSettings();
        };

        _profileTimer.Interval = _configuration.PollIntervalMilliseconds;
        _profileTimer.Tick += (_, _) => EvaluateAutomaticProfiles();
        _profileTimer.Start();

        Shown += (_, _) =>
        {
            RegisterGlobalHotkeys();
            EvaluateAutomaticProfiles();
            if (startMinimized)
            {
                HideToTray();
            }
        };

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized && _configuration.MinimizeToTray)
            {
                HideToTray();
            }
        };

        FormClosing += HandleFormClosing;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey)
        {
            switch (message.WParam.ToInt32())
            {
                case CycleHotkeyId:
                    CycleRustPreset();
                    return;
                case RestoreHotkeyId:
                    RestoreOriginal(showMessage: true);
                    return;
                case AutoHotkeyId:
                    ResumeAutomaticProfiles();
                    return;
            }
        }

        base.WndProc(ref message);
    }

    private Control BuildRoot()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = WindowColor,
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);

        _tabs.Dock = DockStyle.Fill;
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.ItemSize = new Size(150, 38);
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.Padding = new Point(18, 6);
        _tabs.DrawItem += DrawTab;
        _tabs.TabPages.Add(BuildRustVisualTab());
        _tabs.TabPages.Add(BuildProfilesTab());
        _tabs.TabPages.Add(BuildAdvancedTab());
        _tabs.TabPages.Add(BuildAutomationTab());
        root.Controls.Add(_tabs, 0, 1);
        return root;
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            Padding = new Padding(24, 12, 24, 10)
        };

        var title = new Label
        {
            Text = "DISPLAYLIFT",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
            Location = new Point(24, 12)
        };
        var subtitle = new Label
        {
            Text = "RUST VISUAL PANEL  •  WINDOWS 10/11  •  EXTERNAL DISPLAY CONTROLS",
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(27, 48)
        };

        _engineStatusLabel.AutoSize = false;
        _engineStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _engineStatusLabel.Text = "ENGINE READY";
        _engineStatusLabel.ForeColor = Color.White;
        _engineStatusLabel.BackColor = Color.FromArgb(37, 113, 73);
        _engineStatusLabel.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _engineStatusLabel.Size = new Size(160, 34);
        _engineStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _engineStatusLabel.Location = new Point(Width - 210, 21);
        header.Resize += (_, _) => _engineStatusLabel.Left = Math.Max(24, header.ClientSize.Width - _engineStatusLabel.Width - 24);

        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_engineStatusLabel);
        return header;
    }

    private TabPage BuildRustVisualTab()
    {
        var tab = NewTab("Rust Visual");
        var root = NewScrollingColumn();

        var profileRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Width = 1120,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 12)
        };
        profileRow.Controls.Add(new Label
        {
            Text = "TUNING PROFILE",
            AutoSize = true,
            ForeColor = MutedColor,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 9, 12, 0)
        });
        _profileSelector.Width = 300;
        _profileSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        profileRow.Controls.Add(_profileSelector);
        _selectedProfileLabel.AutoSize = true;
        _selectedProfileLabel.ForeColor = MutedColor;
        _selectedProfileLabel.Margin = new Padding(16, 9, 0, 0);
        profileRow.Controls.Add(_selectedProfileLabel);

        var intro = NewCard(1120, 92);
        intro.Controls.Add(new Label
        {
            Text = "CLEAN VISUALS. BETTER RUST VISIBILITY.",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
            Location = new Point(20, 15)
        });
        intro.Controls.Add(new Label
        {
            Text = "Real-time color, vibrance, light and tone controls with one-click biome presets.",
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(22, 51)
        });
        var reset = CreateSecondaryButton("RESET TO NEUTRAL", (_, _) => ApplyPreset(RustVisualPreset.Neutral));
        reset.Location = new Point(930, 26);
        reset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        intro.Resize += (_, _) => reset.Left = intro.ClientSize.Width - reset.Width - 20;
        intro.Controls.Add(reset);

        var presetTitle = SectionTitle("ONE-CLICK RUST PROFILES");
        var presets = new FlowLayoutPanel
        {
            AutoSize = true,
            Width = 1120,
            WrapContents = true,
            Margin = new Padding(0, 4, 0, 4)
        };
        foreach (var preset in new[]
        {
            RustVisualPreset.CleanRust,
            RustVisualPreset.Summer,
            RustVisualPreset.Winter,
            RustVisualPreset.Desert,
            RustVisualPreset.Night,
            RustVisualPreset.Competitive,
            RustVisualPreset.MaximumColor
        })
        {
            var captured = preset;
            presets.Controls.Add(CreatePresetButton(RustPresetCatalog.GetName(preset), (_, _) => ApplyPreset(captured)));
        }

        _presetDescriptionLabel.AutoSize = true;
        _presetDescriptionLabel.MaximumSize = new Size(1100, 0);
        _presetDescriptionLabel.ForeColor = MutedColor;
        _presetDescriptionLabel.Margin = new Padding(2, 5, 0, 14);

        var sliderGrid = new TableLayoutPanel
        {
            Width = 1120,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 14)
        };
        sliderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        sliderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        sliderGrid.Controls.Add(BuildSliderCard("COLOR", new[]
        {
            ("Saturation", _saturationTrack, _saturationValue),
            ("Vibrance", _vibranceTrack, _vibranceValue),
            ("Brightness", _brightnessTrack, _brightnessValue),
            ("Contrast", _contrastTrack, _contrastValue)
        }), 0, 0);
        sliderGrid.Controls.Add(BuildSliderCard("LIGHT & TONE", new[]
        {
            ("Exposure", _exposureTrack, _exposureValue),
            ("Gamma / midtones", _gammaTrack, _gammaValue),
            ("Shadow lift", _shadowTrack, _shadowValue),
            ("Temperature", _temperatureTrack, _temperatureValue),
            ("Tint", _tintTrack, _tintValue)
        }), 1, 0);

        _livePreviewBox.Text = "LIVE PREVIEW — apply changes while dragging";
        _livePreviewBox.AutoSize = true;
        _livePreviewBox.Checked = true;
        _livePreviewBox.ForeColor = Color.WhiteSmoke;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Width = 1120,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 6)
        };
        actions.Controls.Add(CreatePrimaryButton("SAVE + APPLY", (_, _) => SaveAndApplySelectedProfile()));
        actions.Controls.Add(CreateSecondaryButton("SAVE PROFILE", (_, _) => SaveSelectedProfile()));
        actions.Controls.Add(CreateSecondaryButton("RESTORE ORIGINAL", (_, _) => RestoreOriginal(showMessage: true)));
        actions.Controls.Add(CreateSecondaryButton("RESUME AUTO", (_, _) => ResumeAutomaticProfiles()));

        _activeModeLabel.AutoSize = true;
        _activeModeLabel.MaximumSize = new Size(1100, 0);
        _activeModeLabel.ForeColor = MutedColor;
        _activeModeLabel.Margin = new Padding(2, 8, 0, 0);

        root.Controls.Add(profileRow);
        root.Controls.Add(intro);
        root.Controls.Add(presetTitle);
        root.Controls.Add(presets);
        root.Controls.Add(_presetDescriptionLabel);
        root.Controls.Add(sliderGrid);
        root.Controls.Add(_livePreviewBox);
        root.Controls.Add(actions);
        root.Controls.Add(_activeModeLabel);
        tab.Controls.Add(root);
        return tab;
    }

    private TabPage BuildProfilesTab()
    {
        var tab = NewTab("Profiles");
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 380,
            BackColor = WindowColor,
            Padding = new Padding(18)
        };

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = WindowColor
        };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.Controls.Add(new Label
        {
            Text = "SAVED APPLICATION PROFILES",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);

        _profileList.Dock = DockStyle.Fill;
        _profileList.IntegralHeight = false;
        left.Controls.Add(_profileList, 0, 1);

        var profileButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        profileButtons.Controls.Add(CreatePrimaryButton("ADD RUNNING", (_, _) => AddRunningProfile()));
        profileButtons.Controls.Add(CreateSecondaryButton("BROWSE EXE", (_, _) => AddProfileFromExecutable()));
        profileButtons.Controls.Add(CreateSecondaryButton("BLANK", (_, _) => AddBlankProfile()));
        profileButtons.Controls.Add(CreateSecondaryButton("CLONE", (_, _) => CloneSelectedProfile()));
        profileButtons.Controls.Add(CreateSecondaryButton("DELETE", (_, _) => DeleteSelectedProfile()));
        profileButtons.Controls.Add(CreateSecondaryButton("IMPORT", (_, _) => ImportProfile()));
        profileButtons.Controls.Add(CreateSecondaryButton("EXPORT", (_, _) => ExportProfile()));
        left.Controls.Add(profileButtons, 0, 2);
        split.Panel1.Controls.Add(left);

        var editorScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowColor };
        var editor = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(20),
            BackColor = CardColor,
            Width = 700
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));
        editor.Controls.Add(new Label
        {
            Text = "PROFILE DETAILS",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 18)
        }, 0, 0);
        editor.SetColumnSpan(editor.GetControlFromPosition(0, 0), 2);

        AddEditorRow(editor, 1, "Name", _nameBox);
        AddEditorRow(editor, 2, "Process", _processBox);

        var pathPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pathBox.Dock = DockStyle.Fill;
        pathPanel.Controls.Add(_pathBox, 0, 0);
        pathPanel.Controls.Add(CreateSecondaryButton("…", (_, _) => BrowsePathForSelectedProfile()), 1, 0);
        AddEditorRow(editor, 3, "Executable", pathPanel);

        _triggerBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _triggerBox.Items.AddRange(Enum.GetValues<ProfileTrigger>().Cast<object>().ToArray());
        AddEditorRow(editor, 4, "Activation", _triggerBox);

        _priorityBox.Minimum = 0;
        _priorityBox.Maximum = 999;
        _priorityBox.Width = 120;
        AddEditorRow(editor, 5, "Priority", _priorityBox);

        _enabledBox.Text = "Profile enabled";
        _enabledBox.AutoSize = true;
        editor.Controls.Add(_enabledBox, 1, 6);
        _restoreOnDeactivateBox.Text = "Restore original display when this profile deactivates";
        _restoreOnDeactivateBox.AutoSize = true;
        editor.Controls.Add(_restoreOnDeactivateBox, 1, 7);

        var info = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(600, 0),
            ForeColor = MutedColor,
            Text = "Select a profile here, then use the Rust Visual and Advanced tabs to tune its color settings. Profiles can activate only when focused or whenever their process is running.",
            Margin = new Padding(0, 14, 0, 14)
        };
        editor.Controls.Add(info, 0, 8);
        editor.SetColumnSpan(info, 2);

        var editorActions = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        editorActions.Controls.Add(CreatePrimaryButton("SAVE PROFILE", (_, _) => SaveSelectedProfile()));
        editorActions.Controls.Add(CreateSecondaryButton("OPEN VISUAL TUNING", (_, _) => _tabs.SelectedIndex = 0));
        editor.Controls.Add(editorActions, 0, 9);
        editor.SetColumnSpan(editorActions, 2);
        editorScroll.Controls.Add(editor);
        split.Panel2.Controls.Add(editorScroll);
        tab.Controls.Add(split);
        return tab;
    }

    private TabPage BuildAdvancedTab()
    {
        var tab = NewTab("Advanced Color");
        var root = NewScrollingColumn();

        var card = NewCard(1120, 390);
        card.Controls.Add(new Label
        {
            Text = "ADVANCED OUTPUT CONTROL",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 18)
        });
        card.Controls.Add(new Label
        {
            Text = "Driver-level vibrance plus per-channel gains for stronger subject/background separation.",
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(22, 51)
        });

        _useNvidiaBox.Text = "Use NVIDIA driver Digital Vibrance when available";
        _useNvidiaBox.AutoSize = true;
        _useNvidiaBox.Location = new Point(22, 82);
        card.Controls.Add(_useNvidiaBox);

        var advancedGrid = new TableLayoutPanel
        {
            Location = new Point(18, 120),
            Size = new Size(1065, 210),
            ColumnCount = 3,
            RowCount = 3,
            BackColor = CardColor
        };
        advancedGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        advancedGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        advancedGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        AddSliderRow(advancedGrid, 0, "Red gain", _redGainTrack, _redGainValue);
        AddSliderRow(advancedGrid, 1, "Green gain", _greenGainTrack, _greenGainValue);
        AddSliderRow(advancedGrid, 2, "Blue gain", _blueGainTrack, _blueGainValue);
        card.Controls.Add(advancedGrid);

        var resetAdvanced = CreateSecondaryButton("RESET RGB GAINS", (_, _) =>
        {
            _redGainTrack.Value = 100;
            _greenGainTrack.Value = 100;
            _blueGainTrack.Value = 100;
        });
        resetAdvanced.Location = new Point(22, 342);
        card.Controls.Add(resetAdvanced);

        _driverLabel.AutoSize = true;
        _driverLabel.MaximumSize = new Size(1080, 0);
        _driverLabel.ForeColor = MutedColor;
        _driverLabel.Text = _effects.NvidiaStatus;

        var explanation = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            ForeColor = MutedColor,
            Text = "Vibrance is applied through NVIDIA's display driver on supported systems. Saturation, contrast, brightness, exposure, temperature, tint and RGB gains use a Windows desktop color matrix. If driver vibrance is unavailable, DisplayLift approximates the vibrance slider through that matrix. Gamma and shadow lift use the Windows gamma ramp."
        };

        root.Controls.Add(card);
        root.Controls.Add(SectionTitle("BACKEND STATUS"));
        root.Controls.Add(_driverLabel);
        root.Controls.Add(explanation);
        tab.Controls.Add(root);
        return tab;
    }

    private TabPage BuildAutomationTab()
    {
        var tab = NewTab("Automation & Safety");
        var root = NewScrollingColumn();

        var settingsCard = NewCard(1120, 250);
        settingsCard.Controls.Add(new Label
        {
            Text = "AUTOMATIC PROFILE SWITCHING",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 18)
        });

        _autoStartBox.Text = "Start DisplayLift with Windows, minimized to tray";
        _autoStartBox.AutoSize = true;
        _autoStartBox.Location = new Point(22, 62);
        settingsCard.Controls.Add(_autoStartBox);

        _restoreInactiveBox.Text = "Restore the original display when no profile matches";
        _restoreInactiveBox.AutoSize = true;
        _restoreInactiveBox.Location = new Point(22, 94);
        settingsCard.Controls.Add(_restoreInactiveBox);

        _minimizeToTrayBox.Text = "Closing or minimizing hides DisplayLift in the system tray";
        _minimizeToTrayBox.AutoSize = true;
        _minimizeToTrayBox.Location = new Point(22, 126);
        settingsCard.Controls.Add(_minimizeToTrayBox);

        var pollLabel = new Label
        {
            Text = "Detection interval (milliseconds)",
            AutoSize = true,
            ForeColor = MutedColor,
            Location = new Point(22, 169)
        };
        settingsCard.Controls.Add(pollLabel);
        _pollIntervalBox.Minimum = 150;
        _pollIntervalBox.Maximum = 3000;
        _pollIntervalBox.Increment = 50;
        _pollIntervalBox.Width = 110;
        _pollIntervalBox.Location = new Point(245, 164);
        settingsCard.Controls.Add(_pollIntervalBox);

        var resume = CreatePrimaryButton("RESUME AUTOMATIC MODE", (_, _) => ResumeAutomaticProfiles());
        resume.Location = new Point(22, 205);
        settingsCard.Controls.Add(resume);

        var rustCard = NewCard(1120, 150);
        rustCard.Controls.Add(new Label
        {
            Text = "RUST DETECTION",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 16)
        });
        _rustPathLabel.AutoSize = true;
        _rustPathLabel.MaximumSize = new Size(850, 0);
        _rustPathLabel.ForeColor = MutedColor;
        _rustPathLabel.Location = new Point(22, 52);
        rustCard.Controls.Add(_rustPathLabel);
        var detect = CreateSecondaryButton("DETECT RUST AGAIN", (_, _) => DetectRustPath());
        detect.Location = new Point(22, 98);
        rustCard.Controls.Add(detect);

        var diagnostics = NewCard(1120, 190);
        diagnostics.Controls.Add(new Label
        {
            Text = "STATUS & HOTKEYS",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 16)
        });
        _foregroundLabel.AutoSize = true;
        _foregroundLabel.ForeColor = MutedColor;
        _foregroundLabel.Location = new Point(22, 50);
        diagnostics.Controls.Add(_foregroundLabel);
        var hotkeys = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1040, 0),
            ForeColor = MutedColor,
            Location = new Point(22, 78),
            Text = "Ctrl+Alt+F9 cycles Rust presets  •  Ctrl+Alt+F10 restores original settings  •  Ctrl+Alt+F11 resumes automatic switching"
        };
        diagnostics.Controls.Add(hotkeys);
        var openData = CreateSecondaryButton("OPEN PROFILE DATA", (_, _) => OpenProfileFolder());
        openData.Location = new Point(22, 124);
        diagnostics.Controls.Add(openData);
        var exit = CreateSecondaryButton("EXIT DISPLAYLIFT", (_, _) =>
        {
            _allowClose = true;
            Close();
        });
        exit.Location = new Point(190, 124);
        diagnostics.Controls.Add(exit);

        var safety = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            ForeColor = MutedColor,
            Text = "Safety boundary: DisplayLift does not inject DLLs, modify Rust files, draw an overlay, read game memory, automate input or bypass Easy Anti-Cheat. It observes only process names and changes external Windows/NVIDIA display settings. This is an independent implementation and is not affiliated with Rust, Facepunch or No Mercy Visual."
        };

        root.Controls.Add(settingsCard);
        root.Controls.Add(rustCard);
        root.Controls.Add(diagnostics);
        root.Controls.Add(SectionTitle("BOUNDARY"));
        root.Controls.Add(safety);
        tab.Controls.Add(root);
        return tab;
    }

    private GroupBox BuildSliderCard(string title, (string Name, TrackBar Track, Label Value)[] rows)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = CardColor,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 12, 0)
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = rows.Length,
            BackColor = CardColor
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        for (var index = 0; index < rows.Length; index++)
        {
            AddSliderRow(grid, index, rows[index].Name, rows[index].Track, rows[index].Value);
        }
        group.Controls.Add(grid);
        return group;
    }

    private static void AddSliderRow(TableLayoutPanel grid, int row, string text, TrackBar track, Label value)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 0, 8, 0)
        };
        track.Dock = DockStyle.Fill;
        track.BackColor = CardColor;
        track.Margin = new Padding(0, 7, 6, 0);
        value.Anchor = AnchorStyles.Right;
        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(track, 1, row);
        grid.Controls.Add(value, 2, row);
    }

    private void HookEvents()
    {
        foreach (var track in GetAllTracks())
        {
            track.ValueChanged += (_, _) =>
            {
                UpdateValueLabels();
                if (!_loadingEditor && _livePreviewBox.Checked)
                {
                    _previewTimer.Stop();
                    _previewTimer.Start();
                }
            };
        }

        _profileList.SelectedIndexChanged += (_, _) =>
        {
            if (!_syncingSelection)
            {
                SelectProfile(_profileList.SelectedItem as DisplayProfile);
            }
        };
        _profileSelector.SelectedIndexChanged += (_, _) =>
        {
            if (!_syncingSelection)
            {
                SelectProfile(_profileSelector.SelectedItem as DisplayProfile);
            }
        };

        _autoStartBox.CheckedChanged += (_, _) =>
        {
            if (_loadingEditor) return;
            try
            {
                StartupManager.SetEnabled(_autoStartBox.Checked);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "DisplayLift startup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        _restoreInactiveBox.CheckedChanged += (_, _) =>
        {
            if (_loadingEditor) return;
            _configuration.RestoreWhenNoProfile = _restoreInactiveBox.Checked;
            SaveConfiguration(false);
        };
        _minimizeToTrayBox.CheckedChanged += (_, _) =>
        {
            if (_loadingEditor) return;
            _configuration.MinimizeToTray = _minimizeToTrayBox.Checked;
            SaveConfiguration(false);
        };
        _pollIntervalBox.ValueChanged += (_, _) =>
        {
            if (_loadingEditor) return;
            _configuration.PollIntervalMilliseconds = (int)_pollIntervalBox.Value;
            _profileTimer.Interval = _configuration.PollIntervalMilliseconds;
            SaveConfiguration(false);
        };
    }

    private IEnumerable<TrackBar> GetAllTracks()
    {
        yield return _saturationTrack;
        yield return _vibranceTrack;
        yield return _brightnessTrack;
        yield return _contrastTrack;
        yield return _exposureTrack;
        yield return _gammaTrack;
        yield return _shadowTrack;
        yield return _temperatureTrack;
        yield return _tintTrack;
        yield return _redGainTrack;
        yield return _greenGainTrack;
        yield return _blueGainTrack;
    }

    private void RefreshProfileControls(Guid? selectId = null)
    {
        selectId ??= _editingProfile?.Id ?? _configuration.LastSelectedProfileId;
        _syncingSelection = true;
        try
        {
            _profileList.Items.Clear();
            _profileSelector.Items.Clear();
            foreach (var profile in _configuration.Profiles)
            {
                _profileList.Items.Add(profile);
                _profileSelector.Items.Add(profile);
            }

            var selected = _configuration.Profiles.FirstOrDefault(profile => profile.Id == selectId)
                ?? _configuration.Profiles.FirstOrDefault();
            if (selected is not null)
            {
                _profileList.SelectedItem = selected;
                _profileSelector.SelectedItem = selected;
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        SelectProfile(_profileList.SelectedItem as DisplayProfile);
    }

    private void SelectProfile(DisplayProfile? profile)
    {
        if (profile is null) return;

        if (_editingProfile is not null && !_loadingEditor && _editingProfile.Id != profile.Id)
        {
            CaptureEditor(_editingProfile);
        }

        _editingProfile = profile;
        _configuration.LastSelectedProfileId = profile.Id;
        _syncingSelection = true;
        try
        {
            _profileList.SelectedItem = profile;
            _profileSelector.SelectedItem = profile;
        }
        finally
        {
            _syncingSelection = false;
        }
        LoadEditor(profile);
        SaveConfiguration(false);
    }

    private void LoadEditor(DisplayProfile profile)
    {
        _loadingEditor = true;
        try
        {
            _nameBox.Text = profile.Name;
            _processBox.Text = profile.ProcessName;
            _pathBox.Text = profile.ExecutablePath;
            _triggerBox.SelectedItem = profile.Trigger;
            _priorityBox.Value = Math.Clamp(profile.Priority, 0, 999);
            _enabledBox.Checked = profile.Enabled;
            _restoreOnDeactivateBox.Checked = profile.RestoreOnDeactivate;
            _useNvidiaBox.Checked = profile.UseNvidiaVibrance;

            _saturationTrack.Value = Math.Clamp(profile.SaturationPercent, _saturationTrack.Minimum, _saturationTrack.Maximum);
            _vibranceTrack.Value = Math.Clamp(profile.VibrancePercent, _vibranceTrack.Minimum, _vibranceTrack.Maximum);
            _brightnessTrack.Value = Math.Clamp(profile.BrightnessPercent, _brightnessTrack.Minimum, _brightnessTrack.Maximum);
            _contrastTrack.Value = Math.Clamp(profile.ContrastPercent, _contrastTrack.Minimum, _contrastTrack.Maximum);
            _exposureTrack.Value = Math.Clamp(profile.ExposureHundredths, _exposureTrack.Minimum, _exposureTrack.Maximum);
            _gammaTrack.Value = Math.Clamp(profile.GammaPercent, _gammaTrack.Minimum, _gammaTrack.Maximum);
            _shadowTrack.Value = Math.Clamp(profile.ShadowLiftPercent, _shadowTrack.Minimum, _shadowTrack.Maximum);
            _temperatureTrack.Value = Math.Clamp(profile.Temperature, _temperatureTrack.Minimum, _temperatureTrack.Maximum);
            _tintTrack.Value = Math.Clamp(profile.Tint, _tintTrack.Minimum, _tintTrack.Maximum);
            _redGainTrack.Value = Math.Clamp(profile.RedGainPercent, _redGainTrack.Minimum, _redGainTrack.Maximum);
            _greenGainTrack.Value = Math.Clamp(profile.GreenGainPercent, _greenGainTrack.Minimum, _greenGainTrack.Maximum);
            _blueGainTrack.Value = Math.Clamp(profile.BlueGainPercent, _blueGainTrack.Minimum, _blueGainTrack.Maximum);

            _selectedProfileLabel.Text = $"Preset: {RustPresetCatalog.GetName(profile.LastPreset)}";
            _presetDescriptionLabel.Text = RustPresetCatalog.GetDescription(profile.LastPreset);
            UpdateValueLabels();
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private void CaptureEditor(DisplayProfile profile)
    {
        profile.Name = _nameBox.Text;
        profile.ProcessName = _processBox.Text;
        profile.ExecutablePath = _pathBox.Text.Trim();
        profile.Trigger = _triggerBox.SelectedItem is ProfileTrigger trigger ? trigger : ProfileTrigger.Foreground;
        profile.Priority = (int)_priorityBox.Value;
        profile.Enabled = _enabledBox.Checked;
        profile.RestoreOnDeactivate = _restoreOnDeactivateBox.Checked;
        profile.UseNvidiaVibrance = _useNvidiaBox.Checked;
        ReadVisualControls(profile);
        profile.Validate();
    }

    private void ReadVisualControls(DisplayProfile profile)
    {
        profile.SaturationPercent = _saturationTrack.Value;
        profile.VibrancePercent = _vibranceTrack.Value;
        profile.BrightnessPercent = _brightnessTrack.Value;
        profile.ContrastPercent = _contrastTrack.Value;
        profile.ExposureHundredths = _exposureTrack.Value;
        profile.GammaPercent = _gammaTrack.Value;
        profile.ShadowLiftPercent = _shadowTrack.Value;
        profile.Temperature = _temperatureTrack.Value;
        profile.Tint = _tintTrack.Value;
        profile.RedGainPercent = _redGainTrack.Value;
        profile.GreenGainPercent = _greenGainTrack.Value;
        profile.BlueGainPercent = _blueGainTrack.Value;
    }

    private void SaveSelectedProfile()
    {
        if (_editingProfile is null) return;
        CaptureEditor(_editingProfile);
        SaveConfiguration(true);
        RefreshProfileControls(_editingProfile.Id);
        UpdateStatus($"Saved profile '{_editingProfile.Name}'.", SuccessColor);
    }

    private void SaveAndApplySelectedProfile()
    {
        if (_editingProfile is null) return;
        CaptureEditor(_editingProfile);
        SaveConfiguration(true);
        _manualMode = true;
        _activeProfileId = _editingProfile.Id;
        var result = _effects.Apply(_editingProfile);
        UpdateStatus(result.Message, result.Applied ? SuccessColor : Color.IndianRed);
        UpdateModeLabel();
    }

    private void PreviewCurrentSettings()
    {
        if (_editingProfile is null || _loadingEditor) return;
        var preview = _editingProfile.Clone(_editingProfile.Name);
        preview.UseNvidiaVibrance = _useNvidiaBox.Checked;
        ReadVisualControls(preview);
        _manualMode = true;
        _activeProfileId = _editingProfile.Id;
        var result = _effects.Apply(preview);
        UpdateStatus(result.Message, result.Applied ? SuccessColor : Color.IndianRed);
        UpdateModeLabel();
    }

    private void ApplyPreset(RustVisualPreset preset)
    {
        var rust = GetRustProfile() ?? _editingProfile;
        if (rust is null) return;
        SelectProfile(rust);
        rust.ApplyPreset(preset);
        LoadEditor(rust);
        SaveConfiguration(false);
        _manualMode = true;
        _activeProfileId = rust.Id;
        var result = _effects.Apply(rust);
        UpdateStatus($"{RustPresetCatalog.GetName(preset)}: {result.Message}", result.Applied ? SuccessColor : Color.IndianRed);
        UpdateModeLabel();
    }

    private void CycleRustPreset()
    {
        var rust = GetRustProfile();
        if (rust is null) return;
        var sequence = new[]
        {
            RustVisualPreset.CleanRust,
            RustVisualPreset.Summer,
            RustVisualPreset.Winter,
            RustVisualPreset.Desert,
            RustVisualPreset.Night,
            RustVisualPreset.Competitive,
            RustVisualPreset.MaximumColor
        };
        var index = Array.IndexOf(sequence, rust.LastPreset);
        var next = sequence[(index + 1 + sequence.Length) % sequence.Length];
        ApplyPreset(next);
    }

    private void RestoreOriginal(bool showMessage)
    {
        _manualMode = true;
        _activeProfileId = null;
        var result = _effects.Restore();
        UpdateStatus(result.Message, result.Applied ? SuccessColor : Color.IndianRed);
        UpdateModeLabel();
        if (showMessage && !result.Applied)
        {
            MessageBox.Show(result.Message, "DisplayLift restore", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ResumeAutomaticProfiles()
    {
        _manualMode = false;
        _activeProfileId = null;
        UpdateStatus("Automatic profile switching resumed.", SuccessColor);
        EvaluateAutomaticProfiles();
    }

    private void EvaluateAutomaticProfiles()
    {
        var foreground = ForegroundProcess.GetName();
        _foregroundLabel.Text = string.IsNullOrWhiteSpace(foreground)
            ? "Foreground process: unavailable"
            : $"Foreground process: {foreground}.exe";

        if (_manualMode)
        {
            UpdateModeLabel();
            return;
        }

        var running = GetRunningProcessNames();
        var match = _configuration.Profiles
            .Where(profile => profile.Enabled && !string.IsNullOrWhiteSpace(profile.ProcessName))
            .Where(profile => profile.Trigger == ProfileTrigger.Foreground
                ? string.Equals(profile.ProcessName, foreground, StringComparison.OrdinalIgnoreCase)
                : running.Contains(profile.ProcessName))
            .OrderByDescending(profile => profile.Priority)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (match is not null)
        {
            if (_activeProfileId != match.Id)
            {
                var result = _effects.Apply(match);
                _activeProfileId = match.Id;
                UpdateStatus($"Auto: {match.Name}. {result.Message}", result.Applied ? SuccessColor : Color.IndianRed);
            }
        }
        else if (_activeProfileId is Guid previousId)
        {
            var previous = _configuration.Profiles.FirstOrDefault(profile => profile.Id == previousId);
            if (_configuration.RestoreWhenNoProfile && (previous?.RestoreOnDeactivate ?? true))
            {
                var result = _effects.Restore();
                UpdateStatus(result.Message, result.Applied ? SuccessColor : Color.IndianRed);
            }
            _activeProfileId = null;
        }

        UpdateModeLabel();
    }

    private HashSet<string> GetRunningProcessNames()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    result.Add(process.ProcessName);
                }
                catch
                {
                    // Process exited while enumerating.
                }
            }
        }
        return result;
    }

    private void AddBlankProfile()
    {
        var profile = new DisplayProfile { Name = "New application profile", Priority = 50 };
        profile.ApplyPreset(RustVisualPreset.CleanRust);
        _configuration.Profiles.Add(profile);
        SaveConfiguration(false);
        RefreshProfileControls(profile.Id);
    }

    private void AddRunningProfile()
    {
        using var dialog = new ProcessPickerDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var profile = new DisplayProfile
        {
            Name = dialog.SelectedProcessName,
            ProcessName = dialog.SelectedProcessName,
            ExecutablePath = dialog.SelectedExecutablePath,
            Priority = 50
        };
        profile.ApplyPreset(RustVisualPreset.CleanRust);
        _configuration.Profiles.Add(profile);
        SaveConfiguration(false);
        RefreshProfileControls(profile.Id);
    }

    private void AddProfileFromExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe",
            Title = "Choose an application"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var profile = new DisplayProfile
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            ProcessName = Path.GetFileNameWithoutExtension(dialog.FileName),
            ExecutablePath = dialog.FileName,
            Priority = 50
        };
        profile.ApplyPreset(RustVisualPreset.CleanRust);
        _configuration.Profiles.Add(profile);
        SaveConfiguration(false);
        RefreshProfileControls(profile.Id);
    }

    private void CloneSelectedProfile()
    {
        if (_editingProfile is null) return;
        CaptureEditor(_editingProfile);
        var clone = _editingProfile.Clone();
        _configuration.Profiles.Add(clone);
        SaveConfiguration(false);
        RefreshProfileControls(clone.Id);
    }

    private void DeleteSelectedProfile()
    {
        if (_editingProfile is null) return;
        if (_configuration.Profiles.Count <= 1)
        {
            MessageBox.Show("DisplayLift must keep at least one profile.", "Profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show($"Delete '{_editingProfile.Name}'?", "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }
        var deletedId = _editingProfile.Id;
        _configuration.Profiles.RemoveAll(profile => profile.Id == deletedId);
        _editingProfile = null;
        SaveConfiguration(false);
        RefreshProfileControls(_configuration.Profiles.FirstOrDefault()?.Id);
    }

    private void ImportProfile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "DisplayLift profile (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import DisplayLift profile"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var profile = _profileStore.ImportProfile(dialog.FileName);
            _configuration.Profiles.Add(profile);
            SaveConfiguration(false);
            RefreshProfileControls(profile.Id);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Import profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportProfile()
    {
        if (_editingProfile is null) return;
        CaptureEditor(_editingProfile);
        using var dialog = new SaveFileDialog
        {
            Filter = "DisplayLift profile (*.json)|*.json",
            FileName = SanitizeFileName(_editingProfile.Name) + ".json",
            Title = "Export DisplayLift profile"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _profileStore.ExportProfile(_editingProfile, dialog.FileName);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Export profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowsePathForSelectedProfile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe",
            Title = "Choose executable"
        };
        if (File.Exists(_pathBox.Text))
        {
            dialog.FileName = _pathBox.Text;
        }
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _pathBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_processBox.Text))
        {
            _processBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void DetectRustPath()
    {
        var path = ProfileStore.FindRustExecutable();
        var rust = GetRustProfile();
        if (rust is null) return;
        rust.ExecutablePath = path;
        SaveConfiguration(false);
        LoadRustPathLabel();
        UpdateStatus(string.IsNullOrWhiteSpace(path) ? "RustClient.exe was not found automatically." : "RustClient.exe detected.", string.IsNullOrWhiteSpace(path) ? Color.Goldenrod : SuccessColor);
    }

    private DisplayProfile? GetRustProfile() => _configuration.Profiles.FirstOrDefault(profile =>
        string.Equals(profile.ProcessName, "RustClient", StringComparison.OrdinalIgnoreCase));

    private void LoadSettingsControls()
    {
        _loadingEditor = true;
        try
        {
            _autoStartBox.Checked = StartupManager.IsEnabled();
            _restoreInactiveBox.Checked = _configuration.RestoreWhenNoProfile;
            _minimizeToTrayBox.Checked = _configuration.MinimizeToTray;
            _pollIntervalBox.Value = Math.Clamp(_configuration.PollIntervalMilliseconds, 150, 3000);
            LoadRustPathLabel();
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private void LoadRustPathLabel()
    {
        var path = GetRustProfile()?.ExecutablePath;
        _rustPathLabel.Text = string.IsNullOrWhiteSpace(path)
            ? "RustClient.exe was not found automatically. The process-name trigger still works after Rust launches."
            : $"Detected: {path}";
    }

    private void SaveConfiguration(bool showErrors)
    {
        try
        {
            _profileStore.Save(_configuration);
        }
        catch (Exception exception)
        {
            if (showErrors)
            {
                MessageBox.Show(exception.Message, "Save profiles", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OpenProfileFolder()
    {
        Directory.CreateDirectory(_profileStore.DirectoryPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = _profileStore.DirectoryPath,
            UseShellExecute = true
        });
    }

    private void UpdateValueLabels()
    {
        _saturationValue.Text = (_saturationTrack.Value / 100.0).ToString("0.00");
        _vibranceValue.Text = FormatSignedDecimal(_vibranceTrack.Value / 100.0);
        _brightnessValue.Text = FormatSignedDecimal(_brightnessTrack.Value / 100.0);
        _contrastValue.Text = (_contrastTrack.Value / 100.0).ToString("0.00");
        _exposureValue.Text = $"{FormatSignedDecimal(_exposureTrack.Value / 100.0)} EV";
        _gammaValue.Text = (_gammaTrack.Value / 100.0).ToString("0.00");
        _shadowValue.Text = $"{_shadowTrack.Value}%";
        _temperatureValue.Text = FormatSignedInteger(_temperatureTrack.Value);
        _tintValue.Text = FormatSignedInteger(_tintTrack.Value);
        _redGainValue.Text = (_redGainTrack.Value / 100.0).ToString("0.00");
        _greenGainValue.Text = (_greenGainTrack.Value / 100.0).ToString("0.00");
        _blueGainValue.Text = (_blueGainTrack.Value / 100.0).ToString("0.00");
    }

    private void UpdateStatus(string message, Color color)
    {
        _engineStatusLabel.Text = color == SuccessColor ? "ENGINE ACTIVE" : "ENGINE NOTICE";
        _engineStatusLabel.BackColor = color == SuccessColor ? Color.FromArgb(37, 113, 73) : Color.FromArgb(130, 76, 35);
        _activeModeLabel.Text = message;
        _activeModeLabel.ForeColor = color;
    }

    private void UpdateModeLabel()
    {
        var mode = _manualMode ? "Manual preview/apply mode" : "Automatic profile mode";
        var active = _activeProfileId is Guid id
            ? _configuration.Profiles.FirstOrDefault(profile => profile.Id == id)?.Name ?? "Unknown profile"
            : "Original display / no active profile";
        _selectedProfileLabel.Text = _editingProfile is null
            ? mode
            : $"{mode}  •  {RustPresetCatalog.GetName(_editingProfile.LastPreset)}";
        if (string.IsNullOrWhiteSpace(_activeModeLabel.Text))
        {
            _activeModeLabel.Text = $"{mode}. Active: {active}.";
        }
    }

    private NotifyIcon CreateTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open DisplayLift", null, (_, _) => ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Clean Rust", null, (_, _) => ApplyPreset(RustVisualPreset.CleanRust));
        menu.Items.Add("Competitive", null, (_, _) => ApplyPreset(RustVisualPreset.Competitive));
        menu.Items.Add("Night", null, (_, _) => ApplyPreset(RustVisualPreset.Night));
        menu.Items.Add("Maximum Color", null, (_, _) => ApplyPreset(RustVisualPreset.MaximumColor));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Restore original", null, (_, _) => RestoreOriginal(showMessage: false));
        menu.Items.Add("Resume automatic", null, (_, _) => ResumeAutomaticProfiles());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _allowClose = true;
            Close();
        });

        var icon = new NotifyIcon
        {
            Text = "DisplayLift Visual Panel",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => ShowFromTray();
        return icon;
    }

    private void HideToTray()
    {
        Hide();
        WindowState = FormWindowState.Normal;
        _trayIcon.Visible = true;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (!_allowClose && eventArgs.CloseReason == CloseReason.UserClosing && _configuration.MinimizeToTray)
        {
            eventArgs.Cancel = true;
            HideToTray();
            return;
        }
        Shutdown();
    }

    private void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        SaveConfiguration(false);
        _profileTimer.Stop();
        _previewTimer.Stop();
        UnregisterGlobalHotkeys();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _effects.Dispose();
    }

    private void RegisterGlobalHotkeys()
    {
        _ = RegisterHotKey(Handle, CycleHotkeyId, ModControl | ModAlt, (uint)Keys.F9);
        _ = RegisterHotKey(Handle, RestoreHotkeyId, ModControl | ModAlt, (uint)Keys.F10);
        _ = RegisterHotKey(Handle, AutoHotkeyId, ModControl | ModAlt, (uint)Keys.F11);
    }

    private void UnregisterGlobalHotkeys()
    {
        if (!IsHandleCreated) return;
        _ = UnregisterHotKey(Handle, CycleHotkeyId);
        _ = UnregisterHotKey(Handle, RestoreHotkeyId);
        _ = UnregisterHotKey(Handle, AutoHotkeyId);
    }

    private void DrawTab(object? sender, DrawItemEventArgs eventArgs)
    {
        var selected = eventArgs.Index == _tabs.SelectedIndex;
        var bounds = eventArgs.Bounds;
        using var background = new SolidBrush(selected ? CardColor : PanelColor);
        using var textBrush = new SolidBrush(selected ? Color.White : MutedColor);
        eventArgs.Graphics.FillRectangle(background, bounds);
        var text = _tabs.TabPages[eventArgs.Index].Text;
        TextRenderer.DrawText(eventArgs.Graphics, text, Font, bounds, textBrush.Color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (selected)
        {
            using var accent = new SolidBrush(AccentColor);
            eventArgs.Graphics.FillRectangle(accent, bounds.Left + 12, bounds.Bottom - 4, bounds.Width - 24, 4);
        }
    }

    private static TabPage NewTab(string text) => new()
    {
        Text = text,
        BackColor = WindowColor,
        ForeColor = Color.White,
        Padding = new Padding(0)
    };

    private static FlowLayoutPanel NewScrollingColumn() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(24, 20, 24, 28),
        BackColor = WindowColor
    };

    private static Panel NewCard(int width, int height) => new()
    {
        Width = width,
        Height = height,
        BackColor = CardColor,
        Margin = new Padding(0, 0, 0, 14)
    };

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = MutedColor,
        Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
        Margin = new Padding(2, 8, 0, 8)
    };

    private static TrackBar CreateTrackBar(int minimum, int maximum, int value, int tickFrequency) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Value = value,
        TickFrequency = tickFrequency,
        AutoSize = false,
        Height = 38
    };

    private static Label CreateValueLabel() => new()
    {
        AutoSize = true,
        ForeColor = AccentColor,
        Font = new Font("Consolas", 10f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleRight
    };

    private static Button CreatePrimaryButton(string text, EventHandler handler)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(118, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 8)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        button.Click += handler;
        return button;
    }

    private static Button CreateSecondaryButton(string text, EventHandler handler)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(105, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = InputColor,
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 8)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 76, 82);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 57, 62);
        button.Click += handler;
        return button;
    }

    private static Button CreatePresetButton(string text, EventHandler handler)
    {
        var button = CreateSecondaryButton(text.ToUpperInvariant(), handler);
        button.MinimumSize = new Size(125, 42);
        return button;
    }

    private static void AddEditorRow(TableLayoutPanel grid, int row, string labelText, Control control)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = MutedColor,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 10, 14)
        };
        control.Dock = control is NumericUpDown ? DockStyle.None : DockStyle.Top;
        control.Margin = new Padding(0, 2, 0, 12);
        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private static void ApplyTheme(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.BackColor = InputColor;
                    textBox.ForeColor = Color.WhiteSmoke;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = InputColor;
                    comboBox.ForeColor = Color.WhiteSmoke;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;
                case ListBox listBox:
                    listBox.BackColor = InputColor;
                    listBox.ForeColor = Color.WhiteSmoke;
                    listBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case NumericUpDown numeric:
                    numeric.BackColor = InputColor;
                    numeric.ForeColor = Color.WhiteSmoke;
                    break;
                case TabControl tabControl:
                    tabControl.BackColor = WindowColor;
                    break;
            }
            ApplyTheme(control);
        }
    }

    private static string FormatSignedDecimal(double value) => value >= 0 ? $"+{value:0.00}" : value.ToString("0.00");
    private static string FormatSignedInteger(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static string SanitizeFileName(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(character, '_');
        }
        return string.IsNullOrWhiteSpace(value) ? "DisplayLift-profile" : value;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
