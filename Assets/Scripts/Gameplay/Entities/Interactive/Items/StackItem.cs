
namespace Gameplay.Entities.Interactive.Items
{
    public sealed class StackItem : InventoryItem
    {
        protected override bool Store(IItemHolder holder) => holder.StoreStack(Definition);
    }
}
