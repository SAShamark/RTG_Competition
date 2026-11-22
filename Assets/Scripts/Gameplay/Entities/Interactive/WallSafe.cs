using Gameplay.Dialogue;
using Gameplay.Entities.Player;
using System;
using Services;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class WallSafe : Interactable
    {
        [SerializeField] private Transform _doorHinge;
        [SerializeField] private Vector3 _axis = Vector3.up;
        [SerializeField] private float _openAngle = 110f;
        [SerializeField] private float _duration = 0.8f;
        [SerializeField] private DocumentsStash _documents;

        private Quaternion _closedRotation;

        public bool IsOpen { get; private set; }
        public override bool CanInteract => !IsOpen;
        public override HandGesture Gesture => HandGesture.Grab;

        public event Action<WallSafe> OpenRequested;
        public event Action<WallSafe> Opened;

        public override string GetHint(Transform interactor) => _texts[TextId.HintOpenSafe];

        protected override void OnInteract(Transform interactor)
        {
            if (!IsOpen)
            {
                OpenRequested?.Invoke(this);
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            MotionService.Instance.Play(_duration, k => _doorHinge.localRotation = _closedRotation * Quaternion.AngleAxis(_openAngle * Easing.OutCubic(k), _axis));
            AudioService.Instance.PlayAt(SoundType.SafeDoorOpen, transform.position);

            _documents.Unlock();

            Opened?.Invoke(this);
        }

        private void Awake()
        {
            _closedRotation = _doorHinge.localRotation;
        }

    }
}
