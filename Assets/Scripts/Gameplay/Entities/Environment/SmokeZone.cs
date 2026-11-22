using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SmokeZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider _box;
        [SerializeField] private Color _color = new(0.55f, 0.5f, 0.47f);
        [SerializeField] private float _density = 0.05f;
        [SerializeField] private int _priority;
        [SerializeField] private float _blendSpeed = 1.5f;

        public Color Color => _color;
        public float Density => _density;
        public int Priority => _priority;
        public float BlendSpeed => _blendSpeed;

        public bool Contains(Vector3 point) => _box.bounds.Contains(point);
    }
}
