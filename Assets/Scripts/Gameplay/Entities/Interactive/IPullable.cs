using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public interface IPullable : IInteractable
    {
        bool CanPull { get; }
        Vector3 PullPoint { get; }
        void BeginPull();
        void MovePull(Vector3 pullPoint);
        void EndPull(Vector3 velocity);
    }

    public static class Pullables
    {
        public static readonly List<IPullable> All = new();
    }
}
