using System;
using Game.Scenes;
using UnityEngine;
using UnityEngine.Playables;

public class IntroCutscene : MonoBehaviour, ICinematic
{
    public PlayableDirector Director;
    public FirstPersonController Player;
    public bool PlayOnStart = true;
    public GameObject[] EnableOnFinish;

    [SerializeField] private CinematicSettings _settings = new()
    {
        BarsInTime = 0f,
        BarsOutTime = 2f,
        TitleLine = "вул. Липова, 12",
        TitleSubLine = "18:47  ·  виклик №112  ·  «щось горить»",
        TitleTime = 0.4f,
        TitleDuration = 1.95f,
    };

    [SerializeField] private SubtitleCue[] _subtitles =
    {
        new() { Time = 2.45f, Duration = 2.45f, Speaker = "Напарник", SpeakerColor = new Color(1f, 0.76f, 0.29f), Text = "Ти шо, пожежу обганяєш?! Гальмуй, Махмуд!" },
    };

    bool _playing;

    PlayableDirector ICinematic.Director => Director;
    public CinematicSettings Settings => _settings;
    public SubtitleCue[] Subtitles => _subtitles;
    public bool IsPlaying => _playing;

    public event Action OnStarted;
    public event Action OnFinished;

    public void Skip()
    {
        if (!_playing) return;
        Director.time = Director.duration;
        Director.Evaluate();
        Finish();
    }

    void Start()
    {
        Player.enabled = false;
        foreach (var g in EnableOnFinish) g.SetActive(false);
        if (PlayOnStart) Play(); else Finish();
    }

    public void Play()
    {
        Director.time = 0;
        Director.Play();
        _playing = true;
        OnStarted?.Invoke();
    }

    void Update()
    {
        if (_playing && Director.time >= Director.duration - 0.02) Finish();
    }

    void Finish()
    {
        _playing = false;
        Director.Stop();
        Player.enabled = true;
        foreach (var g in EnableOnFinish) g.SetActive(true);
        OnFinished?.Invoke();
    }
}
