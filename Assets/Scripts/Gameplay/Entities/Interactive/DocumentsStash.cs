using Gameplay.Dialogue;
using System;
using Gameplay.Entities.Characters.Player;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class DocumentsStash : Interactable
    {
        [SerializeField] private string _blockedHint = "дверцята підпер комод";
        [SerializeField] private GameObject _folderVisual;

        private bool _isUnlocked;

        public bool IsTaken { get; private set; }
        public override bool CanInteract => !IsTaken;
        public override HandGesture Gesture => _isUnlocked ? HandGesture.Grab : HandGesture.Rattle;

        public event Action Taken;
        public event Action BlockedAttempt;

        public void Unlock() => _isUnlocked = true;

        public override string GetHint(Transform interactor) =>
            _isUnlocked ? _texts[TextId.HintTakeDocuments] : _texts.Format(TextId.HintOpenCabinetBlocked, _blockedHint);

        protected override void OnInteract(Transform interactor)
        {
            if (IsTaken)
            {
                return;
            }

            if (!_isUnlocked)
            {
                BlockedAttempt?.Invoke();
                return;
            }

            IsTaken = true;
            if (_folderVisual != null)
            {
                _folderVisual.SetActive(false);
            }

            Taken?.Invoke();
        }
    }
}
