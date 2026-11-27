using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Player;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        [SerializeField] protected TextLibrary _texts;

        public virtual bool CanInteract => true;
        public abstract string GetHint(Transform interactor);
        public void Interact(Transform interactor)
        {
            if (CanInteract)
            {
                OnInteract(interactor);
            }
        }

        protected abstract void OnInteract(Transform interactor);
    }
}
