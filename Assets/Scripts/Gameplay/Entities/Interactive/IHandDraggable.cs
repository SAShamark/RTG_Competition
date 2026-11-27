using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public interface IHandDraggable : IInteractable
    {
        bool CanDrag { get; }
        void BeginDrag(Transform interactor, Pose hand, bool isLeft);
        void Drag(Pose hand);
        bool EndDrag();
    }
}
