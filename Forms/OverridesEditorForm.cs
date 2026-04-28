using System.ComponentModel;
using System.Net.Http;
using System.Windows.Forms;
using WindowRPC.Models;
using WindowRPC.Services;

namespace WindowRPC.Forms;

internal sealed class OverridesEditorForm : Form
{
    private static readonly HttpClient PreviewHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private readonly ConfigurationService _configurationService;
    private readonly ForegroundWindowWatcher _windowWatcher;
    private readonly BindingList<OverrideEditorItem> _items;
    private readonly DataGridView _grid;
    private readonly TextBox _nameTextBox;
    private readonly ComboBox _overrideModeComboBox;
    private readonly ComboBox _matchModeComboBox;
    private readonly CheckBox _ignoreCheckBox;
    private readonly Label _playerLabel;
    private readonly TextBox _logoTextBox;
    private readonly TextBox _detailsTextBox;
    private readonly TextBox _stateTextBox;
    private readonly Label _artworkLabel;
    private readonly TextBox _playerTextBox;
    private readonly TextBox _artworkTextBox;
    private readonly Label _artworkSourcesLabel;
    private readonly TextBox _artworkSourcesTextBox;
    private readonly Label _mprogressLabel;
    private readonly CheckBox _mprogressCheckBox;
    private readonly Panel _previewCard;
    private readonly Label _previewHeaderLabel;
    private readonly Label _previewAppNameLabel;
    private readonly Label _previewDetailsLabel;
    private readonly Label _previewStateLabel;
    private readonly Label _previewTimestampLabel;
    private readonly Label _previewArtworkLabel;
    private readonly Label _previewLogoWarningLabel;
    private readonly PictureBox _previewArtworkPictureBox;
    private int _previewRequestVersion;
    private bool _isBinding;

    public OverridesEditorForm(ConfigurationService configurationService, ForegroundWindowWatcher windowWatcher)
    {
        _configurationService = configurationService;
        _windowWatcher = windowWatcher;
        _items = new BindingList<OverrideEditorItem>(
            configurationService.Current.Overrides
                .Select(OverrideEditorItem.FromRule)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());

        Text = "WindowRPC Override Editor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 700);
        ClientSize = new Size(1180, 760);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(16)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var leftTitle = new Label
        {
            Text = "Overrides",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = _items
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(OverrideEditorItem.Name),
            HeaderText = "Override",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(OverrideEditorItem.OverrideMode),
            HeaderText = "Mode",
            Width = 80
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = string.Empty,
            Text = "×",
            UseColumnTextForButtonValue = true,
            Width = 40
        });
        _grid.CellClick += OnGridCellClick;
        _grid.SelectionChanged += (_, _) => BindSelectedItem();

