using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using DropLite.Models;
using DropLite.Services;

namespace DropLite.UI;

/// <summary>
/// 无边框置顶悬浮图标：通过 UpdateLayeredWindow 按像素 alpha 渲染，圆形边缘平滑无白边；
/// 接受 OLE 文件拖放；左键按住拖动可移动位置（OLE 拖放不会触发 MouseDown，两者互不冲突）。
/// </summary>
internal sealed class FloatingIconForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x00080000;

    private const int ULW_ALPHA = 0x2;
    private const byte AC_SRC_OVER = 0x0;
    private const byte AC_SRC_ALPHA = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int Width;
        public int Height;

        public SIZE(int w, int h) { Width = w; Height = h; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE pSize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pBlend, int dwFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

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
        int size = Math.Clamp(app.Config.IconSize, 32, 160);
        Size = new Size(size, size);
        Location = new Point(profile.X, profile.Y);

        _spinTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _spinTimer.Tick += (_, _) =>
        {
            _spin = (_spin + 14f) % 360f;
            ApplyLayeredBitmap();
        };

        DragEnter += OnDragEnter;
        DragLeave += (_, _) => SetHovered(false);
        DragDrop += OnDragDrop;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        DoubleClick += (_, _) => _app.ShowSettings(_profile);
        Resize += (_, _) => ApplyLayeredBitmap();

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
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyLayeredBitmap();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spinTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    public void RefreshVisual() => ApplyLayeredBitmap();

    /// <summary>盘面绘制区域，四周留出阴影扩散的空隙。</summary>
    private RectangleF DiscRect => new(3f, 3f, Width - 7f, Height - 7f);

    // ---------------------------------------------------------------- layered rendering

    private void ApplyLayeredBitmap()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            IconFactory.DrawDropIcon(
                g,
                DiscRect,
                Color.FromArgb(_profile.IconColorArgb),
                hovered: _hovered,
                busy: _busy,
                _spin);
        }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        try
        {
            IntPtr memDc = CreateCompatibleDC(screenDc);
            try
            {
                IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                try
                {
                    IntPtr previous = SelectObject(memDc, hBitmap);
                    try
                    {
                        var dst = new POINT(Left, Top);
                        var size = new SIZE(bmp.Width, bmp.Height);
                        var src = new POINT(0, 0);
                        var blend = new BLENDFUNCTION
                        {
                            BlendOp = AC_SRC_OVER,
                            BlendFlags = 0,
                            SourceConstantAlpha = 255,
                            AlphaFormat = AC_SRC_ALPHA,
                        };
                        UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
                    }
                    finally
                    {
                        SelectObject(memDc, previous);
                    }
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            finally
            {
                DeleteDC(memDc);
            }
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
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
        ApplyLayeredBitmap();
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
        ApplyLayeredBitmap();
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
