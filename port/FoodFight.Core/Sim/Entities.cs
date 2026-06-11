// Entity types and constants for the Food Fight simulation.
// This layer is plain C# (no MonoGame types) so it can run headless
// for the selftest and compile unchanged in the UWP/Xbox head.

using System;
using System.Collections.Generic;

namespace FoodFight
{
    public static class C
    {
        public const int SW = 960;
        public const int SH = 720;
        public const int Wall = 26;
        public const int Fps = 60;

        public const float PlayerSpeed = 200f;
        public const int PlayerR = 14;
        public const float FireRate = 0.11f;
        public const float ProjSpeed = 540f;
        public const float ChefProjSpeed = 330f;
        public const int MaxCarry = 30;
        public const int PileCapacity = 18;
        public const int StartLives = 3;
        public const int ExtraLifeEvery = 15000;

        public static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        public static float Dist(float ax, float ay, float bx, float by)
        {
            return (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        }
    }

    public struct Rgb
    {
        public byte R, G, B;

        public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }
    }

    public struct FoodDef
    {
        public string Name;
        public Rgb Color;
        public int Points;

        public FoodDef(string name, Rgb color, int points)
        {
            Name = name; Color = color; Points = points;
        }
    }

    public static class Foods
    {
        public static readonly FoodDef[] All =
        {
            new FoodDef("PEAS", new Rgb(110, 205, 60), 50),
            new FoodDef("TOMATOES", new Rgb(225, 60, 40), 75),
            new FoodDef("BANANAS", new Rgb(245, 215, 70), 100),
            new FoodDef("PIES", new Rgb(246, 240, 214), 150),
            new FoodDef("WATERMELON", new Rgb(236, 90, 125), 200),
        };

        public static readonly Rgb[] ChefAccents =
        {
            new Rgb(70, 110, 230), new Rgb(180, 70, 200), new Rgb(60, 170, 90),
            new Rgb(235, 140, 40), new Rgb(50, 180, 190), new Rgb(210, 60, 120),
        };
    }

    public enum GameState { Title, LevelStart, Playing, Death, LevelClear, Replay, GameOver }

    public enum ChefState { Waiting, Rising, Active, Dead }

    public enum DeathKind { Melt, Hole, Chef, Hit }

    /// <summary>One frame of merged input. Movement/fire axes are direction
    /// intents (sign matters, magnitude is normalized by the sim); the bools
    /// are edge-triggered (true only on the frame the key/button went down).</summary>
    public struct SimInput
    {
        public float MoveX, MoveY;     // WASD / left stick / dpad
        public float FireX, FireY;     // IJKL / right stick
        public bool StartPressed;      // Space, Enter, pad Start or A
        public bool BackPressed;       // Esc, pad Back
        public bool PausePressed;      // P (pad Start doubles as pause in-game)
        public bool AnyPressed;        // any key/button edge (skips the replay)
    }

    public class Projectile
    {
        public float X, Y, Vx, Vy;
        public Rgb Color;
        public bool FromPlayer;
        public int Points;
    }

    public class Pile
    {
        public float X, Y;
        public int FoodIdx;
        public int Amount = C.PileCapacity;
    }

    public class Hole
    {
        public float X, Y;
        public float R = 30f;
    }

    public class Splat
    {
        public float X, Y;
        public Rgb Color;
        public float Ttl = 2.5f;
    }

    public class Chef
    {
        public const float RiseT = 0.7f;
        public const float SplatT = 0.6f;

        public int AccentIdx;
        public float Speed;
        public float X, Y;
        public ChefState State = ChefState.Waiting;
        public float Timer;
        public float ThrowCd;
        public float Phase;
    }

    public class ReplayChef
    {
        public float X, Y;
        public ChefState State;
    }

    public class ReplayProj
    {
        public float X, Y;
        public Rgb Color;
    }

    public class ReplayFrame
    {
        public float Px, Py;
        public ReplayChef[] Chefs;
        public ReplayProj[] Projs;
        public float Melt;
    }
}
