using System;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using UnityEngine;

namespace Gameplay.Mission.Entrance
{
    [Serializable]
    public sealed class EntranceSetup
    {
        [SerializeField] private Door[] _doors;
        [SerializeField] private ToolItem _axe;
        [SerializeField] private ChainLock _chainLock;
        [SerializeField] private Transform _insidePoint;
        [SerializeField] private Transform _doorPoint;
        [SerializeField] private Transform _lockerPoint;
        [SerializeField] private float _cutsceneDelay = 0.7f;
        [SerializeField] private float _burstDuration = 0.22f;

        public Door[] Doors => _doors;
        public ToolItem Axe => _axe;
        public ChainLock ChainLock => _chainLock;
        public Transform InsidePoint => _insidePoint;
        public Transform DoorPoint => _doorPoint;
        public Transform LockerPoint => _lockerPoint;
        public float CutsceneDelay => _cutsceneDelay;
        public float BurstDuration => _burstDuration;
    }
}
