
namespace Gameplay.Entities.Interactive.Items
{
    public sealed class InventoryEntry
    {
        public ItemDefinition Definition { get; private set; }
        public ToolItem Tool { get; private set; }
        public int Count { get; private set; }

        public ItemType Type => Definition != null ? Definition.Type : ItemType.None;
        public bool IsEmpty => Definition == null;

        public void Set(ItemDefinition definition, ToolItem tool, int count)
        {
            Definition = definition;
            Tool = tool;
            Count = count;
        }

        public void Add(ItemDefinition definition)
        {
            Definition = definition;
            Count++;
        }

        public void RemoveOne()
        {
            Count--;
            if (Count <= 0)
            {
                Clear();
            }
        }

        public void Clear() => Set(null, null, 0);

        public void CopyFrom(InventoryEntry other) => Set(other.Definition, other.Tool, other.Count);
    }
}
