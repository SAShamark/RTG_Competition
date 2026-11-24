using Gameplay.Dialogue;
using Gameplay.Entities.Environment;
using System;
using Gameplay.Entities.Characters.Player;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class EvacuationWindow : Interactable, IUsableWhileCarrying
    {
        [SerializeField] private Trampoline _trampoline;
        [SerializeField] private Transform _launchPoint;
        [SerializeField] private Transform _cameraAnchor;
        [SerializeField] private GameObject[] _closedParts;
        [SerializeField] private bool _isReady = true;
        [SerializeField] private string _notReadyHint = "батута внизу ще немає";

        private CarryLogic _carry;
        private bool _isOpen;

        public Trampoline Trampoline => _trampoline;
        public Transform LaunchPoint => _launchPoint;
        public Transform CameraAnchor => _cameraAnchor;
        public bool IsReady => _isReady;

        public override bool CanInteract => _carry.IsCarrying;
        public override HandGesture Gesture => HandGesture.None;

        public event Action<EvacuationWindow> ThrowRequested;

        public void Init(CarryLogic carry)
        {
            _carry = carry;
        }

        public void SetReady(bool isReady) => _isReady = isReady;

        public override string GetHint(Transform interactor)
        {
            if (!_isReady)
            {
                return _texts.Format(TextId.HintWindowNotReady, _notReadyHint);
            }

            string name = _carry.Carried != null ? _carry.Carried.DisplayName : _texts[TextId.ResidentFallbackName];
            return _texts.Format(TextId.HintThrowResident, name);
        }

        protected override void OnInteract(Transform interactor)
        {
            if (_isReady)
            {
                ThrowRequested?.Invoke(this);
            }
        }

        public void Open()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;

            foreach (GameObject part in _closedParts)
            {
                part.SetActive(false);
            }
        }
    }
}
