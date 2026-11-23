using System;
using UI.Widgets;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    public sealed class IntroDriftControl : MonoBehaviour
    {
        [SerializeField] private Transform _truck;
        [SerializeField, Range(0f, 1f)] private float _slowMotionAt = 0.15f;
        [SerializeField, Min(0)] private int _holdFrames = 3;
        [SerializeField, Min(0.001f)] private float _maxStep = 0.05f;
        [SerializeField] private DriftPath _path = new();
        [SerializeField] private DriftCameraShot _shot = new();
        [SerializeField] private DriftSmokeLogic _smoke = new();
        [SerializeField] private SlowMotionCredit _slowMotion = new();
        [SerializeField] private DriftAudio _audio = new();
        [SerializeField] private DriftLighting _lighting = new();
        [SerializeField] private DriftPostFX _postFX = new();
        [SerializeField] private ScreenFade _fade;
        [SerializeField, Min(0.01f)] private float _fadeTime = 0.5f;

        private float _distance;
        private bool _slowMotionStarted;
        private int _heldFrames;
        private float _fadeIn = -1f;

        public bool IsPlaying { get; private set; }

        public event Action Finished;

        private void Awake()
        {
            _path.Init();
            _smoke.Init();
            _slowMotion.Init();
            _postFX.Init();
            _fade.SetAlpha(0f);
            _slowMotion.PanelArrived += _audio.PanelHit;
        }

        public void Play()
        {
            IsPlaying = true;
            _distance = 0f;
            _slowMotionStarted = false;
            _heldFrames = 0;
            _fadeIn = -1f;
            _fade.SetAlpha(0f);
            _shot.Begin();
            _audio.Begin();
            _lighting.Begin();
            _postFX.Begin();
            Place();
            _smoke.Clear();
        }

        public void Skip()
        {
            if (IsPlaying)
            {
                Finish();
            }
        }

        public void StopEffects()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            _audio.Stop();
            _lighting.Stop();
            _postFX.Stop();
            _fadeIn = -1f;
            _fade.SetAlpha(0f);
        }

        private void Update()
        {
            if (_audio.IsTailing)
            {
                _audio.TickTail(Time.deltaTime);
            }

            if (_lighting.IsReleasing)
            {
                _lighting.TickRelease(Time.deltaTime);
            }

            if (_postFX.IsReleasing)
            {
                _postFX.TickRelease(Time.deltaTime);
            }

            if (_fadeIn >= 0f)
            {
                _fadeIn += Time.deltaTime;
                _fade.SetAlpha(1f - Mathf.Clamp01(_fadeIn / _fadeTime));
                if (_fadeIn >= _fadeTime)
                {
                    _fadeIn = -1f;
                }
            }

            if (!IsPlaying)
            {
                return;
            }

            // The first frames of the scene can hitch for seconds; hold the opening pose and cap the step so the truck never jumps.
            if (_heldFrames < _holdFrames)
            {
                _heldFrames++;
                Place();
                return;
            }

            _slowMotion.Tick(Mathf.Min(Time.unscaledDeltaTime, _maxStep));
            _audio.Tick(_slowMotion.Slowness);
            _distance += _path.SpeedAt(_distance) * Mathf.Min(Time.deltaTime, _maxStep * Time.timeScale);
            if (!_slowMotionStarted && _path.ProgressAt(_distance) >= _slowMotionAt)
            {
                _slowMotionStarted = true;
                _slowMotion.Begin();
                _shot.Kick();
                _audio.Screech();
            }

            if (_distance >= _path.Length)
            {
                Finish();
                return;
            }

            Place();
        }

        private void Place()
        {
            DriftPose pose = _path.Sample(_distance);
            _truck.SetPositionAndRotation(pose.Position, pose.Rotation);
            _smoke.SetSlip(pose.Slip);
            _shot.Tick(pose, _distance - _path.TurnIn, _distance);
            float remaining = (_path.Length - _distance) / _path.SpeedAt(_distance);
            _fade.SetAlpha(1f - Mathf.Clamp01(remaining / _fadeTime));
            _postFX.Tick(pose.Position);
        }

        private void Finish()
        {
            IsPlaying = false;
            _distance = _path.Length;
            DriftPose pose = _path.Sample(_distance);
            _truck.SetPositionAndRotation(pose.Position, pose.Rotation);
            _smoke.SetSlip(0f);
            _slowMotion.Stop();
            _audio.BeginTail();
            _lighting.BeginRelease();
            _postFX.BeginRelease();
            _fade.SetAlpha(1f);
            _fadeIn = 0f;
            Finished?.Invoke();
            _shot.End();
        }

        private void OnDisable()
        {
            if (IsPlaying)
            {
                _slowMotion.Stop();
            }
        }
    }
}
