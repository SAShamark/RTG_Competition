using System;
using Services;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Gameplay.Cinematics
{
    public abstract class CinematicBase : MonoBehaviour, ICinematic
    {
        private const float EndTolerance = 0.02f;

        [SerializeField] protected PlayableDirector _director;

        private TimelineAsset _clock;

        public PlayableDirector Director => _director;
        public abstract CinematicSettings Settings { get; }
        public abstract SubtitleCue[] Subtitles { get; }
        public bool IsPlaying { get; private set; }

        protected float ElapsedTime => (float)_director.time;
        protected virtual float Duration => (float)_director.duration;

        public event Action Started;
        public event Action Finished;

        public virtual void Skip()
        {
            if (!IsPlaying)
            {
                return;
            }

            _director.time = Duration;
            _director.Evaluate();
            _director.Stop();
        }

        protected bool TryBegin()
        {
            if (IsPlaying)
            {
                return false;
            }

            IsPlaying = true;
            return true;
        }

        protected void PlayTimeline()
        {
            _director.stopped += HandleStopped;
            _director.time = 0;
            _director.Play();
            Started?.Invoke();
        }

        protected void PlayClock()
        {
            if (_clock == null)
            {
                _clock = ScriptableObject.CreateInstance<TimelineAsset>();
                _clock.name = name + "Clock";
                _clock.hideFlags = HideFlags.DontSave;
            }

            _clock.durationMode = TimelineAsset.DurationMode.FixedLength;
            _clock.fixedDuration = Duration;
            _director.playableAsset = _clock;
            _director.extrapolationMode = DirectorWrapMode.None;
            _director.RebuildGraph();
            PlayTimeline();
        }

        protected void StopAtEnd()
        {
            if (ElapsedTime >= Duration - EndTolerance)
            {
                _director.Stop();
            }
        }

        protected abstract void OnStopped();

        protected static Vector3 Arc(Vector3 from, Vector3 to, float apex, float u) =>
            Vector3.Lerp(from, to, u) + Vector3.up * (apex * Easing.Parabola(u));

        protected virtual void OnDisable()
        {
            if (IsPlaying)
            {
                _director.stopped -= HandleStopped;
                IsPlaying = false;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_clock != null)
            {
                Destroy(_clock);
            }
        }

        private void HandleStopped(PlayableDirector director)
        {
            _director.stopped -= HandleStopped;
            IsPlaying = false;
            OnStopped();
            Finished?.Invoke();
        }
    }
}
