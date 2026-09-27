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
            Font = new Font("Microsoft YaHei UI", 9f),
            AutoScaleDimensions = new SizeF(96f, 96f),
            AutoScaleMode = AutoScaleMode.Dpi,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = owner is Form ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen,
            ClientSize = new Size(380, 124),
        };

        var lbl = new Label { Text = label, AutoSize = true, Location = new Point(12, 13) };
        var txt = new TextBox { Text = initial, Location = new Point(12, 36), Width = 352 };
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 84, Location = new Point(194, 80) };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 84, Location = new Point(284, 80) };

        form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
