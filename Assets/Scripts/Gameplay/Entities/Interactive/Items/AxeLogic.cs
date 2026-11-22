using Services.Audio;
using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class AxeLogic
    {
        [SerializeField] private float _reach = 2.4f;
        [SerializeField] private float _radius = 0.08f;
        [SerializeField] private LayerMask _hitMask = ~0;

        private Transform _aim;
        private InventoryLogic _items;

        public bool IsReady => _items.HeldItem == ItemType.Axe;

        public event Action<RaycastHit> Struck;
        public event Action Missed;

        public void Init(Transform aim, InventoryLogic items)
        {
            _aim = aim;
            _items = items;
        }

        public void Strike()
        {
            if (!Physics.SphereCast(_aim.position, _radius, _aim.forward, out RaycastHit hit, _reach, _hitMask, QueryTriggerInteraction.Ignore))
            {
                Missed?.Invoke();
                return;
            }

            bool isChoppable = hit.collider.GetComponentInParent<IChoppable>() != null || hit.collider.GetComponentInChildren<IChoppable>() != null;
            AudioService.Instance.PlayAt(isChoppable ? SoundType.AxeHitWood : SoundType.AxeHitHard, hit.point);

            foreach (IChoppable target in hit.collider.GetComponentsInParent<IChoppable>())
            {
                target.Chop(hit.point, _aim.forward);
            }

            foreach (IChoppable target in hit.collider.GetComponentsInChildren<IChoppable>())
            {
                if (target is Component component && component.transform != hit.collider.transform)
                {
                    target.Chop(hit.point, _aim.forward);
                }
            }

            Struck?.Invoke(hit);
        }
    }
}
