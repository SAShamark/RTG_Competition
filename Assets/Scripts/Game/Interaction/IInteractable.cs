using Game.Player;
using UnityEngine;

namespace Game.Interaction
{
    public interface IInteractable
    {
        bool CanInteract { get; }
        HandGesture Gesture { get; }
        string GetHint(Transform interactor);
        void Interact(Transform interactor);
    }
}
