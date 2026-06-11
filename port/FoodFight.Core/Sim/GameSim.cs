// The complete Food Fight game simulation — a 1:1 port of main.py's Game
// update logic. No MonoGame dependency: rendering reads this state, audio
// drains PendingSounds. Deterministic when constructed with a seed.

using System;
using System.Collections.Generic;

namespace FoodFight
{
    public class GameSim
    {
        public GameState State = GameState.Title;
        public float StateT;
        public float Blink;
        public bool Paused;
        public bool QuitRequested;

        public int Score;
        public int HighScore;
        public int Lives;
        public int Level;
        private int _nextExtra;

        public float MeltTime;
        public float MeltFrac;
        public float ConeX, ConeY;
        public float PlayerStartX, PlayerStartY;

        public List<Hole> Holes = new List<Hole>();
        public List<Pile> Piles = new List<Pile>();
        public List<Chef> Chefs = new List<Chef>();
        public List<Projectile> Projectiles = new List<Projectile>();
        public List<Splat> Splats = new List<Splat>();
        public List<ReplayFrame> ReplayFrames = new List<ReplayFrame>();
        public bool CloseCall;
        public int ReplayI;

        public float Px, Py;
        public float Pvx, Pvy;
        public float FaceX = -1f, FaceY;
        public int Ammo;
        public int AmmoFood = 1;
        private float _fireCd;
        private float _pickupCd;

        public DeathKind DeathKind;
        public int ClearBonus;

        public List<string> PendingSounds = new List<string>();

        private readonly Random _rng;

        public GameSim(int seed = -1)
        {
            _rng = seed < 0 ? new Random() : new Random(seed);
            ResetGame();
        }

        private void Sound(string name) { PendingSounds.Add(name); }

        private float Uniform(float a, float b)
        {
            return a + (float)_rng.NextDouble() * (b - a);
        }

        // ------------------------------------------------------- game setup
        public void ResetGame()
        {
            Score = 0;
            Lives = C.StartLives;
            Level = 1;
            _nextExtra = C.ExtraLifeEvery;
        }

        public void NewLevel()
        {
            MeltTime = Math.Max(18f, 40f - 2f * (Level - 1));
            MeltFrac = 1f;
            int nChefs = Math.Min(3 + (Level - 1) / 2, 6);
            float chefSpeed = Math.Min(115f + 12f * (Level - 1), 195f);

            ConeX = C.Wall + 70;
            ConeY = C.SH / 2f;
            PlayerStartX = C.SW - C.Wall - 70;
            PlayerStartY = C.SH / 2f;

            // holes: keep them away from the cone, the player start and each other
            Holes.Clear();
            int tries = 0;
            int want = Math.Min(4 + Level / 3, 6);
            while (Holes.Count < want && tries < 400)
            {
                tries++;
                float x = Uniform(C.Wall + 120, C.SW - C.Wall - 120);
                float y = Uniform(C.Wall + 90, C.SH - C.Wall - 90);
                if (C.Dist(x, y, ConeX, ConeY) < 150) continue;
                if (C.Dist(x, y, PlayerStartX, PlayerStartY) < 150) continue;
                bool tooClose = false;
                foreach (Hole h in Holes)
                    if (C.Dist(x, y, h.X, h.Y) < 140) { tooClose = true; break; }
                if (tooClose) continue;
                Holes.Add(new Hole { X = x, Y = y });
            }

            // food piles
            Piles.Clear();
            tries = 0;
            while (Piles.Count < 8 && tries < 500)
            {
                tries++;
                float x = Uniform(C.Wall + 60, C.SW - C.Wall - 60);
                float y = Uniform(C.Wall + 60, C.SH - C.Wall - 60);
                if (C.Dist(x, y, ConeX, ConeY) < 110) continue;
                bool tooClose = false;
                foreach (Hole h in Holes)
                    if (C.Dist(x, y, h.X, h.Y) < 95) { tooClose = true; break; }
                if (!tooClose)
                    foreach (Pile p in Piles)
                        if (C.Dist(x, y, p.X, p.Y) < 95) { tooClose = true; break; }
                if (tooClose) continue;
                Piles.Add(new Pile { X = x, Y = y, FoodIdx = _rng.Next(Foods.All.Length) });
            }

            Chefs.Clear();
            for (int i = 0; i < nChefs; i++)
            {
                Chefs.Add(new Chef
                {
                    AccentIdx = i % Foods.ChefAccents.Length,
                    Speed = chefSpeed * Uniform(0.85f, 1.1f),
                    Timer = Uniform(0.5f, 2.0f),
                    ThrowCd = Uniform(1.5f, 3.0f),
                    Phase = Uniform(0f, (float)(Math.PI * 2)),
                });
            }
            Projectiles.Clear();
            Splats.Clear();
            ReplayFrames.Clear();
            CloseCall = false;
            ResetPlayer();
        }

