using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class TrampolineMembrane
    {
        private const float EdgeClearance = 1.5f;

        [SerializeField] private MeshFilter _filter;
        [SerializeField, Min(0f)] private float _radius = 0.8f;
        [SerializeField, Min(0f)] private float _contactRadius = 0.16f;
        [SerializeField, Min(0f)] private float _profilePower = 1.6f;

        private Mesh _mesh;
        private Vector3[] _rest;
        private Vector3[] _deformed;
        private Vector2 _contact;
        private float _offset;

        public Transform Surface => _filter.transform;
        public float Radius => _radius;

        public void Init()
        {
            _mesh = _filter.mesh;
            _mesh.MarkDynamic();
            _rest = _mesh.vertices;
            _deformed = new Vector3[_rest.Length];
        }

        public void Dispose()
        {
            Object.Destroy(_mesh);
        }

        public void Apply(Vector2 contact, float offset)
        {
            _contact = Vector2.ClampMagnitude(contact, _radius - _contactRadius * EdgeClearance);
            _offset = offset;

            for (int i = 0; i < _rest.Length; i++)
            {
                Vector3 vertex = _rest[i];
                vertex.y += Sample(new Vector2(vertex.x, vertex.z));
                _deformed[i] = vertex;
            }

            _mesh.vertices = _deformed;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        public float Sample(Vector2 point)
        {
            Vector2 delta = point - _contact;
            float distance = delta.magnitude;
            if (distance <= _contactRadius)
            {
                return _offset;
            }

            // Ray from the contact point to the rim circle: reach = -c·d + sqrt((c·d)² + R² - |c|²).
            // The cloth is pinned at the rim (s = 1) and fully pressed under the contact patch (s = 0).
            Vector2 direction = delta / distance;
            float along = Vector2.Dot(_contact, direction);
            float reach = -along + Mathf.Sqrt(along * along + _radius * _radius - _contact.sqrMagnitude);
            float s = Mathf.Clamp01((distance - _contactRadius) / (reach - _contactRadius));
            return _offset * Mathf.Pow(1f - s, _profilePower);
        }
    }
}
