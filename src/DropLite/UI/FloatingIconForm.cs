using System;
using System.Diagnostics;
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
    private readonly ToolTip _tooltip = new();

    private string? _previewText;
    private readonly Stopwatch _previewClock = new();
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
        DragLeave += (_, _) => { SetHovered(false); HideDropPreview(); };
        DragDrop += OnDragDrop;
        // 拖放循环里窗口收不到鼠标消息，tooltip 的悬停机制失效，由 DragOver 周期性续显。
        DragOver += (_, _) => KeepDropPreviewAlive();
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
        menu.Items.Add("退出 DropLite", null, (_, _) => _app.ExitApp());
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
        ClampToWorkArea();
        ApplyLayeredBitmap();
    }

    /// <summary>位置越界保护：显示器被拔掉或分辨率变化后，把图标拉回可见工作区。</summary>
    private void ClampToWorkArea()
    {
        var area = Screen.FromPoint(Location).WorkingArea;
        int x = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        int y = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        if (x != Left || y != Top)
        {
            Location = new Point(x, y);
            _profile.X = x;
            _profile.Y = y;
            Logger.Info($"icon '{_profile.Name}' clamped to visible area: {x},{y}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spinTimer.Dispose();
            _tooltip.Dispose();
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
        HideDropPreview();
        bool hasFiles = e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        if (_busy || _app.Paused || !hasFiles)
        {
            e.Effect = DragDropEffects.None;
            if (_busy && hasFiles)
            {
                // 拒绝投放时给出原因，避免用户以为功能失灵。
                _previewText = "上一批文件还在处理中，请等旋转动画结束后再拖";
                ShowDropPreview();
            }
            return;
        }
        e.Effect = DragDropEffects.Copy;
        SetHovered(true);
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            _previewText = BuildDropPreview(paths);
            ShowDropPreview();
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        SetHovered(false);
        HideDropPreview();
        if (_busy || _app.Paused || e.Data?.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            return;
        }
        _ = ProcessDropAsync(paths);
    }

    // 拖放期间窗口收不到鼠标消息，WinForms ToolTip 的悬停机制不会触发；
    // 改为 DragEnter 时显式 Show，并在 DragOver 里周期性续显。
    private void ShowDropPreview()
    {
        if (_previewText is null || IsDisposed)
        {
            return;
        }
        _previewClock.Restart();
        var pos = PointToClient(new Point(Cursor.Position.X + 14, Cursor.Position.Y + 18));
        _tooltip.Show(_previewText, this, pos, 3000);
    }

    private void KeepDropPreviewAlive()
    {
        if (_previewText is null || !_previewClock.IsRunning || _previewClock.ElapsedMilliseconds < 2500)
        {
            return;
        }
        ShowDropPreview();
    }

    private void HideDropPreview()
    {
        _previewText = null;
        _previewClock.Reset();
        _tooltip.Hide(this);
    }

    /// <summary>拖入悬停预览：按第一个文件匹配规则，生成将执行的动作说明。</summary>
    private string? BuildDropPreview(string[] paths)
    {
        try
        {
            Destination? dest = FileProcessor.MatchDestination(_profile, paths[0]);
            string preview;
            if (dest is null)
            {
                preview = "没有匹配的规则，文件将被忽略";
            }
            else
            {
                string target = dest.TargetPath.Trim();
                string suffix;
                if (dest.Action == DropAction.Rename)
                {
                    // Rename 的 TargetPath 是命名模板而非路径。
                    suffix = target.Length > 0 ? $"（模板 {target}）" : string.Empty;
                }
                else if (target.Length > 0 && dest.Action is DropAction.Move or DropAction.Copy
                         or DropAction.Compress or DropAction.Extract)
                {
                    suffix = $" → {Environment.ExpandEnvironmentVariables(target)}";
                }
                else
                {
                    suffix = string.Empty;
                }
                preview = $"将执行「{dest.Name}」：{DropActionText.Label(dest.Action)}" + suffix;
            }
            if (paths.Length > 1)
            {
                preview = $"{paths.Length} 项\n" + preview;
            }
            return preview;
        }
        catch
        {
            // 预览失败不影响拖放本身。
            return null;
        }
    }

    private async Task ProcessDropAsync(string[] paths)
    {
        SetBusy(true);
        string profileName = _profile.Name;
        try
        {
            // ProcessAsync 内部会对 profile 做快照，这里不必重复 Clone。
            Logger.Info($"drop on '{profileName}': {paths.Length} item(s)");
            ProcessResult result = await Task.Run(
                () => FileProcessor.ProcessAsync(_profile, paths, message => Logger.Info(message)));
            Logger.Info($"drop on '{profileName}' finished: {result.Summary()}");
            _app.NotifyResult(profileName, result);
        }
        catch (Exception ex)
        {
            Logger.Error("drop processing failed", ex);
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
