using System;
using System.Drawing;
using System.Windows.Forms;

namespace DropLite.UI;

internal static class Dialogs
{
    public static string? PromptText(IWin32Window? owner, string title, string label, string initial)
    {
        using var form = new Form
        {
            Text = title,
            Font = new Font("Segoe UI", 9f),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = owner is Form f ? FormStartPosition.CenterParent : Form.FormStartPosition.CenterScreen,
            ClientSize = new Size(380, 120),
        };

        var lbl = new Label { Text = label, AutoSize = true, Location = new Point(12, 12) };
        var txt = new TextBox { Text = initial, Location = new Point(12, 34), Width = 352 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80, Location = new Point(198, 78) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80, Location = new Point(284, 78) };

        form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