        public void ResetPlayer()
        {
            Px = PlayerStartX;
            Py = PlayerStartY;
            Pvx = Pvy = 0f;
            FaceX = -1f; FaceY = 0f;
            Ammo = 0;
            AmmoFood = 1;
            _fireCd = 0f;
            _pickupCd = 0f;
            Projectiles.Clear();
            foreach (Chef c in Chefs)
            {
                c.State = ChefState.Waiting;
                c.Timer = Uniform(0.4f, 1.8f);
            }
        }

        // --------------------------------------------------------- helpers
        public void AddScore(int pts)
        {
            Score += pts;
            if (Score >= _nextExtra)
            {
                _nextExtra += C.ExtraLifeEvery;
                Lives++;
                Sound("eat");
            }
            if (Score > HighScore) HighScore = Score;
        }

        public void SetState(GameState st)
        {
            State = st;
            StateT = 0f;
        }

        private void KillPlayer(DeathKind kind)
        {
            DeathKind = kind;
            Sound(kind == DeathKind.Hole ? "fall"
                : kind == DeathKind.Melt ? "melt" : "death");
            SetState(GameState.Death);
        }

        // ---------------------------------------------------------- update
        /// <summary>Advance one frame. Returns false when the player asked
        /// to quit from the title screen.</summary>
        public bool Tick(float dt, SimInput inp)
        {
            if (State == GameState.Playing &&
                (inp.PausePressed || (inp.StartPressed && Paused)))
            {
                Paused = !Paused;
            }
            if (Paused && State == GameState.Playing) return true;

            StateT += dt;
            Blink += dt;

            if (inp.StartPressed)
            {
                if (State == GameState.Title)
                {
                    ResetGame();
                    NewLevel();
                    Sound("start");
                    SetState(GameState.LevelStart);
                }
                else if (State == GameState.GameOver)
                {
                    SetState(GameState.Title);
                }
            }
            if (State == GameState.Replay && inp.AnyPressed)
            {
                FinishReplay();
                return true;
            }
            if (inp.BackPressed)
            {
                if (State == GameState.Title)
                {
                    QuitRequested = true;
                    return false;
                }
                SetState(GameState.Title);
            }

            if (State == GameState.LevelStart && StateT > 1.6f)
            {
                SetState(GameState.Playing);
            }
            else if (State == GameState.Playing)
            {
                UpdatePlaying(dt, inp);
            }
            else if (State == GameState.Death && StateT > 1.4f)
            {
                Lives--;
                if (Lives <= 0)
                {
                    SetState(GameState.GameOver);
                }
                else
                {
                    if (DeathKind == DeathKind.Melt) MeltFrac = 1f;
                    ResetPlayer();
                    SetState(GameState.LevelStart);
                }
            }
            else if (State == GameState.LevelClear)
            {
                if (StateT > 2.4f)
                {
                    if (CloseCall && ReplayFrames.Count > 0)
                    {
                        ReplayI = 0;
                        SetState(GameState.Replay);
                    }
                    else
                    {
                        FinishReplay();
                    }
                }
            }
            else if (State == GameState.Replay)
            {
                ReplayI += 2;       // play back slightly fast
                if (ReplayI >= ReplayFrames.Count) FinishReplay();
            }
            return true;
        }

        private void FinishReplay()
        {
            Level++;
            NewLevel();
            Sound("start");
            SetState(GameState.LevelStart);
        }

