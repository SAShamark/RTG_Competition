using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class WallSafe : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform _doorHinge;
        [SerializeField] private Vector3 _axis = Vector3.up;
        [SerializeField] private float _openAngle = 110f;
        [SerializeField] private float _duration = 0.8f;
        [SerializeField] private DocumentsStash _documents;
        [SerializeField] private AudioSource _audio;

        private Quaternion _closedRotation;
        private float _progress = -1f;
        private bool _isOpen;

        public bool IsOpen => _isOpen;
        public bool CanInteract => !_isOpen;
        public HandGesture Gesture => HandGesture.Grab;

        public event Action<WallSafe> OnOpenRequested;
        public event Action<WallSafe> OnOpened;

        public string GetHint(Transform interactor) => "[E] Відкрити сейф";

        public void Interact(Transform interactor)
        {
            if (!_isOpen)
            {
                OnOpenRequested?.Invoke(this);
            }
        }

        public void Open()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;
            _progress = 0f;
            if (_audio != null)
            {
                _audio.Play();
            }

            _documents.Unlock();

            OnOpened?.Invoke(this);
        }

        private void Awake()
        {
            _closedRotation = _doorHinge.localRotation;
        }

        private void Update()
        {
            if (_progress < 0f || _progress >= 1f)
            {
                return;
            }

            _progress = Mathf.Min(1f, _progress + Time.deltaTime / _duration);
            float k = 1f - Mathf.Pow(1f - _progress, 3f);
            _doorHinge.localRotation = _closedRotation * Quaternion.AngleAxis(_openAngle * k, _axis);
        }
    }
}
