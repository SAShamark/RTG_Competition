using System;
using System.Collections.Generic;
using UnityEngine;

namespace Services.Motion
{
    [DefaultExecutionOrder(-1000)]
    public sealed class MotionService : MonoSingleton<MotionService>
    {
        private readonly List<MotionHandle> _motions = new();

        private void Awake()
        {
            InitializeSingleton(false);
        }

        public MotionHandle Play(float duration, Action<float> step, Action completed = null)
        {
            var motion = new MotionHandle(duration, step, completed);
            if (motion.IsPlaying)
            {
                _motions.Add(motion);
            }
            else
            {
                step(1f);
                completed?.Invoke();
            }

            return motion;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            for (int i = _motions.Count - 1; i >= 0; i--)
            {
                MotionHandle motion = _motions[i];
                if (motion.IsPlaying)
                {
                    motion.Tick(deltaTime);
                }

                if (!motion.IsPlaying)
                {
                    _motions.RemoveAt(i);
                }
            }
        }
    }
}
