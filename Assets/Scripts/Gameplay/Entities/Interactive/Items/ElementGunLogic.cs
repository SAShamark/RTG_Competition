using System;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class ElementGunLogic
    {
        private const float MinFlatDistance = 0.01f;

        [SerializeField, Min(0f)] private float _shotInterval = 0.45f;
        [SerializeField, Min(0.1f)] private float _shotSpeed = 60f;
        [SerializeField, Min(0f)] private float _aimDistance = 30f;
        [SerializeField, Min(0f)] private float _minAimDistance = 1.5f;
        [SerializeField] private LayerMask _aimMask = ~0;
        [SerializeField] private SoundType _shotSound = SoundType.ItemThrow;
        [SerializeField] private SoundType _switchSound = SoundType.ItemSwitch;

        private Transform _aim;
        private Collider _owner;
        private InventoryLogic _items;
        private ElementGunItem _current;
        private float _cooldown;

        public bool IsHeld => _current != null;
        public bool IsReady => IsHeld && _cooldown <= 0f;

        public void Init(Transform aim, InventoryLogic items, Collider owner)
        {
            _aim = aim;
            _items = items;
            _owner = owner;
            _items.ItemChanged += HandleItemChanged;
        }

        public void Dispose() => _items.ItemChanged -= HandleItemChanged;

        public void Tick(float deltaTime) => _cooldown = Mathf.Max(0f, _cooldown - deltaTime);

        public void Shoot()
        {
            _cooldown = _shotInterval;
            Transform muzzle = _current.Muzzle;
            bool hasHit = Physics.Raycast(_aim.position, _aim.forward, out RaycastHit hit, _aimDistance, _aimMask, QueryTriggerInteraction.Ignore);
            float distance = hasHit ? Mathf.Max(hit.distance, _minAimDistance) : _aimDistance;
            Vector3 velocity = LaunchVelocity(muzzle.position, _aim.position + _aim.forward * distance);

            ElementProjectile projectile = UnityEngine.Object.Instantiate(_current.Projectile, muzzle.position, Quaternion.LookRotation(velocity));
            projectile.Launch(velocity, _owner);
            _current.PlayShot();
            AudioService.Instance.Play(_shotSound);
        }

        // Low arc of a projectile under gravity that passes through the target: tan(a) = (v^2 - sqrt(v^4 - g(g x^2 + 2 y v^2))) / (g x).
        private Vector3 LaunchVelocity(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            var flat = new Vector3(offset.x, 0f, offset.z);
            float distance = flat.magnitude;
            float gravity = -Physics.gravity.y;
            float speedSqr = _shotSpeed * _shotSpeed;
            float root = speedSqr * speedSqr - gravity * (gravity * distance * distance + 2f * offset.y * speedSqr);
            if (distance < MinFlatDistance || root < 0f)
            {
                return offset.normalized * _shotSpeed;
            }

            float rise = (speedSqr - Mathf.Sqrt(root)) / (gravity * distance);
            return (flat / distance + Vector3.up * rise).normalized * _shotSpeed;
        }

        public void SwitchElement()
        {
            _current.SelectNext();
            AudioService.Instance.Play(_switchSound);
        }

        private void HandleItemChanged(ItemType type) =>
            _current = type == ItemType.ElementGun ? _items.HeldTool.GetComponent<ElementGunItem>() : null;
    }
}
