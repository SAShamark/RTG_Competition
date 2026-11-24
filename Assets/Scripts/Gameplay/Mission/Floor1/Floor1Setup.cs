using System;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Mission.Floor1
{
    [Serializable]
    public sealed class Floor1Setup
    {
        [SerializeField] private FireSource[] _fires;
        [SerializeField] private ElevatorButton _elevatorButton;
        [SerializeField] private Door _knockDoor;
        [SerializeField] private Door[] _emptyFlatDoors;

        public FireSource[] Fires => _fires;
        public ElevatorButton ElevatorButton => _elevatorButton;
        public Door KnockDoor => _knockDoor;
        public Door[] EmptyFlatDoors => _emptyFlatDoors;

        public bool AreFiresOut
        {
            get
            {
                foreach (FireSource fire in _fires)
                {
                    if (!fire.IsOut)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
