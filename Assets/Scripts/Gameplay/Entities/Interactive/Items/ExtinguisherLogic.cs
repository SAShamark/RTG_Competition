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
        [SerializeField] private float _farPowerFactor = 0.4f;
        [SerializeField] private float _hitSlack = 0.9f;

        private Transform _aim;
        private InventoryLogic _items;
        private ExtinguisherItem _current;

        public bool IsSpraying { get; private set; }

        public event Action<FireSource> FireHit;

        public void Init(Transform aim, InventoryLogic items)
        {
            _aim = aim;
            _items = items;
            _items.ItemChanged += HandleItemChanged;
        }

        public void Dispose()
        {
            _items.ItemChanged -= HandleItemChanged;
        }

        public void Tick(bool wantsToSpray, float deltaTime)
        {
            bool spray = wantsToSpray && _current != null;
            if (spray != IsSpraying)
            {
                IsSpraying = spray;
                _current?.SetSpraying(spray);
            }

            if (!IsSpraying)
            {
                return;
            }

            Vector3 aimPoint = _aim.position + _aim.forward * (_range * _aimRangeFactor);
            _current.Nozzle.rotation = Quaternion.LookRotation(aimPoint - _current.Nozzle.position);

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
                fire.Extinguish(_power * distanceFactor * angleFactor, deltaTime);
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
                if (!Physics.Raycast(origin, toPoint / distance, out RaycastHit hit, distance, _obstacleMask, QueryTriggerInteraction.Ignore)
                    || hit.collider.GetComponentInParent<FireSource>() != null
                    || hit.distance > distance - _hitSlack)
                {
                    return true;
                }
            }

            return false;
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
