using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public interface IChoppable
    {
        void Chop(Vector3 point, Vector3 direction);
    }
}
