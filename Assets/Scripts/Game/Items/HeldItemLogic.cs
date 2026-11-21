using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Items
{
    [Serializable]
    public class HeldItemLogic
    {
        [SerializeField] private Transform _handSocket;

        private readonly List<Pickable> _owned = new();
        private Pickable _held;

        public ItemType HeldItem => _held != null ? _held.ItemType : ItemType.None;
        public Pickable HeldPickable => _held;

        public event Action<ItemType> OnItemChanged;

        public bool Owns(ItemType type) => Find(type) != null;

        public void SetHidden(bool isHidden)
        {
            if (_held != null)
            {
                _held.gameObject.SetActive(!isHidden);
            }
        }

        public bool Take(Pickable pickable)
        {
            if (_owned.Contains(pickable))
            {
                return false;
            }

            _owned.Add(pickable);
            Transform item = pickable.transform;
            item.SetParent(_handSocket, false);
            item.localPosition = pickable.HoldPosition;
            item.localRotation = Quaternion.Euler(pickable.HoldEuler);
            foreach (Renderer itemRenderer in item.GetComponentsInChildren<Renderer>())
            {
                itemRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Equip(pickable);
            return true;
        }

        public bool Select(ItemType type)
        {
            Pickable pickable = Find(type);
            if (pickable == null || pickable == _held)
            {
                return false;
            }

            Equip(pickable);
            return true;
        }

        private void Equip(Pickable pickable)
        {
            _held = pickable;
            foreach (Pickable owned in _owned)
            {
                owned.gameObject.SetActive(owned == pickable);
            }

            OnItemChanged?.Invoke(HeldItem);
        }

        private Pickable Find(ItemType type)
        {
            foreach (Pickable owned in _owned)
            {
                if (owned.ItemType == type)
                {
                    return owned;
                }
            }

            return null;
        }
    }
}
