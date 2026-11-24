using Gameplay.Dialogue;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Mission.Core;

namespace Gameplay.Mission.Floor1
{
    public sealed class TakeExtinguisherStep : MissionStep
    {
        private readonly InventoryLogic _items;

        public TakeExtinguisherStep(MissionContext context) : base(context)
        {
            _items = context.Player.Items;
        }

        public override string RadioHint => Texts[TextId.RadioOpenCabinet];

        public override void Enter()
        {
            if (_items.Owns(ItemType.Extinguisher))
            {
                Complete();
                return;
            }

            SetObjective(TextId.ObjectiveTakeExtinguisher);
            _items.InventoryChanged += HandleInventoryChanged;
        }

        public override void Dispose() => _items.InventoryChanged -= HandleInventoryChanged;

        private void HandleInventoryChanged()
        {
            if (!_items.Owns(ItemType.Extinguisher))
            {
                return;
            }

            Say(DialogIds.PartnerTakeExtinguisher);
            Complete();
        }
    }
}
