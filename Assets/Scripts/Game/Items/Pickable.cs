using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.Items
{
    public class Pickable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemType _itemType;
        [SerializeField] private string _displayName = "лом";
        [SerializeField] private Vector3 _holdPosition;
        [SerializeField] private Vector3 _holdEuler;

        private bool _isTaken;

        public ItemType ItemType => _itemType;
        public Vector3 HoldPosition => _holdPosition;
        public Vector3 HoldEuler => _holdEuler;
        public bool CanInteract => !_isTaken;
        public HandGesture Gesture => HandGesture.Grab;

        public event Action<Pickable> OnTaken;

        public string GetHint(Transform interactor) => $"[E] Взяти {_displayName}";

        public void Interact(Transform interactor)
        {
            if (_isTaken || !interactor.TryGetComponent(out IItemHolder holder))
            {
                return;
            }

            if (holder.TakeItem(this))
            {
                _isTaken = true;
                foreach (Collider itemCollider in GetComponentsInChildren<Collider>())
                {
                    itemCollider.enabled = false;
                }

                OnTaken?.Invoke(this);
            }
        }
    }
}
