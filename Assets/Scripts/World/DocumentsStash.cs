using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class DocumentsStash : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _blockedHint = "дверцята підпер комод";
        [SerializeField] private GameObject _folderVisual;

        private bool _isUnlocked;
        private bool _isTaken;

        public bool IsTaken => _isTaken;
        public bool CanInteract => !_isTaken;
        public HandGesture Gesture => _isUnlocked ? HandGesture.Grab : HandGesture.Rattle;

        public event Action OnTaken;
        public event Action OnBlockedAttempt;

        public void Unlock() => _isUnlocked = true;

        public string GetHint(Transform interactor) =>
            _isUnlocked ? "[E] Забрати документи" : $"[E] Відчинити шафу  <color=#ff9a7a>({_blockedHint})</color>";

        public void Interact(Transform interactor)
        {
            if (_isTaken)
            {
                return;
            }

            if (!_isUnlocked)
            {
                OnBlockedAttempt?.Invoke();
                return;
            }

            _isTaken = true;
            if (_folderVisual != null)
            {
                _folderVisual.SetActive(false);
            }

            OnTaken?.Invoke();
        }
    }
}
