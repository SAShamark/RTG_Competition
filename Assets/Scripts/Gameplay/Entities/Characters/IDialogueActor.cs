using UnityEngine;

namespace Gameplay.Entities.Characters
{
    public interface IDialogueActor
    {
        Transform Head { get; }

        void SetTalking(bool isTalking);
    }
}
