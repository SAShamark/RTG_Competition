using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public abstract class InventoryItem : ItemPickup
    {
        [SerializeField] private ItemDefinition _definition;

        public ItemDefinition Definition => _definition;
        public ItemType Type => _definition.Type;

        protected override string DisplayName => _definition.DisplayName;

        protected override bool TryCollect(Transform interactor) =>
            interactor.TryGetComponent(out IItemHolder holder) && Store(holder);

        protected abstract bool Store(IItemHolder holder);
    }
}
