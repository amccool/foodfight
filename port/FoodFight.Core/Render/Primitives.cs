// Runtime-generated textures and shape helpers. The pygame original drew
// everything with primitives; we do the same with a handful of CPU-
// rasterized textures so there is no content pipeline at all.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FoodFight
{
    public class Primitives
    {
        public Texture2D Pixel;     // 1x1 white
        public Texture2D Circle;    // 128x128 white filled circle, soft edge
        public Texture2D Triangle;  // 64x64 white triangle pointing down (cone)
        public Texture2D Cap;       // the player's red cap arc (66x60)

        public Primitives(GraphicsDevice gd)
        {
            Pixel = new Texture2D(gd, 1, 1);
            Pixel.SetData(new[] { Color.White });

            Circle = MakeTexture(gd, 128, 128, (x, y) =>
            {
                float dx = x - 63.5f, dy = y - 63.5f;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                float a = C.Clamp(63.5f - d + 0.5f, 0f, 1f);
                return new Color(1f, 1f, 1f, a);
            });

            Triangle = MakeTexture(gd, 64, 64, (x, y) =>
            {
                float half = 31.5f * (1f - y / 63f);
                return Math.Abs(x - 31.5f) <= half ? Color.White : Color.Transparent;
            });

            // pygame: arc in rect 22x20 (ellipse a=11 b=10), angles 0.2..pi-0.2,
            // width 6 inward. Rasterized at 3x.
            Cap = MakeTexture(gd, 66, 60, (x, y) =>
            {
                float dx = x - 32.5f, dy = y - 29.5f;
                float ro = (dx / 33f) * (dx / 33f) + (dy / 30f) * (dy / 30f);
                float ri = (dx / 15f) * (dx / 15f) + (dy / 12f) * (dy / 12f);
                double ang = Math.Atan2(-dy, dx);
                bool inBand = ro <= 1f && ri >= 1f;
                bool inArc = ang >= 0.2 && ang <= Math.PI - 0.2;
                return inBand && inArc ? Color.White : Color.Transparent;
            });
        }

        private static Texture2D MakeTexture(GraphicsDevice gd, int w, int h,
                                             Func<int, int, Color> shade)
        {
            var tex = new Texture2D(gd, w, h);
            var data = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    data[y * w + x] = shade(x, y);
            tex.SetData(data);
            return tex;
        }

        public static Color ToColor(Rgb c) { return new Color(c.R, c.G, c.B); }

        public void Rect(SpriteBatch sb, float x, float y, float w, float h, Color col)
        {
            sb.Draw(Pixel, new Rectangle((int)x, (int)y, (int)w, (int)h), col);
        }

        public void RectOutline(SpriteBatch sb, float x, float y, float w, float h,
                                int t, Color col)
        {
            Rect(sb, x, y, w, t, col);
            Rect(sb, x, y + h - t, w, t, col);
            Rect(sb, x, y, t, h, col);
            Rect(sb, x + w - t, y, t, h, col);
        }

        public void Ellipse(SpriteBatch sb, float x, float y, float w, float h, Color col)
        {
            sb.Draw(Circle, new Rectangle((int)x, (int)y, (int)w, (int)h), col);
        }

        public void CircleAt(SpriteBatch sb, float cx, float cy, float r, Color col)
        {
            Ellipse(sb, cx - r, cy - r, r * 2, r * 2, col);
        }

        /// <summary>pygame-style outlined ellipse: outline colour drawn full
        /// size, fill inset by the outline width on top.</summary>
        public void EllipseOutlined(SpriteBatch sb, float x, float y, float w, float h,
                                    int t, Color fill, Color line)
        {
            Ellipse(sb, x, y, w, h, line);
            Ellipse(sb, x + t, y + t, w - 2 * t, h - 2 * t, fill);
        }
    }
}
