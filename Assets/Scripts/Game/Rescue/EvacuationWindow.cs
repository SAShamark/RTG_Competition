using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.Rescue
{
    public class EvacuationWindow : MonoBehaviour, IInteractable, IUsableWhileCarrying
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

        public bool CanInteract => _carry.IsCarrying;
        public HandGesture Gesture => HandGesture.None;

        public event Action<EvacuationWindow> OnThrowRequested;
        public event Action<EvacuationWindow> OnNotReady;

        public void Init(CarryLogic carry)
        {
            _carry = carry;
        }

        public void SetReady(bool isReady) => _isReady = isReady;

        public string GetHint(Transform interactor)
        {
            if (!_isReady)
            {
                return $"Вікно · {_notReadyHint}";
            }

            string name = _carry.Carried != null ? _carry.Carried.DisplayName : "мешканця";
            return $"[E] Кинути {name} на батут";
        }

        public void Interact(Transform interactor)
        {
            if (!_isReady)
            {
                OnNotReady?.Invoke(this);
                return;
            }

            OnThrowRequested?.Invoke(this);
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
