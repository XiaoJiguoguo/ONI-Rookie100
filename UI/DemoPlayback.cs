using System;

namespace Rookie100.UI
{
    /// <summary>Real-time playback state, separate from colony simulation and UI lifetimes.</summary>
    public sealed class DemoPlayback
    {
        public const float Duration = 10f;
        public float EndSeconds = Duration;
        public float Seconds;
        public bool Playing = true;
        public DemoPlayback Copy() => new DemoPlayback { Seconds = Seconds, Playing = Playing, EndSeconds = EndSeconds };
        public void Advance(float delta)
        {
            if (!Playing || delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            Seconds = Math.Min(EndSeconds, Seconds + delta);
            if (Seconds >= EndSeconds) Playing = false;
        }
        public void Toggle()
        {
            if (Seconds >= EndSeconds) Seconds = 0f;
            Playing = !Playing;
        }
        public void Replay() { Seconds = 0f; Playing = true; }
        public void Pause() { Playing = false; }
    }
}
