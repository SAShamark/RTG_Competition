using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class InventoryLogic
    {
        public const int SlotCount = 3;

        [SerializeField] private Transform _handSocket;
        [SerializeField] private Transform _bodySocket;
        [SerializeField] private float _dropSpeed = 2.5f;

        private readonly InventoryEntry _hand = new();
        private readonly InventoryEntry[] _slots = { new(), new(), new() };
        private bool _hidden;
        private bool _useBodySocket;

        public ItemType HeldItem => _hand.Type;
        public ToolItem HeldTool => _hand.Tool;
        public InventoryEntry Hand => _hand;
        public InventoryEntry Slot(int index) => _slots[index];

        private bool IsHoldingHandOnly => _hand.Tool != null && _hand.Tool.IsHandOnly;

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

        public void SetBodyHold(bool useBodySocket)
        {
            _useBodySocket = useBodySocket;
            if (_hand.Tool != null)
            {
                Attach(_hand.Tool);
            }
        }

        public bool Take(ToolItem tool)
        {
            if (tool.IsHandOnly && !StowHand())
            {
                return false;
            }

            InventoryEntry target = FreeEntry();
            if (target == null)
            {
                InventoryFull?.Invoke();
                return false;
            }

            Attach(tool);
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

            if (IsHoldingHandOnly)
            {
                Release(Vector3.zero);
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

            if (IsHoldingHandOnly)
            {
                Release(Vector3.zero);
            }

            var held = new InventoryEntry();
            held.CopyFrom(_hand);
            _hand.CopyFrom(slot);
            slot.CopyFrom(held);
            Changed(true);
        }

        public void Drop(Transform head) => Release(head.forward * _dropSpeed + Vector3.up);

        private void Release(Vector3 velocity)
        {
            ToolItem dropped = _hand.Tool;
            if (dropped == null)
            {
                return;
            }

            _hand.Clear();
            dropped.gameObject.SetActive(true);
            dropped.Release(dropped.transform.position, velocity);
            Changed(true);
        }

        private void Attach(ToolItem tool)
        {
            bool inBodyHand = _useBodySocket && tool.HoldInBodyHand;
            Transform item = tool.transform;
            item.SetParent(inBodyHand ? _bodySocket : _handSocket, false);
            item.localPosition = inBodyHand ? tool.BodyHoldPosition : tool.HoldPosition;
            item.localRotation = Quaternion.Euler(inBodyHand ? tool.BodyHoldEuler : tool.HoldEuler);

            var shadows = _useBodySocket ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (Renderer itemRenderer in item.GetComponentsInChildren<Renderer>(true))
            {
                itemRenderer.shadowCastingMode = shadows;
            }
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
            if (handChanged && _hand.Tool != null)
            {
                Attach(_hand.Tool);
            }

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
