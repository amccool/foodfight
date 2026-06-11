using System;

namespace FoodFight
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            foreach (string a in args)
            {
                if (a == "--selftest")
                {
                    Environment.Exit(Selftest.Run());
                }
            }
            using (var game = new FoodFightGame())
            {
                game.Run();
            }
        }
    }
}
