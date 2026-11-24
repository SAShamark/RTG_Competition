using Gameplay.Entities.Interactive;
using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class ExtinguisherLogic
    {
        [SerializeField] private float _range = 4.5f;
        [SerializeField] private float _coneAngle = 28f;
        [SerializeField] private float _power = 1.4f;
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [SerializeField] private float _aimRangeFactor = 0.7f;
        [SerializeField] private float _minAimDistance = 1.2f;
        [SerializeField] private float _farPowerFactor = 0.4f;
        [SerializeField] private float _hitSlack = 0.9f;

        [Header("Pressure")]
        [SerializeField] private float _capacity = 6f;
        [SerializeField] private float _rechargeDelay = 0.8f;
        [SerializeField] private float _rechargeRate = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _emptyPowerFactor = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _resumePressure = 0.2f;

        private const int MaxRayHits = 16;

        private readonly RaycastHit[] _rayHits = new RaycastHit[MaxRayHits];
        private Transform _aim;
        private Transform _owner;
        private InventoryLogic _items;
        private ExtinguisherItem _current;
        private float _sinceReleased;
        private bool _isEmpty;

        public bool IsSpraying { get; private set; }
        public float Pressure { get; private set; } = 1f;
        public bool IsEmpty => _isEmpty;

        public event Action<FireSource> FireHit;

        public void Init(Transform aim, InventoryLogic items)
        {
            _aim = aim;
            _owner = aim.GetComponentInParent<CharacterController>().transform;
            _items = items;
            _items.ItemChanged += HandleItemChanged;
        }

        public void Dispose()
        {
            _items.ItemChanged -= HandleItemChanged;
        }

        public void Tick(bool wantsToSpray, float deltaTime)
        {
            bool hasImpact = false;
            RaycastHit impact = default;
            if (_current != null)
            {
                hasImpact = CastIgnoringOwner(_aim.position, _aim.forward, _range, out impact);
                float aimDistance = hasImpact ? Mathf.Max(impact.distance, _minAimDistance) : _range * _aimRangeFactor;
                _current.AimAt(_aim.position + _aim.forward * aimDistance, deltaTime);
            }

            bool spray = wantsToSpray && _current != null && !_isEmpty;
            if (spray != IsSpraying)
            {
                IsSpraying = spray;
                _current?.SetSpraying(spray);
            }

            if (!IsSpraying)
            {
                _sinceReleased += deltaTime;
                if (_sinceReleased > _rechargeDelay)
                {
                    Pressure = Mathf.Min(1f, Pressure + _rechargeRate * deltaTime);
                }

                _isEmpty &= Pressure < _resumePressure;

                return;
            }

            _sinceReleased = 0f;
            Pressure = Mathf.Max(0f, Pressure - deltaTime / _capacity);
            _isEmpty = Pressure <= 0f;
            _current.SetImpact(hasImpact, impact.point, impact.normal, impact.collider, _obstacleMask);
            _current.SetPressure(Pressure);

            HitFires(deltaTime);
        }

        private void HitFires(float deltaTime)
        {
            Vector3 origin = _aim.position;
            Vector3 forward = _aim.forward;
            var fires = FireSource.Active;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                FireSource fire = fires[i];
                if (fire.IsOut)
                {
                    continue;
                }

                Vector3 target = fire.transform.position + Vector3.up * 0.5f;
                Vector3 toFire = target - origin;
                float distance = toFire.magnitude;
                if (distance > _range)
                {
                    continue;
                }

                float angle = Vector3.Angle(forward, toFire);
                if (angle > _coneAngle)
                {
                    continue;
                }

                if (!CanReach(origin, fire))
                {
                    continue;
                }

                float distanceFactor = Mathf.Lerp(1f, _farPowerFactor, distance / _range);
                float angleFactor = Mathf.Lerp(1f, 0.5f, angle / _coneAngle);
                float pressureFactor = Mathf.Lerp(_emptyPowerFactor, 1f, Pressure);
                fire.Extinguish(_power * distanceFactor * angleFactor * pressureFactor, deltaTime);
                FireHit?.Invoke(fire);
            }
        }

        private bool CanReach(Vector3 origin, FireSource fire)
        {
            for (int i = 0; i < ProbeHeights.Length; i++)
            {
                Vector3 point = fire.transform.position + Vector3.up * ProbeHeights[i];
                Vector3 toPoint = point - origin;
                float distance = toPoint.magnitude;
                if (!CastIgnoringOwner(origin, toPoint / distance, distance, out RaycastHit hit)
                    || hit.collider.GetComponentInParent<FireSource>() != null
                    || hit.distance > distance - _hitSlack)
                {
                    return true;
                }
            }

            return false;
        }

        // The aim ray starts inside the player's own capsule, which reports a hit when looking down.
        private bool CastIgnoringOwner(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _rayHits, distance, _obstacleMask, QueryTriggerInteraction.Ignore);
            nearest = default;
            nearest.distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_rayHits[i].distance < nearest.distance && !_rayHits[i].transform.IsChildOf(_owner))
                {
                    nearest = _rayHits[i];
                }
            }

            return nearest.distance < float.MaxValue;
        }

        private static readonly float[] ProbeHeights = { 0.9f, 0.5f, 1.4f, 0.15f };

        private void HandleItemChanged(ItemType type)
        {
            _current?.SetSpraying(false);
            IsSpraying = false;
            ToolItem held = _items.HeldTool;
            _current = held != null ? held.GetComponent<ExtinguisherItem>() : null;
        }
    }
}
