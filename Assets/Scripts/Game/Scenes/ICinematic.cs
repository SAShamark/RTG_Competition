using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Game.Scenes
{
    [Serializable]
    public struct SubtitleCue
    {
        public float Time;
        public float Duration;
        public string Speaker;
        public Color SpeakerColor;
        [TextArea] public string Text;
    }

    [Serializable]
    public struct CinematicSettings
    {
        public float BarsInTime;
        public float BarsOutTime;
        public string TitleLine;
        public string TitleSubLine;
        public float TitleTime;
        public float TitleDuration;
    }

    public interface ICinematic
    {
        PlayableDirector Director { get; }
        CinematicSettings Settings { get; }
        SubtitleCue[] Subtitles { get; }
        bool IsPlaying { get; }
        void Skip();
        event Action OnStarted;
        event Action OnFinished;
    }
}
