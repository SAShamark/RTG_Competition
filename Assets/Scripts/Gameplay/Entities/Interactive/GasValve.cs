using Gameplay.Dialogue;
using System;
using Gameplay.Entities.Characters.Player;
using Services;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class GasValve : Interactable, IHandDraggable
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

        [Header("Hand turn")]
        [SerializeField] private float _dragMoveAngle = 8f;
        [SerializeField] private float _closeTolerance = 3f;

        private ISoundLoop _hiss = SilentLoop.Instance;

        private bool _hasHandle;
        private MotionHandle _turn = MotionHandle.Idle;
        private Quaternion _handleStart;
        private Vector3 _twistAxis;
        private Vector3 _lastTwist;
        private bool _hasTwist;
        private float _dragAngle;
        private float _dragTravel;

        public bool IsClosed { get; private set; }
        public override bool CanInteract => !IsClosed && !_turn.IsPlaying && !_remoteOnly;
        public bool CanDrag => CanInteract && _hasHandle;

        private bool _remoteOnly;

        public void SetRemoteOnly(bool remoteOnly) => _remoteOnly = remoteOnly;
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

        public void BeginDrag(Transform interactor, Pose hand, bool isLeft)
        {
            _handleVisual.SetActive(true);
            _twistAxis = HandTwist.WorldAxis(_handleVisual.transform, _handleStart, _turnAxis);
            _lastTwist = HandTwist.Reference(hand.rotation, _twistAxis);
            _dragTravel = 0f;
            _hasTwist = false;
        }

        public void Drag(Pose hand)
        {
            if (IsClosed)
            {
                return;
            }

            Vector3 reference = HandTwist.Reference(hand.rotation, _twistAxis);
            if (!_hasTwist)
            {
                _hasTwist = true;
                _lastTwist = reference;
                return;
            }

            float delta = Vector3.SignedAngle(_lastTwist, reference, _twistAxis);
            _lastTwist = reference;
            _dragTravel += Mathf.Abs(delta);
            _dragAngle = Mathf.Clamp(_dragAngle + delta, 0f, _turnAngle);
            ApplyTurn(_dragAngle / _turnAngle);
            if (_dragAngle >= _turnAngle - _closeTolerance)
            {
                ApplyTurn(1f);
                CompleteTurn();
            }
        }

        public bool EndDrag() => _dragTravel > _dragMoveAngle;

        private void StartTurn(float duration) => _turn = MotionService.Instance.Play(duration, TickTurn, CompleteTurn);

        private void TickTurn(float progress) => ApplyTurn(Easing.Smooth(progress));

        private void ApplyTurn(float k)
        {
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
