using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>Sounds generated in code, so the plugin needs no audio assets.</summary>
    internal static class ProceduralAudio
    {
        private const int Rate = 44100;
        private static AudioClip _beep;

        /// <summary>Short detector chirp.</summary>
        public static AudioClip Beep()
        {
            if (_beep != null)
            {
                return _beep;
            }

            var n = (int)(Rate * 0.06f);
            var data = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = (float)i / Rate;
                var env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (n - i) / 600f);
                data[i] = env * 0.8f * Mathf.Sin(2 * Mathf.PI * 2300f * t);
            }

            _beep = AudioClip.Create("sptstalker_beep", n, 1, Rate, false);
            _beep.SetData(data, 0);
            return _beep;
        }

        /// <summary>
        /// One-second seamless loop: a low hum at `pitch` with harmonics, plus crackle for electric/fiery types.
        /// The pitch is rounded to whole Hz so the loop has a whole number of cycles (no click at the seam).
        /// </summary>
        public static AudioClip Hum(string key, float pitch, float crackle, int seed)
        {
            var hz = Mathf.Max(20, Mathf.Round(pitch));
            var data = new float[Rate];
            var rng = new System.Random(seed);
            var spark = 0f;
            for (var i = 0; i < Rate; i++)
            {
                var t = (float)i / Rate;
                var s = 0.55f * Mathf.Sin(2 * Mathf.PI * hz * t)
                        + 0.25f * Mathf.Sin(2 * Mathf.PI * hz * 2 * t)
                        + 0.12f * Mathf.Sin(2 * Mathf.PI * hz * 3 * t);
                // slow wobble (whole number of cycles per second keeps the loop seamless)
                s *= 0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * 3 * t);
                if (crackle > 0)
                {
                    if (rng.NextDouble() < crackle * 0.0004)
                    {
                        spark = 1f;
                    }

                    spark *= 0.993f;
                    s += spark * (float)(rng.NextDouble() * 2 - 1) * 0.9f;
                }

                data[i] = 0.5f * s;
            }

            var clip = AudioClip.Create("sptstalker_hum_" + key, Rate, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
