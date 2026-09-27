using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using DropLite.Models;
using DropLite.Services;

namespace DropLite.UI;

/// <summary>
/// Borderless always-on-top disc that accepts file drops and can be dragged around
/// with the left button (OLE drag-drop never reaches MouseDown, so the two don't clash).
/// </summary>
internal sealed class FloatingIconForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private readonly AppContext _app;
    private readonly Profile _profile;
    private readonly System.Windows.Forms.Timer _spinTimer;

    private bool _hovered;
    private bool _busy;
    private bool _movingWindow;
    private bool _windowMoved;
    private Point _grabOffset;
    private float _spin;

    public FloatingIconForm(AppContext app, Profile profile)
    {
        _app = app;
        _profile = profile;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        MinimizeBox = false;
        MaximizeBox = false;
        AllowDrop = true;
        Opacity = 0.95;
        int size = Math.Clamp(app.Config.IconSize, 32, 160);
        Size = new Size(size, size);
        Location = new Point(profile.X, profile.Y);

        _spinTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _spinTimer.Tick += (_, _) =>
        {
            _spin = (_spin + 14f) % 360f;
            Invalidate();
        };

        DragEnter += OnDragEnter;
        DragLeave += (_, _) => { SetHovered(false); };
        DragDrop += OnDragDrop;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        DoubleClick += (_, _) => _app.ShowSettings(_profile);
        Paint += (_, e) => Draw(e.Graphics);
        Resize += (_, _) => UpdateRegion();
        UpdateRegion();

        var menu = new ContextMenuStrip();
        menu.Items.Add("设置…", null, (_, _) => _app.ShowSettings(_profile));
        menu.Items.Add("隐藏此图标", null, (_, _) =>
        {
            _profile.ShowIcon = false;
            _app.SaveConfig();
            _app.SpawnProfileWindows();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出 DropLite", null, (_, _) => Application.Exit());
        ContextMenuStrip = menu;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spinTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    public void RefreshVisual() => Invalidate();

    // ---------------------------------------------------------------- layout

    private void UpdateRegion()
    {
        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, Width, Height);
        Region old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    private void Draw(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        IconFactory.DrawDropIcon(
            g,
            rect,
            Color.FromArgb(_profile.IconColorArgb),
            hovered: _hovered,
            busy: _busy,
            _spin);
    }

    // ---------------------------------------------------------------- drag & drop

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_app.Paused || e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
        {
            e.Effect = DragDropEffects.None;
            return;
        }
        e.Effect = DragDropEffects.Copy;
        SetHovered(true);
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        SetHovered(false);
        if (_app.Paused || e.Data?.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            return;
        }
        _ = ProcessDropAsync(paths);
    }

    private async Task ProcessDropAsync(string[] paths)
    {
        SetBusy(true);
        try
        {
            Profile snapshot = ConfigStore.Clone(_profile);
            ProcessResult result = await Task.Run(() => FileProcessor.ProcessAsync(snapshot, paths));
            _app.NotifyResult(snapshot.Name, result);
        }
        catch (Exception ex)
        {
            _app.NotifyError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetHovered(bool hovered)
    {
        if (_hovered == hovered)
        {
            return;
        }
        _hovered = hovered;
        Invalidate();
    }

    private void SetBusy(bool busy)
    {
        if (IsDisposed)
        {
            return;
        }
        if (InvokeRequired)
        {
            BeginInvoke(() => SetBusy(busy));
            return;
        }
        _busy = busy;
        if (busy)
        {
            _spinTimer.Start();
        }
        else
        {
            _spinTimer.Stop();
        }
        Invalidate();
    }

    // ---------------------------------------------------------------- window dragging

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        _movingWindow = true;
        _windowMoved = false;
        _grabOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_movingWindow)
        {
            return;
        }
        var pos = new Point(Cursor.Position.X - _grabOffset.X, Cursor.Position.Y - _grabOffset.Y);
        if (!_windowMoved && Math.Abs(pos.X - Left) + Math.Abs(pos.Y - Top) > 4)
        {
            _windowMoved = true;
        }
        if (_windowMoved)
        {
            Location = pos;
        }
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_movingWindow)
        {
            return;
        }
        _movingWindow = false;
        if (_windowMoved)
        {
            _profile.X = Left;
            _profile.Y = Top;
            _app.SaveConfig();
        }
    }
}
