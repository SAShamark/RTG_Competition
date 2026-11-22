using System;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay.Mission
{
    [Serializable]
    public sealed class EntranceSetup
    {
        [SerializeField] private Door[] _doors;
        [SerializeField, FormerlySerializedAs("_crowbar")] private ToolItem _axe;
        [SerializeField] private ChainLock _chainLock;
        [SerializeField] private Transform _insidePoint;
        [SerializeField] private Transform _doorPoint;
        [SerializeField] private Transform _lockerPoint;
        [SerializeField] private float _cutsceneDelay = 0.7f;

        public Door[] Doors => _doors;
        public ToolItem Axe => _axe;
        public ChainLock ChainLock => _chainLock;
        public Transform InsidePoint => _insidePoint;
        public Transform DoorPoint => _doorPoint;
        public Transform LockerPoint => _lockerPoint;
        public float CutsceneDelay => _cutsceneDelay;
    }
}
