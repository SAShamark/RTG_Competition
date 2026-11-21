using System;
using Game.Interaction;
using Game.Items;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class Door : MonoBehaviour, IInteractable, IUsableWhileCarrying
    {
        [SerializeField] private float _openAngle = 100f;
        [SerializeField] private float _openDuration = 0.55f;
        [SerializeField] private float _closeDuration = 0.4f;
        [SerializeField] private bool _isLocked;
        [SerializeField] private float _ajarAngle;
        [SerializeField] private string _lockedHint = "Зачинено";
        [SerializeField] private string _lockedAction = "Відчинити";
        [SerializeField] private ItemType _forceItem = ItemType.None;
        [SerializeField] private string _forceHint = "[E] Зламати замок";
        [SerializeField] private float _rattleAngle = 2.5f;
        [SerializeField] private float _rattleDuration = 0.35f;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _openClip;
        [SerializeField] private AudioClip _closeClip;
        [SerializeField] private AudioClip _lockedClip;

        private float _closedYaw;
        private float _fromAngle;
        private float _toAngle;
        private float _currentAngle;
        private float _progress = 1f;
        private float _duration = 1f;
        private float _rattleTime = -1f;
        private bool _isOpen;
        private bool _isAjar;

        public bool IsOpen => _isOpen;
        public bool IsLocked => _isLocked;
        public bool CanInteract => true;
        public HandGesture Gesture => _isLocked ? (_canBeForcedNow ? HandGesture.Pry : HandGesture.Rattle) : HandGesture.Push;

        private bool _canBeForcedNow;

        public event Action<Door> OnLockedAttempt;
        public event Action<Door> OnOpened;
        public event Action<Door> OnForced;

        private void Awake()
        {
            _closedYaw = transform.localEulerAngles.y;
            if (!Mathf.Approximately(_ajarAngle, 0f))
            {
                _isAjar = true;
                _currentAngle = _ajarAngle;
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

        public string GetHint(Transform interactor)
        {
            if (_isLocked)
            {
                _canBeForcedNow = HolderCanForce(interactor);
                if (_canBeForcedNow)
                {
                    return _forceHint;
                }

                return string.IsNullOrEmpty(_lockedHint)
                    ? $"[E] {_lockedAction}"
                    : $"[E] {_lockedAction}  <color=#ff9a7a>({_lockedHint})</color>";
            }

            return _isOpen && !_isAjar ? "[E] Зачинити" : "[E] Відчинити";
        }

        public void Interact(Transform interactor)
        {
            if (_isLocked && HolderCanForce(interactor))
            {
                _rattleTime = 0f;
                Play(_lockedClip);
                OnForced?.Invoke(this);
                return;
            }

            if (_isLocked)
            {
                _rattleTime = 0f;
                Play(_lockedClip);
                OnLockedAttempt?.Invoke(this);
                return;
            }

            if (_isAjar)
            {
                _isAjar = false;
                _isOpen = true;
                StartSwing(Mathf.Sign(_ajarAngle) * _openAngle, _openDuration);
                Play(_openClip);
                OnOpened?.Invoke(this);
            }
            else if (_isOpen)
            {
                Close();
            }
            else
            {
                Open(interactor.position);
            }
        }

        public void Open(Vector3 awayFrom)
        {
            _isLocked = false;
            _isOpen = true;
            StartSwing(SwingSign(awayFrom) * _openAngle, _openDuration);
            Play(_openClip);
            OnOpened?.Invoke(this);
        }

        public void Close()
        {
            _isOpen = false;
            StartSwing(0f, _closeDuration);
            Play(_closeClip);
        }

        private void Update()
        {
            if (_progress < 1f)
            {
                _progress = Mathf.Min(1f, _progress + Time.deltaTime / _duration);
                _currentAngle = Mathf.LerpUnclamped(_fromAngle, _toAngle, EaseOutBack(_progress));
            }

            float rattle = 0f;
            if (_rattleTime >= 0f)
            {
                _rattleTime += Time.deltaTime;
                float t = _rattleTime / _rattleDuration;
                rattle = t < 1f ? Mathf.Sin(t * Mathf.PI * 6f) * _rattleAngle * (1f - t) : 0f;
                if (t >= 1f)
                {
                    _rattleTime = -1f;
                }
            }

            Vector3 euler = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(euler.x, _closedYaw + _currentAngle + rattle, euler.z);
        }

        private void StartSwing(float target, float duration)
        {
            _fromAngle = _currentAngle;
            _toAngle = target;
            _duration = Mathf.Max(0.01f, duration);
            _progress = 0f;
        }

        private float SwingSign(Vector3 awayFrom)
        {
            Vector3 leafCenter = LeafCenter();
            Vector3 radius = leafCenter - transform.position;
            radius.y = 0f;
            Vector3 positiveMove = Quaternion.AngleAxis(10f, transform.up) * radius - radius;
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

        private void Play(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.2f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
