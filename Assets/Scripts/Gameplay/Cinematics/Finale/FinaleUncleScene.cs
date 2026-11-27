using Gameplay.Entities.Interactive;
using System;
using Services;
using Gameplay.Entities.Characters;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay.Cinematics.Finale
{
    [Serializable]
    public sealed class FinaleUncleScene
    {
        [SerializeField] private CharacterControl _uncle;
        [SerializeField] private Transform _uncleSpot;
        [SerializeField] private FireSource _lastFlame;
        [SerializeField] private ParticleSystem _stream;
        [FormerlySerializedAs("_peeCamera")]
        [SerializeField] private Transform _viewSpot;

        [Header("Timing")]
        [SerializeField] private float _runIn = 1.7f;
        [SerializeField] private Vector3 _runInFrom = new(5.0f, 0f, -3.0f);
        [SerializeField] private float _pee = 3.4f;
        [FormerlySerializedAs("_orbit")]
        [SerializeField] private float _bow = 2.6f;

        [Header("Run-in")]
        [SerializeField] private float _uncleTurnStart = 0.6f;
        [SerializeField] private float _uncleArriveAt = 0.85f;

        [Header("Stream")]
        [SerializeField] private float _streamStart = 0.6f;
        [SerializeField] private float _streamEndLead = 0.2f;
        [SerializeField] private float _lastFlameDelay = 1.0f;
        [SerializeField] private float _peePower = 0.6f;
        [SerializeField] private float _peeEndLead = 0.3f;

        [Header("Bow to the player")]
        [FormerlySerializedAs("_orbitTurnStart")]
        [SerializeField] private float _bowTurnStart = 0.15f;
        [FormerlySerializedAs("_orbitTurnEnd")]
        [SerializeField] private float _bowTurnEnd = 0.6f;
        [FormerlySerializedAs("_orbitLookAt")]
        [SerializeField] private float _victoryAt = 0.45f;

        private bool _started;
        private bool _streamOn;
        private bool _bowStarted;
        private Quaternion _bowFromRotation;

        public float RunIn => _runIn;
        public float Pee => _pee;
        public float Duration => _runIn + _pee + _bow;
        public Transform ViewSpot => _viewSpot;
        public Transform Spot => _uncleSpot;

        public void Init()
        {
            _uncle.gameObject.SetActive(false);
            _lastFlame.gameObject.SetActive(false);
        }

        public void Prepare()
        {
            _started = false;
            _streamOn = false;
            _bowStarted = false;
        }

        public void Tick(Transform viewer, float time)
        {
            if (!_started)
            {
                Start();
            }

            if (time < _runIn)
            {
                TickRunIn(time / _runIn);
                return;
            }

            Transform uncle = _uncle.transform;
            if (uncle.position != _uncleSpot.position && time - Time.deltaTime < _runIn)
            {
                uncle.SetPositionAndRotation(_uncleSpot.position, _uncleSpot.rotation);
                _uncle.SetPose(CharacterPose.Animated);
            }

            float local = time - _runIn;
            TickStream(local);
            if (local >= _pee)
            {
                Bow(viewer, Mathf.Clamp01((local - _pee) / _bow));
            }
        }

        public void Finish()
        {
            _stream.Stop();
            _lastFlame.SetReignite(false);
            _lastFlame.ForceOut();
            if (_uncle.gameObject.activeSelf)
            {
                _uncle.SetPose(CharacterPose.Victory);
            }
        }

        public void StandIdle() => _uncle.SetPose(CharacterPose.Animated);

        private void Start()
        {
            _started = true;
            _lastFlame.gameObject.SetActive(true);
            Vector3 from = _uncleSpot.position + _runInFrom;
            _uncle.gameObject.SetActive(true);
            _uncle.SetPose(CharacterPose.Running);
            _uncle.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Vector3.ProjectOnPlane(_uncleSpot.position - from, Vector3.up)));
        }

        private void TickRunIn(float k)
        {
            k = Mathf.Clamp01(k);
            Vector3 from = _uncleSpot.position + _runInFrom;
            Transform uncle = _uncle.transform;
            uncle.position = Vector3.Lerp(from, _uncleSpot.position, Easing.OutQuad(k));
            Quaternion heading = Quaternion.LookRotation(Vector3.ProjectOnPlane(_uncleSpot.position - from, Vector3.up));
            uncle.rotation = Quaternion.Slerp(heading, _uncleSpot.rotation, Easing.Smooth(Mathf.InverseLerp(_uncleTurnStart, 1f, k)));
            if (k > _uncleArriveAt)
            {
                _uncle.SetPose(CharacterPose.Animated);
            }
        }

        private void TickStream(float local)
        {
            bool streamOn = local > _streamStart && local < _pee - _streamEndLead;
            if (streamOn != _streamOn)
            {
                _streamOn = streamOn;
                if (streamOn)
                {
                    _stream.Play();
                }
                else
                {
                    _stream.Stop();
                }
            }

            if (_lastFlame.IsOut || local <= _lastFlameDelay)
            {
                return;
            }

            _lastFlame.SetReignite(false);
            _lastFlame.Extinguish(_peePower, Time.deltaTime);
            if (local > _pee - _peeEndLead)
            {
                _lastFlame.ForceOut();
            }
        }

        private void Bow(Transform viewer, float progress)
        {
            Transform uncle = _uncle.transform;
            if (!_bowStarted)
            {
                _bowStarted = true;
                _bowFromRotation = uncle.rotation;
            }

            Vector3 toViewer = Vector3.ProjectOnPlane(viewer.position - uncle.position, Vector3.up);
            if (toViewer.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion facing = Quaternion.LookRotation(toViewer.normalized);
                uncle.rotation = Quaternion.Slerp(_bowFromRotation, facing, Easing.Smooth(Mathf.InverseLerp(_bowTurnStart, _bowTurnEnd, progress)));
            }

            if (progress > _victoryAt)
            {
                _uncle.SetPose(CharacterPose.Victory);
            }
        }
    }
}
