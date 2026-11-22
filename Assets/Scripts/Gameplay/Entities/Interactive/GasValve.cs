using Gameplay.Dialogue;
using Gameplay.Entities.Player;
using System;
using Services;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class GasValve : Interactable
    {

        [SerializeField] private GameObject _handleVisual;
        [SerializeField] private Rigidbody _handleBody;
        [SerializeField] private Collider _handleCollider;
        [SerializeField] private ParticleSystem _leakJets;
        [SerializeField] private Vector3 _turnAxis = Vector3.right;
        [SerializeField] private float _turnAngle = 90f;
        [SerializeField] private float _turnTime = 0.8f;
        [SerializeField] private float _burstClunkPitch = 0.8f;
        [SerializeField] private float _burstHissPitch = 1.25f;
        [SerializeField] private float _handleLift = 0.8f;
        [SerializeField] private float _handleSpeed = 3.5f;
        [SerializeField] private float _handleSpin = 20f;

        private ISoundLoop _hiss = SilentLoop.Instance;

        private bool _hasHandle;
        private MotionHandle _turn = MotionHandle.Idle;
        private Quaternion _handleStart;

        public bool IsClosed { get; private set; }
        public override bool CanInteract => !IsClosed && !_turn.IsPlaying && !_remoteOnly;

        private bool _remoteOnly;

        public void SetRemoteOnly(bool remoteOnly) => _remoteOnly = remoteOnly;
        public override HandGesture Gesture => _hasHandle ? HandGesture.Grab : HandGesture.Rattle;

        public event Action NoHandle;
        public event Action Closed;

        private void Awake()
        {
            _handleStart = _handleVisual.transform.localRotation;
            _handleVisual.SetActive(false);
        }

        public void GiveHandle() => _hasHandle = true;

        public void ForceClose()
        {
            if (IsClosed)
            {
                return;
            }

            _hasHandle = true;
            _handleVisual.SetActive(true);

            StartTurn(0f);
        }

        public void Burst()
        {
            IsClosed = false;
            _turn.Stop();
            if (_handleVisual.activeSelf)
            {
                Transform handle = _handleVisual.transform;
                handle.SetParent(null, true);
                _handleCollider.enabled = true;
                _handleBody.isKinematic = false;
                _handleBody.linearVelocity = (-transform.right + Vector3.up * _handleLift) * _handleSpeed + UnityEngine.Random.insideUnitSphere;
                _handleBody.angularVelocity = UnityEngine.Random.insideUnitSphere * _handleSpin;
            }

            AudioService.Instance.PlayAt(SoundType.GasClunk, transform.position, _burstClunkPitch);
            _hiss.Stop();
            _hiss = AudioService.Instance.PlayLoop(SoundType.GasHiss, transform);
            _hiss.SetPitch(_burstHissPitch);
        }

        public void StartHiss()
        {
            if (!IsClosed && !_hiss.IsPlaying)
            {
                _hiss = AudioService.Instance.PlayLoop(SoundType.GasHiss, transform);
            }
        }

        public override string GetHint(Transform interactor) =>
            _hasHandle ? _texts[TextId.HintCloseGas] : _texts[TextId.HintCloseGasNoHandle];

        protected override void OnInteract(Transform interactor)
        {
            if (!CanInteract)
            {
                return;
            }

            if (!_hasHandle)
            {
                NoHandle?.Invoke();
                return;
            }

            _handleVisual.SetActive(true);
            StartTurn(_turnTime);
        }

        private void StartTurn(float duration) => _turn = MotionService.Instance.Play(duration, TickTurn, CompleteTurn);

        private void TickTurn(float progress)
        {
            float k = Easing.Smooth(progress);
            _handleVisual.transform.localRotation = _handleStart * Quaternion.AngleAxis(_turnAngle * k, _turnAxis);
            _hiss.SetVolume(1f - k);
        }

        private void CompleteTurn()
        {
            IsClosed = true;
            _hiss.Stop();
            _leakJets.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            AudioService.Instance.PlayAt(SoundType.GasClunk, transform.position);
            Closed?.Invoke();
        }
    }
}
