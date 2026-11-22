using System;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Entities.Residents;
using UnityEngine;

namespace Gameplay.Mission
{
    [Serializable]
    public sealed class Floor2Setup
    {
        [SerializeField] private TriggerZone _arrivalZone;
        [SerializeField] private Door _grandmaDoor;
        [SerializeField] private FireSource _doorFire;
        [SerializeField] private Rescuable _grandma;
        [SerializeField] private WallSafe _safe;
        [SerializeField] private WallPainting[] _paintings;
        [SerializeField] private DocumentsStash _documents;
        [SerializeField] private StackItem _flywheel;
        [SerializeField] private EvacuationWindow _window;

        [Header("Navigation")]
        [SerializeField] private Transform[] _routeToFlat;
        [SerializeField] private Collider _flatZone;
        [SerializeField] private Transform[] _routeUp;

        public TriggerZone ArrivalZone => _arrivalZone;
        public Door GrandmaDoor => _grandmaDoor;
        public FireSource DoorFire => _doorFire;
        public Rescuable Grandma => _grandma;
        public WallSafe Safe => _safe;
        public WallPainting[] Paintings => _paintings;
        public DocumentsStash Documents => _documents;
        public StackItem Flywheel => _flywheel;
        public EvacuationWindow Window => _window;
        public Transform[] RouteToFlat => _routeToFlat;
        public Collider FlatZone => _flatZone;
        public Transform[] RouteUp => _routeUp;
    }
}
