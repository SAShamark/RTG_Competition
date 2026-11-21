using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class Hatch : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _displayName = "відсік";
        [SerializeField] private Vector3 _axis = Vector3.right;
        [SerializeField] private float _openAngle = -100f;
        [SerializeField] private float _duration = 0.5f;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _openClip;
        [SerializeField] private AudioClip _closeClip;

        private Quaternion _closedRotation;
        private float _from;
        private float _to;
        private float _angle;
        private float _progress = 1f;
        private bool _isOpen;

        public bool IsOpen => _isOpen;
        public bool CanInteract => _progress >= 1f;
        public HandGesture Gesture => HandGesture.Push;

        public event Action<Hatch> OnOpened;

        public string GetHint(Transform interactor) => _isOpen ? $"[E] Зачинити {_displayName}" : $"[E] Відчинити {_displayName}";

        public void Interact(Transform interactor)
        {
            if (!CanInteract)
            {
                return;
            }

            _isOpen = !_isOpen;
            _from = _angle;
            _to = _isOpen ? _openAngle : 0f;
            _progress = 0f;
            AudioClip clip = _isOpen ? _openClip : _closeClip;
            if (_audio != null && clip != null)
            {
                _audio.PlayOneShot(clip);
            }

            if (_isOpen)
            {
                OnOpened?.Invoke(this);
            }
        }

        private void Awake()
        {
            _closedRotation = transform.localRotation;
        }

        private void Update()
        {
            if (_progress >= 1f)
            {
                return;
            }

            _progress = Mathf.Min(1f, _progress + Time.deltaTime / _duration);
            float k = 1f - Mathf.Pow(1f - _progress, 3f);
            _angle = Mathf.LerpUnclamped(_from, _to, k);
            transform.localRotation = _closedRotation * Quaternion.AngleAxis(_angle, _axis);
        }
    }
}
