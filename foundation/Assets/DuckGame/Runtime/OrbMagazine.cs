using System;
namespace DuckGame
{
    [Serializable] public sealed class OrbMagazine
    {
        public int Loaded { get; private set; }
        public double LoadProgress { get; private set; }
        public double ShotProgress => LoadProgress;
        public bool Ready => Loaded == 1;
        // Kept for older HUD/verification callers; there is no preloaded second round.
        public bool Staged => false;
        public int Shots { get; private set; }

        public bool Tick(double delta, bool trigger, double loadInterval, double legacyShotInterval)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta)) delta = 0;
            if (double.IsNaN(loadInterval) || double.IsInfinity(loadInterval)) loadInterval = .8;
            loadInterval = Math.Max(.05, loadInterval);
            if (!Ready)
            {
                LoadProgress = Math.Min(1, LoadProgress + Math.Max(0, delta) / loadInterval);
                if (LoadProgress >= 1 - 1e-9) { LoadProgress = 1; Loaded = 1; }
            }
            if (!trigger || !Ready) return false;
            Loaded = 0; LoadProgress = 0; Shots++;
            // Discard surplus frame time: no catch-up shot or credit toward the next cycle.
            return true;
        }
        public void Reset() { Loaded = 0; LoadProgress = 0; Shots = 0; }
    }
}
