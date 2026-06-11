// Square-wave synth sound bank — same tone data as main.py's Sounds class,
// rendered into SoundEffect PCM buffers at startup. No audio assets.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace FoodFight
{
    public class Synth
    {
        private const int Rate = 22050;

        private readonly Dictionary<string, SoundEffect> _bank =
            new Dictionary<string, SoundEffect>();
        private readonly bool _ok;

        public Synth()
        {
            try
            {
                _bank["throw"] = Tone(new[] { T(880, 0.03f), T(660, 0.03f) }, 0.18f);
                _bank["splat"] = Noise(0.12f, 0.25f);
                _bank["pickup"] = Tone(new[] { T(520, 0.04f), T(780, 0.05f) }, 0.18f);
                _bank["eat"] = Tone(new[] { T(523, 0.09f), T(659, 0.09f),
                    T(784, 0.09f), T(1047, 0.2f) }, 0.25f);
                _bank["death"] = Tone(new[] { T(400, 0.1f), T(300, 0.1f),
                    T(200, 0.12f), T(120, 0.25f) }, 0.25f);
                _bank["chefdie"] = Tone(new[] { T(200, 0.05f), T(300, 0.05f),
                    T(150, 0.08f) }, 0.22f);
                _bank["start"] = Tone(new[] { T(392, 0.1f), T(523, 0.1f),
                    T(659, 0.1f), T(784, 0.18f) }, 0.25f);
                _bank["melt"] = Tone(new[] { T(700, 0.12f), T(500, 0.12f),
                    T(350, 0.2f) }, 0.22f);
                _bank["bonus"] = Tone(new[] { T(990, 0.03f) }, 0.15f);
                _bank["fall"] = Tone(new[] { T(600, 0.06f), T(450, 0.06f),
                    T(330, 0.06f), T(240, 0.06f), T(170, 0.1f) }, 0.22f);
                _ok = true;
            }
            catch (Exception)
            {
                _ok = false;     // no audio device — play silently
            }
        }

        private struct Note { public float Freq, Dur; }

        private static Note T(float freq, float dur)
        {
            return new Note { Freq = freq, Dur = dur };
        }

        private static SoundEffect Tone(Note[] seq, float vol)
        {
            int total = 0;
            foreach (Note n in seq) total += (int)(Rate * n.Dur);
            var data = new byte[total * 2];
            int pos = 0;
            short amp = (short)(32767 * vol);
            foreach (Note n in seq)
            {
                int count = (int)(Rate * n.Dur);
                float period = Rate / Math.Max(n.Freq, 1f);
                for (int i = 0; i < count; i++)
                {
                    short v = (i % period) < period / 2 ? amp : (short)(-amp);
                    data[pos * 2] = (byte)(v & 0xFF);
                    data[pos * 2 + 1] = (byte)((v >> 8) & 0xFF);
                    pos++;
                }
            }
            return new SoundEffect(data, Rate, AudioChannels.Mono);
        }

        private static SoundEffect Noise(float dur, float vol)
        {
            int count = (int)(Rate * dur);
            var data = new byte[count * 2];
            var rng = new Random(7);
            float amp = 32767 * vol;
            for (int i = 0; i < count; i++)
            {
                float fade = 1f - (float)i / count;
                short v = (short)((rng.NextDouble() * 2 - 1) * amp * fade);
                data[i * 2] = (byte)(v & 0xFF);
                data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            return new SoundEffect(data, Rate, AudioChannels.Mono);
        }

        public void Play(string name)
        {
            if (!_ok) return;
            SoundEffect snd;
            if (_bank.TryGetValue(name, out snd))
            {
                try { snd.Play(); } catch (Exception) { }
            }
        }
    }
}
