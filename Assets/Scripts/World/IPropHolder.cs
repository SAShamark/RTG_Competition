namespace Game.World
{
    public interface IPropHolder
    {
        bool GrabProp(ClutterItem item);
        void PutDownProp();
    }
}
