using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class InventoryLogic
    {
        public const int SlotCount = 3;

        [SerializeField] private Transform _handSocket;
        [SerializeField] private float _dropForward = 0.7f;
        [SerializeField] private float _dropDown = 0.3f;
        [SerializeField] private float _dropSpeed = 2.5f;

        private readonly InventoryEntry _hand = new();
        private readonly InventoryEntry[] _slots = { new(), new(), new() };
        private bool _hidden;

        public ItemType HeldItem => _hand.Type;
        public ToolItem HeldTool => _hand.Tool;
        public InventoryEntry Hand => _hand;
        public InventoryEntry Slot(int index) => _slots[index];

        public event Action<ItemType> ItemChanged;
        public event Action InventoryChanged;
        public event Action InventoryFull;

        public bool Owns(ItemType type)
        {
            if (_hand.Type == type)
            {
                return true;
            }

            foreach (InventoryEntry slot in _slots)
            {
                if (slot.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetHidden(bool isHidden)
        {
            _hidden = isHidden;
            ShowHandBody();
        }

        public bool Take(ToolItem tool)
        {
            InventoryEntry target = FreeEntry();
            if (target == null)
            {
                InventoryFull?.Invoke();
                return false;
            }

            Transform item = tool.transform;
            item.SetParent(_handSocket, false);
            item.localPosition = tool.HoldPosition;
            item.localRotation = Quaternion.Euler(tool.HoldEuler);
            foreach (Renderer itemRenderer in item.GetComponentsInChildren<Renderer>())
            {
                itemRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            target.Set(tool.Definition, tool, 1);
            Changed(target == _hand);
            return true;
        }

        public bool StowHand()
        {
            if (_hand.IsEmpty)
            {
                return true;
            }

            foreach (InventoryEntry slot in _slots)
            {
                if (slot.IsEmpty)
                {
                    slot.CopyFrom(_hand);
                    _hand.Clear();
                    Changed(true);
                    return true;
                }
            }

            InventoryFull?.Invoke();
            return false;
        }

        public bool AddStack(ItemDefinition definition)
        {
            InventoryEntry target = Find(definition.Type) ?? FreeEntry();
            if (target == null)
            {
                InventoryFull?.Invoke();
                return false;
            }

            bool toHand = target == _hand && target.IsEmpty;
            target.Add(definition);
            Changed(toHand);
            return true;
        }

        public bool RemoveOne(ItemType type)
        {
            InventoryEntry entry = Find(type);
            if (entry == null)
            {
                return false;
            }

            bool hand = entry == _hand;
            entry.RemoveOne();

            Changed(hand);
            return true;
        }

        public void SelectSlot(int index)
        {
            InventoryEntry slot = _slots[index];
            if (slot.IsEmpty && _hand.IsEmpty)
            {
                return;
            }

            var held = new InventoryEntry();
            held.CopyFrom(_hand);
            _hand.CopyFrom(slot);
            slot.CopyFrom(held);
            Changed(true);
        }

        public void Drop(Transform head)
        {
            ToolItem dropped = _hand.Tool;
            if (dropped == null)
            {
                return;
            }

            _hand.Clear();
            dropped.gameObject.SetActive(true);
            dropped.Release(head.position + head.forward * _dropForward - Vector3.up * _dropDown, head.forward * _dropSpeed + Vector3.up);
            Changed(true);
        }

        private InventoryEntry FreeEntry()
        {
            if (_hand.IsEmpty)
            {
                return _hand;
            }

            foreach (InventoryEntry slot in _slots)
            {
                if (slot.IsEmpty)
                {
                    return slot;
                }
            }

            return null;
        }

        private InventoryEntry Find(ItemType type)
        {
            if (_hand.Type == type)
            {
                return _hand;
            }

            foreach (InventoryEntry slot in _slots)
            {
                if (slot.Type == type)
                {
                    return slot;
                }
            }

            return null;
        }

        private void Changed(bool handChanged)
        {
            ShowHandBody();
            InventoryChanged?.Invoke();
            if (handChanged)
            {
                ItemChanged?.Invoke(_hand.Type);
            }
        }

        private void ShowHandBody()
        {
            if (_hand.Tool != null)
            {
                _hand.Tool.gameObject.SetActive(!_hidden);
            }

            foreach (InventoryEntry slot in _slots)
            {
                if (slot.Tool != null)
                {
                    slot.Tool.gameObject.SetActive(false);
                }
            }
        }
    }
}
