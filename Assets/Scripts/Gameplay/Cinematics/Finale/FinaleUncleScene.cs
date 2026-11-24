using Gameplay.Entities.Interactive;
using System;
using Gameplay.Entities.Characters.Residents;
using Services;
using UnityEngine;

namespace Gameplay.Cinematics.Finale
{
    [Serializable]
    public sealed class FinaleUncleScene
    {
        [SerializeField] private PosedActor _uncle;
        [SerializeField] private Transform _uncleSpot;
        [SerializeField] private FireSource _lastFlame;
        [SerializeField] private ParticleSystem _stream;
        [SerializeField] private Transform _peeCamera;

        [Header("Timing")]
        [SerializeField] private float _runIn = 1.7f;
        [SerializeField] private Vector3 _runInFrom = new(5.0f, 0f, -3.0f);
        [SerializeField] private float _pee = 3.4f;
        [SerializeField] private float _orbit = 2.6f;

        [Header("Run-in")]
        [SerializeField] private float _uncleTurnStart = 0.6f;
        [SerializeField] private float _uncleArriveAt = 0.85f;
        [SerializeField] private float _uncleAimHeight = 1.1f;

        [Header("Stream")]
        [SerializeField] private float _streamStart = 0.6f;
        [SerializeField] private float _streamEndLead = 0.2f;
        [SerializeField] private float _lastFlameDelay = 1.0f;
        [SerializeField] private float _peePower = 0.6f;
        [SerializeField] private float _peeEndLead = 0.3f;

        [Header("Orbit")]
        [SerializeField] private float _orbitDistance = 2.3f;
        [SerializeField] private float _orbitHeight = 1.55f;
        [SerializeField] private float _orbitTurnStart = 0.15f;
        [SerializeField] private float _orbitTurnEnd = 0.6f;
        [SerializeField] private float _orbitLookAt = 0.45f;
        [SerializeField] private float _orbitAngle = 25f;
        [SerializeField] private float _orbitAimHeight = 1.25f;

        private bool _started;
        private bool _streamOn;
        private bool _orbitStarted;
        private Vector3 _orbitFrom;
        private Quaternion _orbitFromRotation;

        public float RunIn => _runIn;
        public float Pee => _pee;
        public float Duration => _runIn + _pee + _orbit;

        public void Init() => _uncle.gameObject.SetActive(false);

        public void Prepare()
        {
            _started = false;
            _streamOn = false;
            _orbitStarted = false;
            _lastFlame.gameObject.SetActive(true);
        }

        public void Tick(Transform cam, float time)
        {
            if (!_started)
            {
                Start(cam);
            }

            if (time < _runIn)
            {
                TickRunIn(cam, time / _runIn);
                return;
            }

            Transform uncle = _uncle.transform;
            if (uncle.position != _uncleSpot.position && time - Time.deltaTime < _runIn)
            {
                uncle.SetPositionAndRotation(_uncleSpot.position, _uncleSpot.rotation);
                _uncle.SetPose(ResidentPose.Animated);
            }

            float local = time - _runIn;
            TickStream(local);

            if (local < _pee)
            {
                AimAtUncle(cam);
                return;
            }

            Orbit(cam, Mathf.Clamp01((local - _pee) / _orbit));
        }

        public void Finish()
        {
            _stream.Stop();
            if (_uncle.gameObject.activeSelf)
            {
                _uncle.SetPose(ResidentPose.Victory);
            }
        }

        private void Start(Transform cam)
        {
            _started = true;
            Vector3 from = _uncleSpot.position + _runInFrom;
            _uncle.gameObject.SetActive(true);
            _uncle.SetPose(ResidentPose.Running);
            _uncle.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Vector3.ProjectOnPlane(_uncleSpot.position - from, Vector3.up)));
            cam.SetPositionAndRotation(_peeCamera.position, _peeCamera.rotation);
        }

        private void TickRunIn(Transform cam, float k)
        {
            k = Mathf.Clamp01(k);
            Vector3 from = _uncleSpot.position + _runInFrom;
            Transform uncle = _uncle.transform;
            uncle.position = Vector3.Lerp(from, _uncleSpot.position, Easing.OutQuad(k));
            Quaternion heading = Quaternion.LookRotation(Vector3.ProjectOnPlane(_uncleSpot.position - from, Vector3.up));
            uncle.rotation = Quaternion.Slerp(heading, _uncleSpot.rotation, Easing.Smooth(Mathf.InverseLerp(_uncleTurnStart, 1f, k)));
            if (k > _uncleArriveAt)
            {
                _uncle.SetPose(ResidentPose.Animated);
            }

            AimAtUncle(cam);
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

        private void AimAtUncle(Transform cam) =>
            cam.rotation = Quaternion.LookRotation(_uncleSpot.position + Vector3.up * _uncleAimHeight - cam.position, Vector3.up);

        private void Orbit(Transform cam, float o)
        {
            Transform uncle = _uncle.transform;
            if (!_orbitStarted)
            {
                _orbitStarted = true;
                _orbitFrom = cam.position;
                _orbitFromRotation = uncle.rotation;
            }

            float ease = Easing.Smooth(o);
            Quaternion toCamera = Quaternion.LookRotation(Vector3.ProjectOnPlane(-_uncleSpot.forward, Vector3.up).normalized);
            uncle.rotation = Quaternion.Slerp(_orbitFromRotation, toCamera, Easing.Smooth(Mathf.InverseLerp(_orbitTurnStart, _orbitTurnEnd, o)));
            if (o > _orbitLookAt)
            {
                _uncle.SetPose(ResidentPose.Victory);
            }

            Vector3 centre = uncle.position;
            Vector3 fromOffset = _orbitFrom - centre;
            Vector3 toOffset = Quaternion.AngleAxis(_orbitAngle, Vector3.up) * (-_uncleSpot.forward * _orbitDistance) + Vector3.up * _orbitHeight;
            float angle = Mathf.LerpAngle(Mathf.Atan2(fromOffset.x, fromOffset.z) * Mathf.Rad2Deg, Mathf.Atan2(toOffset.x, toOffset.z) * Mathf.Rad2Deg, ease);
            float radius = Mathf.Lerp(new Vector2(fromOffset.x, fromOffset.z).magnitude, new Vector2(toOffset.x, toOffset.z).magnitude, ease);
            float height = Mathf.Lerp(fromOffset.y, toOffset.y, ease);
            cam.position = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius + Vector3.up * height;
            cam.rotation = Quaternion.LookRotation(centre + Vector3.up * _orbitAimHeight - cam.position, Vector3.up);
        }
    }
}
