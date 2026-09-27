using System;
using System.Drawing;
using System.Windows.Forms;
using DropLite.Models;

namespace DropLite.UI;

/// <summary>Edits a single destination (rule) of a profile.</summary>
internal sealed class DestinationEditorForm : Form
{
    private static readonly string[] ActionNames =
    {
        "Move",
        "Copy",
        "Recycle (delete)",
        "Compress to ZIP",
        "Extract ZIP",
        "Rename",
        "Open",
        "Ignore",
    };

    private static readonly string[] ConflictNames = { "Auto-rename", "Overwrite", "Skip" };

    private readonly TextBox _name = new() { Width = 360 };
    private readonly ComboBox _action = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly TextBox _pattern = new() { Width = 360 };
    private readonly TextBox _target = new() { Width = 296 };
    private readonly TextBox _zip = new() { Width = 360 };
    private readonly ComboBox _conflict = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly Label _hint = new()
    {
        AutoSize = false,
        Size = new Size(488, 46),
        ForeColor = SystemColors.GrayText,
    };

    public DestinationEditorForm(Destination dest, bool isNew)
    {
        Text = isNew ? "DropLite — New rule" : "DropLite — Edit rule";
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 430);

        _action.Items.AddRange(ActionNames);
        _conflict.Items.AddRange(ConflictNames);
        _action.SelectedIndex = (int)dest.Action;
        _conflict.SelectedIndex = (int)dest.Conflict;
        _name.Text = dest.Name;
        _pattern.Text = dest.Pattern;
        _target.Text = dest.TargetPath;
        _zip.Text = dest.ZipName;

        var lblName = new Label { Text = "Name:", AutoSize = true, Location = new Point(14, 17) };
        _name.Location = new Point(130, 13);
        var lblAction = new Label { Text = "Action:", AutoSize = true, Location = new Point(14, 52) };
        _action.Location = new Point(130, 48);
        var lblPattern = new Label { Text = "File mask:", AutoSize = true, Location = new Point(14, 87) };
        _pattern.Location = new Point(130, 83);
        var lblTarget = new Label { Text = "Target folder:", AutoSize = true, Location = new Point(14, 122) };
        _target.Location = new Point(130, 118);
        var browse = new Button { Text = "Browse…", Location = new Point(432, 116), Width = 74 };
        browse.Click += (_, _) =>
        {
            using var fb = new FolderBrowserDialog { ShowNewFolderButton = true };
            if (fb.ShowDialog(this) == DialogResult.OK)
            {
                _target.Text = fb.SelectedPath;
            }
        };
        var lblZip = new Label { Text = "ZIP file name:", AutoSize = true, Location = new Point(14, 157) };
        _zip.Location = new Point(130, 153);
        var lblConflict = new Label { Text = "If target exists:", AutoSize = true, Location = new Point(14, 192) };
        _conflict.Location = new Point(130, 188);
        _hint.Location = new Point(14, 232);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90, Location = new Point(304, 386) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Location = new Point(404, 386) };
        ok.Click += (_, _) =>
        {
            dest.Name = _name.Text.Trim();
            if (dest.Name.Length == 0)
            {
                dest.Name = "Rule";
            }
            dest.Action = (DropAction)Math.Max(0, _action.SelectedIndex);
            dest.Pattern = _pattern.Text.Trim();
            dest.TargetPath = _target.Text.Trim();
            dest.ZipName = _zip.Text.Trim();
            dest.Conflict = (ConflictPolicy)Math.Max(0, _conflict.SelectedIndex);
        };

        _action.SelectedIndexChanged += (_, _) => RefreshEnabled();
        RefreshEnabled();

        Controls.AddRange(new Control[]
        {
            lblName, _name,
            lblAction, _action,
            lblPattern, _pattern,
            lblTarget, _target, browse,
            lblZip, _zip,
            lblConflict, _conflict,
            _hint,
            ok, cancel,
        });
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void RefreshEnabled()
    {
        var action = (DropAction)Math.Max(0, _action.SelectedIndex);
        _hint.Text = HintFor(action);
        bool needTarget = action is DropAction.Move or DropAction.Copy
            or DropAction.Compress or DropAction.Extract or DropAction.Rename;
        _target.Enabled = needTarget;
        _zip.Enabled = action == DropAction.Compress;
    }

    private static string HintFor(DropAction action) => action switch
    {
        DropAction.Move => "Move matching items into the target folder.",
        DropAction.Copy => "Copy matching items into the target folder; originals are kept.",
        DropAction.Delete => "Send matching items to the Recycle Bin.",
        DropAction.Compress => "Append matching items to a ZIP inside the target folder. ZIP name variables: {date:yyyy-MM-dd}, {n}.",
        DropAction.Extract => "Extract matching ZIP archives into the target folder.",
        DropAction.Rename => "Rename using the target field as a template. Variables: {name}, {ext}, {date:yyyyMMdd}, {n}.",
        DropAction.Open => "Open matching items with their default program.",
        DropAction.Ignore => "Do nothing — useful as a final catch-all rule.",
        _ => string.Empty,
    };
}
