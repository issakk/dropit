using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DropLite.Services;

/// <summary>Draws the DropLite emblem (a falling arrow inside a disc) for the tray and floating icons.</summary>
internal static class IconFactory
{
    public static Icon CreateIcon(Color baseColor, int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawDropIcon(g, new RectangleF(0, 0, size, size), baseColor, hovered: false, busy: false);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static void DrawDropIcon(
        Graphics g, RectangleF rect, Color baseColor, bool hovered, bool busy, float spinAngle = 0f)
    {
        using (var discPath = new GraphicsPath())
        {
            discPath.AddEllipse(rect);
            using var discBrush = new LinearGradientBrush(
                rect, Lighten(baseColor, 0.25f), Darken(baseColor, 0.30f), LinearGradientMode.Vertical);
            g.FillPath(discBrush, discPath);

            if (hovered)
            {
                float w = Math.Max(2f, rect.Width / 22f);
                using var glow = new Pen(Color.FromArgb(220, 255, 255, 255), w);
                g.DrawPath(glow, discPath);
            }
        }

        float x0 = rect.X, y0 = rect.Y, w0 = rect.Width, h0 = rect.Height;

        if (busy)
        {
            float radius = w0 * 0.38f;
            using var ringPen = new Pen(Color.FromArgb(210, 255, 255, 255), Math.Max(2f, w0 / 16f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawArc(ringPen, x0 + w0 / 2f - radius, y0 + h0 / 2f - radius, radius * 2f, radius * 2f, spinAngle, 110f);
            return;
        }

        PointF[] arrow =
        {
            new(x0 + 0.42f * w0, y0 + 0.20f * h0),
            new(x0 + 0.58f * w0, y0 + 0.20f * h0),
            new(x0 + 0.58f * w0, y0 + 0.48f * h0),
            new(x0 + 0.72f * w0, y0 + 0.48f * h0),
            new(x0 + 0.50f * w0, y0 + 0.78f * h0),
            new(x0 + 0.28f * w0, y0 + 0.48f * h0),
            new(x0 + 0.42f * w0, y0 + 0.48f * h0),
        };
        using (var arrowPath = new GraphicsPath())
        {
            arrowPath.AddPolygon(arrow);
            using var arrowBrush = new SolidBrush(Color.FromArgb(245, 255, 255, 255));
            g.FillPath(arrowBrush, arrowPath);
        }
    }

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
