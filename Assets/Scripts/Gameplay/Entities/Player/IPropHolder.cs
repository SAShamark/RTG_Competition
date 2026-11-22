using Gameplay.Entities.Interactive;
namespace Gameplay.Entities.Player
{
    public interface IPropHolder
    {
        bool GrabProp(ClutterItem item);
        void PutDownProp();
    }
}
