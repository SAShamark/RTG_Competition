using Gameplay.Entities.Interactive;

namespace Gameplay.Entities.Characters.Player
{
    public interface IPropHolder
    {
        bool GrabProp(ClutterItem item);
        void PutDownProp();
    }
}
