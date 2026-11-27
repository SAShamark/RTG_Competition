using System;
using Gameplay.Entities.Interactive.Items;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class BeltLogic
    {
        private const int NoSlot = -1;

        [SerializeField] private Transform _root;
        [SerializeField] private Transform[] _holsters;
        [SerializeField] private SpriteRenderer[] _icons;
        [SerializeField] private Renderer[] _rings;
        [SerializeField] private float _waistBelowHead = 0.62f;
        [SerializeField] private float _forwardOffset = 0.05f;
        [SerializeField] private float _followAngle = 40f;
        [SerializeField] private float _turnSharpness = 6f;
        [SerializeField] private float _reach = 0.17f;
        [SerializeField] private Color _idleColor = new(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color _hoverColor = new(1f, 0.75f, 0.2f, 0.9f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _block;
        private float _yaw;
        private bool _isTurning;

        public void Init(InventoryLogic items)
        {
            _block = new MaterialPropertyBlock();
            Refresh(items);
        }

        public void Tick(Transform head, float deltaTime, int hoveredSlot)
        {
            float headYaw = head.eulerAngles.y;
            float offset = Mathf.DeltaAngle(_yaw, headYaw);
            _isTurning |= Mathf.Abs(offset) > _followAngle;
            if (_isTurning)
            {
                _yaw = Mathf.LerpAngle(_yaw, headYaw, 1f - Mathf.Exp(-_turnSharpness * deltaTime));
                _isTurning = Mathf.Abs(Mathf.DeltaAngle(_yaw, headYaw)) > 1f;
            }

            Quaternion rotation = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 position = head.position + Vector3.down * _waistBelowHead + rotation * (Vector3.forward * _forwardOffset);
            _root.SetPositionAndRotation(position, rotation);

            for (int i = 0; i < _rings.Length; i++)
            {
                _block.SetColor(BaseColorId, i == hoveredSlot ? _hoverColor : _idleColor);
                _rings[i].SetPropertyBlock(_block);
            }
        }

        public void SnapTo(Transform head) => _yaw = head.eulerAngles.y;

        public int FindSlot(Vector3 point)
        {
            int best = NoSlot;
            float bestDistance = _reach * _reach;
            for (int i = 0; i < _holsters.Length; i++)
            {
                float distance = (_holsters[i].position - point).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        public void Refresh(InventoryLogic items)
        {
            for (int i = 0; i < _icons.Length; i++)
            {
                InventoryEntry slot = items.Slot(i);
                _icons[i].sprite = slot.IsEmpty ? null : slot.Definition.Icon;
            }
        }
    }
}
