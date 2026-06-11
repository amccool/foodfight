# Getting Food Fight onto a real Xbox One

The `port/` tree is a MonoGame (C#) port of `main.py` with identical
gameplay. It has three parts:

| Project | What it is | Builds with |
|---------|-----------|-------------|
| `FoodFight.Core/` | All game logic + rendering + synth audio (shared sources, no csproj) | compiled into each head |
| `FoodFight.Desktop/` | Windows desktop head (DesktopGL) — for development and testing | `dotnet build` |
| `FoodFight.Uwp/` | **Xbox One head** (UWP, MonoGame 3.8.1) | Visual Studio 2022 + UWP workload |

Controls: keyboard WASD + IJKL on desktop; on gamepad (both heads)
**left stick / dpad = move, right stick = constant directional fire**,
Start = start/pause, Back = quit to title.

## Why UWP

Xbox consoles do not run Win32 apps. The two routes onto a console are:

1. **UWP via Xbox Dev Mode + Xbox Live Creators Program** — self-service,
   no approval gate. This is what `FoodFight.Uwp` targets.
2. **ID@Xbox + GDK** — full retail path with platform features and store
   placement; requires Microsoft approval, NDA and dev kits. MonoGame
   supports this too if it's ever wanted.

Console constraints for UWP/Creators titles: ~1 GB usable RAM, shared
GPU budget, no XDK/GDK-only APIs. Food Fight uses a fraction of that.

> MonoGame note: **3.8.1 is the last version with UWP support**
> (`MonoGame.Framework.WindowsUniversal 3.8.1.303`). Do not bump that
> package. The Desktop head can track newer 3.8.x freely — the shared
> Core sources compile against both.

## One-time setup

1. **Partner Center account** — register at
   https://partner.microsoft.com/dashboard (one-time fee, ~$19 individual).
2. **Activate Dev Mode on the console** — install the **Dev Mode
   Activation** app from the Xbox store, follow its prompts (it links to
   your Partner Center account), and the console reboots into Dev Mode.
   You can switch back to retail mode at any time.
3. **Dev machine** — Visual Studio 2022 with the **Universal Windows
   Platform development** workload, including Windows 10 SDKs 10.0.22621
   and 10.0.17763. (Neither VS install on this machine currently has the
   UWP workload — add it via the VS Installer.)

## Build and deploy to the console

1. Open `port/FoodFight.Uwp/FoodFight.Uwp.csproj` in VS 2022.
2. Configuration **Release / x64** (Xbox One is x64).
3. In `Package.appxmanifest`, associate the app with your Store identity
   (Project → Publish → Associate App with the Store) or at minimum set
   `Identity Publisher` to match your dev certificate.
4. With the console in Dev Mode and on the same network: Debug →
   Properties → **Remote Machine**, enter the console's IP (shown in
   Dev Home on the console), Authentication = Universal.
5. F5 (deploy + debug) or Build → Deploy. The game appears in Dev Home
   and runs fullscreen at 1080p (the 960x720 playfield is letterboxed
   1.5x — crisp and inside the TV-safe area).

## Publishing (Xbox Live Creators Program)

1. In Partner Center, create the app, enable the **Xbox Live Creators
   Program** for it, and add Xbox as a target device family.
2. Associate the project with the Store app identity in VS, build a
   Release x64 `.appxupload` (Project → Publish → Create App Packages —
   this runs the .NET Native compile, expect it to be slow).
3. Upload through Partner Center, pass certification, done — the game is
   installable on any retail Xbox One from the Creators Collection.

## What still needs a human + hardware

Everything in `FoodFight.Uwp` is scaffolded and the shared Core sources
are proven by the Desktop head (CI builds + headless selftest + launch
smoke test). The steps that cannot be verified without a console and a
Partner Center account: the .NET Native Release build, remote deploy,
and on-console input/perf checks.