        var leftButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0)
        };

        var addButton = new Button
        {
            Text = "Add Override",
            AutoSize = true
        };
        addButton.Click += (_, _) => AddOverride();

        var duplicateButton = new Button
        {
            Text = "Duplicate",
            AutoSize = true
        };
        duplicateButton.Click += (_, _) => DuplicateSelected();

        leftButtons.Controls.Add(addButton);
        leftButtons.Controls.Add(duplicateButton);

        leftPanel.Controls.Add(leftTitle, 0, 0);
        leftPanel.Controls.Add(_grid, 0, 1);
        leftPanel.Controls.Add(leftButtons, 0, 2);

        var editorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 0, 0, 0),
            AutoScroll = true
        };

        var editorLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true
        };
        editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var editorTitle = new Label
        {
            Text = "Selected Override",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        editorLayout.Controls.Add(editorTitle, 0, 0);
        editorLayout.SetColumnSpan(editorTitle, 2);

        (_, _nameTextBox) = AddTextRow(editorLayout, 1, "Name");
        _overrideModeComboBox = AddComboRow(editorLayout, 2, "Override mode", ["normal", "game", "media"]);
        _matchModeComboBox = AddComboRow(editorLayout, 3, "Match mode", ["inline", "exact"]);
        (_, _ignoreCheckBox) = AddCheckRow(editorLayout, 4, "Ignore this match");
        (_, _logoTextBox) = AddTextRow(editorLayout, 5, "Logo");
        _detailsTextBox = AddMultilineRow(editorLayout, 6, "Details");
        _stateTextBox = AddMultilineRow(editorLayout, 7, "State");
        (_playerLabel, _playerTextBox) = AddTextRow(editorLayout, 8, "Player filter");
        (_artworkLabel, _artworkTextBox) = AddTextRow(editorLayout, 9, "Artwork key");
        (_artworkSourcesLabel, _artworkSourcesTextBox) = AddTextRow(editorLayout, 10, "Artwork sources");
        (_mprogressLabel, _mprogressCheckBox) = AddCheckRow(editorLayout, 11, "Use mprogress timestamps");

        var usageLabel = new Label
        {
            Text = "Template aliases: appname, timestamp, totaltimestamp, mtitle, martist, malbum, mtotal, mcollapsed, mplayer",
            AutoSize = false,
            Height = 44,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 12, 0, 0)
        };
        editorLayout.Controls.Add(usageLabel, 0, 12);
        editorLayout.SetColumnSpan(usageLabel, 2);

        var guidanceLabel = new Label
        {
            Text = "Use normal for active-window title matching, game to match any visible window, and media for active media sessions. Artwork sources should be comma-separated, for example: musicbrainz, itunes, lastfm",
            AutoSize = false,
            Height = 54,
            Dock = DockStyle.Top
        };
        editorLayout.Controls.Add(guidanceLabel, 0, 13);
        editorLayout.SetColumnSpan(guidanceLabel, 2);

        var previewTitle = new Label
        {
            Text = "Preview",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 8)
        };
        editorLayout.Controls.Add(previewTitle, 0, 14);
        editorLayout.SetColumnSpan(previewTitle, 2);

        _previewCard = new Panel
        {
            Height = 170,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 0),
            Padding = new Padding(16),
            BackColor = Color.FromArgb(34, 36, 41)
        };

        _previewHeaderLabel = new Label
        {
            Text = "PLAYING A GAME",
            ForeColor = Color.FromArgb(185, 187, 190),
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 14)
        };

        var artworkPanel = new Panel
        {
            BackColor = Color.FromArgb(43, 45, 49),
            Location = new Point(16, 40),
            Size = new Size(72, 72)
        };

        _previewArtworkPictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Visible = false
        };

        _previewArtworkLabel = new Label
        {
            Text = "ART",
            ForeColor = Color.FromArgb(220, 221, 222),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        artworkPanel.Controls.Add(_previewArtworkPictureBox);
        artworkPanel.Controls.Add(_previewArtworkLabel);

        _previewAppNameLabel = new Label
        {
            Text = "WindowRPC",
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(104, 40)
        };

        _previewDetailsLabel = new Label
        {
            Text = "Details preview",
            ForeColor = Color.FromArgb(220, 221, 222),
            AutoSize = false,
            Location = new Point(104, 64),
            Size = new Size(430, 20)
        };

        _previewStateLabel = new Label
        {
            Text = "State preview",
            ForeColor = Color.FromArgb(185, 187, 190),
            AutoSize = false,
            Location = new Point(104, 86),
            Size = new Size(430, 20)
        };

        _previewTimestampLabel = new Label
        {
            Text = "No timer",
            ForeColor = Color.FromArgb(185, 187, 190),
            AutoSize = false,
            Location = new Point(104, 108),
            Size = new Size(430, 20)
        };

        _previewLogoWarningLabel = new Label
        {
            Text = string.Empty,
            ForeColor = Color.FromArgb(250, 166, 26),
            AutoSize = false,
            Location = new Point(104, 130),
            Size = new Size(430, 24)
        };

        _previewCard.Controls.Add(_previewHeaderLabel);
        _previewCard.Controls.Add(artworkPanel);
        _previewCard.Controls.Add(_previewAppNameLabel);
        _previewCard.Controls.Add(_previewDetailsLabel);
        _previewCard.Controls.Add(_previewStateLabel);
        _previewCard.Controls.Add(_previewTimestampLabel);
        _previewCard.Controls.Add(_previewLogoWarningLabel);

        editorLayout.Controls.Add(_previewCard, 0, 15);
        editorLayout.SetColumnSpan(_previewCard, 2);

        editorPanel.Controls.Add(editorLayout);

        var footer = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0)
        };

        var closeButton = new Button
        {
            Text = "Close",
            AutoSize = true
        };
        closeButton.Click += (_, _) => Close();

        var saveButton = new Button
        {
            Text = "Save Overrides",
            AutoSize = true
        };
        saveButton.Click += (_, _) => SaveOverrides();

        footer.Controls.Add(closeButton);
        footer.Controls.Add(saveButton);

        root.Controls.Add(leftPanel, 0, 0);
        root.Controls.Add(editorPanel, 1, 0);
        root.Controls.Add(footer, 0, 1);
        root.SetColumnSpan(footer, 2);

        Controls.Add(root);

        WireEditorEvents();
        Shown += (_, _) => InitializeSelection();
    }

    private void AddOverride()
    {
        using var dialog = new AddOverrideForm(_windowWatcher.GetVisibleWindowTitles());
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.CreatedItem is null)
        {
            return;
        }

        var newName = dialog.CreatedItem.Name;
        if (_items.Any(item => string.Equals(item.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, "An override with that name already exists.", "Duplicate Override");
            return;
        }

        _items.Add(dialog.CreatedItem);
        SelectItem(dialog.CreatedItem);
    }

    private void DuplicateSelected()
    {
        var item = GetSelectedItem();
        if (item is null)
        {
            return;
        }

        var duplicate = new OverrideEditorItem
        {
            Name = MakeUniqueName($"{item.Name} Copy"),
            Logo = item.Logo,
            Details = item.Details,
            State = item.State,
            MatchMode = item.MatchMode,
            OverrideMode = item.OverrideMode,
            Ignore = item.Ignore,
            Player = item.Player,
            Artwork = item.Artwork,
            ArtworkSources = item.ArtworkSources,
            MProgress = item.MProgress
        };

        _items.Add(duplicate);
        SelectItem(duplicate);
    }

    private void OnGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (_grid.Columns[e.ColumnIndex] is DataGridViewButtonColumn)
        {
            var item = _items[e.RowIndex];
            var confirm = MessageBox.Show(
                this,
                $"Remove override '{item.Name}'?",
                "Delete Override",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                _items.RemoveAt(e.RowIndex);
                BindSelectedItem();
            }
        }
    }

    private void WireEditorEvents()
    {
        _nameTextBox.TextChanged += (_, _) => UpdateSelectedItem(item => item.Name = _nameTextBox.Text.Trim());
        _overrideModeComboBox.SelectedIndexChanged += (_, _) =>
            UpdateSelectedItem(item => item.OverrideMode = _overrideModeComboBox.SelectedItem?.ToString() ?? "normal");
        _matchModeComboBox.SelectedIndexChanged += (_, _) =>
            UpdateSelectedItem(item => item.MatchMode = _matchModeComboBox.SelectedItem?.ToString() ?? "inline");
        _ignoreCheckBox.CheckedChanged += (_, _) => UpdateSelectedItem(item => item.Ignore = _ignoreCheckBox.Checked);
        _logoTextBox.TextChanged += (_, _) => UpdateSelectedItem(item => item.Logo = _logoTextBox.Text);
        _detailsTextBox.TextChanged += (_, _) => UpdateSelectedItem(item => item.Details = _detailsTextBox.Text);
        _stateTextBox.TextChanged += (_, _) => UpdateSelectedItem(item => item.State = _stateTextBox.Text);
        _playerTextBox.TextChanged += (_, _) => UpdateSelectedItem(item => item.Player = _playerTextBox.Text);
        _artworkTextBox.TextChanged += (_, _) => UpdateSelectedItem(item => item.Artwork = _artworkTextBox.Text);
        _artworkSourcesTextBox.TextChanged += (_, _) => UpdateSelectedItem(item => item.ArtworkSources = _artworkSourcesTextBox.Text);
        _mprogressCheckBox.CheckedChanged += (_, _) => UpdateSelectedItem(item => item.MProgress = _mprogressCheckBox.Checked);
    }

    private void UpdateSelectedItem(Action<OverrideEditorItem> apply)
    {
        if (_isBinding)
        {
            return;
        }

        var item = GetSelectedItem();
        if (item is null)
        {
            return;
        }

        apply(item);
        _grid.Refresh();
        UpdatePreview(item);
    }

    private void BindSelectedItem()
    {
        var item = GetSelectedItem();
        _isBinding = true;
        try
        {
            var enabled = item is not null;
            foreach (Control control in new Control[]
                     {
                         _nameTextBox, _overrideModeComboBox, _matchModeComboBox, _ignoreCheckBox, _logoTextBox,
                         _detailsTextBox, _stateTextBox, _playerTextBox, _artworkTextBox, _artworkSourcesTextBox,
                         _mprogressCheckBox
                     })
            {
                control.Enabled = enabled;
            }

            if (item is null)
            {
                _nameTextBox.Text = string.Empty;
                _overrideModeComboBox.SelectedIndex = 0;
                _matchModeComboBox.SelectedIndex = 0;
                _ignoreCheckBox.Checked = false;
                _logoTextBox.Text = string.Empty;
                _detailsTextBox.Text = string.Empty;
                _stateTextBox.Text = string.Empty;
                _playerTextBox.Text = string.Empty;
                _artworkTextBox.Text = string.Empty;
                _artworkSourcesTextBox.Text = string.Empty;
                _mprogressCheckBox.Checked = false;
                UpdateMediaFieldVisibility("normal");
                UpdatePreview(null);
                return;
            }

            _nameTextBox.Text = item.Name;
            _overrideModeComboBox.SelectedItem = string.IsNullOrWhiteSpace(item.OverrideMode) ? "normal" : item.OverrideMode;
            _matchModeComboBox.SelectedItem = string.IsNullOrWhiteSpace(item.MatchMode) ? "inline" : item.MatchMode;
            _ignoreCheckBox.Checked = item.Ignore;
            _logoTextBox.Text = item.Logo;
            _detailsTextBox.Text = item.Details;
            _stateTextBox.Text = item.State;
            _playerTextBox.Text = item.Player;
            _artworkTextBox.Text = item.Artwork;
            _artworkSourcesTextBox.Text = item.ArtworkSources;
            _mprogressCheckBox.Checked = item.MProgress;
            UpdateMediaFieldVisibility(item.OverrideMode);
            UpdatePreview(item);
        }
        finally
        {
            _isBinding = false;
        }
    }

    private void SaveOverrides()
    {
        var duplicates = _items
            .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (_items.Any(item => string.IsNullOrWhiteSpace(item.Name)))
        {
            MessageBox.Show(this, "Every override needs a name.", "Missing Name");
            return;
        }

        if (duplicates.Count > 0)
        {
            MessageBox.Show(this, $"Duplicate override names found: {string.Join(", ", duplicates)}", "Duplicate Names");
            return;
        }

        var overrides = new Dictionary<string, OverrideEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _items.OrderBy(item => item.Name.Length).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            overrides[item.Name.Trim()] = item.ToOverrideEntry();
        }

        _configurationService.SaveOverrides(overrides);
        MessageBox.Show(this, "Overrides saved and reloaded.", "WindowRPC");
    }

    private OverrideEditorItem? GetSelectedItem()
    {
        return _grid.CurrentRow?.DataBoundItem as OverrideEditorItem;
    }

    private void InitializeSelection()
    {
        if (_items.Count == 0)
        {
            BindSelectedItem();
            return;
        }

        SelectItem(_items[0]);
    }

    private void SelectItem(OverrideEditorItem item)
    {
        var index = _items.IndexOf(item);
        if (index < 0 || index >= _grid.Rows.Count || _grid.Rows[index].Cells.Count == 0)
        {
            BindSelectedItem();
            return;
        }

        _grid.ClearSelection();
        _grid.Rows[index].Selected = true;
        _grid.CurrentCell = _grid.Rows[index].Cells[0];
        BindSelectedItem();
    }

    private string MakeUniqueName(string baseName)
    {
        var candidate = baseName;
        var counter = 2;
        while (_items.Any(item => string.Equals(item.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {counter}";
            counter++;
        }

        return candidate;
    }

    private void UpdateMediaFieldVisibility(string? overrideMode)
    {
        var isMedia = string.Equals(overrideMode, "media", StringComparison.OrdinalIgnoreCase);

        _playerLabel.Visible = isMedia;
        _playerTextBox.Visible = isMedia;
        _artworkLabel.Visible = isMedia;
        _artworkTextBox.Visible = isMedia;
        _artworkSourcesLabel.Visible = isMedia;
        _artworkSourcesTextBox.Visible = isMedia;
        _mprogressLabel.Visible = isMedia;
        _mprogressCheckBox.Visible = isMedia;
    }

    private void UpdatePreview(OverrideEditorItem? item)
    {
        if (item is null)
        {
            _previewHeaderLabel.Text = "RICH PRESENCE";
            _previewAppNameLabel.Text = "WindowRPC";
            _previewDetailsLabel.Text = "No override selected";
            _previewStateLabel.Text = "Pick an override to preview it";
            _previewTimestampLabel.Text = "No timer";
            _previewLogoWarningLabel.Text = string.Empty;
            SetPreviewArtworkPlaceholder("ART");
            return;
        }

        var header = (item.OverrideMode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "media" => "LISTENING",
            "game" => "PLAYING A GAME",
            _ => "PLAYING"
        };

        var previewWindow = item.Name;
        var previewTotal = "17m 42s";
        var previewOverride = "3m 18s";
        var previewMediaTitle = "Midnight City";
        var previewMediaArtist = "M83";
        var previewMediaAlbum = "Hurry Up, We're Dreaming";
        var previewMediaTotal = "4:04";
        var previewMediaPosition = item.MProgress ? "1:21" : "Paused";
        var previewMediaPlayer = string.IsNullOrWhiteSpace(item.Player) ? "Spotify.exe" : item.Player;

        var details = RenderPreviewText(item.Details, previewWindow, previewTotal, previewOverride, previewMediaTitle, previewMediaArtist, previewMediaAlbum, previewMediaTotal, previewMediaPosition, previewMediaPlayer);
        var state = RenderPreviewText(item.State, previewWindow, previewTotal, previewOverride, previewMediaTitle, previewMediaArtist, previewMediaAlbum, previewMediaTotal, previewMediaPosition, previewMediaPlayer);

        _previewHeaderLabel.Text = header;
        _previewAppNameLabel.Text = "WindowRPC";
        _previewDetailsLabel.Text = string.IsNullOrWhiteSpace(details) ? "No details" : details;
        _previewStateLabel.Text = string.IsNullOrWhiteSpace(state) ? "No state" : state;
        _previewTimestampLabel.Text = item.MProgress
            ? "Timer preview: Discord timestamps enabled"
            : "No timer";
        _ = UpdatePreviewArtworkAsync(item.Logo);
    }

    private static string RenderPreviewText(
        string? template,
        string previewWindow,
        string previewTotal,
        string previewOverride,
        string previewMediaTitle,
        string previewMediaArtist,
        string previewMediaAlbum,
        string previewMediaTotal,
        string previewMediaPosition,
        string previewMediaPlayer)
    {
        return (template ?? string.Empty)
            .Replace("appname", previewWindow, StringComparison.Ordinal)
            .Replace("totaltimestamp", previewTotal, StringComparison.Ordinal)
            .Replace("timestamp", previewOverride, StringComparison.Ordinal)
            .Replace("mtitle", previewMediaTitle, StringComparison.Ordinal)
            .Replace("martist", previewMediaArtist, StringComparison.Ordinal)
            .Replace("malbum", previewMediaAlbum, StringComparison.Ordinal)
            .Replace("mtotal", previewMediaTotal, StringComparison.Ordinal)
            .Replace("mcollapsed", previewMediaPosition, StringComparison.Ordinal)
            .Replace("mplayer", previewMediaPlayer, StringComparison.Ordinal);
    }

    private static string ShortenArtworkLabel(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= 12)
        {
            return trimmed;
        }

        return trimmed[..9] + "...";
    }

    private async Task UpdatePreviewArtworkAsync(string? logoValue)
    {
        var requestVersion = ++_previewRequestVersion;
        var trimmed = (logoValue ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            _previewLogoWarningLabel.Text = string.Empty;
            SetPreviewArtworkPlaceholder("rpc_icon");
            return;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _previewLogoWarningLabel.Text = string.Empty;
            SetPreviewArtworkPlaceholder(ShortenArtworkLabel(trimmed));
            return;
        }

        try
        {
            using var response = await PreviewHttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
            using var tempImage = Image.FromStream(stream);
            var clonedImage = new Bitmap(tempImage);

            if (requestVersion != _previewRequestVersion)
            {
                clonedImage.Dispose();
                return;
            }

            var previous = _previewArtworkPictureBox.Image;
            _previewArtworkPictureBox.Image = clonedImage;
            _previewArtworkPictureBox.Visible = true;
            _previewArtworkLabel.Visible = false;
            _previewLogoWarningLabel.Text = string.Empty;
            previous?.Dispose();
        }
        catch
        {
            if (requestVersion != _previewRequestVersion)
            {
                return;
            }

            _previewLogoWarningLabel.Text = "Warning: this logo URL could not be loaded.";
            SetPreviewArtworkPlaceholder("BROKEN");
        }
    }

    private void SetPreviewArtworkPlaceholder(string text)
    {
        var previous = _previewArtworkPictureBox.Image;
        _previewArtworkPictureBox.Image = null;
        _previewArtworkPictureBox.Visible = false;
        _previewArtworkLabel.Text = text;
        _previewArtworkLabel.Visible = true;
        previous?.Dispose();
    }

    private static (Label label, TextBox textBox) AddTextRow(TableLayoutPanel layout, int row, string label)
    {
        EnsureRow(layout, row);
        var textLabel = MakeLabel(label);
        var textBox = new TextBox { Dock = DockStyle.Top };
        layout.Controls.Add(textLabel, 0, row);
        layout.Controls.Add(textBox, 1, row);
        return (textLabel, textBox);
    }

    private static TextBox AddMultilineRow(TableLayoutPanel layout, int row, string label)
    {
        EnsureRow(layout, row);
        var textLabel = MakeLabel(label);
        var textBox = new TextBox
        {
            Dock = DockStyle.Top,
            Multiline = true,
            Height = 68,
            ScrollBars = ScrollBars.Vertical
        };
        layout.Controls.Add(textLabel, 0, row);
        layout.Controls.Add(textBox, 1, row);
        return textBox;
    }

    private static ComboBox AddComboRow(TableLayoutPanel layout, int row, string label, string[] values)
    {
        EnsureRow(layout, row);
        var textLabel = MakeLabel(label);
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        comboBox.Items.AddRange(values);
        comboBox.SelectedIndex = 0;
        layout.Controls.Add(textLabel, 0, row);
        layout.Controls.Add(comboBox, 1, row);
        return comboBox;
    }

    private static (Label label, CheckBox checkBox) AddCheckRow(TableLayoutPanel layout, int row, string label)
    {
        EnsureRow(layout, row);
        var textLabel = MakeLabel(label);
        var checkBox = new CheckBox { Dock = DockStyle.Left };
        layout.Controls.Add(textLabel, 0, row);
        layout.Controls.Add(checkBox, 1, row);
        return (textLabel, checkBox);
    }

    private static void EnsureRow(TableLayoutPanel layout, int row)
    {
        while (layout.RowStyles.Count <= row)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 6, 12, 12)
        };
    }
}
