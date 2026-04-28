using System.Windows.Forms;
using WindowRPC.Models;

namespace WindowRPC.Forms;

internal sealed class AddOverrideForm : Form
{
    private readonly RadioButton _detectedRadio;
    private readonly RadioButton _customRadio;
    private readonly ListBox _windowList;
    private readonly TextBox _customNameTextBox;
    private readonly ComboBox _matchModeComboBox;
    private readonly Label _helperLabel;

    public AddOverrideForm(IReadOnlyList<string> detectedWindows)
    {
        Text = "Add Override";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 500);

        var titleLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 16),
            Size = new Size(500, 44),
            Text = "Choose a detected window or enter a custom name. Exact mode only matches the full title. Match mode works like a contains search."
        };

        _detectedRadio = new RadioButton
        {
            Text = "Use a detected window title",
            Location = new Point(20, 72),
            Checked = true,
            AutoSize = true
        };
        _detectedRadio.CheckedChanged += (_, _) => UpdateModeState();

        _windowList = new ListBox
        {
            Location = new Point(20, 100),
            Size = new Size(500, 210),
            HorizontalScrollbar = true
        };
        _windowList.Items.AddRange(detectedWindows
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray());

        _customRadio = new RadioButton
        {
            Text = "Use a custom name",
            Location = new Point(20, 326),
            AutoSize = true
        };
        _customRadio.CheckedChanged += (_, _) => UpdateModeState();

        _customNameTextBox = new TextBox
        {
            Location = new Point(20, 354),
            Size = new Size(500, 27),
            Enabled = false
        };

        var matchModeLabel = new Label
        {
            Text = "Detection mode",
            Location = new Point(20, 396),
            AutoSize = true
        };

        _matchModeComboBox = new ComboBox
        {
            Location = new Point(20, 420),
            Size = new Size(180, 27),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _matchModeComboBox.Items.AddRange(["inline", "exact"]);
        _matchModeComboBox.SelectedIndex = 0;
        _matchModeComboBox.SelectedIndexChanged += (_, _) => UpdateHelperText();

        _helperLabel = new Label
        {
            AutoSize = false,
            Location = new Point(220, 396),
            Size = new Size(300, 54)
        };

        var addButton = new Button
        {
            Text = "Add",
            DialogResult = DialogResult.OK,
            Location = new Point(364, 458),
            Size = new Size(75, 28)
        };
        addButton.Click += (_, e) => ValidateAndClose(e);

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(445, 458),
            Size = new Size(75, 28)
        };

        Controls.AddRange(
        [
            titleLabel,
            _detectedRadio,
            _windowList,
            _customRadio,
            _customNameTextBox,
            matchModeLabel,
            _matchModeComboBox,
            _helperLabel,
            addButton,
            cancelButton
        ]);

        AcceptButton = addButton;
        CancelButton = cancelButton;

        UpdateModeState();
        UpdateHelperText();
    }

    public OverrideEditorItem? CreatedItem { get; private set; }

    private void UpdateModeState()
    {
        _windowList.Enabled = _detectedRadio.Checked;
        _customNameTextBox.Enabled = _customRadio.Checked;
    }

    private void UpdateHelperText()
    {
        _helperLabel.Text = _matchModeComboBox.SelectedItem?.ToString() == "exact"
            ? "Exact only matches one full window title."
            : "Match mode will trigger when the text appears anywhere in the window title.";
    }

    private void ValidateAndClose(EventArgs e)
    {
        var selectedName = _detectedRadio.Checked
            ? _windowList.SelectedItem?.ToString()
            : _customNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(selectedName))
        {
            MessageBox.Show(this, "Pick a detected window or enter a custom override name.", "Missing Name");
            DialogResult = DialogResult.None;
            return;
        }

        CreatedItem = new OverrideEditorItem
        {
            Name = selectedName,
            MatchMode = _matchModeComboBox.SelectedItem?.ToString() ?? "inline",
            Logo = "rpc_icon"
        };
    }
}
