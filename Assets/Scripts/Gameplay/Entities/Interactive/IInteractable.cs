using Gameplay.Entities.Characters.Player;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public interface IInteractable
    {
        bool CanInteract { get; }
        HandGesture Gesture { get; }
        string GetHint(Transform interactor);
        void Interact(Transform interactor);
    }
}
