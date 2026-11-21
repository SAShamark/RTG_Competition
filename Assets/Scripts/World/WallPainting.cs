using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class WallPainting : MonoBehaviour, IInteractable
    {
        [SerializeField] private Vector3 _axis = Vector3.forward;
        [SerializeField] private float _swingAngle = 75f;
        [SerializeField] private float _duration = 0.7f;
        [SerializeField] private bool _hidesSafe;
        [SerializeField] private AudioSource _audio;

        private Quaternion _restRotation;
        private float _progress = -1f;
        private bool _isMoved;

        public bool HidesSafe => _hidesSafe;
        public bool IsMoved => _isMoved;
        public bool CanInteract => !_isMoved;
        public HandGesture Gesture => HandGesture.Push;

        public event Action<WallPainting> OnMoved;

        public string GetHint(Transform interactor) => "[E] Відсунути картину";

        public void Interact(Transform interactor)
        {
            if (_isMoved)
            {
                return;
            }

            _isMoved = true;
            _progress = 0f;
            if (_audio != null)
            {
                _audio.Play();
            }

            OnMoved?.Invoke(this);
        }

        private void Awake()
        {
            _restRotation = transform.localRotation;
        }

        private void Update()
        {
            if (_progress < 0f || _progress >= 1f)
            {
                return;
            }

            _progress = Mathf.Min(1f, _progress + Time.deltaTime / _duration);
            float t = _progress;
            float angle = _swingAngle * (1f - Mathf.Exp(-5f * t) * Mathf.Cos(t * 9f));
            if (t >= 1f)
            {
                angle = _swingAngle;
            }

            transform.localRotation = _restRotation * Quaternion.AngleAxis(angle, _axis);
        }
    }
}
