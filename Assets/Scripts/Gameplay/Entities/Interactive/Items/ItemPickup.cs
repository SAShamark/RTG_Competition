using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Player;
using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public abstract class ItemPickup : Interactable
    {

        public bool IsTaken { get; private set; }
        public override bool CanInteract => !IsTaken;
        public override HandGesture Gesture => HandGesture.Grab;

        protected abstract string DisplayName { get; }

        public event Action<ItemPickup> Taken;

        public override string GetHint(Transform interactor) => _texts.Format(TextId.HintTake, DisplayName);

        protected override void OnInteract(Transform interactor)
        {
            if (IsTaken || !TryCollect(interactor))
            {
                return;
            }

            IsTaken = true;
            OnCollected();
            Taken?.Invoke(this);
        }

        protected abstract bool TryCollect(Transform interactor);

        protected virtual void OnCollected() => gameObject.SetActive(false);

        protected void ReturnToWorld() => IsTaken = false;
    }
}
