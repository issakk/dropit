using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DropLite.Models;
using DropLite.Services;

namespace DropLite.UI;

internal sealed class SettingsForm : Form
{
    private readonly AppContext _app;
    private readonly AppConfig _edit;
    private Profile? _selectedProfile;

    private readonly ComboBox _profileCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListView _destList = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
    };
    private readonly Button _btnAddProfile = new() { Text = "Add" };
    private readonly Button _btnRenameProfile = new() { Text = "Rename" };
    private readonly Button _btnDeleteProfile = new() { Text = "Delete" };
    private readonly Button _btnAddDest = new() { Text = "Add rule" };
    private readonly Button _btnEditDest = new() { Text = "Edit" };
    private readonly Button _btnDeleteDest = new() { Text = "Delete" };
    private readonly Button _btnUpDest = new() { Text = "▲ Up" };
    private readonly Button _btnDownDest = new() { Text = "▼ Down" };
    private readonly CheckBox _chkAutostart = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _chkNotify = new() { Text = "Show notification balloons", AutoSize = true };
    private readonly NumericUpDown _numIconSize = new() { Minimum = 32, Maximum = 128, Width = 60 };
    private readonly Button _btnOk = new() { Text = "Save & apply" };
    private readonly Button _btnCancel = new() { Text = "Cancel" };

    public SettingsForm(AppContext app, string? focusProfileId = null)
    {
        _app = app;
        _edit = ConfigStore.Clone(app.Config);
        _selectedProfile = _edit.Profiles.FirstOrDefault(p => p.Id == focusProfileId)
                           ?? _edit.Profiles.FirstOrDefault();

        Text = "DropLite — Settings";
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(740, 540);
        ShowInTaskbar = false;

        _destList.Columns.Add("#", 28);
        _destList.Columns.Add("Name", 120);
        _destList.Columns.Add("Action", 100);
        _destList.Columns.Add("Mask", 150);
        _destList.Columns.Add("Target", 240);
        _destList.DoubleClick += (_, _) => EditSelectedDest();

        var lblProfile = new Label { Text = "Profile:", AutoSize = true, Location = new Point(12, 16) };
        _profileCombo.Location = new Point(60, 12);
        _profileCombo.Width = 220;
        _btnAddProfile.SetBounds(300, 10, 70, 26);
        _btnRenameProfile.SetBounds(374, 10, 70, 26);
        _btnDeleteProfile.SetBounds(448, 10, 70, 26);

        var grpDest = new GroupBox { Text = "Destinations (rules match top-down)", Location = new Point(12, 44), Size = new Size(716, 336) };
        _destList.SetBounds(10, 22, 566, 300);
        _btnAddDest.SetBounds(588, 24, 116, 28);
        _btnEditDest.SetBounds(588, 56, 116, 28);
        _btnDeleteDest.SetBounds(588, 88, 116, 28);
        _btnUpDest.SetBounds(588, 140, 116, 28);
        _btnDownDest.SetBounds(588, 172, 116, 28);
        grpDest.Controls.AddRange(new Control[]
        {
            _destList, _btnAddDest, _btnEditDest, _btnDeleteDest, _btnUpDest, _btnDownDest,
        });

        var grpGeneral = new GroupBox { Text = "General", Location = new Point(12, 388), Size = new Size(716, 92) };
        _chkAutostart.SetBounds(14, 26, 200, 22);
        _chkNotify.SetBounds(14, 52, 240, 22);
        var lblIconSize = new Label { Text = "Icon size:", AutoSize = true, Location = new Point(300, 28) };
        _numIconSize.Location = new Point(360, 24);
        grpGeneral.Controls.AddRange(new Control[] { _chkAutostart, _chkNotify, lblIconSize, _numIconSize });

        _btnOk.SetBounds(500, 494, 110, 30);
        _btnCancel.SetBounds(618, 494, 110, 30);

        Controls.AddRange(new Control[]
        {
            lblProfile, _profileCombo, _btnAddProfile, _btnRenameProfile, _btnDeleteProfile,
            grpDest, grpGeneral, _btnOk, _btnCancel,
        });

        _profileCombo.SelectedIndexChanged += (_, _) =>
        {
            int i = _profileCombo.SelectedIndex;
            if (i >= 0 && i < _edit.Profiles.Count)
            {
                _selectedProfile = _edit.Profiles[i];
                RefreshDestList();
            }
        };
        _btnAddProfile.Click += (_, _) =>
        {
            string? name = Dialogs.PromptText(this, "New profile", "Profile name:", "New profile");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }
            var profile = new Profile
            {
                Name = name.Trim(),
                IconColorArgb = ColorFromHsv((_edit.Profiles.Count * 67) % 360, 0.62f, 0.92f).ToArgb(),
                X = 100 + _edit.Profiles.Count * 40,
                Y = 100,
            };
            _edit.Profiles.Add(profile);
            _selectedProfile = profile;
            RefreshProfileCombo();
            RefreshDestList();
        };
        _btnRenameProfile.Click += (_, _) =>
        {
            if (_selectedProfile is null)
            {
                return;
            }
            string? name = Dialogs.PromptText(this, "Rename profile", "Profile name:", _selectedProfile.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }
            _selectedProfile.Name = name.Trim();
            RefreshProfileCombo();
        };
        _btnDeleteProfile.Click += (_, _) =>
        {
            if (_selectedProfile is null)
            {
                return;
            }
            if (MessageBox.Show(
                    this,
                    $"Delete profile '{_selectedProfile.Name}' and all of its rules?",
                    "DropLite",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            _edit.Profiles.Remove(_selectedProfile);
            _selectedProfile = _edit.Profiles.FirstOrDefault();
            RefreshProfileCombo();
            RefreshDestList();
        };

        _btnAddDest.Click += (_, _) =>
        {
            if (_selectedProfile is null)
            {
                return;
            }
            var dest = new Destination();
            using var dlg = new DestinationEditorForm(dest, isNew: true);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _selectedProfile.Destinations.Add(dest);
                RefreshDestList();
            }
        };
        _btnEditDest.Click += (_, _) => EditSelectedDest();
        _btnDeleteDest.Click += (_, _) =>
        {
            int idx = SelectedDestIndex();
            if (_selectedProfile is null || idx < 0)
            {
                return;
            }
            _selectedProfile.Destinations.RemoveAt(idx);
            RefreshDestList();
        };
        _btnUpDest.Click += (_, _) => MoveDest(-1);
        _btnDownDest.Click += (_, _) => MoveDest(1);

        _btnOk.Click += (_, _) =>
        {
            _edit.ShowNotifications = _chkNotify.Checked;
            _edit.StartWithWindows = _chkAutostart.Checked;
            _edit.IconSize = (int)_numIconSize.Value;
            _app.ApplyConfig(_edit);
            Close();
        };
        _btnCancel.Click += (_, _) => Close();

        _chkAutostart.Checked = _edit.StartWithWindows;
        _chkNotify.Checked = _edit.ShowNotifications;
        _numIconSize.Value = Math.Clamp(_edit.IconSize, 32, 128);

        RefreshProfileCombo();
        RefreshDestList();
    }

    public void FocusProfile(Profile? profile)
    {
        if (profile is null)
        {
            return;
        }
        Profile? target = _edit.Profiles.FirstOrDefault(p => p.Id == profile.Id);
        if (target is null)
        {
            return;
        }
        _selectedProfile = target;
        RefreshProfileCombo();
        RefreshDestList();
    }

    // ---------------------------------------------------------------- refresh

    private void RefreshProfileCombo()
    {
        _profileCombo.Items.Clear();
        foreach (Profile p in _edit.Profiles)
        {
            _profileCombo.Items.Add(p.Name);
        }
        int idx = _selectedProfile is null ? -1 : _edit.Profiles.IndexOf(_selectedProfile);
        if (idx >= 0)
        {
            _profileCombo.SelectedIndex = idx;
        }
    }

    private void RefreshDestList()
    {
        _destList.BeginUpdate();
        _destList.Items.Clear();
        if (_selectedProfile is not null)
        {
            for (int i = 0; i < _selectedProfile.Destinations.Count; i++)
            {
                Destination d = _selectedProfile.Destinations[i];
                var item = new ListViewItem(new[]
                {
                    (i + 1).ToString(),
                    d.Name,
                    ActionLabel(d.Action),
                    string.IsNullOrWhiteSpace(d.Pattern) ? "*" : d.Pattern,
                    string.IsNullOrWhiteSpace(d.TargetPath) ? "—" : d.TargetPath,
                });
                _destList.Items.Add(item);
            }
        }
        _destList.EndUpdate();
    }

    private int SelectedDestIndex()
        => _destList.SelectedIndices.Count > 0 ? _destList.SelectedIndices[0] : -1;

    private void EditSelectedDest()
    {
        if (_selectedProfile is null)
        {
            return;
        }
        int idx = SelectedDestIndex();
        if (idx < 0)
        {
            return;
        }
        Destination dest = _selectedProfile.Destinations[idx];
        using var dlg = new DestinationEditorForm(dest, isNew: false);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            RefreshDestList();
        }
    }

    private void MoveDest(int delta)
    {
        if (_selectedProfile is null)
        {
            return;
        }
        int idx = SelectedDestIndex();
        int target = idx + delta;
        if (idx < 0 || target < 0 || target >= _selectedProfile.Destinations.Count)
        {
            return;
        }
        (_selectedProfile.Destinations[idx], _selectedProfile.Destinations[target]) =
            (_selectedProfile.Destinations[target], _selectedProfile.Destinations[idx]);
        RefreshDestList();
        if (target < _destList.Items.Count)
        {
            _destList.Items[target].Selected = true;
            _destList.Items[target].Focused = true;
        }
    }

    private static string ActionLabel(DropAction action) => action switch
    {
        DropAction.Move => "Move",
        DropAction.Copy => "Copy",
        DropAction.Delete => "Recycle",
        DropAction.Compress => "Compress",
        DropAction.Extract => "Extract",
        DropAction.Rename => "Rename",
        DropAction.Open => "Open",
        DropAction.Ignore => "Ignore",
        _ => action.ToString(),
    };

    private static Color ColorFromHsv(float hue, float saturation, float value)
    {
        int hi = (int)(hue / 60f) % 6;
        float f = hue / 60f - (int)(hue / 60f);
        value *= 255;
        int v = (int)value;
        int p = (int)(value * (1 - saturation));
        int q = (int)(value * (1 - f * saturation));
        int t = (int)(value * (1 - (1 - f) * saturation));
        return hi switch
        {
            0 => Color.FromArgb(255, v, t, p),
            1 => Color.FromArgb(255, q, v, p),
            2 => Color.FromArgb(255, p, v, t),
            3 => Color.FromArgb(255, p, q, v),
            4 => Color.FromArgb(255, t, p, v),
            _ => Color.FromArgb(255, v, p, q),
        };
    }
}
