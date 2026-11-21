using System;
using Game.Fire;
using UnityEngine;

namespace Game.Items.Extinguisher
{
    [Serializable]
    public class ExtinguisherLogic
    {
        [SerializeField] private float _range = 4.5f;
        [SerializeField] private float _coneAngle = 28f;
        [SerializeField] private float _power = 1.4f;
        [SerializeField] private LayerMask _obstacleMask = ~0;

        private Transform _aim;
        private HeldItemLogic _items;
        private ExtinguisherItem _current;
        private bool _isSpraying;

        public bool IsSpraying => _isSpraying;

        public event Action<float> OnChargeChanged;
        public event Action OnEmpty;
        public event Action<FireSource> OnFireHit;

        public void Init(Transform aim, HeldItemLogic items)
        {
            _aim = aim;
            _items = items;
            _items.OnItemChanged += HandleItemChanged;
        }

        public void Dispose()
        {
            _items.OnItemChanged -= HandleItemChanged;
        }

        public void Tick(bool wantsToSpray, float deltaTime)
        {
            bool spray = wantsToSpray && _current != null && !_current.IsEmpty;
            if (spray != _isSpraying)
            {
                _isSpraying = spray;
                _current?.SetSpraying(spray);
                if (!spray && wantsToSpray && _current != null && _current.IsEmpty)
                {
                    OnEmpty?.Invoke();
                }
            }

            if (!_isSpraying)
            {
                return;
            }

            Vector3 aimPoint = _aim.position + _aim.forward * (_range * 0.7f);
            _current.Nozzle.rotation = Quaternion.LookRotation(aimPoint - _current.Nozzle.position);

            _current.Consume(deltaTime);
            OnChargeChanged?.Invoke(_current.Charge);
            HitFires(deltaTime);

            if (_current.IsEmpty)
            {
                _isSpraying = false;
                _current.SetSpraying(false);
                OnEmpty?.Invoke();
            }
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

                float distanceFactor = Mathf.Lerp(1f, 0.4f, distance / _range);
                float angleFactor = Mathf.Lerp(1f, 0.5f, angle / _coneAngle);
                fire.Extinguish(_power * distanceFactor * angleFactor, deltaTime);
                OnFireHit?.Invoke(fire);
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
                    || hit.distance > distance - 0.9f)
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
            _isSpraying = false;
            Pickable held = _items.HeldPickable;
            _current = held != null ? held.GetComponent<ExtinguisherItem>() : null;
            if (_current != null)
            {
                OnChargeChanged?.Invoke(_current.Charge);
            }
        }
    }
}
