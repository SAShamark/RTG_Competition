using Gameplay.Dialogue;
using Gameplay.Entities.Interactive.Items;
using System;
using Gameplay.Entities.Characters.Player;
using Services;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class Door : Interactable, IUsableWhileCarrying, IChoppable
    {
        private const float MinDuration = 0.01f;
        private const float ProbeAngle = 10f;
        private const float BackOvershoot = 1.2f;

        [SerializeField] private float _openAngle = 100f;
        [SerializeField] private float _openDuration = 0.55f;
        [SerializeField] private float _closeDuration = 0.4f;
        [SerializeField] private bool _isLocked;
        [SerializeField] private float _ajarAngle;
        [SerializeField] private string _lockedHint = "Зачинено";
        [SerializeField] private string _lockedAction = "Відчинити";
        [SerializeField] private ItemType _forceItem = ItemType.None;
        [SerializeField] private string _forceHint = "[ЛКМ] Збити замок сокирою";
        [SerializeField] private float _rattleAngle = 2.5f;
        [SerializeField] private float _rattleDuration = 0.35f;
        [SerializeField] private float _rattleShakes = 6f;
        [SerializeField] private bool _isChopOnly;
        [SerializeField] private string _chopHint = "[ЛКМ] Рубай сокирою";

        private float _closedYaw;
        private float _currentAngle;
        private float _rattle;
        private MotionHandle _swing = MotionHandle.Idle;
        private bool _isAjar;
        private BreakableDoor _planks;

        public bool IsOpen { get; private set; }
        public bool IsLocked => _isLocked;
        public override HandGesture Gesture => _isChopOnly ? HandGesture.None : _isLocked ? HandGesture.Rattle : HandGesture.Push;
        public override bool CanInteract => !_isChopOnly || !_planks.IsBreached;

        public event Action<Door> LockedAttempt;
        public event Action<Door> Opened;
        public event Action<Door> Forced;

        private void Awake()
        {
            _planks = GetComponentInChildren<BreakableDoor>(true);
            _closedYaw = transform.localEulerAngles.y;
            if (!Mathf.Approximately(_ajarAngle, 0f))
            {
                _isAjar = true;
                _currentAngle = _ajarAngle;
                ApplyRotation();
            }
        }

        public void Setup(float openAngle, bool isLocked = false)
        {
            _openAngle = Mathf.Abs(openAngle);
            _isLocked = isLocked;
        }

        public void SetLocked(bool isLocked) => _isLocked = isLocked;

        public void SetLockedPrompt(string action, string hint)
        {
            _lockedAction = action;
            _lockedHint = hint;
        }

        public void SetForceItem(ItemType item) => _forceItem = item;

        private bool HolderCanForce(Transform interactor)
        {
            return _forceItem != ItemType.None
                && interactor.TryGetComponent(out IItemHolder holder) && holder.HeldItem == _forceItem;
        }

        public override string GetHint(Transform interactor)
        {
            if (_isChopOnly)
            {
                return _chopHint;
            }

            if (_isLocked)
            {
                if (HolderCanForce(interactor))
                {
                    return _forceHint;
                }

                return string.IsNullOrEmpty(_lockedHint)
                    ? _texts.Format(TextId.HintAction, _lockedAction)
                    : _texts.Format(TextId.HintActionBlocked, _lockedAction, _lockedHint);
            }

            return IsOpen && !_isAjar ? _texts[TextId.HintClose] : _texts[TextId.HintOpen];
        }

        protected override void OnInteract(Transform interactor)
        {
            if (_isChopOnly)
            {
                return;
            }

            if (_isLocked)
            {
                Rattle();
                Play(SoundType.DoorLocked);
                LockedAttempt?.Invoke(this);
                return;
            }

            if (_isAjar)
            {
                _isAjar = false;
                IsOpen = true;
                StartSwing(Mathf.Sign(_ajarAngle) * _openAngle, _openDuration);
                Play(SoundType.DoorOpen);
                Opened?.Invoke(this);
            }
            else if (IsOpen)
            {
                Close();
            }
            else
            {
                Open(interactor.position);
            }
        }

        public void Chop(Vector3 point, Vector3 direction)
        {
            if (!_isLocked || _forceItem != ItemType.Axe)
            {
                return;
            }

            Rattle();
            Play(SoundType.DoorLocked);
            Forced?.Invoke(this);
        }

        public void Open(Vector3 awayFrom) => Open(awayFrom, _openDuration, SoundType.DoorOpen);

        public void BurstOpen(Vector3 awayFrom, float duration) => Open(awayFrom, duration, SoundType.DoorBurst);

        private void Open(Vector3 awayFrom, float duration, SoundType sound)
        {
            _isLocked = false;
            IsOpen = true;
            StartSwing(SwingSign(awayFrom) * _openAngle, duration);
            Play(sound);
            Opened?.Invoke(this);
        }

        public void Close()
        {
            IsOpen = false;
            StartSwing(0f, _closeDuration);
            Play(SoundType.DoorClose);
        }

        private void Rattle() =>
            MotionService.Instance.Play(_rattleDuration, t =>
            {
                _rattle = t < 1f ? Mathf.Sin(t * Mathf.PI * _rattleShakes) * _rattleAngle * (1f - t) : 0f;
                ApplyRotation();
            });

        private void ApplyRotation()
        {
            Vector3 euler = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(euler.x, _closedYaw + _currentAngle + _rattle, euler.z);
        }

        private void StartSwing(float target, float duration)
        {
            _swing.Stop();
            float from = _currentAngle;
            _swing = MotionService.Instance.Play(Mathf.Max(MinDuration, duration), k =>
            {
                _currentAngle = Mathf.LerpUnclamped(from, target, Easing.OutBack(k, BackOvershoot));
                ApplyRotation();
            });
        }

        private float SwingSign(Vector3 awayFrom)
        {
            Vector3 leafCenter = LeafCenter();
            Vector3 radius = leafCenter - transform.position;
            radius.y = 0f;
            Vector3 positiveMove = Quaternion.AngleAxis(ProbeAngle, transform.up) * radius - radius;
            Vector3 toPoint = awayFrom - leafCenter;
            return Vector3.Dot(positiveMove, toPoint) > 0f ? -1f : 1f;
        }

        private Vector3 LeafCenter()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return transform.position + transform.right * 0.5f;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }

        private void Play(SoundType sound) => AudioService.Instance.PlayAt(sound, transform.position);

    }
}
