using System;
using System.Drawing;
using System.Windows.Forms;
using DropLite.Models;

namespace DropLite.UI;

/// <summary>编辑档案中的单条目标规则。</summary>
internal sealed class DestinationEditorForm : Form
{
    // 顺序必须与 DropAction 枚举一致：Move, Copy, Delete, Compress, Extract, Rename, Open, Ignore
    private static readonly string[] ActionNames =
    {
        "移动",
        "复制",
        "移入回收站",
        "压缩为 ZIP",
        "解压 ZIP",
        "重命名",
        "打开",
        "忽略",
    };

    // 顺序必须与 ConflictPolicy 枚举一致：AutoRename, Overwrite, Skip
    private static readonly string[] ConflictNames = { "自动重命名", "覆盖", "跳过" };

    private readonly TextBox _name = new() { Width = 370 };
    private readonly ComboBox _action = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 370 };
    private readonly TextBox _pattern = new() { Width = 370 };
    private readonly TextBox _target = new() { Width = 302 };
    private readonly TextBox _zip = new() { Width = 370 };
    private readonly ComboBox _conflict = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 370 };
    private readonly Label _hint = new()
    {
        AutoSize = false,
        Size = new Size(510, 56),
        ForeColor = SystemColors.GrayText,
    };

    public DestinationEditorForm(Destination dest, bool isNew)
    {
        Text = isNew ? "DropLite — 新建规则" : "DropLite — 编辑规则";
        Font = new Font("Microsoft YaHei UI", 9f);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(540, 446);

        _action.Items.AddRange(ActionNames);
        _conflict.Items.AddRange(ConflictNames);
        _action.SelectedIndex = (int)dest.Action;
        _conflict.SelectedIndex = (int)dest.Conflict;
        _name.Text = dest.Name;
        _pattern.Text = dest.Pattern;
        _target.Text = dest.TargetPath;
        _zip.Text = dest.ZipName;

        var lblName = new Label { Text = "名称:", AutoSize = true, Location = new Point(16, 17) };
        _name.Location = new Point(118, 13);
        var lblAction = new Label { Text = "动作:", AutoSize = true, Location = new Point(16, 52) };
        _action.Location = new Point(118, 48);
        var lblPattern = new Label { Text = "文件掩码:", AutoSize = true, Location = new Point(16, 87) };
        _pattern.Location = new Point(118, 83);
        var lblTarget = new Label { Text = "目标文件夹:", AutoSize = true, Location = new Point(16, 122) };
        _target.Location = new Point(118, 118);
        var browse = new Button { Text = "浏览…", Location = new Point(428, 116), Width = 78 };
        browse.Click += (_, _) =>
        {
            using var fb = new FolderBrowserDialog { ShowNewFolderButton = true };
            if (fb.ShowDialog(this) == DialogResult.OK)
            {
                _target.Text = fb.SelectedPath;
            }
        };
        var lblZip = new Label { Text = "ZIP 文件名:", AutoSize = true, Location = new Point(16, 157) };
        _zip.Location = new Point(118, 153);
        var lblConflict = new Label { Text = "目标已存在时:", AutoSize = true, Location = new Point(16, 192) };
        _conflict.Location = new Point(118, 188);
        _hint.Location = new Point(16, 236);

        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 90, Location = new Point(320, 398) };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 90, Location = new Point(420, 398) };
        ok.Click += (_, _) =>
        {
            dest.Name = _name.Text.Trim();
            if (dest.Name.Length == 0)
            {
                dest.Name = "规则";
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
        DropAction.Move => "将匹配的文件/文件夹移动到目标文件夹。",
        DropAction.Copy => "将匹配项复制到目标文件夹，原件保留。",
        DropAction.Delete => "将匹配项移入回收站，可随时还原。",
        DropAction.Compress => "将匹配项追加进目标文件夹中的 ZIP 压缩包。ZIP 名支持变量 {date:yyyy-MM-dd}、{n}。",
        DropAction.Extract => "将匹配的 ZIP 压缩包解压到目标文件夹。",
        DropAction.Rename => "按模板重命名，此处的“目标文件夹”字段作为模板使用。变量：{name}、{ext}、{date:yyyyMMdd}、{n}。",
        DropAction.Open => "用系统默认程序打开匹配项。",
        DropAction.Ignore => "不做任何处理，适合作为最后的兜底规则。",
        _ => string.Empty,
    };
}
