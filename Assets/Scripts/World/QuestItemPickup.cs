using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class QuestItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _displayName = "ручку вентиля";

        private bool _taken;

        public bool IsTaken => _taken;
        public bool CanInteract => !_taken;
        public HandGesture Gesture => HandGesture.Grab;

        public event Action<QuestItemPickup> OnTaken;

        public string GetHint(Transform interactor) => $"[E] Взяти {_displayName}";

        public void Interact(Transform interactor)
        {
            if (_taken)
            {
                return;
            }

            _taken = true;
            gameObject.SetActive(false);
            OnTaken?.Invoke(this);
        }
    }
}
