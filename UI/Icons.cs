using System.Numerics;
using Raylib_cs;

namespace FluidSimulationByAI.UI;

public enum Icon { None, Play, Pause, Reset, Step, Add, Attract, Repel, Erase, Square, Cube, ChevronDown, ChevronRight, Help, Eye, Fullscreen }

/// <summary>Small vector icons drawn with primitives (no image assets needed).</summary>
public static class Icons
{
    public static void Draw(Icon icon, Vector2 c, float s, Color col)
    {
        float h = s * 0.5f;
        float t = MathF.Max(1.6f, s * 0.11f); // stroke width
        switch (icon)
        {
            case Icon.Play:
                UI.Draw.Triangle(new(c.X - h * 0.55f, c.Y - h * 0.75f), new(c.X - h * 0.55f, c.Y + h * 0.75f), new(c.X + h * 0.8f, c.Y), col);
                break;

            case Icon.Pause:
                UI.Draw.RoundRect(new Rectangle(c.X - h * 0.65f, c.Y - h * 0.7f, h * 0.45f, h * 1.4f), 1.5f, col);
                UI.Draw.RoundRect(new Rectangle(c.X + h * 0.2f, c.Y - h * 0.7f, h * 0.45f, h * 1.4f), 1.5f, col);
                break;

            case Icon.Step:
                UI.Draw.Triangle(new(c.X - h * 0.7f, c.Y - h * 0.7f), new(c.X - h * 0.7f, c.Y + h * 0.7f), new(c.X + h * 0.35f, c.Y), col);
                UI.Draw.RoundRect(new Rectangle(c.X + h * 0.4f, c.Y - h * 0.7f, h * 0.32f, h * 1.4f), 1f, col);
                break;

            case Icon.Reset:
            {
                float r = h * 0.78f;
                Raylib.DrawRing(c, r - t, r, -60, 220, 32, col);
                // Arrow head at the start of the arc (angle -60°)
                float a = -60f * MathF.PI / 180f;
                var tip = new Vector2(c.X + MathF.Cos(a) * (r - t * 0.5f), c.Y + MathF.Sin(a) * (r - t * 0.5f));
                float hs = h * 0.5f;
                UI.Draw.Triangle(tip + new Vector2(-hs * 0.15f, -hs * 0.9f), tip + new Vector2(hs * 0.95f, hs * 0.2f), tip + new Vector2(-hs * 0.55f, hs * 0.55f), col);
                break;
            }

            case Icon.Add:
                UI.Draw.RoundRect(new Rectangle(c.X - h * 0.8f, c.Y - t * 0.6f, h * 1.6f, t * 1.2f), t * 0.5f, col);
                UI.Draw.RoundRect(new Rectangle(c.X - t * 0.6f, c.Y - h * 0.8f, t * 1.2f, h * 1.6f), t * 0.5f, col);
                break;

            case Icon.Attract:
            case Icon.Repel:
            {
                Raylib.DrawCircleV(c, h * 0.22f, col);
                bool inward = icon == Icon.Attract;
                for (int i = 0; i < 4; i++)
                {
                    float a = i * MathF.PI / 2 + MathF.PI / 4;
                    var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    var p0 = c + dir * h * 0.45f;
                    var p1 = c + dir * h * 0.98f;
                    Raylib.DrawLineEx(p0, p1, t, col);
                    var tip = inward ? p0 : p1;
                    var back = inward ? dir : -dir;
                    var perp = new Vector2(-dir.Y, dir.X);
                    UI.Draw.Triangle(tip - back * 0.0f, tip + back * h * 0.32f + perp * h * 0.2f, tip + back * h * 0.32f - perp * h * 0.2f, col);
                }
                break;
            }

            case Icon.Erase:
            {
                var rect = new Rectangle(c.X, c.Y, s * 0.42f, s * 0.78f);
                Raylib.DrawRectanglePro(rect, new Vector2(rect.Width / 2, rect.Height / 2), 45f, Theme.WithAlpha(col, 0.35f));
                var top = new Rectangle(c.X + h * 0.24f, c.Y - h * 0.24f, s * 0.42f, s * 0.39f);
                Raylib.DrawRectanglePro(top, new Vector2(top.Width / 2, top.Height / 2), 45f, col);
                break;
            }

            case Icon.Square:
                UI.Draw.RoundRectLines(new Rectangle(c.X - h * 0.7f, c.Y - h * 0.7f, h * 1.4f, h * 1.4f), 2.5f, t, col);
                break;

            case Icon.Cube:
            {
                float r = h * 0.85f;
                var top = new Vector2(c.X, c.Y - r);
                var ur = new Vector2(c.X + r * 0.87f, c.Y - r * 0.5f);
                var lr = new Vector2(c.X + r * 0.87f, c.Y + r * 0.5f);
                var bot = new Vector2(c.X, c.Y + r);
                var ll = new Vector2(c.X - r * 0.87f, c.Y + r * 0.5f);
                var ul = new Vector2(c.X - r * 0.87f, c.Y - r * 0.5f);
                float lw = t * 0.85f;
                Raylib.DrawLineEx(top, ur, lw, col); Raylib.DrawLineEx(ur, lr, lw, col);
                Raylib.DrawLineEx(lr, bot, lw, col); Raylib.DrawLineEx(bot, ll, lw, col);
                Raylib.DrawLineEx(ll, ul, lw, col); Raylib.DrawLineEx(ul, top, lw, col);
                Raylib.DrawLineEx(ul, c, lw, col); Raylib.DrawLineEx(ur, c, lw, col); Raylib.DrawLineEx(c, bot, lw, col);
                break;
            }

            case Icon.ChevronDown:
                Raylib.DrawLineEx(new(c.X - h * 0.5f, c.Y - h * 0.22f), new(c.X, c.Y + h * 0.28f), t, col);
                Raylib.DrawLineEx(new(c.X, c.Y + h * 0.28f), new(c.X + h * 0.5f, c.Y - h * 0.22f), t, col);
                break;

            case Icon.ChevronRight:
                Raylib.DrawLineEx(new(c.X - h * 0.22f, c.Y - h * 0.5f), new(c.X + h * 0.28f, c.Y), t, col);
                Raylib.DrawLineEx(new(c.X + h * 0.28f, c.Y), new(c.X - h * 0.22f, c.Y + h * 0.5f), t, col);
                break;

            case Icon.Help:
                Raylib.DrawRing(c, h * 0.85f - t, h * 0.85f, 0, 360, 32, col);
                Fonts.DrawIn("?", new Rectangle(c.X - h, c.Y - h, s, s), (int)(s * 0.7f), col, Weight.Bold, 1);
                break;

            case Icon.Eye:
                Raylib.DrawRing(c, h * 0.3f, h * 0.3f + t, 0, 360, 24, col);
                Raylib.DrawCircleV(c, h * 0.16f, col);
                Raylib.DrawLineEx(new(c.X - h * 0.95f, c.Y), new(c.X - h * 0.45f, c.Y - h * 0.45f), t, col);
                Raylib.DrawLineEx(new(c.X - h * 0.45f, c.Y - h * 0.45f), new(c.X + h * 0.45f, c.Y - h * 0.45f), t, col);
                Raylib.DrawLineEx(new(c.X + h * 0.45f, c.Y - h * 0.45f), new(c.X + h * 0.95f, c.Y), t, col);
                Raylib.DrawLineEx(new(c.X - h * 0.95f, c.Y), new(c.X - h * 0.45f, c.Y + h * 0.45f), t, col);
                Raylib.DrawLineEx(new(c.X - h * 0.45f, c.Y + h * 0.45f), new(c.X + h * 0.45f, c.Y + h * 0.45f), t, col);
                Raylib.DrawLineEx(new(c.X + h * 0.45f, c.Y + h * 0.45f), new(c.X + h * 0.95f, c.Y), t, col);
                break;

            case Icon.Fullscreen:
            {
                float k = h * 0.8f, l = h * 0.45f;
                foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                {
                    var corner = new Vector2(c.X + sx * k, c.Y + sy * k);
                    Raylib.DrawLineEx(corner, corner + new Vector2(-sx * l, 0), t, col);
                    Raylib.DrawLineEx(corner, corner + new Vector2(0, -sy * l), t, col);
                }
                break;
            }
        }
    }
}
