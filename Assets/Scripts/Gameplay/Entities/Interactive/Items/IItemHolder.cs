
namespace Gameplay.Entities.Interactive.Items
{
    public interface IItemHolder
    {
        ItemType HeldItem { get; }
        bool TakeTool(ToolItem tool);
        bool StoreStack(ItemDefinition definition);
    }
}
