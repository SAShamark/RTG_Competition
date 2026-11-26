using System;
using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Player;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class HoseOutlet : Interactable
    {
        [SerializeField] private string _actionName = "під'єднати рукав";
        [SerializeField] private string _blockedReason = "потрібен рукав";
        [SerializeField] private SoundType _connectSound = SoundType.HoseConnect;

        private Func<bool> _hasHose;

        public bool IsConnected { get; private set; }
        public override bool CanInteract => !IsConnected;
        public override HandGesture Gesture => HandGesture.Push;

        public event Action Connected;

        public void Setup(Func<bool> hasHose) => _hasHose = hasHose;

        public override string GetHint(Transform interactor) => _hasHose()
            ? _texts.Format(TextId.HintAction, _actionName)
            : _texts.Format(TextId.HintActionBlocked, _actionName, _blockedReason);

        protected override void OnInteract(Transform interactor)
        {
            if (!_hasHose())
            {
                return;
            }

            IsConnected = true;
            AudioService.Instance.PlayAt(_connectSound, transform.position);
            Connected?.Invoke();
        }
    }
}
