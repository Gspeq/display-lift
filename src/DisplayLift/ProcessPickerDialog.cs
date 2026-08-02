using System.Diagnostics;

namespace DisplayLift;

internal sealed class ProcessPickerDialog : Form
{
    private readonly ListBox _processList = new();
    private readonly List<ProcessChoice> _choices = [];

    public ProcessPickerDialog()
    {
        Text = "Add running application";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(680, 520);
        MinimumSize = new Size(560, 420);
        BackColor = Color.FromArgb(16, 18, 20);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 9.5f);

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            ForeColor = Color.FromArgb(171, 178, 185),
            Text = "Choose a running application. DisplayLift stores only its executable name and path; it does not read process memory."
        };

        _processList.Dock = DockStyle.Fill;
        _processList.DisplayMember = nameof(ProcessChoice.DisplayText);
        _processList.BackColor = Color.FromArgb(38, 42, 46);
        _processList.ForeColor = Color.WhiteSmoke;
        _processList.BorderStyle = BorderStyle.FixedSingle;
        _processList.DoubleClick += (_, _) => AcceptSelection();

        var refresh = CreateButton("REFRESH", (_, _) => LoadProcesses(), false);
        var add = CreateButton("ADD SELECTED", (_, _) => AcceptSelection(), true);
        var cancel = CreateButton("CANCEL", (_, _) => Close(), false);
        cancel.DialogResult = DialogResult.Cancel;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(add);
        buttons.Controls.Add(refresh);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 3,
            ColumnCount = 1,
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(intro, 0, 0);
        root.Controls.Add(_processList, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);

        AcceptButton = add;
        CancelButton = cancel;
        LoadProcesses();
    }

    public string SelectedProcessName { get; private set; } = string.Empty;
    public string SelectedExecutablePath { get; private set; } = string.Empty;

    private static Button CreateButton(string text, EventHandler action, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(110, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(235, 103, 45) : Color.FromArgb(38, 42, 46),
            ForeColor = Color.WhiteSmoke,
            Margin = new Padding(8, 10, 0, 0)
        };
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 76, 82);
        button.Click += action;
        return button;
    }

    private void LoadProcesses()
    {
        _choices.Clear();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId || process.MainWindowHandle == IntPtr.Zero) continue;
                    var title = string.IsNullOrWhiteSpace(process.MainWindowTitle) ? "No window title" : process.MainWindowTitle.Trim();
                    var path = string.Empty;
                    try { path = process.MainModule?.FileName ?? string.Empty; }
                    catch { }
                    _choices.Add(new ProcessChoice(process.ProcessName, path, $"{process.ProcessName}.exe  —  {title}"));
                }
                catch
                {
                    // Process exited or denied access while enumerating.
                }
            }
        }

        _choices.Sort((left, right) => string.Compare(left.DisplayText, right.DisplayText, StringComparison.OrdinalIgnoreCase));
        _processList.DataSource = null;
        _processList.DataSource = _choices;
        if (_choices.Count > 0) _processList.SelectedIndex = 0;
    }

    private void AcceptSelection()
    {
        if (_processList.SelectedItem is not ProcessChoice choice) return;
        SelectedProcessName = choice.ProcessName;
        SelectedExecutablePath = choice.ExecutablePath;
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record ProcessChoice(string ProcessName, string ExecutablePath, string DisplayText);
}
