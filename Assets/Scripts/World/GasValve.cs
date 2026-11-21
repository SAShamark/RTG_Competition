using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class GasValve : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject _handleVisual;
        [SerializeField] private Vector3 _turnAxis = Vector3.right;
        [SerializeField] private float _turnAngle = 90f;
        [SerializeField] private float _turnTime = 0.8f;
        [SerializeField] private AudioSource _hiss;
        [SerializeField] private AudioSource _clunk;

        private bool _hasHandle;
        private bool _isClosed;
        private float _turn = -1f;
        private Quaternion _handleStart;

        public bool IsClosed => _isClosed;
        public bool CanInteract => !_isClosed && _turn < 0f && !_remoteOnly;

        private bool _remoteOnly;

        public void SetRemoteOnly(bool remoteOnly) => _remoteOnly = remoteOnly;
        public HandGesture Gesture => _hasHandle ? HandGesture.Grab : HandGesture.Rattle;

        public event Action OnNoHandle;
        public event Action OnClosed;

        private void Awake()
        {
            _handleStart = _handleVisual.transform.localRotation;
            _handleVisual.SetActive(false);
        }

        public void GiveHandle() => _hasHandle = true;

        public void ForceClose()
        {
            if (_isClosed)
            {
                return;
            }

            _hasHandle = true;
            _handleVisual.SetActive(true);

            _turn = 0.999f;
        }

        public void Burst()
        {
            _isClosed = false;
            _turn = -1f;
            if (_handleVisual.activeSelf)
            {
                Transform handle = _handleVisual.transform;
                handle.SetParent(null, true);
                var body = _handleVisual.AddComponent<Rigidbody>();
                body.mass = 0.3f;
                _handleVisual.AddComponent<BoxCollider>();
                body.linearVelocity = (-transform.right + Vector3.up * 0.8f) * 3.5f + UnityEngine.Random.insideUnitSphere;
                body.angularVelocity = UnityEngine.Random.insideUnitSphere * 20f;
            }

            _clunk.pitch = 0.8f;
            _clunk.Play();

            _hiss.volume = 1f;
            _hiss.pitch = 1.25f;
            _hiss.Play();
        }

        public void StartHiss()
        {
            if (!_isClosed && !_hiss.isPlaying)
            {
                _hiss.Play();
            }
        }

        public string GetHint(Transform interactor) =>
            _hasHandle ? "[E] Поставити маховик і перекрити газ" : "[E] Перекрити газ  <color=#ff9a7a>(немає маховика)</color>";

        public void Interact(Transform interactor)
        {
            if (!CanInteract)
            {
                return;
            }

            if (!_hasHandle)
            {
                OnNoHandle?.Invoke();
                return;
            }

            _handleVisual.SetActive(true);

            _turn = 0f;
        }

        private void Update()
        {
            if (_turn < 0f || _isClosed)
            {
                return;
            }

            _turn = Mathf.Min(1f, _turn + Time.deltaTime / _turnTime);
            float k = _turn * _turn * (3f - 2f * _turn);
            _handleVisual.transform.localRotation = _handleStart * Quaternion.AngleAxis(_turnAngle * k, _turnAxis);

            _hiss.volume = Mathf.Lerp(_hiss.volume, 0f, k);

            if (_turn >= 1f)
            {
                _isClosed = true;
                _turn = -1f;
                _hiss.Stop();

                _clunk.Play();

                OnClosed?.Invoke();
            }
        }
    }
}
