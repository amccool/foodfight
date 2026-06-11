// Draws the GameSim state — a 1:1 port of main.py's draw functions
// (title, world, cone, player, chefs, food, HUD, overlays, replay).

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FoodFight
{
    public class Renderer
    {
        private static readonly Color Floor = new Color(24, 26, 36);
        private static readonly Color FloorTile = new Color(30, 33, 46);
        private static readonly Color WallC = new Color(96, 60, 130);
        private static readonly Color WallEdge = new Color(150, 105, 200);
        private static readonly Color White = new Color(240, 240, 240);
        private static readonly Color Yellow = new Color(255, 230, 90);
        private static readonly Color Red = new Color(240, 70, 60);

        private const int FontBig = 9;   // 5x7 glyphs * scale ~= pygame 72px
        private const int FontMid = 5;
        private const int FontSml = 3;

        private readonly Primitives _p;
        private readonly PixelFont _font;

        public Renderer(Primitives prims, PixelFont font)
        {
            _p = prims;
            _font = font;
        }

        public void DrawAll(SpriteBatch sb, GameSim g)
        {
            if (g.Paused && g.State == GameState.Playing)
            {
                DrawWorld(sb, g, null);
                Dim(sb);
                CenterText(sb, "PAUSED", FontBig, White, 0);
                return;
            }
            switch (g.State)
            {
                case GameState.Title:
                    DrawTitle(sb, g);
                    break;
                case GameState.GameOver:
                    DrawWorld(sb, g, null);
                    Dim(sb);
                    CenterText(sb, "GAME OVER", FontBig, Red, -40);
                    CenterText(sb, "SCORE  " + g.Score, FontMid, White, 30);
                    if (g.Blink % 1f < 0.6f)
                        CenterText(sb, "PRESS START", FontSml, Yellow, 90);
                    break;
                case GameState.Replay:
                    int i = Math.Min(g.ReplayI, g.ReplayFrames.Count - 1);
                    DrawWorld(sb, g, g.ReplayFrames[i]);
                    if (g.Blink % 0.5f < 0.3f)
                        CenterText(sb, "INSTANT REPLAY", FontBig, Yellow,
                                   -C.SH / 2 + 90);
                    break;
                default:
                    DrawWorld(sb, g, null);
                    if (g.State == GameState.LevelStart)
                    {
                        CenterText(sb, "LEVEL " + g.Level, FontBig, Yellow, -20);
                        CenterText(sb, "EAT THE CONE BEFORE IT MELTS!",
                                   FontSml, White, 50);
                    }
                    else if (g.State == GameState.Death)
                    {
                        string msg =
                            g.DeathKind == DeathKind.Melt ? "THE CONE MELTED!" :
                            g.DeathKind == DeathKind.Hole ? "DOWN THE HOLE!" :
                            g.DeathKind == DeathKind.Chef ? "CAUGHT BY THE CHEF!" :
                            "SPLATTED!";
                        CenterText(sb, msg, FontMid, Red, -10);
                    }
                    else if (g.State == GameState.LevelClear)
                    {
                        CenterText(sb, "DELICIOUS!", FontBig, Yellow, -30);
                        CenterText(sb, "BONUS  " + g.ClearBonus, FontMid, White, 40);
                    }
                    break;
            }
        }

        private void Dim(SpriteBatch sb)
        {
            _p.Rect(sb, 0, 0, C.SW, C.SH, Color.Black * 0.59f);
        }

        private void CenterText(SpriteBatch sb, string txt, int scale,
                                Color col, int dy)
        {
            int w = _font.Measure(txt, scale);
            float x = C.SW / 2f - w / 2f;
            float y = C.SH / 2f + dy - PixelFont.GlyphH * scale / 2f;
            _font.Draw(sb, txt, x + 3, y + 3, scale, Color.Black);
            _font.Draw(sb, txt, x, y, scale, col);
        }

        private void DrawTitle(SpriteBatch sb, GameSim g)
        {
            _p.Rect(sb, 0, 0, C.SW, C.SH, Floor);
            for (int i = 0; i < C.SW; i += 48)
                _p.Rect(sb, i, 0, 1, C.SH, FloorTile);
            for (int i = 0; i < C.SH; i += 48)
                _p.Rect(sb, 0, i, C.SW, 1, FloorTile);

            float t = g.Blink;
            var rng = new Random(3);            // fresh each frame, like main.py
            for (int k = 0; k < 14; k++)
            {
                float fx = (float)((rng.NextDouble() * C.SW
                    + t * (40 + 30 * rng.NextDouble())) % C.SW);
                float fy = (float)((rng.NextDouble() * C.SH
                    + t * (25 + 40 * rng.NextDouble())) % C.SH);
                FoodDef food = Foods.All[k % Foods.All.Length];
                _p.CircleAt(sb, fx, fy, 9, Primitives.ToColor(food.Color));
            }

            CenterText(sb, "FOOD FIGHT", FontBig, Yellow, -190);
            CenterText(sb, "A FAST-FOOD ARCADE HOMAGE", FontSml,
                       new Color(180, 180, 200), -130);
            int y = -60;
            CenterText(sb, "W A S D  :  MOVE", FontSml, White, y); y += 34;
            CenterText(sb, "I J K L  :  THROW FOOD (HOLD = RAPID FIRE)",
                       FontSml, White, y); y += 34;
            CenterText(sb, "GAMEPAD  :  LEFT STICK MOVE, RIGHT STICK THROW",
                       FontSml, White, y); y += 34;
            Color orange = new Color(255, 170, 90);
            CenterText(sb, "EAT THE ICE CREAM CONE BEFORE IT MELTS",
                       FontSml, orange, y); y += 34;
            CenterText(sb, "GRAB FOOD PILES FOR AMMO - SPLAT THE CHEFS",
                       FontSml, orange, y); y += 34;
            CenterText(sb, "DON'T FALL IN THE HOLES", FontSml, orange, y); y += 34;
            CenterText(sb, "HIGH SCORE  " + g.HighScore, FontSml,
                       new Color(160, 220, 160), y + 20);
            if (g.Blink % 1f < 0.6f)
                CenterText(sb, "PRESS SPACE OR START", FontMid, Yellow, y + 80);
        }

        private void DrawWorld(SpriteBatch sb, GameSim g, ReplayFrame snap)
        {
            _p.Rect(sb, 0, 0, C.SW, C.SH, Floor);
            for (int i = C.Wall; i < C.SW - C.Wall; i += 48)
                _p.Rect(sb, i, C.Wall, 1, C.SH - 2 * C.Wall, FloorTile);
            for (int i = C.Wall; i < C.SH - C.Wall; i += 48)
                _p.Rect(sb, C.Wall, i, C.SW - 2 * C.Wall, 1, FloorTile);

            // walls
            _p.Rect(sb, 0, 0, C.SW, C.Wall, WallC);
            _p.Rect(sb, 0, C.SH - C.Wall, C.SW, C.Wall, WallC);
            _p.Rect(sb, 0, 0, C.Wall, C.SH, WallC);
            _p.Rect(sb, C.SW - C.Wall, 0, C.Wall, C.SH, WallC);
            _p.RectOutline(sb, C.Wall - 3, C.Wall - 3,
                           C.SW - 2 * C.Wall + 6, C.SH - 2 * C.Wall + 6, 3, WallEdge);

            foreach (Hole h in g.Holes)
            {
                _p.EllipseOutlined(sb, h.X - h.R, h.Y - h.R * 0.7f,
                                   h.R * 2, h.R * 1.4f, 3,
                                   new Color(8, 8, 12), new Color(60, 60, 75));
            }

            foreach (Splat s in g.Splats)
            {
                float a = C.Clamp(s.Ttl / 2.5f, 0f, 1f);
                var col = new Color(
                    (int)(s.Color.R * a + Floor.R * (1 - a)),
                    (int)(s.Color.G * a + Floor.G * (1 - a)),
                    (int)(s.Color.B * a + Floor.B * (1 - a)));
                _p.Ellipse(sb, s.X - 14, s.Y - 9, 28, 18, col);
            }

            foreach (Pile p in g.Piles)
            {
                if (p.Amount <= 0) continue;
                FoodDef food = Foods.All[p.FoodIdx];
                Color col = Primitives.ToColor(food.Color);
                Color dark = new Color((int)(food.Color.R * 0.6f),
                                       (int)(food.Color.G * 0.6f),
                                       (int)(food.Color.B * 0.6f));
                float size = 8 + 14f * p.Amount / C.PileCapacity;
                _p.CircleAt(sb, p.X, p.Y, size * 0.55f + 1.5f, dark);
                _p.CircleAt(sb, p.X - 8 * size / 14, p.Y + 2 * size / 14, size * 0.8f * 0.55f, col);
                _p.CircleAt(sb, p.X + 8 * size / 14, p.Y + 2 * size / 14, size * 0.8f * 0.55f, col);
                _p.CircleAt(sb, p.X, p.Y - 4 * size / 14, size * 0.55f, col);
                _p.CircleAt(sb, p.X, p.Y + 6 * size / 14, size * 0.9f * 0.55f, col);
            }

            float melt = snap != null ? snap.Melt : g.MeltFrac;
            DrawCone(sb, g.ConeX, g.ConeY, melt);

            if (snap != null)
            {
                for (int i = 0; i < snap.Chefs.Length && i < g.Chefs.Count; i++)
                    DrawChef(sb, snap.Chefs[i].X, snap.Chefs[i].Y,
                             snap.Chefs[i].State, g.Chefs[i].AccentIdx);
                foreach (ReplayProj pr in snap.Projs)
                    DrawFood(sb, pr.X, pr.Y, pr.Color);
                DrawPlayer(sb, g, snap.Px, snap.Py);
            }
            else
            {
                foreach (Chef c in g.Chefs)
                    DrawChef(sb, c.X, c.Y, c.State, c.AccentIdx);
                foreach (Projectile pr in g.Projectiles)
                    DrawFood(sb, pr.X, pr.Y, pr.Color);
                bool hidePlayer = g.State == GameState.Death
                    && g.DeathKind == DeathKind.Hole && g.StateT > 0.5f;
                if (!hidePlayer)
                    DrawPlayer(sb, g, g.Px, g.Py);
            }

            DrawHud(sb, g, melt);
        }

        private void DrawCone(SpriteBatch sb, float x, float y, float frac)
        {
            int pud = (int)(34 * (1 - frac));
            if (pud > 4)
                _p.Ellipse(sb, x - pud, y + 26 - pud / 3f, pud * 2, pud * 0.7f,
                           new Color(250, 200, 215));
            // cone: outline triangle slightly larger behind the fill
            DrawTri(sb, x, y + 1, 18, 33, new Color(160, 115, 60));
            DrawTri(sb, x, y + 2, 16, 32, new Color(210, 160, 90));
            // scoops shrink as it melts
            Color[] scoopCols = { new Color(250, 190, 205),
                                  new Color(250, 245, 235),
                                  new Color(150, 95, 60) };
            for (int i = 0; i < 3; i++)
            {
                float keep = C.Clamp(frac * 3 - (2 - i), 0f, 1f);
                if (keep <= 0.05f) continue;
                float r = (int)(15 * keep) + 2;
                _p.CircleAt(sb, x, y - 4 - i * 16 * keep, r, scoopCols[i]);
            }
        }

        private void DrawTri(SpriteBatch sb, float cx, float topY,
                             float halfW, float h, Color col)
        {
            sb.Draw(_p.Triangle, new Rectangle((int)(cx - halfW), (int)topY,
                                               (int)(halfW * 2), (int)h), col);
        }

        private void DrawPlayer(SpriteBatch sb, GameSim g, float x, float y)
        {
            _p.Ellipse(sb, x - 11, y - 4, 22, 20, new Color(90, 160, 235));
            _p.CircleAt(sb, x, y - 10, 10, new Color(250, 215, 180));
            sb.Draw(_p.Cap, new Rectangle((int)(x - 11), (int)(y - 24), 22, 20),
                    new Color(230, 60, 60));
            int ex = (int)(g.FaceX * 3), ey = (int)(g.FaceY * 2);
            Color eye = new Color(20, 20, 30);
            _p.CircleAt(sb, x - 4 + ex, y - 11 + ey, 2, eye);
            _p.CircleAt(sb, x + 4 + ex, y - 11 + ey, 2, eye);
            Color feet = new Color(60, 60, 80);
            _p.CircleAt(sb, x - 6, y + 15, 4, feet);
            _p.CircleAt(sb, x + 6, y + 15, 4, feet);
        }

        private void DrawChef(SpriteBatch sb, float x, float y,
                              ChefState state, int accentIdx)
        {
            if (state == ChefState.Waiting) return;
            if (state == ChefState.Dead)
            {
                _p.Ellipse(sb, x - 16, y - 8, 32, 16, new Color(200, 200, 210));
                return;
            }
            Color accent = Primitives.ToColor(Foods.ChefAccents[accentIdx]);
            float rise = state == ChefState.Rising ? 0.5f : 1f;
            int h = (int)(26 * rise);
            _p.Ellipse(sb, x - 12, y - h / 2f, 24, h, new Color(235, 235, 240));
            _p.Rect(sb, x - 9, y + 2 - (1 - rise) * 8, 18, 7, accent);
            float hy = y - h / 2f - 6;
            _p.CircleAt(sb, x, hy, 9, new Color(250, 220, 190));
            _p.Rect(sb, x - 8, hy - 16, 16, 9, White);
            _p.CircleAt(sb, x - 6, hy - 16, 5, White);
            _p.CircleAt(sb, x, hy - 18, 6, White);
            _p.CircleAt(sb, x + 6, hy - 16, 5, White);
            Color eye = new Color(30, 30, 40);
            _p.CircleAt(sb, x - 3, hy - 2, 2, eye);
            _p.CircleAt(sb, x + 3, hy - 2, 2, eye);
            _p.Rect(sb, x - 5, hy + 2, 10, 3, accent);
        }

        private void DrawFood(SpriteBatch sb, float x, float y, Rgb c)
        {
            var dark = new Color((int)(c.R * 0.6f), (int)(c.G * 0.6f), (int)(c.B * 0.6f));
            _p.CircleAt(sb, x, y, 6, dark);
            _p.CircleAt(sb, x, y, 5, Primitives.ToColor(c));
        }

        private void DrawHud(SpriteBatch sb, GameSim g, float melt)
        {
            _font.Draw(sb, "SCORE " + g.Score, C.Wall + 8, 2, FontSml, White);
            _font.Draw(sb, "HI " + g.HighScore, C.Wall + 230, 2, FontSml,
                       new Color(180, 180, 200));
            _font.Draw(sb, "LEVEL " + g.Level, C.SW / 2 - 40, 2, FontSml, White);
            // lives
            for (int i = 0; i < g.Lives - 1; i++)
            {
                float lx = C.SW - C.Wall - 20 - i * 26;
                _p.CircleAt(sb, lx, 13, 8, new Color(250, 215, 180));
                sb.Draw(_p.Cap, new Rectangle((int)(lx - 9), 2, 18, 16),
                        new Color(230, 60, 60));
            }
            // ammo readout (bottom left)
            FoodDef food = Foods.All[g.AmmoFood];
            _p.CircleAt(sb, C.Wall + 14, C.SH - 13, 7, Primitives.ToColor(food.Color));
            _font.Draw(sb, "X " + g.Ammo, C.Wall + 28, C.SH - 25, FontSml, White);
            // melt bar (bottom)
            int bw = 300;
            _p.Rect(sb, C.SW / 2 - bw / 2, C.SH - 19, bw, 13, new Color(60, 60, 75));
            Color col = melt > 0.4f ? new Color(110, 205, 60)
                : melt > 0.18f ? new Color(245, 215, 70) : Red;
            _p.Rect(sb, C.SW / 2 - bw / 2 + 2, C.SH - 17,
                    (int)((bw - 4) * melt), 9, col);
            _font.Draw(sb, "MELT", C.SW / 2 - bw / 2 - 70, C.SH - 25, FontSml, White);
        }
    }
}
