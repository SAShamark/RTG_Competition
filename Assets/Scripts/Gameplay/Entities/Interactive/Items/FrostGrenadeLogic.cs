using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class FrostGrenadeLogic
    {
        [SerializeField, Min(0f)] private float _throwSpeed = 13f;
        [SerializeField, Min(0f)] private float _throwLift = 2.5f;

        private Transform _aim;
        private Collider _owner;
        private InventoryLogic _items;
        private FrostGrenadeItem _current;

        public bool IsHeld => _current != null;
        public bool IsArmed => IsHeld && _current.IsArmed;

        public void Init(Transform aim, InventoryLogic items, Collider owner)
        {
            _aim = aim;
            _items = items;
            _owner = owner;
            _items.ItemChanged += HandleItemChanged;
        }

        public void Dispose() => _items.ItemChanged -= HandleItemChanged;

        public void Tick()
        {
            if (IsHeld)
            {
                _current.CheckFuse();
            }
        }

        public void Arm()
        {
            _current.Arm();
            _current.Exploded += HandleExploded;
            _items.IsHandLocked = true;
        }

        public void Throw()
        {
            if (!IsHeld)
            {
                return;
            }

            FrostGrenadeItem grenade = _current;
            _items.Throw(_aim.forward * _throwSpeed + Vector3.up * _throwLift);
            grenade.Launch(_owner);
        }

        private void HandleItemChanged(ItemType type)
        {
            if (_current != null)
            {
                _current.Exploded -= HandleExploded;
            }

            _current = type == ItemType.FrostGrenade ? _items.HeldTool.GetComponent<FrostGrenadeItem>() : null;
            if (IsArmed)
            {
                _current.Exploded += HandleExploded;
            }
        }

        private void HandleExploded(FrostGrenadeItem grenade)
        {
            grenade.Exploded -= HandleExploded;
            _items.Consume();
        }
    }
}
