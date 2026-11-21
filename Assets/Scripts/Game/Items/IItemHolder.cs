namespace Game.Items
{
    public interface IItemHolder
    {
        ItemType HeldItem { get; }
        bool TakeItem(Pickable pickable);
    }
}
