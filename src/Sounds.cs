using System;
using System.IO;
using System.Media;

namespace PhanTrang
{
    /// <summary>Tap sounds made in code: a short wooden "cộc" (a ruler tapping the board) and a soft
    /// rubber-stamp "tách". No sound files are shipped.</summary>
    public class Sounds
    {
        readonly SoundPlayer tap, stamp, bell, ting;

        public Sounds()
        {
            tap = Make(Knock);
            stamp = Make(Stamp);
            bell = Make(Bell, 1.6);
            ting = Make(Ting, 0.7);
        }

        public void Tap() { Play(tap); }
        public void StampSound() { Play(stamp); }
        /// <summary>"Ding-dong" when a countdown reaches zero.</summary>
        public void Bell() { Play(bell); }
        /// <summary>A bright "ting" when a name is picked.</summary>
        public void Ting() { Play(ting); }

        static void Play(SoundPlayer p)
        {
            if (p == null || !Settings.Sound) return;
            try { p.Play(); } catch { }
        }

        const int Rate = 44100;

        /// <summary>Two damped partials of a small wooden block plus a click of noise at the start.</summary>
        static double Knock(double t, Random rnd)
        {
            double env = Math.Exp(-t / 0.022);
            double body = 0.62 * Math.Sin(2 * Math.PI * 1180 * t) + 0.38 * Math.Sin(2 * Math.PI * 1830 * t + 0.6);
            double click = t < 0.004 ? (rnd.NextDouble() * 2 - 1) * (1 - t / 0.004) * 0.5 : 0;
            return (body * env + click) * 0.55;
        }

        /// <summary>A low thump with a quick upper tick.</summary>
        static double Stamp(double t, Random rnd)
        {
            double thump = Math.Sin(2 * Math.PI * (190 - 60 * t / 0.09) * t) * Math.Exp(-t / 0.05);
            double tick = Math.Sin(2 * Math.PI * 2400 * t) * Math.Exp(-t / 0.006) * 0.35;
            double noise = t < 0.012 ? (rnd.NextDouble() * 2 - 1) * (1 - t / 0.012) * 0.25 : 0;
            return (thump * 0.8 + tick + noise) * 0.6;
        }

        /// <summary>Two soft bell strikes, high then low, like a school bell chime.</summary>
        static double Bell(double t, Random rnd)
        {
            Func<double, double, double> strike = (f, s) => s < 0 ? 0 : (Math.Sin(2 * Math.PI * f * s) + 0.45 * Math.Sin(2 * Math.PI * f * 2.76 * s) * Math.Exp(-s / 0.15)) * Math.Exp(-s / 0.45);
            return (strike(784, t) + strike(622, t - 0.55)) * 0.32;
        }

        /// <summary>A small glockenspiel note with a shimmer.</summary>
        static double Ting(double t, Random rnd)
        {
            return (Math.Sin(2 * Math.PI * 1568 * t) + 0.3 * Math.Sin(2 * Math.PI * 3136 * t)) * Math.Exp(-t / 0.18) * 0.35;
        }

        static SoundPlayer Make(Func<double, Random, double> wave, double seconds = 0.16)
        {
            try
            {
                int n = (int)(Rate * seconds);
                var rnd = new Random(7);
                var ms = new MemoryStream();
                var w = new BinaryWriter(ms);
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + n * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(n * 2);
                for (int i = 0; i < n; i++)
                {
                    double t = i / (double)Rate;
                    double fadeOut = i > n - 400 ? (n - i) / 400.0 : 1;
                    double v = Math.Max(-1, Math.Min(1, wave(t, rnd) * fadeOut));
                    w.Write((short)(v * 30000));
                }
                w.Flush();
                ms.Position = 0;
                var p = new SoundPlayer(ms);
                p.Load();
                return p;
            }
            catch { return null; }
        }
    }
}
