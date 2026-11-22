using System;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Mission
{
    [Serializable]
    public sealed class Floor1Setup
    {
        [SerializeField] private Hatch _cabinet;
        [SerializeField] private FireSource[] _fires;
        [SerializeField] private ElevatorButton _elevatorButton;
        [SerializeField] private Door _knockDoor;
        [SerializeField] private Door[] _emptyFlatDoors;

        public Hatch Cabinet => _cabinet;
        public FireSource[] Fires => _fires;
        public ElevatorButton ElevatorButton => _elevatorButton;
        public Door KnockDoor => _knockDoor;
        public Door[] EmptyFlatDoors => _emptyFlatDoors;
    }
}
