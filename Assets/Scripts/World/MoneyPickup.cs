using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class MoneyPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private int _amount = 50;
        [SerializeField] private string _description = "гроші";

        private bool _taken;

        public int Amount => _amount;
        public bool CanInteract => !_taken;
        public HandGesture Gesture => HandGesture.Grab;

        public event Action<MoneyPickup> OnTaken;

        public string GetHint(Transform interactor) => $"[E] Взяти {_description}";

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
