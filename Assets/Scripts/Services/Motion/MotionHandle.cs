using System;

namespace Services.Motion
{
    public sealed class MotionHandle
    {
        private readonly float _duration;
        private readonly Action<float> _step;
        private readonly Action _completed;
        private float _elapsed;

        public MotionHandle(float duration, Action<float> step, Action completed)
        {
            _duration = duration;
            _step = step;
            _completed = completed;
            IsPlaying = duration > 0f;
        }

        public static MotionHandle Idle { get; } = new(0f, null, null);

        public bool IsPlaying { get; private set; }
        public float Progress => IsPlaying ? _elapsed / _duration : 1f;

        public void Stop() => IsPlaying = false;

        internal void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            bool isDone = _elapsed >= _duration;
            _step(isDone ? 1f : _elapsed / _duration);
            if (!isDone)
            {
                return;
            }

            IsPlaying = false;
            _completed?.Invoke();
        }
    }
}
