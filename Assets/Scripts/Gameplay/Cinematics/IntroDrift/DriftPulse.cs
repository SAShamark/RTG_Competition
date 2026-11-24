using Services;
using System;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftPulse
    {
        private const float HalfTurn = 0.5f;
        private const int MovesPerCycle = 4;

        [SerializeField] private Transform _body;

        [Header("Shape")]
        [SerializeField, Min(0f)] private float _stretch = 0.26f;
        [SerializeField, Min(0f)] private float _thin = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _squashRatio = 0.5f;
        [SerializeField, Range(0f, 0.4f)] private float _hold = 0.12f;

        [Header("Rhythm")]
        [SerializeField, Min(0.05f)] private float _period = 0.45f;
        [SerializeField, Min(0.05f)] private float _minPeriod = 0.3f;
        [SerializeField, Min(0.05f)] private float _maxPeriod = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _periodAdapt = 0.25f;
        [SerializeField, Min(0.01f)] private float _phaseLockTime = 0.25f;
        [SerializeField, Range(0f, 0.9f)] private float _maxSpeedChange = 0.35f;

        [Header("Envelope")]
        [SerializeField, Min(0.01f)] private float _attack = 0.2f;
        [SerializeField, Min(0.01f)] private float _release = 0.4f;
        [SerializeField, Min(0.01f)] private float _silenceTime = 1f;

        private Vector3 _baseScale;
        private float _currentPeriod;
        private float _phase;
        private float _phaseError;
        private float _clock;
        private float _lastKick;
        private float _energy;
        private bool _isActive;

        public void Begin()
        {
            _baseScale = _body.localScale;
            _currentPeriod = _period;
            _phase = 0f;
            _phaseError = 0f;
            _clock = 0f;
            _lastKick = -1f;
            _energy = 0f;
            _isActive = true;
        }

        public void Kick()
        {
            float measured = _clock - _lastKick;
            if (_lastKick >= 0f && measured >= _minPeriod && measured <= _maxPeriod)
            {
                _currentPeriod = Mathf.Lerp(_currentPeriod, measured, _periodAdapt);
            }

            _lastKick = _clock;
            _phaseError = Mathf.Repeat(PeakPhase - _phase + HalfTurn, 1f) - HalfTurn;
        }

        public void Tick(float deltaTime)
        {
            _clock += deltaTime;
            float advance = deltaTime / _currentPeriod;
            float lockLimit = advance * _maxSpeedChange;
            float lockStep = Mathf.Clamp(_phaseError * Easing.Damp(1f / _phaseLockTime, deltaTime), -lockLimit, lockLimit);
            _phaseError -= lockStep;
            _phase = Mathf.Repeat(_phase + advance + lockStep, 1f);

            bool isBeating = _lastKick >= 0f && _clock - _lastKick < _silenceTime;
            float blendTime = isBeating ? _attack : _release;
            _energy = Mathf.Lerp(_energy, isBeating ? 1f : 0f, Easing.Damp(1f / blendTime, deltaTime));

            float wave = Wave(_phase);
            float shape = (wave > 0f ? wave : wave * _squashRatio) * _energy;
            float width = 1f - shape * _thin;
            _body.localScale = Vector3.Scale(_baseScale, new Vector3(width, 1f + shape * _stretch, width));
        }

        public void Stop()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _body.localScale = _baseScale;
        }

        private float Move => (1f - 2f * _hold) / MovesPerCycle;

        private float PeakPhase => Move + _hold * HalfTurn;

        private float Wave(float phase)
        {
            float move = Move;
            if (phase < move)
            {
                return Mathf.Sin(Mathf.PI * HalfTurn * phase / move);
            }

            phase -= move;
            if (phase < _hold)
            {
                return 1f;
            }

            phase -= _hold;
            if (phase < 2f * move)
            {
                return Mathf.Cos(Mathf.PI * HalfTurn * phase / move);
            }

            phase -= 2f * move;
            if (phase < _hold)
            {
                return -1f;
            }

            phase -= _hold;
            return -Mathf.Cos(Mathf.PI * HalfTurn * phase / move);
        }
    }
}
