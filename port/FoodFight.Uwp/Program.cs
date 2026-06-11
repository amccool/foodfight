// UWP/Xbox One entry point — XAML-less "UWP Core" MonoGame app.
// All gameplay code comes from ..\FoodFight.Core (compiled in directly).

using MonoGame.Framework;
using Windows.ApplicationModel.Core;

namespace FoodFight.Uwp
{
    public static class Program
    {
        public static void Main()
        {
            var factory = new GameFrameworkViewSource<FoodFightGame>();
            CoreApplication.Run(factory);
        }
    }
}