        private void UpdatePlaying(float dt, SimInput inp)
        {
            // ---- player movement (WASD / left stick)
            float mx = inp.MoveX, my = inp.MoveY;
            if (mx != 0f || my != 0f)
            {
                float n = (float)Math.Sqrt(mx * mx + my * my);
                Pvx = mx / n * C.PlayerSpeed;
                Pvy = my / n * C.PlayerSpeed;
                FaceX = mx / n; FaceY = my / n;
            }
            else
            {
                Pvx = Pvy = 0f;
            }
            Px = C.Clamp(Px + Pvx * dt, C.Wall + C.PlayerR, C.SW - C.Wall - C.PlayerR);
            Py = C.Clamp(Py + Pvy * dt, C.Wall + C.PlayerR, C.SH - C.Wall - C.PlayerR);

            // ---- firing (IJKL / right stick, hold for constant fire)
            _fireCd -= dt;
            float fx = inp.FireX, fy = inp.FireY;
            if ((fx != 0f || fy != 0f) && _fireCd <= 0f && Ammo > 0)
            {
                float n = (float)Math.Sqrt(fx * fx + fy * fy);
                FoodDef food = Foods.All[AmmoFood];
                Projectiles.Add(new Projectile
                {
                    X = Px, Y = Py,
                    Vx = fx / n * C.ProjSpeed, Vy = fy / n * C.ProjSpeed,
                    Color = food.Color, FromPlayer = true, Points = food.Points,
                });
                Ammo--;
                _fireCd = C.FireRate;
                Sound("throw");
            }

            // ---- food pickup
            _pickupCd -= dt;
            foreach (Pile p in Piles)
            {
                if (p.Amount > 0 && C.Dist(Px, Py, p.X, p.Y) < 34)
                {
                    if (Ammo < C.MaxCarry && _pickupCd <= 0f)
                    {
                        int grab = Math.Min(Math.Min(3, p.Amount), C.MaxCarry - Ammo);
                        p.Amount -= grab;
                        Ammo += grab;
                        AmmoFood = p.FoodIdx;
                        _pickupCd = 0.12f;
                        Sound("pickup");
                    }
                }
            }

            // ---- melting cone
            MeltFrac -= dt / MeltTime;
            if (MeltFrac < 0.15f) CloseCall = true;
            if (MeltFrac <= 0f)
            {
                MeltFrac = 0f;
                KillPlayer(DeathKind.Melt);
                return;
            }

            // ---- eat the cone -> level clear
            if (C.Dist(Px, Py, ConeX, ConeY) < 42)
            {
                int bonus = (int)(MeltFrac * MeltTime) * 100;
                ClearBonus = 500 + bonus;
                AddScore(ClearBonus);
                Sound("eat");
                SetState(GameState.LevelClear);
                return;
            }

            // ---- holes are deadly
            foreach (Hole h in Holes)
            {
                if (C.Dist(Px, Py, h.X, h.Y) < h.R - 8)
                {
                    KillPlayer(DeathKind.Hole);
                    return;
                }
            }

            // ---- chefs
            foreach (Chef c in Chefs)
            {
                if (c.State == ChefState.Waiting)
                {
                    c.Timer -= dt;
                    if (c.Timer <= 0f && Holes.Count > 0)
                    {
                        Hole hole = Holes[_rng.Next(Holes.Count)];
                        c.X = hole.X; c.Y = hole.Y;
                        c.State = ChefState.Rising;
                        c.Timer = Chef.RiseT;
                    }
                }
                else if (c.State == ChefState.Rising)
                {
                    c.Timer -= dt;
                    if (c.Timer <= 0f) c.State = ChefState.Active;
                }
                else if (c.State == ChefState.Dead)
                {
                    c.Timer -= dt;
                    if (c.Timer <= 0f)
                    {
                        c.State = ChefState.Waiting;
                        c.Timer = Uniform(1.0f, 2.5f);
                    }
                }
                else if (c.State == ChefState.Active)
                {
                    float ang = (float)Math.Atan2(Py - c.Y, Px - c.X);
                    ang += (float)Math.Sin(StateT * 2.2f + c.Phase) * 0.45f;
                    c.X += (float)Math.Cos(ang) * c.Speed * dt;
                    c.Y += (float)Math.Sin(ang) * c.Speed * dt;
                    c.X = C.Clamp(c.X, C.Wall + 14, C.SW - C.Wall - 14);
                    c.Y = C.Clamp(c.Y, C.Wall + 14, C.SH - C.Wall - 14);
                    float d = C.Dist(c.X, c.Y, Px, Py);
                    if (d < 40) CloseCall = true;
                    if (d < C.PlayerR + 13)
                    {
                        KillPlayer(DeathKind.Chef);
                        return;
                    }
                    c.ThrowCd -= dt;
                    if (c.ThrowCd <= 0f && d > 110 && d < 520)
                    {
                        float lead = Uniform(0f, 0.5f);
                        float tx = Px + Pvx * lead;
                        float ty = Py + Pvy * lead;
                        float a = (float)Math.Atan2(ty - c.Y, tx - c.X);
                        a += Uniform(-0.12f, 0.12f);
                        Projectiles.Add(new Projectile
                        {
                            X = c.X, Y = c.Y,
                            Vx = (float)Math.Cos(a) * C.ChefProjSpeed,
                            Vy = (float)Math.Sin(a) * C.ChefProjSpeed,
                            Color = new Rgb(225, 60, 40), FromPlayer = false,
                        });
                        c.ThrowCd = Uniform(1.2f, 2.8f)
                            * Math.Max(0.55f, 1f - Level * 0.04f);
                        Sound("throw");
                    }
                }
            }

            // ---- projectiles
            var alive = new List<Projectile>(Projectiles.Count);
            foreach (Projectile pr in Projectiles)
            {
                pr.X += pr.Vx * dt;
                pr.Y += pr.Vy * dt;
                bool hit = false;
                if (!(pr.X > C.Wall && pr.X < C.SW - C.Wall
                      && pr.Y > C.Wall && pr.Y < C.SH - C.Wall))
                {
                    Splats.Add(new Splat
                    {
                        X = C.Clamp(pr.X, C.Wall, C.SW - C.Wall),
                        Y = C.Clamp(pr.Y, C.Wall, C.SH - C.Wall),
                        Color = pr.Color,
                    });
                    hit = true;
                }
                else if (pr.FromPlayer)
                {
                    foreach (Chef c in Chefs)
                    {
                        if (c.State == ChefState.Active
                            && C.Dist(pr.X, pr.Y, c.X, c.Y) < 24)
                        {
                            c.State = ChefState.Dead;
                            c.Timer = Chef.SplatT;
                            AddScore(pr.Points * Math.Max(1, Level / 2));
                            Splats.Add(new Splat { X = c.X, Y = c.Y, Color = pr.Color });
                            Sound("chefdie");
                            hit = true;
                            break;
                        }
                    }
                }
                else
                {
                    float d = C.Dist(pr.X, pr.Y, Px, Py);
                    if (d < 30) CloseCall = true;
                    if (d < C.PlayerR + 6)
                    {
                        Splats.Add(new Splat { X = Px, Y = Py, Color = pr.Color });
                        Sound("splat");
                        KillPlayer(DeathKind.Hit);
                        return;
                    }
                }
                if (!hit) alive.Add(pr);
                else Sound("splat");
            }
            Projectiles = alive;

            for (int i = Splats.Count - 1; i >= 0; i--)
            {
                Splats[i].Ttl -= dt;
                if (Splats[i].Ttl <= 0f) Splats.RemoveAt(i);
            }

            // ---- record for instant replay (keep the last ~25 seconds)
            var chefSnap = new ReplayChef[Chefs.Count];
            for (int i = 0; i < Chefs.Count; i++)
                chefSnap[i] = new ReplayChef { X = Chefs[i].X, Y = Chefs[i].Y, State = Chefs[i].State };
            var projSnap = new ReplayProj[Projectiles.Count];
            for (int i = 0; i < Projectiles.Count; i++)
                projSnap[i] = new ReplayProj { X = Projectiles[i].X, Y = Projectiles[i].Y, Color = Projectiles[i].Color };
            ReplayFrames.Add(new ReplayFrame
            {
                Px = Px, Py = Py, Chefs = chefSnap, Projs = projSnap, Melt = MeltFrac,
            });
            if (ReplayFrames.Count > C.Fps * 25) ReplayFrames.RemoveAt(0);
        }
    }
}
