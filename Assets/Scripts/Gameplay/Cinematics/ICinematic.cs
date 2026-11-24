using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Gameplay.Cinematics
{
    [Serializable]
    public struct SubtitleCue
    {
        [SerializeField] private float _time;
        [SerializeField] private float _duration;
        [SerializeField] private string _lineId;

        public SubtitleCue(float time, float duration, string lineId)
        {
            _time = time;
            _duration = duration;
            _lineId = lineId;
        }

        public float Time => _time;
        public float Duration => _duration;
        public string LineId => _lineId;
    }

    [Serializable]
    public struct CinematicSettings
    {
        [SerializeField] private float _barsInTime;
        [SerializeField] private float _barsOutTime;
        [SerializeField] private string _titleLine;
        [SerializeField] private string _titleSubLine;
        [SerializeField] private float _titleTime;
        [SerializeField] private float _titleDuration;

        public CinematicSettings(float barsInTime, float barsOutTime, string titleLine = null, string titleSubLine = null,
            float titleTime = 0f, float titleDuration = 0f)
        {
            _barsInTime = barsInTime;
            _barsOutTime = barsOutTime;
            _titleLine = titleLine;
            _titleSubLine = titleSubLine;
            _titleTime = titleTime;
            _titleDuration = titleDuration;
        }

        public float BarsInTime => _barsInTime;
        public float BarsOutTime => _barsOutTime;
        public string TitleLine => _titleLine;
        public string TitleSubLine => _titleSubLine;
        public float TitleTime => _titleTime;
        public float TitleDuration => _titleDuration;
    }

    public interface ICinematic
    {
        PlayableDirector Director { get; }
        CinematicSettings Settings { get; }
        SubtitleCue[] Subtitles { get; }
        bool IsPlaying { get; }
        void Skip();
        event Action Started;
        event Action Finished;
    }
}
