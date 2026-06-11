// The MonoGame shell: input gathering (keyboard WASD/IJKL + twin-stick
// gamepad), fixed-step sim ticks, letterboxed 960x720 rendering (so the
// same code fills a 1080p Xbox screen), and synth audio playback.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FoodFight
{
    public class FoodFightGame : Game
    {
        private readonly GraphicsDeviceManager _gdm;
        private SpriteBatch _sb;
        private Primitives _prims;
        private Renderer _renderer;
        private Synth _synth;
        private GameSim _sim;
        private RenderTarget2D _rt;

        private KeyboardState _prevKb;
        private GamePadState _prevPad;

        public FoodFightGame()
        {
            _gdm = new GraphicsDeviceManager(this);
            _gdm.PreferredBackBufferWidth = C.SW;
            _gdm.PreferredBackBufferHeight = C.SH;
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / C.Fps);
            IsMouseVisible = true;
            Window.Title = "FOOD FIGHT";
        }

        protected override void LoadContent()
        {
            _sb = new SpriteBatch(GraphicsDevice);
            _prims = new Primitives(GraphicsDevice);
            _renderer = new Renderer(_prims, new PixelFont(_prims.Pixel));
            _synth = new Synth();
            _sim = new GameSim();
            _rt = new RenderTarget2D(GraphicsDevice, C.SW, C.SH);
            _prevKb = Keyboard.GetState();
            _prevPad = GamePad.GetState(PlayerIndex.One);
        }

        protected override void Update(GameTime gameTime)
        {
            KeyboardState kb = Keyboard.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            SimInput inp = BuildInput(kb, pad);
            float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 20f);
            _sim.Tick(dt, inp);

            foreach (string snd in _sim.PendingSounds) _synth.Play(snd);
            _sim.PendingSounds.Clear();

            if (_sim.QuitRequested) Exit();

            _prevKb = kb;
            _prevPad = pad;
            base.Update(gameTime);
        }

        private SimInput BuildInput(KeyboardState kb, GamePadState pad)
        {
            SimInput inp = new SimInput();

            // movement: WASD + dpad + left stick (any deflection = full speed,
            // like the original's digital joystick)
            float mx = 0, my = 0;
            if (kb.IsKeyDown(Keys.A)) mx -= 1;
            if (kb.IsKeyDown(Keys.D)) mx += 1;
            if (kb.IsKeyDown(Keys.W)) my -= 1;
            if (kb.IsKeyDown(Keys.S)) my += 1;
            if (pad.DPad.Left == ButtonState.Pressed) mx -= 1;
            if (pad.DPad.Right == ButtonState.Pressed) mx += 1;
            if (pad.DPad.Up == ButtonState.Pressed) my -= 1;
            if (pad.DPad.Down == ButtonState.Pressed) my += 1;
            Vector2 ls = pad.ThumbSticks.Left;
            if (Math.Abs(ls.X) > 0.25f) mx += ls.X;
            if (Math.Abs(ls.Y) > 0.25f) my -= ls.Y;     // stick Y is up-positive
            inp.MoveX = C.Clamp(mx, -1f, 1f);
            inp.MoveY = C.Clamp(my, -1f, 1f);

            // fire: IJKL + right stick (twin-stick, deflection > 0.4)
            float fx = 0, fy = 0;
            if (kb.IsKeyDown(Keys.J)) fx -= 1;
            if (kb.IsKeyDown(Keys.L)) fx += 1;
            if (kb.IsKeyDown(Keys.I)) fy -= 1;
            if (kb.IsKeyDown(Keys.K)) fy += 1;
            Vector2 rs = pad.ThumbSticks.Right;
            if (rs.Length() > 0.4f)
            {
                fx += rs.X;
                fy -= rs.Y;
            }
            inp.FireX = C.Clamp(fx, -1f, 1f);
            inp.FireY = C.Clamp(fy, -1f, 1f);

            inp.StartPressed = KeyEdge(kb, Keys.Space) || KeyEdge(kb, Keys.Enter)
                || PadEdge(pad, Buttons.Start) || PadEdge(pad, Buttons.A);
            inp.BackPressed = KeyEdge(kb, Keys.Escape) || PadEdge(pad, Buttons.Back);
            inp.PausePressed = KeyEdge(kb, Keys.P)
                || (_sim.State == GameState.Playing && !_sim.Paused
                    && PadEdge(pad, Buttons.Start));

            bool anyKey = false;
            Keys[] pressed = kb.GetPressedKeys();
            for (int i = 0; i < pressed.Length; i++)
                if (_prevKb.IsKeyUp(pressed[i])) { anyKey = true; break; }
            inp.AnyPressed = anyKey || inp.StartPressed || inp.BackPressed
                || PadEdge(pad, Buttons.B) || PadEdge(pad, Buttons.X)
                || PadEdge(pad, Buttons.Y);
            return inp;
        }

        private bool KeyEdge(KeyboardState kb, Keys k)
        {
            return kb.IsKeyDown(k) && _prevKb.IsKeyUp(k);
        }

        private bool PadEdge(GamePadState pad, Buttons b)
        {
            return pad.IsButtonDown(b) && _prevPad.IsButtonUp(b);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_rt);
            GraphicsDevice.Clear(Color.Black);
            _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                      SamplerState.LinearClamp);
            _renderer.DrawAll(_sb, _sim);
            _sb.End();

            // letterbox the fixed 960x720 playfield onto whatever display we
            // have (1:1 in the desktop window, 1.5x pillarboxed at 1080p on Xbox)
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            int vw = GraphicsDevice.Viewport.Width;
            int vh = GraphicsDevice.Viewport.Height;
            float scale = Math.Min((float)vw / C.SW, (float)vh / C.SH);
            int dw = (int)(C.SW * scale), dh = (int)(C.SH * scale);
            _sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque,
                      SamplerState.LinearClamp);
            _sb.Draw(_rt, new Rectangle((vw - dw) / 2, (vh - dh) / 2, dw, dh),
                     Color.White);
            _sb.End();
            base.Draw(gameTime);
        }
    }
}
