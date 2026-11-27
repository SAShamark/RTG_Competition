using Gameplay.Dialogue;
using System;
using Gameplay.Entities.Characters.Player;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class WallPainting : Interactable
    {
        [SerializeField] private Vector3 _axis = Vector3.forward;
        [SerializeField] private float _swingAngle = 75f;
        [SerializeField] private float _duration = 0.7f;
        [SerializeField] private bool _hidesSafe;
        [SerializeField] private float _swingDamping = 5f;
        [SerializeField] private float _swingFrequency = 9f;

        private Quaternion _restRotation;

        public bool HidesSafe => _hidesSafe;
        public bool IsMoved { get; private set; }
        public override bool CanInteract => !IsMoved;
        public event Action<WallPainting> Moved;

        public override string GetHint(Transform interactor) => _texts[TextId.HintMovePainting];

        protected override void OnInteract(Transform interactor)
        {
            if (IsMoved)
            {
                return;
            }

            IsMoved = true;
            MotionService.Instance.Play(_duration, Swing);
            AudioService.Instance.PlayAt(SoundType.PaintingMove, transform.position);

            Moved?.Invoke(this);
        }

        private void Awake()
        {
            _restRotation = transform.localRotation;
        }

        private void Swing(float t)
        {
            float angle = t >= 1f ? _swingAngle : _swingAngle * (1f - Mathf.Exp(-_swingDamping * t) * Mathf.Cos(t * _swingFrequency));
            transform.localRotation = _restRotation * Quaternion.AngleAxis(angle, _axis);
        }
    }
}
