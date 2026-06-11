// Headless smoke test of the simulation — mirrors main.py's --selftest:
// ~1200 frames of scripted random input, then a forced level-clear that
// exercises the LEVEL_CLEAR -> INSTANT REPLAY path. No graphics device.

using System;

namespace FoodFight
{
    public static class Selftest
    {
        public static int Run()
        {
            var sim = new GameSim(42);
            sim.ResetGame();
            sim.NewLevel();
            sim.SetState(GameState.Playing);
            var rng = new Random(42);

            for (int frame = 0; frame < 1200; frame++)
            {
                SimInput inp = RandomInput(rng);
                if (sim.Ammo < 5) sim.Ammo = 5;      // keep firing exercised
                sim.Tick(1f / 60f, inp);
                sim.PendingSounds.Clear();
                if (sim.State == GameState.GameOver)
                {
                    sim.ResetGame();
                    sim.NewLevel();
                    sim.SetState(GameState.Playing);
                }
            }

            // force the level-clear / replay paths
            sim.SetState(GameState.Playing);
            sim.CloseCall = true;
            sim.Px = sim.ConeX;
            sim.Py = sim.ConeY;
            sim.Tick(1f / 60f, new SimInput());
            if (sim.State != GameState.LevelClear)
            {
                Console.WriteLine("SELFTEST FAIL: expected LevelClear, got " + sim.State);
                return 1;
            }
            bool sawReplay = false;
            for (int frame = 0; frame < 400; frame++)
            {
                sim.Tick(1f / 60f, new SimInput());
                sim.PendingSounds.Clear();
                if (sim.State == GameState.Replay) sawReplay = true;
            }
            if (!sawReplay)
            {
                Console.WriteLine("SELFTEST FAIL: instant replay never ran");
                return 1;
            }
            Console.WriteLine(string.Format(
                "SELFTEST OK  score={0} level={1} state={2}",
                sim.Score, sim.Level, sim.State));
            return 0;
        }

        private static SimInput RandomInput(Random rng)
        {
            SimInput inp = new SimInput();
            if (rng.NextDouble() < 0.3) inp.MoveY -= 1;   // W
            if (rng.NextDouble() < 0.4) inp.MoveX -= 1;   // A
            if (rng.NextDouble() < 0.3) inp.MoveY += 1;   // S
            if (rng.NextDouble() < 0.3) inp.MoveX += 1;   // D
            if (rng.NextDouble() < 0.3) inp.FireY -= 1;   // I
            if (rng.NextDouble() < 0.3) inp.FireX -= 1;   // J
            if (rng.NextDouble() < 0.3) inp.FireY += 1;   // K
            if (rng.NextDouble() < 0.3) inp.FireX += 1;   // L
            return inp;
        }
    }
}
