using System;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using UnityEngine;

namespace Gameplay.Mission.Floor3
{
    [Serializable]
    public sealed class Floor3Setup
    {
        [SerializeField] private Rescuable _neighbor;
        [SerializeField] private EvacuationWindow _window;
        [SerializeField] private FireSource _stoveFire;
        [SerializeField] private GasValve _valve;
        [SerializeField] private ParticleSystem _gasLeak;
        [SerializeField] private StackItem _handle;
        [SerializeField] private PushableBlocker _fridge;
        [SerializeField] private Door _pantryDoor;
        [SerializeField] private Door _flatDoor;
        [SerializeField] private PipeValve[] _pipeValves;
        [SerializeField] private Transform _partner;
        [SerializeField] private Transform _partnerSpot;

        [Header("Navigation")]
        [SerializeField] private Transform[] _routeToFlat;
        [SerializeField] private Collider _flatZone;
        [SerializeField] private Transform[] _routeToPantry;
        [SerializeField] private Collider _pantryZone;
        [SerializeField] private Transform[] _routeToKitchen;
        [SerializeField] private Collider _kitchenZone;
        [SerializeField] private Transform[] _routeUp;

        public Rescuable Neighbor => _neighbor;
        public EvacuationWindow Window => _window;
        public FireSource StoveFire => _stoveFire;
        public GasValve Valve => _valve;
        public ParticleSystem GasLeak => _gasLeak;
        public StackItem Handle => _handle;
        public PushableBlocker Fridge => _fridge;
        public Door PantryDoor => _pantryDoor;
        public Door FlatDoor => _flatDoor;
        public PipeValve[] PipeValves => _pipeValves;
        public Transform Partner => _partner;
        public Transform PartnerSpot => _partnerSpot;
        public Transform[] RouteToFlat => _routeToFlat;
        public Collider FlatZone => _flatZone;
        public Transform[] RouteToPantry => _routeToPantry;
        public Collider PantryZone => _pantryZone;
        public Transform[] RouteToKitchen => _routeToKitchen;
        public Collider KitchenZone => _kitchenZone;
        public Transform[] RouteUp => _routeUp;
    }
}
