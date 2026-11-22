using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class MoneyPickup : ItemPickup
    {
        [SerializeField] private int _amount = 50;
        [SerializeField] private string _description = "гроші";

        public int Amount => _amount;

        protected override string DisplayName => _description;

        protected override bool TryCollect(Transform interactor) => true;
    }
}
