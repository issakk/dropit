using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DropLite.Services;

/// <summary>
/// 绘制 DropLite 徽标（圆盘 + 下落箭头）：柔和投影、垂直渐变、顶部高光、内外描边，
/// 全部基于像素 alpha，可在分层窗口上呈现平滑边缘。
/// </summary>
internal static class IconFactory
{
    public static Icon CreateIcon(Color baseColor, int size = 32)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawDropIcon(g, new RectangleF(1.5f, 1.5f, size - 4.5f, size - 4.5f), baseColor, hovered: false, busy: false);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static void DrawDropIcon(
        Graphics g, RectangleF rect, Color baseColor, bool hovered, bool busy, float spinAngle = 0f)
    {
        if (busy)
        {
            DrawSpinner(g, rect, baseColor, spinAngle);
            return;
        }

        // 柔和投影（两层椭圆叠出渐弱的月牙影）
        for (int i = 0; i < 2; i++)
        {
            float expand = 1f + i;
            var shadowRect = new RectangleF(
                rect.X - expand + 1f, rect.Y - expand + 2f, rect.Width + expand * 2f, rect.Height + expand * 2f);
            using (var shadowPath = CirclePath(shadowRect))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(i == 0 ? 60 : 30, 0, 0, 0)))
            {
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        // 主盘：垂直渐变 + 裁剪在盘内的顶部径向高光
        using (var disc = CirclePath(rect))
        {
            using (var body = new LinearGradientBrush(
                rect, Lighten(baseColor, 0.32f), Darken(baseColor, 0.30f), LinearGradientMode.Vertical))
            {
                g.FillPath(body, disc);
            }

            g.SetClip(disc);
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(
                    rect.X + rect.Width * 0.04f, rect.Y - rect.Height * 0.38f,
                    rect.Width * 0.92f, rect.Height * 0.85f);
                using var glowBrush = new PathGradientBrush(glow)
                {
                    CenterColor = Color.FromArgb(95, 255, 255, 255),
                    SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) },
                };
                g.FillPath(glowBrush, glow);
            }
            g.ResetClip();
        }

        // 内外描边：外圈收边、内圈提亮
        using (var disc = CirclePath(rect))
        using (var outerRim = new Pen(Color.FromArgb(70, 0, 0, 0), 1.3f))
        {
            g.DrawPath(outerRim, disc);
        }
        using (var inner = CirclePath(Shrink(rect, 1.4f)))
        using (var innerRim = new Pen(Color.FromArgb(120, 255, 255, 255), 1.2f))
        {
            g.DrawPath(innerRim, inner);
        }

        // 箭头：微投影 + 白色渐变
        PointF[] arrow = ArrowPoints(rect);
        using (var arrowShadow = new GraphicsPath())
        {
            var lifted = new PointF[arrow.Length];
            for (int i = 0; i < arrow.Length; i++)
            {
                lifted[i] = new PointF(arrow[i].X, arrow[i].Y + 1.6f);
            }
            arrowShadow.AddPolygon(lifted);
            using var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
            g.FillPath(shadowBrush, arrowShadow);
        }
        using (var arrowPath = new GraphicsPath())
        {
            arrowPath.AddPolygon(arrow);
            using var arrowBrush = new LinearGradientBrush(
                rect, Color.White, Color.FromArgb(226, 240, 255), LinearGradientMode.Vertical);
            g.FillPath(arrowBrush, arrowPath);
        }

        // 悬停光环
        if (hovered)
        {
            using var halo = CirclePath(Expand(rect, 1f));
            using var haloPen = new Pen(Color.FromArgb(235, 255, 255, 255), Math.Max(2f, rect.Width / 16f));
            g.DrawPath(haloPen, halo);
        }
    }

    private static void DrawSpinner(Graphics g, RectangleF rect, Color baseColor, float angle)
    {
        using (var disc = CirclePath(rect))
        using (var body = new LinearGradientBrush(
            rect, Lighten(baseColor, 0.18f), Darken(baseColor, 0.38f), LinearGradientMode.Vertical))
        {
            g.FillPath(body, disc);
        }

        float cx = rect.X + rect.Width / 2f;
        float cy = rect.Y + rect.Height / 2f;
        float radius = rect.Width * 0.32f;
        float thickness = Math.Max(2.5f, rect.Width / 12f);

        using (var track = new Pen(Color.FromArgb(60, 255, 255, 255), thickness))
        {
            g.DrawArc(track, cx - radius, cy - radius, radius * 2f, radius * 2f, 0f, 360f);
        }
        using (var arc = new Pen(Color.White, thickness))
        {
            arc.StartCap = LineCap.Round;
            arc.EndCap = LineCap.Round;
            g.DrawArc(arc, cx - radius, cy - radius, radius * 2f, radius * 2f, angle, 120f);
        }
    }

    private static PointF[] ArrowPoints(RectangleF r)
    {
        float x = r.X, y = r.Y, w = r.Width, h = r.Height;
        return new[]
        {
            new PointF(x + 0.42f * w, y + 0.20f * h),
            new PointF(x + 0.58f * w, y + 0.20f * h),
            new PointF(x + 0.58f * w, y + 0.48f * h),
            new PointF(x + 0.73f * w, y + 0.48f * h),
            new PointF(x + 0.50f * w, y + 0.79f * h),
            new PointF(x + 0.27f * w, y + 0.48f * h),
            new PointF(x + 0.42f * w, y + 0.48f * h),
        };
    }

    private static GraphicsPath CirclePath(RectangleF r)
    {
        var path = new GraphicsPath();
        path.AddEllipse(r);
        return path;
    }

    private static RectangleF Shrink(RectangleF r, float d)
        => new(r.X + d, r.Y + d, r.Width - d * 2f, r.Height - d * 2f);

    private static RectangleF Expand(RectangleF r, float d) => Shrink(r, -d);

    private static Color Lighten(Color c, float amount) => Blend(c, Color.White, amount);

    private static Color Darken(Color c, float amount) => Blend(c, Color.Black, amount);

    private static Color Blend(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            a.A,
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}
