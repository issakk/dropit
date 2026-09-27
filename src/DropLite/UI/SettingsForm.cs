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
    private readonly Button _btnAddProfile = new() { Text = "添加" };
    private readonly Button _btnRenameProfile = new() { Text = "重命名" };
    private readonly Button _btnDeleteProfile = new() { Text = "删除" };
    private readonly Button _btnAddDest = new() { Text = "添加规则" };
    private readonly Button _btnEditDest = new() { Text = "编辑" };
    private readonly Button _btnDeleteDest = new() { Text = "删除" };
    private readonly Button _btnUpDest = new() { Text = "▲ 上移" };
    private readonly Button _btnDownDest = new() { Text = "▼ 下移" };
    private readonly Button _btnPresets = new() { Text = "常用分类 ▾" };
    private readonly CheckBox _chkAutostart = new() { Text = "开机自启", AutoSize = true };
    private readonly CheckBox _chkNotify = new() { Text = "处理完成后显示通知气泡", AutoSize = true };
    private readonly NumericUpDown _numIconSize = new() { Minimum = 32, Maximum = 128, Width = 64 };
    private readonly Button _btnOk = new() { Text = "保存并应用" };
    private readonly Button _btnCancel = new() { Text = "取消" };

    public SettingsForm(AppContext app, string? focusProfileId = null)
    {
        _app = app;
        _edit = ConfigStore.Clone(app.Config);
        _selectedProfile = _edit.Profiles.FirstOrDefault(p => p.Id == focusProfileId)
                           ?? _edit.Profiles.FirstOrDefault();

        Text = "DropLite — 设置";
        Font = new Font("Microsoft YaHei UI", 9f);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 560);
        ShowInTaskbar = false;

        // 档案选择行
        var lblProfile = new Label { Text = "档案:", AutoSize = true, Location = new Point(14, 18) };
        _profileCombo.Location = new Point(74, 13);
        _profileCombo.Width = 200;
        _btnAddProfile.SetBounds(304, 11, 72, 30);
        _btnRenameProfile.SetBounds(380, 11, 84, 30);
        _btnDeleteProfile.SetBounds(468, 11, 72, 30);

        // 规则组
        var grpDest = new GroupBox { Text = "目标规则（自上而下，第一条命中生效）", Location = new Point(12, 50), Size = new Size(756, 348) };
        _destList.SetBounds(12, 28, 612, 306);
        _destList.Columns.Add("序号", 36);
        _destList.Columns.Add("名称", 112);
        _destList.Columns.Add("动作", 78);
        _destList.Columns.Add("掩码", 158);
        _destList.Columns.Add("目标", 198);
        _destList.DoubleClick += (_, _) => EditSelectedDest();

        _btnAddDest.SetBounds(634, 30, 110, 32);
        _btnEditDest.SetBounds(634, 66, 110, 32);
        _btnDeleteDest.SetBounds(634, 102, 110, 32);
        _btnUpDest.SetBounds(634, 158, 110, 32);
        _btnDownDest.SetBounds(634, 194, 110, 32);
        _btnPresets.SetBounds(634, 250, 110, 32);
        grpDest.Controls.AddRange(new Control[]
        {
            _destList, _btnAddDest, _btnEditDest, _btnDeleteDest, _btnUpDest, _btnDownDest, _btnPresets,
        });

        // 通用组
        var grpGeneral = new GroupBox { Text = "通用", Location = new Point(12, 406), Size = new Size(756, 96) };
        _chkAutostart.SetBounds(16, 30, 160, 24);
        _chkNotify.SetBounds(16, 60, 230, 24);
        var lblIconSize = new Label
        {
            Text = "图标大小:",
            AutoSize = false,
            Size = new Size(110, 26),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(320, 28),
        };
        _numIconSize.Location = new Point(440, 29);
        grpGeneral.Controls.AddRange(new Control[] { _chkAutostart, _chkNotify, lblIconSize, _numIconSize });

        _btnOk.SetBounds(534, 512, 116, 34);
        _btnCancel.SetBounds(654, 512, 114, 34);

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
            string? name = Dialogs.PromptText(this, "新建档案", "档案名称：", "新建档案");
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
            string? name = Dialogs.PromptText(this, "重命名档案", "档案名称：", _selectedProfile.Name);
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
                    $"确定删除档案“{_selectedProfile.Name}”及其全部规则吗？",
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

        _btnPresets.Click += (_, _) =>
        {
            if (_selectedProfile is null)
            {
                return;
            }
            var menu = new ContextMenuStrip();
            foreach (PresetCategory preset in PresetCategories.All)
            {
                var item = new ToolStripMenuItem(preset.Name);
                item.Click += (_, _) =>
                {
                    if (_selectedProfile is null)
                    {
                        return;
                    }
                    _selectedProfile.Destinations.Add(new Destination
                    {
                        Name = preset.Name,
                        Action = DropAction.Move,
                        Pattern = preset.Pattern,
                        TargetPath = preset.Target,
                    });
                    RefreshDestList();
                };
                menu.Items.Add(item);
            }
            menu.Show(_btnPresets, new Point(0, _btnPresets.Height));
        };

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
        DropAction.Move => "移动",
        DropAction.Copy => "复制",
        DropAction.Delete => "回收",
        DropAction.Compress => "压缩",
        DropAction.Extract => "解压",
        DropAction.Rename => "重命名",
        DropAction.Open => "打开",
        DropAction.Ignore => "忽略",
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
