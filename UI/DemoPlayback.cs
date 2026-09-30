using System;

namespace Rookie100.UI
{
    /// <summary>Real-time playback state, separate from colony simulation and UI lifetimes.</summary>
    public sealed class DemoPlayback
    {
        public const float Duration = 10f;
        public float Seconds;
        public bool Playing = true;
        public DemoPlayback Copy() => new DemoPlayback { Seconds = Seconds, Playing = Playing };
        public void Advance(float delta)
        {
            if (!Playing || delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            Seconds = Math.Min(Duration, Seconds + delta);
            if (Seconds >= Duration) Playing = false;
        }
        public void Toggle()
        {
            if (Seconds >= Duration) Seconds = 0f;
            Playing = !Playing;
        }
        public void Replay() { Seconds = 0f; Playing = true; }
        public void Pause() { Playing = false; }
    }
}
