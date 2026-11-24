using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class SurfaceMaterial : MonoBehaviour
    {
        [SerializeField] private SurfaceType _type;

        public SurfaceType Type => _type;
    }
}
