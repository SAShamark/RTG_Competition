using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class WaterSplash : MonoBehaviour
    {
        private const int MaxFloorHits = 8;

        private static readonly RaycastHit[] FloorHits = new RaycastHit[MaxFloorHits];

        [Header("Puddle")]
        [SerializeField] private WaterTraceLayer _traces = new();
        [SerializeField, Min(0f)] private float _litres = 12f;
        [SerializeField, Min(0f)] private float _dropHeight = 3f;
        [SerializeField] private LayerMask _floorMask = ~0;
        [SerializeField, Min(1)] private int _pourPoints = 6;
        [SerializeField, Min(0f)] private float _pourRadius = 0.9f;

        [Header("Dousing")]
        [SerializeField, Min(0f)] private float _radius = 1.8f;
        [SerializeField, Min(0f)] private float _height = 1.5f;
        [SerializeField, Min(0f)] private float _power = 0.6f;
        [SerializeField, Min(0f)] private float _duration = 12f;

        private Vector3 _center;

        private void Start()
        {
            _center = transform.position;
            if (TryFindFloor(out RaycastHit floor))
            {
                _center = floor.point;
                Pour(floor);
            }

            Destroy(gameObject, _duration);
        }

        // Whatever stands on the impact point keeps water off the ground right under it, so most of the splash lands in a ring around it.
        private void Pour(RaycastHit floor)
        {
            float share = _litres / (_pourPoints + 1);
            _traces.Wet(floor.collider, floor.point, floor.normal, share);
            for (int i = 0; i < _pourPoints; i++)
            {
                float angle = i * Mathf.PI * 2f / _pourPoints;
                Vector3 point = floor.point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _pourRadius;
                _traces.Wet(floor.collider, point, floor.normal, share);
            }
        }

        // The water falls through whatever cannot hold it, such as the fire it was shot at, down to the first surface that can.
        private bool TryFindFloor(out RaycastHit floor)
        {
            int count = Physics.RaycastNonAlloc(_center, Vector3.down, FloorHits, _dropHeight, _floorMask, QueryTriggerInteraction.Ignore);
            floor = default;
            bool isFound = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = FloorHits[i];
                if (SurfaceSplat.CanStickTo(hit.collider) && (!isFound || hit.distance < floor.distance))
                {
                    floor = hit;
                    isFound = true;
                }
            }

            return isFound;
        }

        private void Update()
        {
            IReadOnlyList<FireSource> fires = FireSource.Active;
            float reach = _radius * _radius;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                Vector3 offset = fires[i].transform.position - _center;
                float height = offset.y;
                offset.y = 0f;
                if (Mathf.Abs(height) <= _height && offset.sqrMagnitude <= reach)
                {
                    fires[i].Douse(_power, Time.deltaTime);
                }
            }
        }
    }
}
